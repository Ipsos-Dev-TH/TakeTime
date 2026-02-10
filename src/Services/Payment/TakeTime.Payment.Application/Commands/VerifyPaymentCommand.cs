using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Payment.Application.DTOs;

namespace TakeTime.Payment.Application.Commands;

/// <summary>
/// Command to verify (approve or reject) a pending payment.
/// </summary>
public sealed class VerifyPaymentCommand : IRequest<PaymentDto>
{
    public Guid PaymentId { get; set; }
    public bool IsApproved { get; set; }
    public string? VerificationNotes { get; set; }
    public string? RejectionReason { get; set; }
    public string? VerifiedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="VerifyPaymentCommand"/>. Verifies or rejects a pending
/// payment, updates the reservation balance accordingly, and publishes events.
/// </summary>
public sealed class VerifyPaymentCommandHandler : IRequestHandler<VerifyPaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IReservationServiceClient _reservationClient;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<VerifyPaymentCommandHandler> _logger;

    public VerifyPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IReservationServiceClient reservationClient,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<VerifyPaymentCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _reservationClient = reservationClient;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<PaymentDto> Handle(VerifyPaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        if (!payment.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Payment {payment.PaymentNumber} cannot be verified. " +
                $"Current status is '{payment.Status}'. Only Pending payments can be verified.");
        }

        var newStatus = request.IsApproved ? "Verified" : "Rejected";

        await _paymentRepository.UpdateStatusAsync(
            request.PaymentId, newStatus, request.VerifiedBy, cancellationToken);

        // If approved, update reservation balance
        if (request.IsApproved)
        {
            var totalPaid = await _paymentRepository.GetTotalPaidForReservationAsync(
                payment.ReservationId, cancellationToken);

            var totalRefunded = await _paymentRepository.GetTotalRefundedForReservationAsync(
                payment.ReservationId, cancellationToken);

            var reservation = await _reservationClient.GetReservationAsync(
                payment.ReservationId, cancellationToken);

            if (reservation is not null)
            {
                var netPaid = totalPaid - totalRefunded + (payment.IsRefund ? -payment.Amount : payment.Amount);
                var balanceDue = reservation.TotalAmount - netPaid;

                await _reservationClient.UpdateReservationBalanceAsync(
                    payment.ReservationId, netPaid, balanceDue, cancellationToken);
            }
        }

        _logger.LogInformation(
            "Payment {PaymentNumber} {Status} by {VerifiedBy} for tenant {TenantId}",
            payment.PaymentNumber, newStatus, request.VerifiedBy, tenantSettings.TenantId);

        // Publish domain event
        await _mediator.Publish(new PaymentVerifiedEvent
        {
            PaymentId = request.PaymentId,
            PaymentNumber = payment.PaymentNumber,
            TenantId = tenantSettings.TenantId,
            ReservationId = payment.ReservationId,
            IsApproved = request.IsApproved,
            RejectionReason = request.RejectionReason,
            Amount = payment.Amount,
            Currency = payment.Currency,
            OccurredOn = DateTime.UtcNow
        }, cancellationToken);

        // Return updated payment
        return await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve verified payment.");
    }
}

/// <summary>
/// Domain event published when a payment is verified or rejected.
/// </summary>
public sealed class PaymentVerifiedEvent : INotification
{
    public Guid PaymentId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public bool IsApproved { get; set; }
    public string? RejectionReason { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "THB";
    public DateTime OccurredOn { get; set; }
}
