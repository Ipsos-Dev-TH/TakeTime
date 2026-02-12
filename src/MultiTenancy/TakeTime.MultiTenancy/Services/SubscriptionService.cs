using Microsoft.EntityFrameworkCore;
using TakeTime.MultiTenancy.Configuration;
using TakeTime.MultiTenancy.Core;

namespace TakeTime.MultiTenancy.Services;

/// <summary>
/// Service for managing tenant subscription lifecycle including trials,
/// plan changes, cancellations, payments, and invoice generation.
/// </summary>
public class SubscriptionService
{
    private readonly TenantDbContext _dbContext;
    private const decimal VatRate = 0.07m;

    public SubscriptionService(TenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Starts a free trial for a tenant on the specified plan.
    /// </summary>
    public async Task<TenantSubscription> StartTrialAsync(
        Guid tenantId, Guid planId, CancellationToken ct = default)
    {
        var plan = await _dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct)
            ?? throw new InvalidOperationException($"Plan '{planId}' not found or inactive.");

        if (!plan.TrialAvailable)
            throw new InvalidOperationException($"Plan '{plan.Name}' does not offer a free trial.");

        // Check if tenant already has an active subscription
        var existingSub = await _dbContext.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId &&
                (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active), ct);

        if (existingSub is not null)
            throw new InvalidOperationException("Tenant already has an active subscription or trial.");

        var now = DateTime.UtcNow;
        var subscription = new TenantSubscription
        {
            TenantId = tenantId,
            PlanId = planId,
            Status = SubscriptionStatus.Trial,
            BillingCycle = BillingCycle.Monthly,
            StartDate = now,
            TrialStartDate = now,
            TrialEndDate = now.AddDays(plan.TrialDays),
            IsTrialUsed = true,
            PriceAtSubscription = plan.MonthlyPrice,
            FinalPrice = 0m, // Trial is free
            Currency = plan.Currency,
            AutoRenew = false
        };

        _dbContext.TenantSubscriptions.Add(subscription);

        // Update tenant subscription tier and limits
        var tenant = await _dbContext.Tenants.FindAsync([tenantId], ct);
        if (tenant is not null)
        {
            tenant.SubscriptionTier = plan.Tier;
            tenant.SubscriptionStartDate = now;
            tenant.SubscriptionEndDate = subscription.TrialEndDate;
            tenant.MaxRooms = plan.MaxRooms;
            tenant.MaxUsers = plan.MaxUsers;
            tenant.MarkUpdated("SubscriptionService");
        }

        await _dbContext.SaveChangesAsync(ct);
        return subscription;
    }

    /// <summary>
    /// Creates a paid subscription for a tenant.
    /// </summary>
    public async Task<TenantSubscription> CreateSubscriptionAsync(
        Guid tenantId, Guid planId, BillingCycle billingCycle,
        string? discountCode = null, CancellationToken ct = default)
    {
        var plan = await _dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct)
            ?? throw new InvalidOperationException($"Plan '{planId}' not found or inactive.");

        // Cancel any existing trial or active subscription
        var existingSub = await _dbContext.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId &&
                (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active), ct);

        if (existingSub is not null)
        {
            existingSub.Status = SubscriptionStatus.Cancelled;
            existingSub.CancelledAt = DateTime.UtcNow;
            existingSub.CancellationReason = "Upgraded to new subscription";
            existingSub.UpdatedAt = DateTime.UtcNow;
        }

        var basePrice = billingCycle == BillingCycle.Yearly ? plan.YearlyPrice : plan.MonthlyPrice;
        var discountPercentage = CalculateDiscount(discountCode);
        var finalPrice = basePrice * (1 - discountPercentage / 100m);

        var now = DateTime.UtcNow;
        var endDate = billingCycle == BillingCycle.Yearly
            ? now.AddYears(1)
            : now.AddMonths(1);

        var subscription = new TenantSubscription
        {
            TenantId = tenantId,
            PlanId = planId,
            Status = SubscriptionStatus.Active,
            BillingCycle = billingCycle,
            StartDate = now,
            EndDate = endDate,
            IsTrialUsed = existingSub?.IsTrialUsed ?? false,
            PriceAtSubscription = basePrice,
            DiscountPercentage = discountPercentage,
            DiscountCode = discountCode,
            FinalPrice = finalPrice,
            Currency = plan.Currency,
            AutoRenew = true,
            NextBillingDate = endDate
        };

        _dbContext.TenantSubscriptions.Add(subscription);

        // Update tenant
        var tenant = await _dbContext.Tenants.FindAsync([tenantId], ct);
        if (tenant is not null)
        {
            tenant.SubscriptionTier = plan.Tier;
            tenant.SubscriptionStartDate = now;
            tenant.SubscriptionEndDate = endDate;
            tenant.MaxRooms = plan.MaxRooms;
            tenant.MaxUsers = plan.MaxUsers;
            tenant.MarkUpdated("SubscriptionService");
        }

        await _dbContext.SaveChangesAsync(ct);
        return subscription;
    }

    /// <summary>
    /// Upgrades a tenant's subscription to a higher-tier plan.
    /// </summary>
    public async Task<TenantSubscription> UpgradePlanAsync(
        Guid tenantId, Guid newPlanId, BillingCycle billingCycle,
        CancellationToken ct = default)
    {
        var currentSub = await GetActiveSubscriptionAsync(tenantId, ct)
            ?? throw new InvalidOperationException("No active subscription found for tenant.");

        var newPlan = await _dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == newPlanId && p.IsActive, ct)
            ?? throw new InvalidOperationException($"Plan '{newPlanId}' not found or inactive.");

        var currentPlan = await _dbContext.SubscriptionPlans.FindAsync([currentSub.PlanId], ct);
        if (currentPlan is not null && newPlan.Tier <= currentPlan.Tier)
            throw new InvalidOperationException("New plan must be a higher tier for upgrade. Use downgrade instead.");

        // Cancel current subscription
        currentSub.Status = SubscriptionStatus.Cancelled;
        currentSub.CancelledAt = DateTime.UtcNow;
        currentSub.CancellationReason = $"Upgraded to {newPlan.Name}";
        currentSub.UpdatedAt = DateTime.UtcNow;

        // Create new subscription
        return await CreateSubscriptionAsync(tenantId, newPlanId, billingCycle, ct: ct);
    }

    /// <summary>
    /// Downgrades a tenant's subscription to a lower-tier plan.
    /// Takes effect at the end of the current billing period.
    /// </summary>
    public async Task<TenantSubscription> DowngradePlanAsync(
        Guid tenantId, Guid newPlanId, BillingCycle billingCycle,
        CancellationToken ct = default)
    {
        var currentSub = await GetActiveSubscriptionAsync(tenantId, ct)
            ?? throw new InvalidOperationException("No active subscription found for tenant.");

        var newPlan = await _dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == newPlanId && p.IsActive, ct)
            ?? throw new InvalidOperationException($"Plan '{newPlanId}' not found or inactive.");

        var currentPlan = await _dbContext.SubscriptionPlans.FindAsync([currentSub.PlanId], ct);
        if (currentPlan is not null && newPlan.Tier >= currentPlan.Tier)
            throw new InvalidOperationException("New plan must be a lower tier for downgrade. Use upgrade instead.");

        // Mark current as cancelled at end of period
        currentSub.Status = SubscriptionStatus.Cancelled;
        currentSub.CancelledAt = DateTime.UtcNow;
        currentSub.CancellationReason = $"Downgraded to {newPlan.Name}";
        currentSub.UpdatedAt = DateTime.UtcNow;

        // Create new subscription starting at end of current period
        var basePrice = billingCycle == BillingCycle.Yearly ? newPlan.YearlyPrice : newPlan.MonthlyPrice;
        var startDate = currentSub.EndDate ?? DateTime.UtcNow;
        var endDate = billingCycle == BillingCycle.Yearly
            ? startDate.AddYears(1)
            : startDate.AddMonths(1);

        var newSubscription = new TenantSubscription
        {
            TenantId = tenantId,
            PlanId = newPlanId,
            Status = SubscriptionStatus.Active,
            BillingCycle = billingCycle,
            StartDate = startDate,
            EndDate = endDate,
            PriceAtSubscription = basePrice,
            FinalPrice = basePrice,
            Currency = newPlan.Currency,
            AutoRenew = true,
            NextBillingDate = endDate
        };

        _dbContext.TenantSubscriptions.Add(newSubscription);

        // Update tenant limits
        var tenant = await _dbContext.Tenants.FindAsync([tenantId], ct);
        if (tenant is not null)
        {
            tenant.SubscriptionTier = newPlan.Tier;
            tenant.SubscriptionStartDate = startDate;
            tenant.SubscriptionEndDate = endDate;
            tenant.MaxRooms = newPlan.MaxRooms;
            tenant.MaxUsers = newPlan.MaxUsers;
            tenant.MarkUpdated("SubscriptionService");
        }

        await _dbContext.SaveChangesAsync(ct);
        return newSubscription;
    }

    /// <summary>
    /// Cancels a tenant's subscription.
    /// </summary>
    public async Task<TenantSubscription> CancelSubscriptionAsync(
        Guid tenantId, string? reason = null, CancellationToken ct = default)
    {
        var subscription = await GetActiveSubscriptionAsync(tenantId, ct)
            ?? throw new InvalidOperationException("No active subscription found for tenant.");

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelledAt = DateTime.UtcNow;
        subscription.CancellationReason = reason;
        subscription.AutoRenew = false;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return subscription;
    }

    /// <summary>
    /// Pauses a tenant's subscription temporarily.
    /// </summary>
    public async Task<TenantSubscription> PauseSubscriptionAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var subscription = await GetActiveSubscriptionAsync(tenantId, ct)
            ?? throw new InvalidOperationException("No active subscription found for tenant.");

        if (subscription.Status != SubscriptionStatus.Active)
            throw new InvalidOperationException("Only active subscriptions can be paused.");

        subscription.Status = SubscriptionStatus.Paused;
        subscription.AutoRenew = false;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return subscription;
    }

    /// <summary>
    /// Resumes a paused subscription.
    /// </summary>
    public async Task<TenantSubscription> ResumeSubscriptionAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var subscription = await _dbContext.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId &&
                s.Status == SubscriptionStatus.Paused, ct)
            ?? throw new InvalidOperationException("No paused subscription found for tenant.");

        subscription.Status = SubscriptionStatus.Active;
        subscription.AutoRenew = true;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return subscription;
    }

    /// <summary>
    /// Renews an existing subscription for a new billing period.
    /// </summary>
    public async Task<TenantSubscription> RenewSubscriptionAsync(
        Guid subscriptionId, CancellationToken ct = default)
    {
        var subscription = await _dbContext.TenantSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
            ?? throw new InvalidOperationException($"Subscription '{subscriptionId}' not found.");

        if (subscription.Plan is null)
            throw new InvalidOperationException("Associated plan not found.");

        var now = DateTime.UtcNow;
        var newEndDate = subscription.BillingCycle == BillingCycle.Yearly
            ? (subscription.EndDate ?? now).AddYears(1)
            : (subscription.EndDate ?? now).AddMonths(1);

        subscription.StartDate = subscription.EndDate ?? now;
        subscription.EndDate = newEndDate;
        subscription.Status = SubscriptionStatus.Active;
        subscription.NextBillingDate = newEndDate;
        subscription.UpdatedAt = now;

        // Update tenant
        var tenant = await _dbContext.Tenants.FindAsync([subscription.TenantId], ct);
        if (tenant is not null)
        {
            tenant.SubscriptionStartDate = subscription.StartDate;
            tenant.SubscriptionEndDate = newEndDate;
            tenant.MarkUpdated("SubscriptionService");
        }

        await _dbContext.SaveChangesAsync(ct);
        return subscription;
    }

    /// <summary>
    /// Records a payment for a subscription, calculating 7% VAT and generating a payment number.
    /// </summary>
    public async Task<SubscriptionPayment> RecordPaymentAsync(
        Guid subscriptionId, SubscriptionPayment payment, CancellationToken ct = default)
    {
        var subscription = await _dbContext.TenantSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
            ?? throw new InvalidOperationException($"Subscription '{subscriptionId}' not found.");

        // Calculate VAT at 7%
        payment.SubscriptionId = subscriptionId;
        payment.TenantId = subscription.TenantId;
        payment.VatAmount = Math.Round(payment.Amount * VatRate, 2);
        payment.TotalAmount = payment.Amount + payment.VatAmount;

        // Generate payment number
        payment.PaymentNumber = GeneratePaymentNumber();

        _dbContext.SubscriptionPayments.Add(payment);

        // If payment is completed, mark subscription as active
        if (payment.Status == SubscriptionPaymentStatus.Completed)
        {
            payment.PaidAt = DateTime.UtcNow;

            if (subscription.Status == SubscriptionStatus.PastDue ||
                subscription.Status == SubscriptionStatus.Trial)
            {
                subscription.Status = SubscriptionStatus.Active;
                subscription.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        return payment;
    }

    /// <summary>
    /// Generates a tax invoice for a subscription billing period.
    /// </summary>
    public async Task<SubscriptionInvoice> GenerateInvoiceAsync(
        Guid subscriptionId, CancellationToken ct = default)
    {
        var subscription = await _dbContext.TenantSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
            ?? throw new InvalidOperationException($"Subscription '{subscriptionId}' not found.");

        var tenant = await _dbContext.Tenants.FindAsync([subscription.TenantId], ct)
            ?? throw new InvalidOperationException($"Tenant '{subscription.TenantId}' not found.");

        var plan = subscription.Plan
            ?? throw new InvalidOperationException("Associated plan not found.");

        var subTotal = subscription.FinalPrice;
        var discountAmount = subscription.PriceAtSubscription - subscription.FinalPrice;
        var vatAmount = Math.Round(subTotal * VatRate, 2);
        var totalAmount = subTotal + vatAmount;

        var invoice = new SubscriptionInvoice
        {
            TenantId = subscription.TenantId,
            SubscriptionId = subscriptionId,
            InvoiceNumber = GenerateInvoiceNumber(),
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            PlanName = plan.Name,
            BillingCycleDescription = subscription.BillingCycle == BillingCycle.Yearly ? "Yearly" : "Monthly",
            BillingPeriodStart = subscription.StartDate,
            BillingPeriodEnd = subscription.EndDate ?? subscription.StartDate.AddMonths(1),
            SubTotal = subTotal,
            DiscountAmount = discountAmount > 0 ? discountAmount : 0,
            VatRate = 7m,
            VatAmount = vatAmount,
            TotalAmount = totalAmount,
            Currency = subscription.Currency,
            Status = InvoiceStatus.Issued,
            BuyerName = tenant.Name,
            BuyerAddress = FormatTenantAddress(tenant),
            SellerName = "TakeTime Co., Ltd.",
            SellerTaxId = "0105566000000",
            SellerAddress = "Bangkok, Thailand"
        };

        _dbContext.SubscriptionInvoices.Add(invoice);
        await _dbContext.SaveChangesAsync(ct);
        return invoice;
    }

    /// <summary>
    /// Gets the current active or trial subscription for a tenant.
    /// </summary>
    public async Task<TenantSubscription?> GetCurrentSubscriptionAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        return await _dbContext.TenantSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.TenantId == tenantId &&
                (s.Status == SubscriptionStatus.Active ||
                 s.Status == SubscriptionStatus.Trial ||
                 s.Status == SubscriptionStatus.PastDue ||
                 s.Status == SubscriptionStatus.Paused))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    // ─── Private Helpers ──────────────────────────────────────────────

    private async Task<TenantSubscription?> GetActiveSubscriptionAsync(
        Guid tenantId, CancellationToken ct)
    {
        return await _dbContext.TenantSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.TenantId == tenantId &&
                (s.Status == SubscriptionStatus.Active ||
                 s.Status == SubscriptionStatus.Trial ||
                 s.Status == SubscriptionStatus.PastDue ||
                 s.Status == SubscriptionStatus.Paused))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private static decimal CalculateDiscount(string? discountCode)
    {
        if (string.IsNullOrWhiteSpace(discountCode))
            return 0m;

        // Simple discount code lookup (can be extended with a proper promo code system)
        return discountCode.ToUpperInvariant() switch
        {
            "WELCOME10" => 10m,
            "ANNUAL20" => 20m,
            "PARTNER15" => 15m,
            _ => 0m
        };
    }

    private static string GeneratePaymentNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Random.Shared.Next(1000, 9999);
        return $"SUB-PAY-{timestamp}-{random}";
    }

    private static string GenerateInvoiceNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Random.Shared.Next(1000, 9999);
        return $"SUB-INV-{timestamp}-{random}";
    }

    private static string FormatTenantAddress(Tenant tenant)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(tenant.AddressLine1)) parts.Add(tenant.AddressLine1);
        if (!string.IsNullOrWhiteSpace(tenant.AddressLine2)) parts.Add(tenant.AddressLine2);
        if (!string.IsNullOrWhiteSpace(tenant.City)) parts.Add(tenant.City);
        if (!string.IsNullOrWhiteSpace(tenant.StateProvince)) parts.Add(tenant.StateProvince);
        if (!string.IsNullOrWhiteSpace(tenant.PostalCode)) parts.Add(tenant.PostalCode);
        if (!string.IsNullOrWhiteSpace(tenant.Country)) parts.Add(tenant.Country);
        return string.Join(", ", parts);
    }
}
