using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Infrastructure.Database;

namespace TakeTime.Affiliate.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core DbContext for the Affiliate microservice.
/// Inherits from <see cref="BaseDbContext"/> for multi-tenant isolation,
/// soft-delete filtering, audit fields, and domain event dispatching.
/// </summary>
public sealed class AffiliateDbContext : BaseDbContext
{
    public AffiliateDbContext(
        DbContextOptions<AffiliateDbContext> options,
        ICurrentTenantService currentTenantService,
        IMediator? mediator = null,
        ILogger<BaseDbContext>? logger = null)
        : base(options, currentTenantService, mediator, logger)
    {
    }

    /// <summary>
    /// Affiliate partner profiles.
    /// </summary>
    public DbSet<AffiliateEntity> Affiliates => Set<AffiliateEntity>();

    /// <summary>
    /// Commission records earned by affiliates.
    /// </summary>
    public DbSet<CommissionEntity> Commissions => Set<CommissionEntity>();

    /// <summary>
    /// Payout records for affiliate payments.
    /// </summary>
    public DbSet<PayoutEntity> Payouts => Set<PayoutEntity>();

    /// <summary>
    /// Referral tracking entries.
    /// </summary>
    public DbSet<ReferralEntity> Referrals => Set<ReferralEntity>();

    protected override void ApplyEntityConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("affiliate");

        modelBuilder.Entity<AffiliateEntity>(entity =>
        {
            entity.ToTable("Affiliates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CommissionRatePercent).HasPrecision(8, 4);
            entity.Property(e => e.TotalEarnings).HasPrecision(18, 4);
            entity.Property(e => e.PendingBalance).HasPrecision(18, 4);
            entity.Property(e => e.PaidBalance).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.AffiliateCode }).IsUnique();
        });

        modelBuilder.Entity<CommissionEntity>(entity =>
        {
            entity.ToTable("Commissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReservationAmount).HasPrecision(18, 4);
            entity.Property(e => e.CommissionRatePercent).HasPrecision(8, 4);
            entity.Property(e => e.CommissionAmount).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.TenantId, e.AffiliateId });
            entity.HasIndex(e => new { e.TenantId, e.ReservationId });
        });

        modelBuilder.Entity<PayoutEntity>(entity =>
        {
            entity.ToTable("Payouts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.TenantId, e.AffiliateId });
        });

        modelBuilder.Entity<ReferralEntity>(entity =>
        {
            entity.ToTable("Referrals");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.AffiliateCode });
        });
    }
}

public sealed class AffiliateEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public string AffiliateCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public decimal CommissionRatePercent { get; set; }
    public string CommissionType { get; set; } = "Percentage";
    public decimal TotalEarnings { get; set; }
    public decimal PendingBalance { get; set; }
    public decimal PaidBalance { get; set; }
    public int TotalReferrals { get; set; }
    public int SuccessfulReferrals { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? TaxId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public sealed class CommissionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? TenantId { get; set; }
    public Guid AffiliateId { get; set; }
    public Guid ReservationId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public decimal ReservationAmount { get; set; }
    public decimal CommissionRatePercent { get; set; }
    public decimal CommissionAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
}

public sealed class PayoutEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? TenantId { get; set; }
    public Guid AffiliateId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? TransactionReference { get; set; }
    public string Status { get; set; } = "Pending";
    public string Currency { get; set; } = "THB";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? Notes { get; set; }
}

public sealed class ReferralEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? TenantId { get; set; }
    public string AffiliateCode { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Guid? ReservationId { get; set; }
    public string? ReferralSource { get; set; }
    public string Status { get; set; } = "Clicked";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
