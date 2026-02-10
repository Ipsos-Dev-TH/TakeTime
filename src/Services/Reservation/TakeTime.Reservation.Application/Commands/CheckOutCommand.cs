using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to check out a guest. Calculates the final balance including any
/// additional charges incurred during the stay, and transitions the reservation
/// status to CheckedOut.
/// </summary>
public sealed class CheckOutCommand : IRequest<CheckOutResultDto>
{
    public Guid ReservationId { get; set; }
    public string? CheckedOutBy { get; set; }
    public string? Notes { get; set; }
    public DateTime? ActualCheckOutTime { get; set; }
    public List<AdditionalChargeDto>? AdditionalCharges { get; set; }
    public bool GenerateReceipt { get; set; } = true;
}

/// <summary>
/// Represents an additional charge to apply at check-out.
/// </summary>
public sealed class AdditionalChargeDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string ChargeType { get; set; } = "Service";
}

/// <summary>
/// Result of a check-out operation, including final balance details.
/// </summary>
public sealed class CheckOutResultDto
{
    public ReservationDto Reservation { get; set; } = null!;
    public decimal OriginalTotal { get; set; }
    public decimal AdditionalCharges { get; set; }
    public decimal LateCheckOutFee { get; set; }
    public decimal FinalTotal { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal FinalBalance { get; set; }
    public decimal VATOnAdditionalCharges { get; set; }
    public string Currency { get; set; } = "THB";
    public bool HasOutstandingBalance { get; set; }
    public DateTime ActualCheckOutTime { get; set; }
}

/// <summary>
/// Handler for <see cref="CheckOutCommand"/>. Calculates the final balance,
/// applies any additional charges and late check-out fees, updates the reservation
/// status, and publishes a GuestCheckedOut domain event.
/// </summary>
public sealed class CheckOutCommandHandler : IRequestHandler<CheckOutCommand, CheckOutResultDto>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IPricingService _pricingService;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<CheckOutCommandHandler> _logger;

    public CheckOutCommandHandler(
        IReservationRepository reservationRepository,
        IPricingService pricingService,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<CheckOutCommandHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _pricingService = pricingService;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<CheckOutResultDto> Handle(CheckOutCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        var reservation = await _reservationRepository.GetReservationDtoByIdAsync(request.ReservationId, cancellationToken)
            ?? throw new InvalidOperationException($"Reservation {request.ReservationId} not found.");

        // Validate current status
        if (!reservation.Status.Equals("CheckedIn", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Reservation {reservation.ReservationNumber} cannot be checked out. " +
                $"Current status is '{reservation.Status}'. Only CheckedIn reservations can be checked out.");
        }

        var actualCheckOutTime = request.ActualCheckOutTime ?? DateTime.UtcNow;
        var originalTotal = reservation.TotalAmount;

        // Calculate late check-out fee if applicable
        decimal lateCheckOutFee = 0;
        if (tenantSettings.LateCheckOutFeeEnabled)
        {
            lateCheckOutFee = CalculateLateCheckOutFee(
                reservation.CheckOutDate, actualCheckOutTime, tenantSettings);
        }

        // Calculate additional charges
        decimal additionalChargesTotal = 0;
        if (request.AdditionalCharges is { Count: > 0 })
        {
            additionalChargesTotal = request.AdditionalCharges.Sum(c => c.Amount);
        }

        // Apply VAT on additional charges if tenant is VAT registered
        decimal vatOnAdditional = 0;
        if (tenantSettings.IsVATRegistered && (additionalChargesTotal > 0 || lateCheckOutFee > 0))
        {
            var chargeableAmount = additionalChargesTotal + lateCheckOutFee;
            vatOnAdditional = _pricingService.CalculateVAT(chargeableAmount, tenantSettings);
        }

        var totalAdditional = additionalChargesTotal + lateCheckOutFee;
        if (!tenantSettings.IsVATInclusive)
        {
            totalAdditional += vatOnAdditional;
        }

        var finalTotal = originalTotal + totalAdditional;
        var totalPaid = reservation.AmountPaid;
        var finalBalance = finalTotal - totalPaid;

        // Build additional items list for persistence
        var additionalItems = request.AdditionalCharges?.Select(c => new CheckOutAdditionalItem
        {
            ItemName = c.Description,
            ItemType = c.ChargeType,
            Quantity = 1,
            UnitPrice = c.Amount,
            TotalPrice = c.Amount,
            ServiceDate = actualCheckOutTime,
            Currency = tenantSettings.Currency ?? "THB"
        }).ToList();

        // Persist check-out
        await _reservationRepository.CheckOutAsync(
            request.ReservationId,
            actualCheckOutTime,
            additionalChargesTotal,
            lateCheckOutFee,
            vatOnAdditional,
            finalTotal,
            finalBalance,
            request.CheckedOutBy,
            request.Notes,
            additionalItems,
            cancellationToken);

        _logger.LogInformation(
            "Guest checked out for reservation {ReservationNumber} at {CheckOutTime}. Final balance: {Balance} for tenant {TenantId}",
            reservation.ReservationNumber, actualCheckOutTime, finalBalance, tenantSettings.TenantId);

        // Publish domain event
        await _mediator.Publish(new GuestCheckedOutEvent
        {
            ReservationId = request.ReservationId,
            ReservationNumber = reservation.ReservationNumber,
            TenantId = tenantSettings.TenantId,
            CustomerName = reservation.CustomerName,
            ActualCheckOutTime = actualCheckOutTime,
            FinalTotal = finalTotal,
            FinalBalance = finalBalance,
            HasOutstandingBalance = finalBalance > 0,
            GenerateReceipt = request.GenerateReceipt,
            Currency = tenantSettings.Currency ?? "THB",
            OccurredOn = DateTime.UtcNow
        }, cancellationToken);

        // Return updated reservation
        var updatedReservation = await _reservationRepository.GetReservationDtoByIdAsync(
            request.ReservationId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve checked-out reservation.");

        return new CheckOutResultDto
        {
            Reservation = updatedReservation,
            OriginalTotal = originalTotal,
            AdditionalCharges = additionalChargesTotal,
            LateCheckOutFee = lateCheckOutFee,
            FinalTotal = finalTotal,
            TotalPaid = totalPaid,
            FinalBalance = finalBalance,
            VATOnAdditionalCharges = vatOnAdditional,
            Currency = tenantSettings.Currency ?? "THB",
            HasOutstandingBalance = finalBalance > 0,
            ActualCheckOutTime = actualCheckOutTime
        };
    }

    private static decimal CalculateLateCheckOutFee(
        DateTime scheduledCheckOut,
        DateTime actualCheckOut,
        TenantSettings tenantSettings)
    {
        var scheduledCheckOutTime = scheduledCheckOut.Date.Add(tenantSettings.StandardCheckOutTime);
        if (actualCheckOut <= scheduledCheckOutTime)
        {
            return 0;
        }

        var lateHours = (actualCheckOut - scheduledCheckOutTime).TotalHours;

        if (lateHours <= (double)tenantSettings.GracePeriodHours)
        {
            return 0;
        }

        if (lateHours <= (double)tenantSettings.HalfDayLateCheckOutHours)
        {
            return tenantSettings.LateCheckOutFeeHalfDay;
        }

        return tenantSettings.LateCheckOutFeeFullDay;
    }
}

/// <summary>
/// Domain event published when a guest checks out.
/// </summary>
public sealed class GuestCheckedOutEvent : INotification
{
    public Guid ReservationId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime ActualCheckOutTime { get; set; }
    public decimal FinalTotal { get; set; }
    public decimal FinalBalance { get; set; }
    public bool HasOutstandingBalance { get; set; }
    public bool GenerateReceipt { get; set; }
    public string Currency { get; set; } = "THB";
    public DateTime OccurredOn { get; set; }
}
