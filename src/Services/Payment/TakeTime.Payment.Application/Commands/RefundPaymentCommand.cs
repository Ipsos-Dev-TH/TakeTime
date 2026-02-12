using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Payment.Application.DTOs;
using TakeTime.Payment.Application.Services;

namespace TakeTime.Payment.Application.Commands;

/// <summary>
/// Command to process a refund for an existing payment. Supports both full
/// and partial refunds. Creates a new refund payment record and updates
/// the original payment status.
/// </summary>
public sealed class RefundPaymentCommand : IRequest<PaymentDto>
{
    public Guid PaymentId { get; set; }
    public decimal? Amount { get; set; }
    public string? Reason { get; set; }
    public string? ProcessedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="RefundPaymentCommand"/>. Validates refund eligibility,
/// creates a refund payment record, updates the original payment status, and
/// publishes a PaymentRefundedEvent.
/// </summary>
public sealed class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITenantContext _tenantContext;
    private readonly VATCalculationService _vatService;
    private readonly IMediator _mediator;
    private readonly ILogger<RefundPaymentCommandHandler> _logger;

    public RefundPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        ITenantContext tenantContext,
        VATCalculationService vatService,
        IMediator mediator,
        ILogger<RefundPaymentCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _tenantContext = tenantContext;
        _vatService = vatService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<PaymentDto> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var currency = tenantSettings.Currency ?? "THB";

        // Get original payment
        var originalPayment = await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        // Validate payment can be refunded
        if (!originalPayment.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase) &&
            !originalPayment.Status.Equals("PartiallyRefunded", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Payment {originalPayment.PaymentNumber} cannot be refunded. " +
                $"Current status is '{originalPayment.Status}'. Only Verified or PartiallyRefunded payments can be refunded.");
        }

        // Determine refund amount
        var refundAmount = request.Amount ?? originalPayment.Amount;

        if (refundAmount <= 0)
        {
            throw new InvalidOperationException("Refund amount must be greater than zero.");
        }

        if (refundAmount > originalPayment.Amount)
        {
            throw new InvalidOperationException(
                $"Refund amount ({refundAmount}) cannot exceed original payment amount ({originalPayment.Amount}).");
        }

        // Calculate VAT for refund
        var vatBreakdown = _vatService.CalculateVAT(refundAmount, tenantSettings);

        // Generate refund payment number
        var today = DateTime.UtcNow;
        var prefix = $"REF-{today:yyyyMMdd}";
        var count = await _paymentRepository.GetTodayPaymentCountAsync(tenantSettings.TenantId, cancellationToken);
        var refundNumber = $"{prefix}-{(count + 1):D4}";

        // Create refund payment record
        var refundPayment = new PaymentDto
        {
            Id = Guid.NewGuid(),
            PaymentNumber = refundNumber,
            TenantId = tenantSettings.TenantId,
            ReservationId = originalPayment.ReservationId,
            CustomerId = originalPayment.CustomerId,
            Amount = refundAmount,
            VATAmount = vatBreakdown.VATAmount,
            AmountBeforeVAT = vatBreakdown.AmountBeforeVAT,
            Currency = currency,
            PaymentMethod = originalPayment.PaymentMethod,
            Status = "Verified",
            Notes = string.IsNullOrWhiteSpace(request.Reason)
                ? $"Refund for payment {originalPayment.PaymentNumber}"
                : $"Refund for payment {originalPayment.PaymentNumber}. Reason: {request.Reason}",
            PaymentDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.ProcessedBy,
            IsRefund = true,
            OriginalPaymentId = request.PaymentId
        };

        await _paymentRepository.CreateAsync(refundPayment, cancellationToken);

        // Update original payment status
        var isFullRefund = refundAmount >= originalPayment.Amount;
        var newStatus = isFullRefund ? "Refunded" : "PartiallyRefunded";
        await _paymentRepository.UpdateStatusAsync(
            request.PaymentId, newStatus, request.ProcessedBy, cancellationToken);

        _logger.LogInformation(
            "Refund {RefundNumber} processed: {Amount} {Currency} for original payment " +
            "{OriginalPaymentNumber} ({RefundType}) by {ProcessedBy} for tenant {TenantId}",
            refundNumber, refundAmount, currency, originalPayment.PaymentNumber,
            isFullRefund ? "Full" : "Partial", request.ProcessedBy, tenantSettings.TenantId);

        // Publish domain event
        await _mediator.Publish(new PaymentRefundedEvent
        {
            PaymentId = refundPayment.Id,
            RefundNumber = refundNumber,
            OriginalPaymentId = request.PaymentId,
            OriginalPaymentNumber = originalPayment.PaymentNumber,
            TenantId = tenantSettings.TenantId,
            ReservationId = originalPayment.ReservationId,
            RefundAmount = refundAmount,
            IsFullRefund = isFullRefund,
            Reason = request.Reason,
            Currency = currency,
            OccurredOn = DateTime.UtcNow
        }, cancellationToken);

        return refundPayment;
    }
}

/// <summary>
/// Domain event published when a payment is refunded.
/// </summary>
public sealed class PaymentRefundedEvent : INotification
{
    public Guid PaymentId { get; set; }
    public string RefundNumber { get; set; } = string.Empty;
    public Guid OriginalPaymentId { get; set; }
    public string OriginalPaymentNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public decimal RefundAmount { get; set; }
    public bool IsFullRefund { get; set; }
    public string? Reason { get; set; }
    public string Currency { get; set; } = "THB";
    public DateTime OccurredOn { get; set; }
}
