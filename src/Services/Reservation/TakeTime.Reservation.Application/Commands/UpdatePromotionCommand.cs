using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to update an existing promotion. Supports partial updates.
/// </summary>
public sealed class UpdatePromotionCommand : IRequest<PromotionDto>
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int? MaxUses { get; set; }
    public decimal? MinimumBookingAmount { get; set; }
    public bool? IsActive { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="UpdatePromotionCommand"/>. Retrieves the promotion,
/// applies changes, and persists the update.
/// </summary>
public sealed class UpdatePromotionCommandHandler : IRequestHandler<UpdatePromotionCommand, PromotionDto>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdatePromotionCommandHandler> _logger;

    public UpdatePromotionCommandHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<UpdatePromotionCommandHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<PromotionDto> Handle(UpdatePromotionCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogInformation(
            "Updating promotion {PromotionId} for tenant {TenantId}",
            request.Id, tenantSettings.TenantId);

        var promotion = await _rateRepository.GetPromotionEntityByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Promotion with ID '{request.Id}' not found.");

        // Check for duplicate code if being changed
        if (!string.IsNullOrWhiteSpace(request.Code) && request.Code != promotion.Code)
        {
            var existing = await _rateRepository.GetPromotionByCodeAsync(request.Code, cancellationToken);
            if (existing is not null && existing.Id != request.Id)
            {
                throw new InvalidOperationException(
                    $"A promotion with code '{request.Code}' already exists.");
            }
        }

        // Apply updates
        var name = request.Name ?? promotion.Name;
        var code = request.Code ?? promotion.Code;
        var discountType = request.DiscountType ?? promotion.DiscountType;
        var discountValue = request.DiscountValue ?? promotion.DiscountValue;
        var validFrom = request.ValidFrom ?? promotion.ValidFrom;
        var validTo = request.ValidTo ?? promotion.ValidTo;
        var maxUses = request.MaxUses ?? promotion.MaxUses;
        var minimumBookingAmount = request.MinimumBookingAmount ?? promotion.MinimumBookingAmount;
        var description = promotion.Description; // Keep existing if not in request

        promotion.Update(
            name, code, discountType, discountValue,
            validFrom, validTo, maxUses, minimumBookingAmount, description);

        promotion.UpdatedBy = request.UpdatedBy;

        if (request.IsActive.HasValue)
        {
            promotion.SetActive(request.IsActive.Value);
        }

        await _rateRepository.UpdatePromotionAsync(promotion, cancellationToken);

        _logger.LogInformation(
            "Promotion {PromotionId} updated for tenant {TenantId}",
            request.Id, tenantSettings.TenantId);

        return await _rateRepository.GetPromotionDtoByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve updated promotion.");
    }
}
