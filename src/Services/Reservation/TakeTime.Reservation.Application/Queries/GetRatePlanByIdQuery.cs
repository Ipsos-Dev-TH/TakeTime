using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to retrieve a rate plan by its unique identifier.
/// </summary>
public sealed class GetRatePlanByIdQuery : IRequest<RatePlanDto?>
{
    public Guid Id { get; set; }

    public GetRatePlanByIdQuery()
    {
    }

    public GetRatePlanByIdQuery(Guid id)
    {
        Id = id;
    }
}

/// <summary>
/// Handler for <see cref="GetRatePlanByIdQuery"/>. Retrieves rate plan details
/// by ID scoped to the current tenant.
/// </summary>
public sealed class GetRatePlanByIdQueryHandler : IRequestHandler<GetRatePlanByIdQuery, RatePlanDto?>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetRatePlanByIdQueryHandler> _logger;

    public GetRatePlanByIdQueryHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<GetRatePlanByIdQueryHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<RatePlanDto?> Handle(GetRatePlanByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieving rate plan {RatePlanId} for tenant {TenantId}",
            request.Id, tenantSettings.TenantId);

        var ratePlan = await _rateRepository.GetRatePlanDtoByIdAsync(request.Id, cancellationToken);

        if (ratePlan is null)
        {
            _logger.LogWarning(
                "Rate plan {RatePlanId} not found for tenant {TenantId}",
                request.Id, tenantSettings.TenantId);
        }

        return ratePlan;
    }
}
