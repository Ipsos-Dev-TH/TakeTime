using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Payment.Application.DTOs;

namespace TakeTime.Payment.Application.Commands;

/// <summary>
/// Command to void a payment. Payment can only be voided within the same day
/// it was created.
/// </summary>
public sealed class VoidPaymentCommand : IRequest<PaymentDto>
{
    public Guid PaymentId { get; set; }
    public string? Reason { get; set; }
    public string? ProcessedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="VoidPaymentCommand"/>. Validates that the payment
/// was created on the same day, updates the status to Voided, and publishes
/// a PaymentVoidedEvent.
/// </summary>
public sealed class VoidPaymentCommandHandler : IRequestHandler<VoidPaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<VoidPaymentCommandHandler> _logger;

    public VoidPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<VoidPaymentCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<PaymentDto> Handle(VoidPaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        // Validate payment can be voided
        if (payment.Status.Equals("Voided", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Payment {payment.PaymentNumber} has already been voided.");
        }

        if (payment.Status.Equals("Refunded", StringComparison.OrdinalIgnoreCase) ||
            payment.Status.Equals("PartiallyRefunded", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Payment {payment.PaymentNumber} cannot be voided because it has been refunded. " +
                $"Current status: '{payment.Status}'.");
        }

        // Validate same-day void
        var today = DateTime.UtcNow.Date;
        var paymentDate = payment.PaymentDate.Date;
        if (paymentDate != today)
        {
            throw new InvalidOperationException(
                $"Payment {payment.PaymentNumber} cannot be voided. " +
                $"Void is only allowed on the same day the payment was created. " +
                $"Payment date: {paymentDate:yyyy-MM-dd}, Today: {today:yyyy-MM-dd}. " +
                "Use refund for payments from previous days.");
        }

        // Update status to Voided
        var notes = string.IsNullOrWhiteSpace(request.Reason)
            ? $"Voided by {request.ProcessedBy}"
            : $"Voided by {request.ProcessedBy}. Reason: {request.Reason}";

        payment.Status = "Voided";
        payment.Notes = string.IsNullOrWhiteSpace(payment.Notes)
            ? notes
            : $"{payment.Notes}\n{notes}";
        payment.UpdatedAt = DateTime.UtcNow;

        await _paymentRepository.UpdateAsync(payment, cancellationToken);

        _logger.LogInformation(
            "Payment {PaymentNumber} voided by {ProcessedBy} for tenant {TenantId}. Reason: {Reason}",
            payment.PaymentNumber, request.ProcessedBy, tenantSettings.TenantId, request.Reason);

        // Publish domain event
        await _mediator.Publish(new PaymentVoidedEvent
        {
            PaymentId = request.PaymentId,
            PaymentNumber = payment.PaymentNumber,
            TenantId = tenantSettings.TenantId,
            ReservationId = payment.ReservationId,
            Amount = payment.Amount,
            Reason = request.Reason,
            VoidedBy = request.ProcessedBy,
            Currency = payment.Currency,
            OccurredOn = DateTime.UtcNow
        }, cancellationToken);

        return payment;
    }
}

/// <summary>
/// Domain event published when a payment is voided.
/// </summary>
public sealed class PaymentVoidedEvent : INotification
{
    public Guid PaymentId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public string? VoidedBy { get; set; }
    public string Currency { get; set; } = "THB";
    public DateTime OccurredOn { get; set; }
}
