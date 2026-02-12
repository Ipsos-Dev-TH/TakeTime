using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;
using TakeTime.Reservation.Domain.Entities;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to create a new seasonal rate configuration.
/// </summary>
public sealed class CreateSeasonalRateCommand : IRequest<SeasonalRateDto>
{
    public string SeasonName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal RateMultiplier { get; set; }
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="CreateSeasonalRateCommand"/>. Creates a new seasonal rate
/// entity and persists it.
/// </summary>
public sealed class CreateSeasonalRateCommandHandler : IRequestHandler<CreateSeasonalRateCommand, SeasonalRateDto>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateSeasonalRateCommandHandler> _logger;

    public CreateSeasonalRateCommandHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<CreateSeasonalRateCommandHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<SeasonalRateDto> Handle(CreateSeasonalRateCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogInformation(
            "Creating seasonal rate '{SeasonName}' for tenant {TenantId}",
            request.SeasonName, tenantSettings.TenantId);

        if (request.EndDate <= request.StartDate)
        {
            throw new InvalidOperationException("End date must be after start date.");
        }

        var seasonalRate = new SeasonalRateEntity(
            request.SeasonName,
            request.StartDate,
            request.EndDate,
            request.RateMultiplier,
            request.Description);

        seasonalRate.TenantId = tenantSettings.TenantId;
        seasonalRate.CreatedBy = request.CreatedBy;

        await _rateRepository.CreateSeasonalRateAsync(seasonalRate, cancellationToken);

        _logger.LogInformation(
            "Seasonal rate '{SeasonName}' (ID: {SeasonalRateId}) created for tenant {TenantId}",
            request.SeasonName, seasonalRate.Id, tenantSettings.TenantId);

        return new SeasonalRateDto
        {
            Id = seasonalRate.Id,
            SeasonName = seasonalRate.SeasonName,
            StartDate = seasonalRate.StartDate,
            EndDate = seasonalRate.EndDate,
            RateMultiplier = seasonalRate.RateMultiplier,
            Description = seasonalRate.Description,
            IsActive = seasonalRate.IsActive,
            CreatedAt = seasonalRate.CreatedAt
        };
    }
}
