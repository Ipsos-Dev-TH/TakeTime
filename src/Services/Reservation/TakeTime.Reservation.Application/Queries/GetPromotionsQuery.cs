using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to retrieve promotions for the current tenant, optionally filtered
/// to active promotions only.
/// </summary>
public sealed class GetPromotionsQuery : IRequest<List<PromotionDto>>
{
    public bool? ActiveOnly { get; set; }
}

/// <summary>
/// Handler for <see cref="GetPromotionsQuery"/>. Retrieves promotions
/// scoped to the current tenant.
/// </summary>
public sealed class GetPromotionsQueryHandler
    : IRequestHandler<GetPromotionsQuery, List<PromotionDto>>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetPromotionsQueryHandler> _logger;

    public GetPromotionsQueryHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<GetPromotionsQueryHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<PromotionDto>> Handle(
        GetPromotionsQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug(
            "Getting promotions for tenant {TenantId}, activeOnly: {ActiveOnly}",
            tenantSettings.TenantId, request.ActiveOnly);

        var promotions = await _rateRepository.GetPromotionsAsync(request.ActiveOnly, cancellationToken);

        _logger.LogDebug(
            "Found {Count} promotions for tenant {TenantId}",
            promotions.Count, tenantSettings.TenantId);

        return promotions;
    }
}
