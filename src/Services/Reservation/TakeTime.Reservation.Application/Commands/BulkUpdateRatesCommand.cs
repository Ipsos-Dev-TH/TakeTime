using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to bulk update rates for multiple accommodations. Supports both
/// fixed-amount adjustments and multiplier-based adjustments.
/// </summary>
public sealed class BulkUpdateRatesCommand : IRequest<BulkUpdateResultDto>
{
    public List<Guid> AccommodationIds { get; set; } = [];
    public decimal? RateAdjustment { get; set; }
    public decimal? RateMultiplier { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="BulkUpdateRatesCommand"/>. Applies rate adjustments
/// to multiple accommodations in a single operation.
/// </summary>
public sealed class BulkUpdateRatesCommandHandler : IRequestHandler<BulkUpdateRatesCommand, BulkUpdateResultDto>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<BulkUpdateRatesCommandHandler> _logger;

    public BulkUpdateRatesCommandHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<BulkUpdateRatesCommandHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<BulkUpdateResultDto> Handle(BulkUpdateRatesCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogInformation(
            "Bulk updating rates for {Count} accommodations for tenant {TenantId}. " +
            "Adjustment: {Adjustment}, Multiplier: {Multiplier}",
            request.AccommodationIds.Count, tenantSettings.TenantId,
            request.RateAdjustment, request.RateMultiplier);

        if (!request.AccommodationIds.Any())
        {
            throw new InvalidOperationException("At least one accommodation ID must be provided.");
        }

        if (!request.RateAdjustment.HasValue && !request.RateMultiplier.HasValue)
        {
            throw new InvalidOperationException(
                "Either a rate adjustment or rate multiplier must be provided.");
        }

        var updatedCount = await _rateRepository.BulkUpdateRatesAsync(
            request.AccommodationIds,
            request.RateAdjustment,
            request.RateMultiplier,
            request.EffectiveFrom,
            request.EffectiveTo,
            cancellationToken);

        _logger.LogInformation(
            "Bulk rate update completed: {UpdatedCount} accommodations updated for tenant {TenantId}",
            updatedCount, tenantSettings.TenantId);

        return new BulkUpdateResultDto
        {
            UpdatedCount = updatedCount,
            Message = $"Successfully updated rates for {updatedCount} accommodation(s)."
        };
    }
}
