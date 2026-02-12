using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to retrieve all rate plans for the current tenant.
/// </summary>
public sealed class GetRatePlansQuery : IRequest<List<RatePlanDto>>
{
}

/// <summary>
/// Handler for <see cref="GetRatePlansQuery"/>. Retrieves all rate plans
/// scoped to the current tenant.
/// </summary>
public sealed class GetRatePlansQueryHandler : IRequestHandler<GetRatePlansQuery, List<RatePlanDto>>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetRatePlansQueryHandler> _logger;

    public GetRatePlansQueryHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<GetRatePlansQueryHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<RatePlanDto>> Handle(GetRatePlansQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug("Getting all rate plans for tenant {TenantId}", tenantSettings.TenantId);

        var ratePlans = await _rateRepository.GetAllRatePlansAsync(cancellationToken);

        _logger.LogDebug(
            "Found {Count} rate plans for tenant {TenantId}",
            ratePlans.Count, tenantSettings.TenantId);

        return ratePlans;
    }
}
