using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TakeTime.MultiTenancy.Configuration;
using TakeTime.MultiTenancy.Core;

namespace TakeTime.MultiTenancy.Services;

/// <summary>
/// Service for managing tenant subscription lifecycle including trials,
/// plan changes, cancellations, payments, invoice generation, refunds,
/// promo code validation, trial expiration, and audit logging.
/// </summary>
public class SubscriptionService
{
    private readonly TenantDbContext _dbContext;
    private const decimal VatRate = 0.07m;

    public SubscriptionService(TenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ─── Trial Management ─────────────────────────────────────────────

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
            FinalPrice = 0m,
            Currency = plan.Currency,
            AutoRenew = false
        };

        _dbContext.TenantSubscriptions.Add(subscription);

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

        LogAudit(tenantId, subscription.Id, "TrialStarted",
            null, SubscriptionStatus.Trial.ToString(), null, planId,
            $"Trial started for plan '{plan.Name}', {plan.TrialDays} days", "system");

        await _dbContext.SaveChangesAsync(ct);
        return subscription;
    }

    /// <summary>
    /// Checks and expires trial subscriptions that have passed their end date.
    /// Returns the number of expired trials.
    /// </summary>
    public async Task<int> ExpireTrialsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiredTrials = await _dbContext.TenantSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Trial &&
                        s.TrialEndDate.HasValue &&
                        s.TrialEndDate.Value <= now)
            .ToListAsync(ct);

        foreach (var trial in expiredTrials)
        {
            trial.Status = SubscriptionStatus.Expired;
            trial.UpdatedAt = now;

            var tenant = await _dbContext.Tenants.FindAsync([trial.TenantId], ct);
            if (tenant is not null)
            {
                tenant.SubscriptionTier = SubscriptionTier.Free;
                tenant.MaxRooms = 5;
                tenant.MaxUsers = 2;
                tenant.MarkUpdated("SubscriptionService");
            }

            LogAudit(trial.TenantId, trial.Id, "TrialExpired",
                SubscriptionStatus.Trial.ToString(), SubscriptionStatus.Expired.ToString(),
                trial.PlanId, null, "Trial period ended", "system");
        }

        if (expiredTrials.Count > 0)
            await _dbContext.SaveChangesAsync(ct);

        return expiredTrials.Count;
    }

    // ─── Subscription Creation ────────────────────────────────────────

    /// <summary>
    /// Creates a paid subscription for a tenant with optional promo code.
    /// </summary>
    public async Task<TenantSubscription> CreateSubscriptionAsync(
        Guid tenantId, Guid planId, BillingCycle billingCycle,
        string? discountCode = null, CancellationToken ct = default)
    {
        var plan = await _dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct)
            ?? throw new InvalidOperationException($"Plan '{planId}' not found or inactive.");

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
        var (discountPercentage, discountAmt) = await CalculateDiscountAsync(discountCode, planId, basePrice, ct);
        var finalPrice = basePrice - discountAmt;
        if (finalPrice < 0) finalPrice = 0;

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

        // Increment promo code usage
        if (!string.IsNullOrWhiteSpace(discountCode))
        {
            var promo = await _dbContext.PromoCodes
                .FirstOrDefaultAsync(p => p.Code == discountCode.ToUpperInvariant() && p.IsActive, ct);
            if (promo is not null)
            {
                promo.CurrentUsageCount++;
                promo.UpdatedAt = DateTime.UtcNow;
            }
        }

        LogAudit(tenantId, subscription.Id, "Subscribed",
            existingSub?.Status.ToString(), SubscriptionStatus.Active.ToString(),
            existingSub?.PlanId, planId,
            JsonSerializer.Serialize(new { plan = plan.Name, billingCycle = billingCycle.ToString(), basePrice, discountCode, finalPrice }),
            "system");

        await _dbContext.SaveChangesAsync(ct);
        return subscription;
    }

    // ─── Plan Changes ─────────────────────────────────────────────────

    /// <summary>
    /// Upgrades a tenant's subscription to a higher-tier plan with pro-rated credit.
    /// </summary>
    public async Task<UpgradeResult> UpgradePlanAsync(
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

        var proRatedCredit = CalculateProRatedCredit(currentSub);

        var oldStatus = currentSub.Status.ToString();
        currentSub.Status = SubscriptionStatus.Cancelled;
        currentSub.CancelledAt = DateTime.UtcNow;
        currentSub.CancellationReason = $"Upgraded to {newPlan.Name}";
        currentSub.UpdatedAt = DateTime.UtcNow;

        LogAudit(tenantId, currentSub.Id, "Upgraded",
            oldStatus, SubscriptionStatus.Cancelled.ToString(),
            currentSub.PlanId, newPlanId,
            JsonSerializer.Serialize(new { from = currentPlan?.Name, to = newPlan.Name, proRatedCredit }),
            "system");

        var newSubscription = await CreateSubscriptionAsync(tenantId, newPlanId, billingCycle, ct: ct);
        return new UpgradeResult(newSubscription, proRatedCredit);
    }

    /// <summary>
    /// Downgrades a tenant's subscription to a lower-tier plan at end of current period.
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

        var oldStatus = currentSub.Status.ToString();
        currentSub.Status = SubscriptionStatus.Cancelled;
        currentSub.CancelledAt = DateTime.UtcNow;
        currentSub.CancellationReason = $"Downgraded to {newPlan.Name}";
        currentSub.UpdatedAt = DateTime.UtcNow;

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

        LogAudit(tenantId, newSubscription.Id, "Downgraded",
            oldStatus, SubscriptionStatus.Active.ToString(),
            currentSub.PlanId, newPlanId,
            JsonSerializer.Serialize(new { from = currentPlan?.Name, to = newPlan.Name, effectiveDate = startDate.ToString("o") }),
            "system");

        await _dbContext.SaveChangesAsync(ct);
        return newSubscription;
    }

    // ─── Subscription Lifecycle ───────────────────────────────────────

    /// <summary>
    /// Cancels a tenant's subscription.
    /// </summary>
    public async Task<TenantSubscription> CancelSubscriptionAsync(
        Guid tenantId, string? reason = null, CancellationToken ct = default)
    {
        var subscription = await GetActiveSubscriptionAsync(tenantId, ct)
            ?? throw new InvalidOperationException("No active subscription found for tenant.");

        var oldStatus = subscription.Status.ToString();
        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelledAt = DateTime.UtcNow;
        subscription.CancellationReason = reason;
        subscription.AutoRenew = false;
        subscription.UpdatedAt = DateTime.UtcNow;

        LogAudit(tenantId, subscription.Id, "Cancelled",
            oldStatus, SubscriptionStatus.Cancelled.ToString(),
            subscription.PlanId, null,
            JsonSerializer.Serialize(new { reason }), "system");

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

        LogAudit(tenantId, subscription.Id, "Paused",
            SubscriptionStatus.Active.ToString(), SubscriptionStatus.Paused.ToString(),
            subscription.PlanId, null, null, "system");

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

        LogAudit(tenantId, subscription.Id, "Resumed",
            SubscriptionStatus.Paused.ToString(), SubscriptionStatus.Active.ToString(),
            subscription.PlanId, null, null, "system");

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

        var oldStatus = subscription.Status.ToString();
        subscription.StartDate = subscription.EndDate ?? now;
        subscription.EndDate = newEndDate;
        subscription.Status = SubscriptionStatus.Active;
        subscription.NextBillingDate = newEndDate;
        subscription.UpdatedAt = now;

        var tenant = await _dbContext.Tenants.FindAsync([subscription.TenantId], ct);
        if (tenant is not null)
        {
            tenant.SubscriptionStartDate = subscription.StartDate;
            tenant.SubscriptionEndDate = newEndDate;
            tenant.MarkUpdated("SubscriptionService");
        }

        LogAudit(subscription.TenantId, subscription.Id, "Renewed",
            oldStatus, SubscriptionStatus.Active.ToString(),
            subscription.PlanId, null,
            JsonSerializer.Serialize(new { newEndDate = newEndDate.ToString("o"), billingCycle = subscription.BillingCycle.ToString() }),
            "system");

        await _dbContext.SaveChangesAsync(ct);
        return subscription;
    }

    // ─── Payments ─────────────────────────────────────────────────────

    /// <summary>
    /// Records a payment for a subscription, calculates VAT, and links to invoice.
    /// </summary>
    public async Task<SubscriptionPayment> RecordPaymentAsync(
        Guid subscriptionId, SubscriptionPayment payment, CancellationToken ct = default)
    {
        var subscription = await _dbContext.TenantSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
            ?? throw new InvalidOperationException($"Subscription '{subscriptionId}' not found.");

        payment.SubscriptionId = subscriptionId;
        payment.TenantId = subscription.TenantId;
        payment.VatAmount = Math.Round(payment.Amount * VatRate, 2);
        payment.TotalAmount = payment.Amount + payment.VatAmount;
        payment.PaymentNumber = GeneratePaymentNumber();

        _dbContext.SubscriptionPayments.Add(payment);

        if (payment.Status == SubscriptionPaymentStatus.Completed)
        {
            payment.PaidAt = DateTime.UtcNow;

            if (subscription.Status == SubscriptionStatus.PastDue ||
                subscription.Status == SubscriptionStatus.Trial)
            {
                subscription.Status = SubscriptionStatus.Active;
                subscription.UpdatedAt = DateTime.UtcNow;
            }

            // Link payment to any open invoice for this subscription
            var openInvoice = await _dbContext.SubscriptionInvoices
                .Where(i => i.SubscriptionId == subscriptionId &&
                           (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Draft))
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (openInvoice is not null)
            {
                openInvoice.PaymentId = payment.Id;
                openInvoice.Status = InvoiceStatus.Paid;
                openInvoice.PaidAt = payment.PaidAt;
                payment.InvoiceNumber = openInvoice.InvoiceNumber;
            }
        }

        LogAudit(subscription.TenantId, subscriptionId, "PaymentRecorded",
            null, null, null, null,
            JsonSerializer.Serialize(new
            {
                paymentNumber = payment.PaymentNumber,
                amount = payment.Amount,
                vat = payment.VatAmount,
                total = payment.TotalAmount,
                method = payment.PaymentMethod.ToString(),
                status = payment.Status.ToString()
            }),
            "system");

        await _dbContext.SaveChangesAsync(ct);
        return payment;
    }

    /// <summary>
    /// Processes a refund for a payment.
    /// </summary>
    public async Task<SubscriptionPayment> RefundPaymentAsync(
        Guid paymentId, string? reason = null, CancellationToken ct = default)
    {
        var payment = await _dbContext.SubscriptionPayments
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw new InvalidOperationException($"Payment '{paymentId}' not found.");

        if (payment.Status == SubscriptionPaymentStatus.Refunded)
            throw new InvalidOperationException("Payment has already been refunded.");

        if (payment.Status != SubscriptionPaymentStatus.Completed)
            throw new InvalidOperationException("Only completed payments can be refunded.");

        payment.Status = SubscriptionPaymentStatus.Refunded;

        // Update related invoice to cancelled
        if (!string.IsNullOrWhiteSpace(payment.InvoiceNumber))
        {
            var invoice = await _dbContext.SubscriptionInvoices
                .FirstOrDefaultAsync(i => i.InvoiceNumber == payment.InvoiceNumber, ct);
            if (invoice is not null)
            {
                invoice.Status = InvoiceStatus.Cancelled;
            }
        }

        LogAudit(payment.TenantId, payment.SubscriptionId, "Refunded",
            null, null, null, null,
            JsonSerializer.Serialize(new
            {
                paymentNumber = payment.PaymentNumber,
                refundAmount = payment.TotalAmount,
                reason
            }),
            "system");

        await _dbContext.SaveChangesAsync(ct);
        return payment;
    }

    // ─── Invoices ─────────────────────────────────────────────────────

    /// <summary>
    /// Generates a tax invoice for a subscription billing period.
    /// Uses tenant BusinessSettings for buyer info.
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
            BuyerTaxId = tenant.BusinessSettings?.TaxId,
            BuyerAddress = FormatTenantAddress(tenant),
            SellerName = "TakeTime Co., Ltd.",
            SellerTaxId = "0105566000000",
            SellerAddress = "Bangkok, Thailand"
        };

        _dbContext.SubscriptionInvoices.Add(invoice);

        LogAudit(subscription.TenantId, subscriptionId, "InvoiceGenerated",
            null, null, null, null,
            JsonSerializer.Serialize(new { invoiceNumber = invoice.InvoiceNumber, totalAmount }),
            "system");

        await _dbContext.SaveChangesAsync(ct);
        return invoice;
    }

    /// <summary>
    /// Checks and marks overdue invoices (past due date and still unpaid).
    /// </summary>
    public async Task<int> MarkOverdueInvoicesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var overdueInvoices = await _dbContext.SubscriptionInvoices
            .Where(i => i.Status == InvoiceStatus.Issued && i.DueDate < now)
            .ToListAsync(ct);

        foreach (var invoice in overdueInvoices)
        {
            invoice.Status = InvoiceStatus.Overdue;

            var subscription = await _dbContext.TenantSubscriptions
                .FirstOrDefaultAsync(s => s.Id == invoice.SubscriptionId &&
                                         s.Status == SubscriptionStatus.Active, ct);
            if (subscription is not null)
            {
                subscription.Status = SubscriptionStatus.PastDue;
                subscription.UpdatedAt = now;
            }
        }

        if (overdueInvoices.Count > 0)
            await _dbContext.SaveChangesAsync(ct);

        return overdueInvoices.Count;
    }

    // ─── Promo Codes ──────────────────────────────────────────────────

    /// <summary>
    /// Validates a promo code and returns the discount details.
    /// </summary>
    public async Task<PromoCodeValidationResult> ValidatePromoCodeAsync(
        string code, Guid planId, CancellationToken ct = default)
    {
        var promo = await _dbContext.PromoCodes
            .FirstOrDefaultAsync(p => p.Code == code.ToUpperInvariant() && p.IsActive, ct);

        if (promo is null)
            return new PromoCodeValidationResult(false, "Promo code not found.");

        if (!promo.IsValid())
            return new PromoCodeValidationResult(false, "Promo code has expired or reached its usage limit.");

        if (!promo.IsApplicableToPlan(planId))
            return new PromoCodeValidationResult(false, "Promo code is not applicable to this plan.");

        return new PromoCodeValidationResult(true, "Valid", promo.DiscountType, promo.DiscountValue);
    }

    // ─── Queries ──────────────────────────────────────────────────────

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

    /// <summary>
    /// Gets subscription audit history for a tenant.
    /// </summary>
    public async Task<List<SubscriptionAuditLog>> GetAuditHistoryAsync(
        Guid tenantId, int take = 50, CancellationToken ct = default)
    {
        return await _dbContext.SubscriptionAuditLogs
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Gets all subscriptions for a tenant including historical ones.
    /// </summary>
    public async Task<List<TenantSubscription>> GetSubscriptionHistoryAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        return await _dbContext.TenantSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
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

    /// <summary>
    /// Calculates discount from promo code (DB-backed or hardcoded fallback).
    /// Returns (percentage, discountAmount).
    /// </summary>
    private async Task<(decimal Percentage, decimal Amount)> CalculateDiscountAsync(
        string? discountCode, Guid planId, decimal basePrice, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(discountCode))
            return (0m, 0m);

        var code = discountCode.ToUpperInvariant();

        // Try database promo codes first
        var promo = await _dbContext.PromoCodes
            .FirstOrDefaultAsync(p => p.Code == code && p.IsActive, ct);

        if (promo is not null && promo.IsValid() && promo.IsApplicableToPlan(planId))
        {
            if (promo.DiscountType == DiscountType.Percentage)
            {
                var amount = Math.Round(basePrice * (promo.DiscountValue / 100m), 2);
                return (promo.DiscountValue, amount);
            }
            else
            {
                return (0m, Math.Min(promo.DiscountValue, basePrice));
            }
        }

        // Fallback: hardcoded codes
        var percentage = code switch
        {
            "WELCOME10" => 10m,
            "ANNUAL20" => 20m,
            "PARTNER15" => 15m,
            _ => 0m
        };

        return (percentage, Math.Round(basePrice * (percentage / 100m), 2));
    }

    /// <summary>
    /// Calculates pro-rated credit for unused time on the current subscription.
    /// </summary>
    private static decimal CalculateProRatedCredit(TenantSubscription subscription)
    {
        if (subscription.EndDate is null || subscription.FinalPrice <= 0)
            return 0m;

        var totalDays = (subscription.EndDate.Value - subscription.StartDate).TotalDays;
        if (totalDays <= 0) return 0m;

        var remainingDays = (subscription.EndDate.Value - DateTime.UtcNow).TotalDays;
        if (remainingDays <= 0) return 0m;

        var dailyRate = subscription.FinalPrice / (decimal)totalDays;
        return Math.Round(dailyRate * (decimal)remainingDays, 2);
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

    private void LogAudit(
        Guid tenantId, Guid? subscriptionId, string action,
        string? oldStatus, string? newStatus, Guid? oldPlanId, Guid? newPlanId,
        string? details, string? performedBy)
    {
        _dbContext.SubscriptionAuditLogs.Add(new SubscriptionAuditLog
        {
            TenantId = tenantId,
            SubscriptionId = subscriptionId,
            Action = action,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            OldPlanId = oldPlanId,
            NewPlanId = newPlanId,
            Details = details,
            PerformedBy = performedBy
        });
    }
}

/// <summary>
/// Result of a promo code validation.
/// </summary>
public record PromoCodeValidationResult(
    bool IsValid,
    string Message,
    DiscountType DiscountType = DiscountType.Percentage,
    decimal DiscountValue = 0m);

/// <summary>
/// Result of a plan upgrade including pro-rated credit.
/// </summary>
public record UpgradeResult(
    TenantSubscription NewSubscription,
    decimal ProRatedCredit);
