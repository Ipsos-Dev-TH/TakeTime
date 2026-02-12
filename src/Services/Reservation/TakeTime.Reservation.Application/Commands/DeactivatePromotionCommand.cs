using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to deactivate a promotion. Sets IsActive to false.
/// </summary>
public sealed class DeactivatePromotionCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
    public string? DeactivatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="DeactivatePromotionCommand"/>. Retrieves the promotion
/// and deactivates it.
/// </summary>
public sealed class DeactivatePromotionCommandHandler : IRequestHandler<DeactivatePromotionCommand, Unit>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DeactivatePromotionCommandHandler> _logger;

    public DeactivatePromotionCommandHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<DeactivatePromotionCommandHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeactivatePromotionCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogInformation(
            "Deactivating promotion {PromotionId} for tenant {TenantId}",
            request.Id, tenantSettings.TenantId);

        var promotion = await _rateRepository.GetPromotionEntityByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Promotion with ID '{request.Id}' not found.");

        promotion.Deactivate();
        promotion.UpdatedBy = request.DeactivatedBy;

        await _rateRepository.UpdatePromotionAsync(promotion, cancellationToken);

        _logger.LogInformation(
            "Promotion '{PromotionName}' (ID: {PromotionId}) deactivated for tenant {TenantId}",
            promotion.Name, request.Id, tenantSettings.TenantId);

        return Unit.Value;
    }
}
