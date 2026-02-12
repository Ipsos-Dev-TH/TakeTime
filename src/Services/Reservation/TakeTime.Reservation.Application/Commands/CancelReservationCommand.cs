using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to cancel a reservation. Enforces the tenant's cancellation policy,
/// calculates any applicable cancellation fees, and transitions the reservation
/// status to Cancelled.
/// </summary>
public sealed class CancelReservationCommand : IRequest<ReservationDto>
{
    public Guid ReservationId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? CancelledBy { get; set; }
    public bool WaiveCancellationFee { get; set; }
    public bool SendCancellationEmail { get; set; } = true;
}

/// <summary>
/// Result of a cancellation policy evaluation.
/// </summary>
public sealed class CancellationPolicyResult
{
    public bool IsCancellationAllowed { get; set; }
    public decimal CancellationFeePercentage { get; set; }
    public decimal CancellationFeeAmount { get; set; }
    public string PolicyDescription { get; set; } = string.Empty;
    public int DaysBeforeCheckIn { get; set; }
    public decimal RefundAmount { get; set; }
}

/// <summary>
/// Handler for <see cref="CancelReservationCommand"/>. Evaluates the tenant's
/// cancellation policy, applies fees if applicable, updates the reservation status,
/// and publishes cancellation events.
/// </summary>
public sealed class CancelReservationCommandHandler : IRequestHandler<CancelReservationCommand, ReservationDto>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<CancelReservationCommandHandler> _logger;

    public CancelReservationCommandHandler(
        IReservationRepository reservationRepository,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<CancelReservationCommandHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<ReservationDto> Handle(CancelReservationCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        var reservation = await _reservationRepository.GetByIdAsync(request.ReservationId, cancellationToken)
            ?? throw new InvalidOperationException($"Reservation {request.ReservationId} not found.");

        // Validate current status allows cancellation
        var nonCancellableStatuses = new[] { "Cancelled", "CheckedOut", "NoShow" };
        if (nonCancellableStatuses.Contains(reservation.Status, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Reservation {reservation.ReservationNumber} cannot be cancelled. " +
                $"Current status is '{reservation.Status}'.");
        }

        // Evaluate cancellation policy
        var policyResult = EvaluateCancellationPolicy(reservation, tenantSettings, request.WaiveCancellationFee);

        if (!policyResult.IsCancellationAllowed && !request.WaiveCancellationFee)
        {
            throw new InvalidOperationException(
                $"Cancellation not allowed for reservation {reservation.ReservationNumber}. " +
                $"Policy: {policyResult.PolicyDescription}");
        }

        // Calculate cancellation fee
        var cancellationFee = request.WaiveCancellationFee ? 0 : policyResult.CancellationFeeAmount;
        var amountPaid = reservation.TotalAmount - reservation.BalanceDue;
        var refundAmount = amountPaid - cancellationFee;
        if (refundAmount < 0) refundAmount = 0;

        // Update reservation status
        await _reservationRepository.CancelReservationAsync(
            request.ReservationId,
            request.Reason,
            cancellationFee,
            refundAmount,
            request.CancelledBy,
            cancellationToken);

        _logger.LogInformation(
            "Reservation {ReservationNumber} cancelled by {CancelledBy}. Fee: {Fee}, Refund: {Refund} for tenant {TenantId}",
            reservation.ReservationNumber, request.CancelledBy, cancellationFee, refundAmount, tenantSettings.TenantId);

        // Publish domain event
        await _mediator.Publish(new ReservationCancelledEvent
        {
            ReservationId = request.ReservationId,
            ReservationNumber = reservation.ReservationNumber,
            TenantId = tenantSettings.TenantId,
            CustomerName = reservation.CustomerName,
            CustomerEmail = null, // CustomerEmail not available in SummaryDto
            CancellationReason = request.Reason,
            CancellationFee = cancellationFee,
            RefundAmount = refundAmount,
            SendCancellationEmail = request.SendCancellationEmail,
            OccurredOn = DateTime.UtcNow
        }, cancellationToken);

        // Return updated reservation
        return await _reservationRepository.GetReservationDtoByIdAsync(request.ReservationId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve cancelled reservation.");
    }

    private static CancellationPolicyResult EvaluateCancellationPolicy(
        ReservationSummaryDto reservation,
        TenantSettings tenantSettings,
        bool waiveFee)
    {
        var daysBeforeCheckIn = (reservation.CheckInDate.Date - DateTime.UtcNow.Date).Days;
        var totalAmount = reservation.TotalAmount;

        if (waiveFee)
        {
            return new CancellationPolicyResult
            {
                IsCancellationAllowed = true,
                CancellationFeePercentage = 0,
                CancellationFeeAmount = 0,
                DaysBeforeCheckIn = daysBeforeCheckIn,
                RefundAmount = reservation.BalanceDue >= 0 ? 0 : Math.Abs(reservation.BalanceDue),
                PolicyDescription = "Fee waived by staff."
            };
        }

        // Apply tenant-specific cancellation policy
        var freeCancellationDays = tenantSettings.FreeCancellationDays;
        decimal feePercentage;
        string policyDescription;

        if (daysBeforeCheckIn >= freeCancellationDays)
        {
            // Free cancellation period
            feePercentage = 0;
            policyDescription = $"Free cancellation (more than {freeCancellationDays} days before check-in).";
        }
        else if (daysBeforeCheckIn >= tenantSettings.LateCancellationDays)
        {
            // Late cancellation - partial fee
            feePercentage = tenantSettings.LateCancellationFeePercentage;
            policyDescription = $"Late cancellation fee ({feePercentage}% of total). " +
                $"Check-in is in {daysBeforeCheckIn} days.";
        }
        else if (daysBeforeCheckIn >= 0)
        {
            // Very late / same-day cancellation - higher fee
            feePercentage = tenantSettings.SameDayCancellationFeePercentage;
            policyDescription = $"Same-day/last-minute cancellation fee ({feePercentage}% of total). " +
                $"Check-in is in {daysBeforeCheckIn} days.";
        }
        else
        {
            // Past check-in date (no-show scenario)
            feePercentage = 100;
            policyDescription = "Check-in date has passed. Full charge applies.";
        }

        var feeAmount = Math.Round(totalAmount * feePercentage / 100m, 2);

        return new CancellationPolicyResult
        {
            IsCancellationAllowed = true,
            CancellationFeePercentage = feePercentage,
            CancellationFeeAmount = feeAmount,
            DaysBeforeCheckIn = daysBeforeCheckIn,
            RefundAmount = Math.Max(0, totalAmount - feeAmount),
            PolicyDescription = policyDescription
        };
    }
}

/// <summary>
/// Domain event published when a reservation is cancelled.
/// </summary>
public sealed class ReservationCancelledEvent : INotification
{
    public Guid ReservationId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string CancellationReason { get; set; } = string.Empty;
    public decimal CancellationFee { get; set; }
    public decimal RefundAmount { get; set; }
    public bool SendCancellationEmail { get; set; }
    public DateTime OccurredOn { get; set; }
}
