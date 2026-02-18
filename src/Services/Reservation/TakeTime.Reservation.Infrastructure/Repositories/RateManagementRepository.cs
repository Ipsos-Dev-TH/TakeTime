using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Infrastructure.Database;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;
using TakeTime.Reservation.Domain.Entities;

namespace TakeTime.Reservation.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for rate management operations including
/// rate plans, seasonal rates, and promotions.
/// Provides data access through EF Core with entity-to-DTO mapping.
/// </summary>
public class RateManagementRepository : IRateManagementRepository
{
    private readonly ReservationDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;

    public RateManagementRepository(
        ReservationDbContext context,
        ICurrentTenantService currentTenantService)
    {
        _dbContext = context ?? throw new ArgumentNullException(nameof(context));
        _currentTenantService = currentTenantService ?? throw new ArgumentNullException(nameof(currentTenantService));
    }

    // ─── Rate Plans ──────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<RatePlanDto>> GetAllRatePlansAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.RatePlans
            .AsNoTracking()
            .OrderByDescending(rp => rp.IsDefault)
            .ThenBy(rp => rp.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToRatePlanDto).ToList();
    }

    /// <inheritdoc />
    public async Task<RatePlanDto?> GetRatePlanDtoByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RatePlans
            .AsNoTracking()
            .FirstOrDefaultAsync(rp => rp.Id == id, cancellationToken);

        return entity is null ? null : MapToRatePlanDto(entity);
    }

    /// <inheritdoc />
    public async Task<RatePlan?> GetRatePlanEntityByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RatePlans
            .FirstOrDefaultAsync(rp => rp.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateRatePlanAsync(RatePlan ratePlan, CancellationToken cancellationToken = default)
    {
        ratePlan.TenantId ??= _currentTenantService.TenantId;
        await _dbContext.RatePlans.AddAsync(ratePlan, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateRatePlanAsync(RatePlan ratePlan, CancellationToken cancellationToken = default)
    {
        _dbContext.Entry(ratePlan).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteRatePlanAsync(Guid id, string? deletedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RatePlans
            .FirstOrDefaultAsync(rp => rp.Id == id, cancellationToken);

        if (entity is null)
            return;

        entity.MarkAsDeleted(deletedBy);
        _dbContext.Entry(entity).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ─── Seasonal Rates ──────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<SeasonalRateDto>> GetSeasonalRatesAsync(int? year, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SeasonalRates.AsNoTracking().AsQueryable();

        if (year.HasValue)
        {
            query = query.Where(sr =>
                sr.StartDate.Year == year.Value || sr.EndDate.Year == year.Value);
        }

        return await query
            .OrderBy(sr => sr.StartDate)
            .Select(sr => new SeasonalRateDto
            {
                Id = sr.Id,
                SeasonName = sr.SeasonName,
                StartDate = sr.StartDate,
                EndDate = sr.EndDate,
                RateMultiplier = sr.RateMultiplier,
                Description = sr.Description,
                IsActive = sr.IsActive,
                CreatedAt = sr.CreatedAt,
                UpdatedAt = sr.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SeasonalRateEntity?> GetSeasonalRateEntityByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SeasonalRates
            .FirstOrDefaultAsync(sr => sr.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateSeasonalRateAsync(SeasonalRateEntity rate, CancellationToken cancellationToken = default)
    {
        rate.TenantId ??= _currentTenantService.TenantId;
        await _dbContext.SeasonalRates.AddAsync(rate, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateSeasonalRateAsync(SeasonalRateEntity rate, CancellationToken cancellationToken = default)
    {
        _dbContext.Entry(rate).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteSeasonalRateAsync(Guid id, string? deletedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.SeasonalRates
            .FirstOrDefaultAsync(sr => sr.Id == id, cancellationToken);

        if (entity is null)
            return;

        entity.MarkAsDeleted(deletedBy);
        _dbContext.Entry(entity).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ─── Promotions ──────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<PromotionDto>> GetPromotionsAsync(bool? activeOnly, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Promotions.AsNoTracking().AsQueryable();

        if (activeOnly == true)
        {
            var now = DateTime.UtcNow;
            query = query.Where(p =>
                p.IsActive &&
                (!p.ValidFrom.HasValue || p.ValidFrom.Value <= now) &&
                (!p.ValidTo.HasValue || p.ValidTo.Value >= now) &&
                (!p.MaxUses.HasValue || p.CurrentUses < p.MaxUses.Value));
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PromotionDto
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                DiscountType = p.DiscountType,
                DiscountValue = p.DiscountValue,
                ValidFrom = p.ValidFrom,
                ValidTo = p.ValidTo,
                MaxUses = p.MaxUses,
                CurrentUses = p.CurrentUses,
                MinimumBookingAmount = p.MinimumBookingAmount,
                Description = p.Description,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PromotionDto?> GetPromotionDtoByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Promotions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (entity is null)
            return null;

        return new PromotionDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            DiscountType = entity.DiscountType,
            DiscountValue = entity.DiscountValue,
            ValidFrom = entity.ValidFrom,
            ValidTo = entity.ValidTo,
            MaxUses = entity.MaxUses,
            CurrentUses = entity.CurrentUses,
            MinimumBookingAmount = entity.MinimumBookingAmount,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    /// <inheritdoc />
    public async Task<Promotion?> GetPromotionEntityByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Promotions
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Promotion?> GetPromotionByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Promotions
            .FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreatePromotionAsync(Promotion promotion, CancellationToken cancellationToken = default)
    {
        promotion.TenantId ??= _currentTenantService.TenantId;
        await _dbContext.Promotions.AddAsync(promotion, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdatePromotionAsync(Promotion promotion, CancellationToken cancellationToken = default)
    {
        _dbContext.Entry(promotion).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ─── Bulk Operations ─────────────────────────────────────────

    /// <inheritdoc />
    public async Task<int> BulkUpdateRatesAsync(
        List<Guid> accommodationIds,
        decimal? rateAdjustment,
        decimal? rateMultiplier,
        DateTime? effectiveFrom,
        DateTime? effectiveTo,
        CancellationToken cancellationToken = default)
    {
        if (accommodationIds.Count == 0)
            return 0;

        var accommodations = await _dbContext.Accommodations
            .Where(a => accommodationIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        if (accommodations.Count == 0)
            return 0;

        foreach (var accommodation in accommodations)
        {
            var currentAmount = accommodation.BasePrice.Amount;
            var currency = accommodation.BasePrice.Currency;
            decimal newAmount;

            if (rateMultiplier.HasValue)
            {
                newAmount = Math.Round(currentAmount * rateMultiplier.Value, 2);
            }
            else if (rateAdjustment.HasValue)
            {
                newAmount = Math.Round(currentAmount + rateAdjustment.Value, 2);
            }
            else
            {
                continue;
            }

            // Ensure the new rate is not negative
            if (newAmount < 0)
                newAmount = 0;

            accommodation.SetBasePrice(Core.Domain.ValueObjects.Money.Create(newAmount, currency));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return accommodations.Count;
    }

    // ─── Private Mapping Helpers ─────────────────────────────────

    /// <summary>
    /// Maps a RatePlan entity to a RatePlanDto.
    /// Declared as a static expression-compatible method for use in LINQ projections.
    /// </summary>
    private static RatePlanDto MapToRatePlanDto(RatePlan entity)
    {
        return new RatePlanDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            BaseRate = entity.BaseRate.Amount,
            Currency = entity.BaseRate.Currency,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
