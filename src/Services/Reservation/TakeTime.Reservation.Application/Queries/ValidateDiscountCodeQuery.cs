using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to validate a discount code against booking parameters. Returns whether
/// the code is valid and the calculated discount amount.
/// </summary>
public sealed class ValidateDiscountCodeQuery : IRequest<DiscountValidationResultDto>
{
    public string Code { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public decimal BookingAmount { get; set; }
}

/// <summary>
/// Handler for <see cref="ValidateDiscountCodeQuery"/>. Looks up the discount code,
/// validates it against the booking parameters, and calculates the discount amount.
/// </summary>
public sealed class ValidateDiscountCodeQueryHandler
    : IRequestHandler<ValidateDiscountCodeQuery, DiscountValidationResultDto>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ValidateDiscountCodeQueryHandler> _logger;

    public ValidateDiscountCodeQueryHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<ValidateDiscountCodeQueryHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<DiscountValidationResultDto> Handle(
        ValidateDiscountCodeQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug(
            "Validating discount code '{Code}' for tenant {TenantId}. " +
            "Check-in: {CheckIn}, Amount: {Amount}",
            request.Code, tenantSettings.TenantId, request.CheckInDate, request.BookingAmount);

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return new DiscountValidationResultDto
            {
                IsValid = false,
                Message = "Discount code is required."
            };
        }

        // Look up the promotion by code
        var promotion = await _rateRepository.GetPromotionByCodeAsync(request.Code, cancellationToken);

        if (promotion is null)
        {
            _logger.LogDebug("Discount code '{Code}' not found", request.Code);
            return new DiscountValidationResultDto
            {
                IsValid = false,
                Message = $"Discount code '{request.Code}' not found."
            };
        }

        // Validate the promotion against booking parameters
        if (!promotion.IsValid(request.CheckInDate, request.BookingAmount))
        {
            var reason = GetInvalidReason(promotion, request);
            _logger.LogDebug(
                "Discount code '{Code}' is not valid: {Reason}",
                request.Code, reason);

            return new DiscountValidationResultDto
            {
                IsValid = false,
                Message = reason,
                PromotionName = promotion.Name,
                DiscountType = promotion.DiscountType,
                DiscountValue = promotion.DiscountValue
            };
        }

        // Calculate the discount
        var calculatedDiscount = promotion.CalculateDiscount(request.BookingAmount);

        _logger.LogInformation(
            "Discount code '{Code}' validated. Discount: {Discount} ({DiscountType}: {DiscountValue})",
            request.Code, calculatedDiscount, promotion.DiscountType, promotion.DiscountValue);

        return new DiscountValidationResultDto
        {
            IsValid = true,
            Message = "Discount code is valid.",
            PromotionName = promotion.Name,
            DiscountType = promotion.DiscountType,
            DiscountValue = promotion.DiscountValue,
            CalculatedDiscount = calculatedDiscount
        };
    }

    private static string GetInvalidReason(
        Domain.Entities.Promotion promotion,
        ValidateDiscountCodeQuery request)
    {
        if (!promotion.IsActive)
            return "This promotion is no longer active.";

        if (promotion.ValidFrom.HasValue && request.CheckInDate.Date < promotion.ValidFrom.Value.Date)
            return $"This promotion is valid from {promotion.ValidFrom.Value:yyyy-MM-dd}.";

        if (promotion.ValidTo.HasValue && request.CheckInDate.Date > promotion.ValidTo.Value.Date)
            return $"This promotion expired on {promotion.ValidTo.Value:yyyy-MM-dd}.";

        if (promotion.MaxUses.HasValue && promotion.CurrentUses >= promotion.MaxUses.Value)
            return "This promotion has reached its maximum number of uses.";

        if (promotion.MinimumBookingAmount.HasValue && request.BookingAmount < promotion.MinimumBookingAmount.Value)
            return $"Minimum booking amount of {promotion.MinimumBookingAmount.Value:N2} is required.";

        return "This promotion is not valid for the selected dates.";
    }
}
