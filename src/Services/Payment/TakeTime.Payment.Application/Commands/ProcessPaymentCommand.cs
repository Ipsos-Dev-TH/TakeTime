using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Payment.Application.DTOs;
using TakeTime.Payment.Application.Services;

namespace TakeTime.Payment.Application.Commands;

/// <summary>
/// Command to process a payment for a reservation. Records the payment,
/// calculates VAT if applicable, and updates the reservation balance.
/// </summary>
public sealed class ProcessPaymentCommand : IRequest<PaymentDto>
{
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? TransactionReference { get; set; }
    public string? GatewayTransactionId { get; set; }
    public string? Notes { get; set; }
    public DateTime? PaymentDate { get; set; }
    public bool IsRefund { get; set; }
    public Guid? OriginalPaymentId { get; set; }
    public string? ProcessedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="ProcessPaymentCommand"/>. Validates the payment amount,
/// calculates VAT based on tenant settings, records the payment, and publishes
/// a PaymentProcessed domain event.
/// </summary>
public sealed class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITenantContext _tenantContext;
    private readonly VATCalculationService _vatService;
    private readonly IMediator _mediator;
    private readonly ILogger<ProcessPaymentCommandHandler> _logger;

    public ProcessPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        ITenantContext tenantContext,
        VATCalculationService vatService,
        IMediator mediator,
        ILogger<ProcessPaymentCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _tenantContext = tenantContext;
        _vatService = vatService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<PaymentDto> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var currency = tenantSettings.Currency ?? "THB";

        // Validate payment amount
        if (request.Amount <= 0 && !request.IsRefund)
        {
            throw new InvalidOperationException("Payment amount must be greater than zero.");
        }

        if (request.IsRefund && request.Amount <= 0)
        {
            throw new InvalidOperationException("Refund amount must be greater than zero.");
        }

        // Validate refund has original payment
        if (request.IsRefund && !request.OriginalPaymentId.HasValue)
        {
            throw new InvalidOperationException("Original payment ID is required for refunds.");
        }

        if (request.IsRefund && request.OriginalPaymentId.HasValue)
        {
            var originalPayment = await _paymentRepository.GetByIdAsync(
                request.OriginalPaymentId.Value, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Original payment {request.OriginalPaymentId} not found.");

            if (request.Amount > originalPayment.Amount)
            {
                throw new InvalidOperationException(
                    $"Refund amount ({request.Amount}) cannot exceed original payment amount ({originalPayment.Amount}).");
            }
        }

        // Calculate VAT breakdown
        var vatBreakdown = _vatService.CalculateVAT(request.Amount, tenantSettings);

        // Generate payment number
        var paymentNumber = await GeneratePaymentNumberAsync(tenantSettings.TenantId, cancellationToken);

        var paymentId = Guid.NewGuid();
        var paymentDate = request.PaymentDate ?? DateTime.UtcNow;

        var payment = new PaymentDto
        {
            Id = paymentId,
            PaymentNumber = paymentNumber,
            TenantId = tenantSettings.TenantId,
            ReservationId = request.ReservationId,
            Amount = request.Amount,
            VATAmount = vatBreakdown.VATAmount,
            AmountBeforeVAT = vatBreakdown.AmountBeforeVAT,
            Currency = currency,
            PaymentMethod = request.PaymentMethod,
            Status = "Pending",
            TransactionReference = request.TransactionReference,
            GatewayTransactionId = request.GatewayTransactionId,
            Notes = request.Notes,
            PaymentDate = paymentDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.ProcessedBy,
            IsRefund = request.IsRefund,
            OriginalPaymentId = request.OriginalPaymentId
        };

        // Persist the payment
        await _paymentRepository.CreateAsync(payment, cancellationToken);

        _logger.LogInformation(
            "Payment {PaymentNumber} processed: {Amount} {Currency} ({Method}) for reservation {ReservationId} by {ProcessedBy} for tenant {TenantId}",
            paymentNumber, request.Amount, currency, request.PaymentMethod,
            request.ReservationId, request.ProcessedBy, tenantSettings.TenantId);

        // Publish domain event
        await _mediator.Publish(new PaymentProcessedEvent
        {
            PaymentId = paymentId,
            PaymentNumber = paymentNumber,
            TenantId = tenantSettings.TenantId,
            ReservationId = request.ReservationId,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            IsRefund = request.IsRefund,
            Currency = currency,
            OccurredOn = DateTime.UtcNow
        }, cancellationToken);

        return payment;
    }

    private async Task<string> GeneratePaymentNumberAsync(string tenantId, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow;
        var prefix = $"PAY-{today:yyyyMMdd}";
        var count = await _paymentRepository.GetTodayPaymentCountAsync(tenantId, cancellationToken);
        return $"{prefix}-{(count + 1):D4}";
    }
}

/// <summary>
/// Domain event published when a payment is processed.
/// </summary>
public sealed class PaymentProcessedEvent : INotification
{
    public Guid PaymentId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public bool IsRefund { get; set; }
    public string Currency { get; set; } = "THB";
    public DateTime OccurredOn { get; set; }
}

/// <summary>
/// Tenant context interface for Payment service.
/// </summary>
public interface ITenantContext
{
    string TenantId { get; }
    Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Tenant settings relevant to the Payment service.
/// </summary>
public class TenantSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string? Currency { get; set; } = "THB";
    public bool IsVATRegistered { get; set; }
    public decimal VATRate { get; set; } = 7;
    public bool IsVATInclusive { get; set; }
    public string? VATRegistrationNumber { get; set; }
    public string? BusinessName { get; set; }
    public string? BusinessAddress { get; set; }
    public string? BusinessTaxId { get; set; }
    public string? BusinessPhone { get; set; }
    public string? BusinessEmail { get; set; }
    public string? BusinessBranch { get; set; }
}

/// <summary>
/// Repository interface for payment operations.
/// </summary>
public interface IPaymentRepository
{
    Task<PaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task CreateAsync(PaymentDto payment, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid paymentId, string status, string? updatedBy, CancellationToken cancellationToken = default);
    Task<int> GetTodayPaymentCountAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<List<PaymentSummaryDto>> GetPaymentsByReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalPaidForReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalRefundedForReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for receipt operations.
/// </summary>
public interface IReceiptRepository
{
    Task<ReceiptDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task CreateAsync(ReceiptDto receipt, CancellationToken cancellationToken = default);
    Task<int> GetTodayReceiptCountAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<ReceiptDto?> GetByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for tax invoice operations.
/// </summary>
public interface ITaxInvoiceRepository
{
    Task<TaxInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task CreateAsync(TaxInvoiceDto invoice, CancellationToken cancellationToken = default);
    Task<int> GetTodayInvoiceCountAsync(string tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for fetching reservation data from the Reservation service.
/// </summary>
public interface IReservationServiceClient
{
    Task<ReservationInfoDto?> GetReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task UpdateReservationBalanceAsync(Guid reservationId, decimal amountPaid, decimal balanceDue, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight reservation info returned by the Reservation service client.
/// </summary>
public sealed class ReservationInfoDto
{
    public Guid Id { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public string Currency { get; set; } = "THB";
    public string Status { get; set; } = string.Empty;
    public List<ReservationLineItemDto> Items { get; set; } = [];
}

/// <summary>
/// Reservation line item for receipt generation.
/// </summary>
public sealed class ReservationLineItemDto
{
    public string Description { get; set; } = string.Empty;
    public string? ItemCode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Category { get; set; }
}
