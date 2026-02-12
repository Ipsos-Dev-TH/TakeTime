using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to retrieve seasonal rates for the current tenant, optionally filtered by year.
/// </summary>
public sealed class GetSeasonalRatesQuery : IRequest<List<SeasonalRateDto>>
{
    public int? Year { get; set; }
}

/// <summary>
/// Handler for <see cref="GetSeasonalRatesQuery"/>. Retrieves seasonal rates
/// scoped to the current tenant.
/// </summary>
public sealed class GetSeasonalRatesQueryHandler
    : IRequestHandler<GetSeasonalRatesQuery, List<SeasonalRateDto>>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetSeasonalRatesQueryHandler> _logger;

    public GetSeasonalRatesQueryHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<GetSeasonalRatesQueryHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<SeasonalRateDto>> Handle(
        GetSeasonalRatesQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug(
            "Getting seasonal rates for tenant {TenantId}, year: {Year}",
            tenantSettings.TenantId, request.Year);

        var rates = await _rateRepository.GetSeasonalRatesAsync(request.Year, cancellationToken);

        _logger.LogDebug(
            "Found {Count} seasonal rates for tenant {TenantId}",
            rates.Count, tenantSettings.TenantId);

        return rates;
    }
}
