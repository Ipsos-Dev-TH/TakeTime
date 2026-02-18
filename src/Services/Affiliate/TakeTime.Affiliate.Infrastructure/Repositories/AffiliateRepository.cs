using Microsoft.EntityFrameworkCore;
using TakeTime.Affiliate.Application.Commands;

namespace TakeTime.Affiliate.Infrastructure.Repositories;

/// <summary>
/// Implementation of <see cref="IAffiliateRepository"/> providing data access
/// for affiliate partners, commissions, and payouts using Entity Framework Core.
/// Maps between the application-layer entry models and the persistence entities
/// defined in <see cref="AffiliateDbContext"/>.
/// </summary>
public sealed class AffiliateRepository : IAffiliateRepository
{
    private readonly AffiliateDbContext _dbContext;

    public AffiliateRepository(AffiliateDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task<Guid> CreateAsync(AffiliateEntry affiliate, CancellationToken cancellationToken = default)
    {
        var entity = MapToAffiliateEntity(affiliate);
        _dbContext.Affiliates.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task<AffiliateEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Affiliates
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return entity is null ? null : MapToAffiliateEntry(entity);
    }

    /// <inheritdoc />
    public async Task<AffiliateEntry?> GetByEmailAsync(string tenantId, string email, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Affiliates
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Email == email, cancellationToken);

        return entity is null ? null : MapToAffiliateEntry(entity);
    }

    /// <inheritdoc />
    public async Task<AffiliateEntry?> GetByCodeAsync(string tenantId, string affiliateCode, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Affiliates
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.AffiliateCode == affiliateCode, cancellationToken);

        return entity is null ? null : MapToAffiliateEntry(entity);
    }

    /// <inheritdoc />
    public async Task<int> GetCountAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Affiliates
            .CountAsync(a => a.TenantId == tenantId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(AffiliateEntry affiliate, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Affiliates
            .FirstOrDefaultAsync(a => a.Id == affiliate.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Affiliate with ID '{affiliate.Id}' not found.");

        entity.Name = affiliate.Name;
        entity.Email = affiliate.Email;
        entity.Phone = affiliate.Phone;
        entity.CompanyName = affiliate.CompanyName;
        entity.AffiliateCode = affiliate.AffiliateCode;
        entity.Status = affiliate.Status;
        entity.CommissionRatePercent = affiliate.CommissionRatePercent;
        entity.CommissionType = affiliate.CommissionType;
        entity.TotalEarnings = affiliate.TotalEarnings;
        entity.PendingBalance = affiliate.PendingBalance;
        entity.PaidBalance = affiliate.PaidBalance;
        entity.TotalReferrals = affiliate.TotalReferrals;
        entity.SuccessfulReferrals = affiliate.SuccessfulReferrals;
        entity.BankAccountName = affiliate.BankAccountName;
        entity.BankAccountNumber = affiliate.BankAccountNumber;
        entity.BankName = affiliate.BankName;
        entity.TaxId = affiliate.TaxId;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AffiliateEntry>> SearchAsync(
        string tenantId,
        string? status,
        string? name,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Affiliates
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(a => a.Name.Contains(name));
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var entities = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToAffiliateEntry).ToList();
    }

    /// <inheritdoc />
    public async Task<Guid> CreateCommissionAsync(CommissionEntry commission, CancellationToken cancellationToken = default)
    {
        var entity = MapToCommissionEntity(commission);
        _dbContext.Commissions.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommissionEntry>> GetCommissionsByAffiliateAsync(
        Guid affiliateId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Commissions
            .AsNoTracking()
            .Where(c => c.AffiliateId == affiliateId)
            .OrderByDescending(c => c.EarnedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToCommissionEntry).ToList();
    }

    /// <inheritdoc />
    public async Task<Guid> CreatePayoutAsync(PayoutEntry payout, CancellationToken cancellationToken = default)
    {
        var entity = MapToPayoutEntity(payout);
        _dbContext.Payouts.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PayoutEntry>> GetPayoutsByAffiliateAsync(
        Guid affiliateId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Payouts
            .AsNoTracking()
            .Where(p => p.AffiliateId == affiliateId)
            .OrderByDescending(p => p.RequestedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToPayoutEntry).ToList();
    }

    /// <inheritdoc />
    public async Task<(decimal totalCommission, int totalBookings, int totalReferrals, decimal conversionRate)> GetPerformanceMetricsAsync(
        string tenantId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var commissionsQuery = _dbContext.Commissions
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId);

        if (from.HasValue)
        {
            commissionsQuery = commissionsQuery.Where(c => c.EarnedAt >= from.Value);
        }

        if (to.HasValue)
        {
            commissionsQuery = commissionsQuery.Where(c => c.EarnedAt <= to.Value);
        }

        var totalCommission = await commissionsQuery.SumAsync(c => c.CommissionAmount, cancellationToken);
        var totalBookings = await commissionsQuery.CountAsync(cancellationToken);

        var referralsQuery = _dbContext.Referrals
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId);

        if (from.HasValue)
        {
            referralsQuery = referralsQuery.Where(r => r.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            referralsQuery = referralsQuery.Where(r => r.CreatedAt <= to.Value);
        }

        var totalReferrals = await referralsQuery.CountAsync(cancellationToken);

        var conversionRate = totalReferrals > 0
            ? Math.Round((decimal)totalBookings / totalReferrals * 100, 2)
            : 0m;

        return (totalCommission, totalBookings, totalReferrals, conversionRate);
    }

    #region Mapping Helpers

    private static AffiliateEntity MapToAffiliateEntity(AffiliateEntry entry)
    {
        return new AffiliateEntity
        {
            Id = entry.Id,
            TenantId = entry.TenantId,
            Name = entry.Name,
            Email = entry.Email,
            Phone = entry.Phone,
            CompanyName = entry.CompanyName,
            AffiliateCode = entry.AffiliateCode,
            Status = entry.Status,
            CommissionRatePercent = entry.CommissionRatePercent,
            CommissionType = entry.CommissionType,
            TotalEarnings = entry.TotalEarnings,
            PendingBalance = entry.PendingBalance,
            PaidBalance = entry.PaidBalance,
            TotalReferrals = entry.TotalReferrals,
            SuccessfulReferrals = entry.SuccessfulReferrals,
            BankAccountName = entry.BankAccountName,
            BankAccountNumber = entry.BankAccountNumber,
            BankName = entry.BankName,
            TaxId = entry.TaxId,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt
        };
    }

    private static AffiliateEntry MapToAffiliateEntry(AffiliateEntity entity)
    {
        return new AffiliateEntry
        {
            Id = entity.Id,
            TenantId = entity.TenantId ?? string.Empty,
            Name = entity.Name,
            Email = entity.Email,
            Phone = entity.Phone,
            CompanyName = entity.CompanyName,
            AffiliateCode = entity.AffiliateCode,
            Status = entity.Status,
            CommissionRatePercent = entity.CommissionRatePercent,
            CommissionType = entity.CommissionType,
            TotalEarnings = entity.TotalEarnings,
            PendingBalance = entity.PendingBalance,
            PaidBalance = entity.PaidBalance,
            TotalReferrals = entity.TotalReferrals,
            SuccessfulReferrals = entity.SuccessfulReferrals,
            BankAccountName = entity.BankAccountName,
            BankAccountNumber = entity.BankAccountNumber,
            BankName = entity.BankName,
            TaxId = entity.TaxId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static CommissionEntity MapToCommissionEntity(CommissionEntry entry)
    {
        return new CommissionEntity
        {
            Id = entry.Id,
            TenantId = entry.TenantId,
            AffiliateId = entry.AffiliateId,
            ReservationId = entry.ReservationId,
            ReservationNumber = entry.ReservationNumber,
            ReservationAmount = entry.ReservationAmount,
            CommissionRatePercent = entry.CommissionRatePercent,
            CommissionAmount = entry.CommissionAmount,
            Status = entry.Status,
            EarnedAt = entry.EarnedAt,
            PaidAt = entry.PaidAt
        };
    }

    private static CommissionEntry MapToCommissionEntry(CommissionEntity entity)
    {
        return new CommissionEntry
        {
            Id = entity.Id,
            TenantId = entity.TenantId ?? string.Empty,
            AffiliateId = entity.AffiliateId,
            ReservationId = entity.ReservationId,
            ReservationNumber = entity.ReservationNumber,
            ReservationAmount = entity.ReservationAmount,
            CommissionRatePercent = entity.CommissionRatePercent,
            CommissionAmount = entity.CommissionAmount,
            Status = entity.Status,
            EarnedAt = entity.EarnedAt,
            PaidAt = entity.PaidAt
        };
    }

    private static PayoutEntity MapToPayoutEntity(PayoutEntry entry)
    {
        return new PayoutEntity
        {
            Id = entry.Id,
            TenantId = entry.TenantId,
            AffiliateId = entry.AffiliateId,
            Amount = entry.Amount,
            PaymentMethod = entry.PaymentMethod,
            TransactionReference = entry.TransactionReference,
            Status = entry.Status,
            Currency = entry.Currency,
            RequestedAt = entry.RequestedAt,
            ProcessedAt = entry.ProcessedAt,
            Notes = entry.Notes
        };
    }

    private static PayoutEntry MapToPayoutEntry(PayoutEntity entity)
    {
        return new PayoutEntry
        {
            Id = entity.Id,
            TenantId = entity.TenantId ?? string.Empty,
            AffiliateId = entity.AffiliateId,
            Amount = entity.Amount,
            PaymentMethod = entity.PaymentMethod,
            TransactionReference = entity.TransactionReference,
            Status = entity.Status,
            Currency = entity.Currency,
            RequestedAt = entity.RequestedAt,
            ProcessedAt = entity.ProcessedAt,
            Notes = entity.Notes
        };
    }

    #endregion
}
