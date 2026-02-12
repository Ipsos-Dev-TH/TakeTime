using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to update an existing seasonal rate. Supports partial updates.
/// </summary>
public sealed class UpdateSeasonalRateCommand : IRequest<SeasonalRateDto>
{
    public Guid Id { get; set; }
    public string? SeasonName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? RateMultiplier { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="UpdateSeasonalRateCommand"/>. Retrieves the seasonal rate,
/// applies changes, and persists the update.
/// </summary>
public sealed class UpdateSeasonalRateCommandHandler : IRequestHandler<UpdateSeasonalRateCommand, SeasonalRateDto>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdateSeasonalRateCommandHandler> _logger;

    public UpdateSeasonalRateCommandHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<UpdateSeasonalRateCommandHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<SeasonalRateDto> Handle(UpdateSeasonalRateCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogInformation(
            "Updating seasonal rate {SeasonalRateId} for tenant {TenantId}",
            request.Id, tenantSettings.TenantId);

        var seasonalRate = await _rateRepository.GetSeasonalRateEntityByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Seasonal rate with ID '{request.Id}' not found.");

        // Apply updates
        var seasonName = request.SeasonName ?? seasonalRate.SeasonName;
        var startDate = request.StartDate ?? seasonalRate.StartDate;
        var endDate = request.EndDate ?? seasonalRate.EndDate;
        var rateMultiplier = request.RateMultiplier ?? seasonalRate.RateMultiplier;
        var description = request.Description ?? seasonalRate.Description;

        seasonalRate.Update(seasonName, startDate, endDate, rateMultiplier, description);
        seasonalRate.UpdatedBy = request.UpdatedBy;

        if (request.IsActive.HasValue)
        {
            seasonalRate.SetActive(request.IsActive.Value);
        }

        await _rateRepository.UpdateSeasonalRateAsync(seasonalRate, cancellationToken);

        _logger.LogInformation(
            "Seasonal rate {SeasonalRateId} updated for tenant {TenantId}",
            request.Id, tenantSettings.TenantId);

        return new SeasonalRateDto
        {
            Id = seasonalRate.Id,
            SeasonName = seasonalRate.SeasonName,
            StartDate = seasonalRate.StartDate,
            EndDate = seasonalRate.EndDate,
            RateMultiplier = seasonalRate.RateMultiplier,
            Description = seasonalRate.Description,
            IsActive = seasonalRate.IsActive,
            CreatedAt = seasonalRate.CreatedAt,
            UpdatedAt = seasonalRate.UpdatedAt
        };
    }
}
