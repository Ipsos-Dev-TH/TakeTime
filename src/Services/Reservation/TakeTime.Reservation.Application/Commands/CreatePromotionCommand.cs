using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;
using TakeTime.Reservation.Domain.Entities;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to create a new promotion or discount code.
/// </summary>
public sealed class CreatePromotionCommand : IRequest<PromotionDto>
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string DiscountType { get; set; } = "Percentage";
    public decimal DiscountValue { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int? MaxUses { get; set; }
    public decimal? MinimumBookingAmount { get; set; }
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="CreatePromotionCommand"/>. Creates a new promotion
/// entity and persists it for the current tenant.
/// </summary>
public sealed class CreatePromotionCommandHandler : IRequestHandler<CreatePromotionCommand, PromotionDto>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreatePromotionCommandHandler> _logger;

    public CreatePromotionCommandHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<CreatePromotionCommandHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<PromotionDto> Handle(CreatePromotionCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogInformation(
            "Creating promotion '{Name}' for tenant {TenantId}",
            request.Name, tenantSettings.TenantId);

        // Check for duplicate discount code if provided
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var existing = await _rateRepository.GetPromotionByCodeAsync(request.Code, cancellationToken);
            if (existing is not null)
            {
                throw new InvalidOperationException(
                    $"A promotion with code '{request.Code}' already exists.");
            }
        }

        var promotion = new Promotion(
            request.Name,
            request.DiscountType,
            request.DiscountValue,
            request.Code,
            request.ValidFrom,
            request.ValidTo,
            request.MaxUses,
            request.MinimumBookingAmount,
            request.Description);

        promotion.TenantId = tenantSettings.TenantId;
        promotion.CreatedBy = request.CreatedBy;

        await _rateRepository.CreatePromotionAsync(promotion, cancellationToken);

        _logger.LogInformation(
            "Promotion '{Name}' (ID: {PromotionId}) created for tenant {TenantId}",
            request.Name, promotion.Id, tenantSettings.TenantId);

        return await _rateRepository.GetPromotionDtoByIdAsync(promotion.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve newly created promotion.");
    }
}
