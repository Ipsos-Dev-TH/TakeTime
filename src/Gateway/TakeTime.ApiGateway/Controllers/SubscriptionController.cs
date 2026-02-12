using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TakeTime.MultiTenancy.Configuration;
using TakeTime.MultiTenancy.Core;
using TakeTime.MultiTenancy.Services;

namespace TakeTime.ApiGateway.Controllers;

/// <summary>
/// Admin API for managing subscription plans, tenant subscriptions,
/// payments, and invoices. Provides full lifecycle management for
/// the TakeTime SaaS billing system.
/// </summary>
[ApiController]
[Route("api/v1/admin/subscription")]
public class SubscriptionController : ControllerBase
{
    private readonly SubscriptionService _subscriptionService;
    private readonly TenantDbContext _dbContext;

    public SubscriptionController(
        SubscriptionService subscriptionService,
        TenantDbContext dbContext)
    {
        _subscriptionService = subscriptionService;
        _dbContext = dbContext;
    }

    // ─── Plan Management ──────────────────────────────────────────────

    /// <summary>Lists all subscription plans.</summary>
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(CancellationToken ct)
    {
        var plans = await _dbContext.SubscriptionPlans
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);
        return Ok(plans);
    }

    /// <summary>Gets a specific subscription plan by ID.</summary>
    [HttpGet("plans/{id:guid}")]
    public async Task<IActionResult> GetPlan(Guid id, CancellationToken ct)
    {
        var plan = await _dbContext.SubscriptionPlans.FindAsync([id], ct);
        if (plan is null)
            return NotFound(new { error = $"Plan '{id}' not found." });
        return Ok(plan);
    }

    /// <summary>Creates a new subscription plan.</summary>
    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan(
        [FromBody] CreatePlanRequest request, CancellationToken ct)
    {
        var existing = await _dbContext.SubscriptionPlans
            .AnyAsync(p => p.Code == request.Code, ct);
        if (existing)
            return BadRequest(new { error = $"Plan with code '{request.Code}' already exists." });

        var plan = new SubscriptionPlan
        {
            Code = request.Code,
            Name = request.Name,
            NameTh = request.NameTh,
            Description = request.Description,
            DescriptionTh = request.DescriptionTh,
            Tier = request.Tier,
            MonthlyPrice = request.MonthlyPrice,
            YearlyPrice = request.YearlyPrice,
            Currency = request.Currency ?? "THB",
            MaxRooms = request.MaxRooms,
            MaxUsers = request.MaxUsers,
            MaxStorageGB = request.MaxStorageGB,
            IncludedFeatures = request.IncludedFeatures ?? [],
            IncludedModules = request.IncludedModules ?? [],
            TrialAvailable = request.TrialAvailable,
            TrialDays = request.TrialDays,
            IsActive = true,
            SortOrder = request.SortOrder
        };

        _dbContext.SubscriptionPlans.Add(plan);
        await _dbContext.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetPlan), new { id = plan.Id }, plan);
    }

    /// <summary>Updates an existing subscription plan.</summary>
    [HttpPut("plans/{id:guid}")]
    public async Task<IActionResult> UpdatePlan(
        Guid id, [FromBody] UpdatePlanRequest request, CancellationToken ct)
    {
        var plan = await _dbContext.SubscriptionPlans.FindAsync([id], ct);
        if (plan is null)
            return NotFound(new { error = $"Plan '{id}' not found." });

        if (request.Name is not null) plan.Name = request.Name;
        if (request.NameTh is not null) plan.NameTh = request.NameTh;
        if (request.Description is not null) plan.Description = request.Description;
        if (request.DescriptionTh is not null) plan.DescriptionTh = request.DescriptionTh;
        if (request.Tier.HasValue) plan.Tier = request.Tier.Value;
        if (request.MonthlyPrice.HasValue) plan.MonthlyPrice = request.MonthlyPrice.Value;
        if (request.YearlyPrice.HasValue) plan.YearlyPrice = request.YearlyPrice.Value;
        if (request.Currency is not null) plan.Currency = request.Currency;
        if (request.MaxRooms.HasValue) plan.MaxRooms = request.MaxRooms.Value;
        if (request.MaxUsers.HasValue) plan.MaxUsers = request.MaxUsers.Value;
        if (request.MaxStorageGB.HasValue) plan.MaxStorageGB = request.MaxStorageGB.Value;
        if (request.IncludedFeatures is not null) plan.IncludedFeatures = request.IncludedFeatures;
        if (request.IncludedModules is not null) plan.IncludedModules = request.IncludedModules;
        if (request.SortOrder.HasValue) plan.SortOrder = request.SortOrder.Value;

        plan.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return Ok(plan);
    }

    /// <summary>Deactivates a subscription plan (soft delete).</summary>
    [HttpDelete("plans/{id:guid}")]
    public async Task<IActionResult> DeactivatePlan(Guid id, CancellationToken ct)
    {
        var plan = await _dbContext.SubscriptionPlans.FindAsync([id], ct);
        if (plan is null)
            return NotFound(new { error = $"Plan '{id}' not found." });

        plan.IsActive = false;
        plan.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new { message = "Plan deactivated successfully." });
    }

    /// <summary>Configures trial settings for a plan.</summary>
    [HttpPut("plans/{id:guid}/trial")]
    public async Task<IActionResult> ConfigureTrial(
        Guid id, [FromBody] ConfigureTrialRequest request, CancellationToken ct)
    {
        var plan = await _dbContext.SubscriptionPlans.FindAsync([id], ct);
        if (plan is null)
            return NotFound(new { error = $"Plan '{id}' not found." });

        plan.TrialAvailable = request.TrialAvailable;
        plan.TrialDays = request.TrialDays;
        plan.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new { message = "Trial settings updated.", plan.TrialAvailable, plan.TrialDays });
    }

    // ─── Tenant Subscription Management ───────────────────────────────

    /// <summary>Gets a tenant's current subscription.</summary>
    [HttpGet("tenants/{tenantId:guid}")]
    public async Task<IActionResult> GetTenantSubscription(Guid tenantId, CancellationToken ct)
    {
        var subscription = await _subscriptionService.GetCurrentSubscriptionAsync(tenantId, ct);
        if (subscription is null)
            return NotFound(new { error = $"No active subscription found for tenant '{tenantId}'." });
        return Ok(subscription);
    }

    /// <summary>Creates a new paid subscription for a tenant.</summary>
    [HttpPost("tenants/{tenantId:guid}/subscribe")]
    public async Task<IActionResult> Subscribe(
        Guid tenantId, [FromBody] SubscribeRequest request, CancellationToken ct)
    {
        try
        {
            var subscription = await _subscriptionService.CreateSubscriptionAsync(
                tenantId, request.PlanId, request.BillingCycle, request.DiscountCode, ct);
            return Ok(subscription);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Starts a free trial for a tenant.</summary>
    [HttpPost("tenants/{tenantId:guid}/trial")]
    public async Task<IActionResult> StartTrial(
        Guid tenantId, [FromBody] StartTrialRequest request, CancellationToken ct)
    {
        try
        {
            var subscription = await _subscriptionService.StartTrialAsync(
                tenantId, request.PlanId, ct);
            return Ok(subscription);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Upgrades a tenant's subscription to a higher-tier plan.</summary>
    [HttpPost("tenants/{tenantId:guid}/upgrade")]
    public async Task<IActionResult> Upgrade(
        Guid tenantId, [FromBody] ChangePlanRequest request, CancellationToken ct)
    {
        try
        {
            var subscription = await _subscriptionService.UpgradePlanAsync(
                tenantId, request.NewPlanId, request.BillingCycle, ct);
            return Ok(subscription);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Downgrades a tenant's subscription to a lower-tier plan.</summary>
    [HttpPost("tenants/{tenantId:guid}/downgrade")]
    public async Task<IActionResult> Downgrade(
        Guid tenantId, [FromBody] ChangePlanRequest request, CancellationToken ct)
    {
        try
        {
            var subscription = await _subscriptionService.DowngradePlanAsync(
                tenantId, request.NewPlanId, request.BillingCycle, ct);
            return Ok(subscription);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Cancels a tenant's subscription.</summary>
    [HttpPost("tenants/{tenantId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid tenantId, [FromBody] CancelRequest request, CancellationToken ct)
    {
        try
        {
            var subscription = await _subscriptionService.CancelSubscriptionAsync(
                tenantId, request.Reason, ct);
            return Ok(new { message = "Subscription cancelled.", subscription.CancelledAt });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Pauses a tenant's subscription.</summary>
    [HttpPost("tenants/{tenantId:guid}/pause")]
    public async Task<IActionResult> Pause(Guid tenantId, CancellationToken ct)
    {
        try
        {
            var subscription = await _subscriptionService.PauseSubscriptionAsync(tenantId, ct);
            return Ok(new { message = "Subscription paused.", subscription.Status });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Resumes a paused subscription.</summary>
    [HttpPost("tenants/{tenantId:guid}/resume")]
    public async Task<IActionResult> Resume(Guid tenantId, CancellationToken ct)
    {
        try
        {
            var subscription = await _subscriptionService.ResumeSubscriptionAsync(tenantId, ct);
            return Ok(new { message = "Subscription resumed.", subscription.Status });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Renews a tenant's subscription for a new billing period.</summary>
    [HttpPost("tenants/{tenantId:guid}/renew")]
    public async Task<IActionResult> Renew(Guid tenantId, CancellationToken ct)
    {
        try
        {
            var currentSub = await _subscriptionService.GetCurrentSubscriptionAsync(tenantId, ct);
            if (currentSub is null)
                return NotFound(new { error = "No active subscription found." });

            var subscription = await _subscriptionService.RenewSubscriptionAsync(currentSub.Id, ct);
            return Ok(new { message = "Subscription renewed.", subscription.EndDate, subscription.NextBillingDate });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ─── Payment Management ───────────────────────────────────────────

    /// <summary>Gets payment history for a tenant's subscription.</summary>
    [HttpGet("tenants/{tenantId:guid}/payments")]
    public async Task<IActionResult> GetPayments(Guid tenantId, CancellationToken ct)
    {
        var payments = await _dbContext.SubscriptionPayments
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
        return Ok(payments);
    }

    /// <summary>Records a payment for a tenant's subscription.</summary>
    [HttpPost("tenants/{tenantId:guid}/payments")]
    public async Task<IActionResult> RecordPayment(
        Guid tenantId, [FromBody] RecordPaymentRequest request, CancellationToken ct)
    {
        try
        {
            var currentSub = await _subscriptionService.GetCurrentSubscriptionAsync(tenantId, ct);
            if (currentSub is null)
                return NotFound(new { error = "No active subscription found." });

            var payment = new SubscriptionPayment
            {
                Amount = request.Amount,
                PaymentMethod = request.PaymentMethod,
                Status = request.Status ?? SubscriptionPaymentStatus.Completed,
                BankName = request.BankName,
                TransferReference = request.TransferReference,
                SlipImageUrl = request.SlipImageUrl,
                CardLast4 = request.CardLast4,
                CardBrand = request.CardBrand,
                GatewayTransactionId = request.GatewayTransactionId,
                BillingPeriodStart = currentSub.StartDate,
                BillingPeriodEnd = currentSub.EndDate ?? currentSub.StartDate.AddMonths(1)
            };

            var result = await _subscriptionService.RecordPaymentAsync(currentSub.Id, payment, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ─── Invoice Management ───────────────────────────────────────────

    /// <summary>Gets invoice history for a tenant.</summary>
    [HttpGet("tenants/{tenantId:guid}/invoices")]
    public async Task<IActionResult> GetInvoices(Guid tenantId, CancellationToken ct)
    {
        var invoices = await _dbContext.SubscriptionInvoices
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
        return Ok(invoices);
    }

    /// <summary>Generates a new invoice for a tenant's current subscription.</summary>
    [HttpPost("tenants/{tenantId:guid}/invoices")]
    public async Task<IActionResult> GenerateInvoice(Guid tenantId, CancellationToken ct)
    {
        try
        {
            var currentSub = await _subscriptionService.GetCurrentSubscriptionAsync(tenantId, ct);
            if (currentSub is null)
                return NotFound(new { error = "No active subscription found." });

            var invoice = await _subscriptionService.GenerateInvoiceAsync(currentSub.Id, ct);
            return Ok(invoice);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ─── Dashboard ────────────────────────────────────────────────────

    /// <summary>Gets subscription statistics for the admin dashboard.</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var totalSubscriptions = await _dbContext.TenantSubscriptions.CountAsync(ct);
        var activeSubscriptions = await _dbContext.TenantSubscriptions
            .CountAsync(s => s.Status == SubscriptionStatus.Active, ct);
        var trialSubscriptions = await _dbContext.TenantSubscriptions
            .CountAsync(s => s.Status == SubscriptionStatus.Trial, ct);
        var cancelledSubscriptions = await _dbContext.TenantSubscriptions
            .CountAsync(s => s.Status == SubscriptionStatus.Cancelled, ct);
        var pausedSubscriptions = await _dbContext.TenantSubscriptions
            .CountAsync(s => s.Status == SubscriptionStatus.Paused, ct);
        var pastDueSubscriptions = await _dbContext.TenantSubscriptions
            .CountAsync(s => s.Status == SubscriptionStatus.PastDue, ct);

        var totalRevenue = await _dbContext.SubscriptionPayments
            .Where(p => p.Status == SubscriptionPaymentStatus.Completed)
            .SumAsync(p => p.TotalAmount, ct);

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var monthlyRevenue = await _dbContext.SubscriptionPayments
            .Where(p => p.Status == SubscriptionPaymentStatus.Completed && p.PaidAt >= monthStart)
            .SumAsync(p => p.TotalAmount, ct);

        var planDistribution = await _dbContext.TenantSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial)
            .Include(s => s.Plan)
            .GroupBy(s => s.Plan!.Name)
            .Select(g => new { Plan = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return Ok(new
        {
            totalSubscriptions,
            activeSubscriptions,
            trialSubscriptions,
            cancelledSubscriptions,
            pausedSubscriptions,
            pastDueSubscriptions,
            totalRevenue,
            monthlyRevenue,
            currency = "THB",
            planDistribution
        });
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────

public record CreatePlanRequest(
    string Code,
    string Name,
    string? NameTh = null,
    string? Description = null,
    string? DescriptionTh = null,
    SubscriptionTier Tier = SubscriptionTier.Free,
    decimal MonthlyPrice = 0m,
    decimal YearlyPrice = 0m,
    string? Currency = "THB",
    int MaxRooms = 10,
    int MaxUsers = 5,
    int MaxStorageGB = 1,
    List<string>? IncludedFeatures = null,
    List<string>? IncludedModules = null,
    bool TrialAvailable = false,
    int TrialDays = 0,
    int SortOrder = 0);

public record UpdatePlanRequest(
    string? Name = null,
    string? NameTh = null,
    string? Description = null,
    string? DescriptionTh = null,
    SubscriptionTier? Tier = null,
    decimal? MonthlyPrice = null,
    decimal? YearlyPrice = null,
    string? Currency = null,
    int? MaxRooms = null,
    int? MaxUsers = null,
    int? MaxStorageGB = null,
    List<string>? IncludedFeatures = null,
    List<string>? IncludedModules = null,
    int? SortOrder = null);

public record ConfigureTrialRequest(
    bool TrialAvailable = false,
    int TrialDays = 14);

public record SubscribeRequest(
    Guid PlanId,
    BillingCycle BillingCycle = BillingCycle.Monthly,
    string? DiscountCode = null);

public record StartTrialRequest(
    Guid PlanId);

public record ChangePlanRequest(
    Guid NewPlanId,
    BillingCycle BillingCycle = BillingCycle.Monthly);

public record CancelRequest(
    string? Reason = null);

public record RecordPaymentRequest(
    decimal Amount,
    SubscriptionPaymentMethod PaymentMethod = SubscriptionPaymentMethod.BankTransfer,
    SubscriptionPaymentStatus? Status = null,
    string? BankName = null,
    string? TransferReference = null,
    string? SlipImageUrl = null,
    string? CardLast4 = null,
    string? CardBrand = null,
    string? GatewayTransactionId = null);
