using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to retrieve a promotion by its unique identifier.
/// </summary>
public sealed class GetPromotionByIdQuery : IRequest<PromotionDto?>
{
    public Guid Id { get; set; }

    public GetPromotionByIdQuery()
    {
    }

    public GetPromotionByIdQuery(Guid id)
    {
        Id = id;
    }
}

/// <summary>
/// Handler for <see cref="GetPromotionByIdQuery"/>. Retrieves promotion details
/// by ID scoped to the current tenant.
/// </summary>
public sealed class GetPromotionByIdQueryHandler : IRequestHandler<GetPromotionByIdQuery, PromotionDto?>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetPromotionByIdQueryHandler> _logger;

    public GetPromotionByIdQueryHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<GetPromotionByIdQueryHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<PromotionDto?> Handle(GetPromotionByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieving promotion {PromotionId} for tenant {TenantId}",
            request.Id, tenantSettings.TenantId);

        var promotion = await _rateRepository.GetPromotionDtoByIdAsync(request.Id, cancellationToken);

        if (promotion is null)
        {
            _logger.LogWarning(
                "Promotion {PromotionId} not found for tenant {TenantId}",
                request.Id, tenantSettings.TenantId);
        }

        return promotion;
    }
}
