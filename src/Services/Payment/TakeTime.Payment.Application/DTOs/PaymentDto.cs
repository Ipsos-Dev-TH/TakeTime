namespace TakeTime.Payment.Application.DTOs;

/// <summary>
/// Full payment data transfer object.
/// </summary>
public sealed class PaymentDto
{
    public Guid Id { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public string? ReservationNumber { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal Amount { get; set; }
    public decimal VATAmount { get; set; }
    public decimal AmountBeforeVAT { get; set; }
    public string Currency { get; set; } = "THB";
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TransactionReference { get; set; }
    public string? GatewayTransactionId { get; set; }
    public string? Notes { get; set; }
    public DateTime PaymentDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public bool IsRefund { get; set; }
    public Guid? OriginalPaymentId { get; set; }
}

/// <summary>
/// DTO for creating a new payment.
/// </summary>
public sealed class CreatePaymentDto
{
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? TransactionReference { get; set; }
    public string? Notes { get; set; }
    public DateTime? PaymentDate { get; set; }
    public bool IsRefund { get; set; }
    public Guid? OriginalPaymentId { get; set; }
}

/// <summary>
/// Lightweight payment summary DTO for list views.
/// </summary>
public sealed class PaymentSummaryDto
{
    public Guid Id { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public string? ReservationNumber { get; set; }
    public string? CustomerName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "THB";
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public bool IsRefund { get; set; }
}

/// <summary>
/// Search criteria for filtering payments.
/// </summary>
public sealed class PaymentSearchCriteria
{
    public string? Status { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? ReservationId { get; set; }
    public bool? IsRefund { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Paginated result wrapper for payment queries.
/// </summary>
public sealed class PaginatedPaymentResult
{
    public List<PaymentSummaryDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Daily payment summary showing totals broken down by payment method.
/// </summary>
public sealed class DailyPaymentSummaryDto
{
    public DateTime Date { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal NetAmount { get; set; }
    public int TransactionCount { get; set; }
    public int RefundCount { get; set; }
    public string Currency { get; set; } = "THB";
    public List<PaymentMethodSummaryDto> ByMethod { get; set; } = [];
}

/// <summary>
/// Payment totals for a specific payment method.
/// </summary>
public sealed class PaymentMethodSummaryDto
{
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal NetAmount { get; set; }
    public int TransactionCount { get; set; }
    public int RefundCount { get; set; }
}

/// <summary>
/// DTO for verifying a payment.
/// </summary>
public sealed class PaymentVerificationDto
{
    public Guid PaymentId { get; set; }
    public bool IsVerified { get; set; }
    public string? VerificationNotes { get; set; }
    public string? VerifiedBy { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>
/// Balance summary for a reservation showing total due vs total paid.
/// </summary>
public sealed class ReservationBalanceDto
{
    public Guid ReservationId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal NetPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public decimal VATAmount { get; set; }
    public string Currency { get; set; } = "THB";
    public bool IsFullyPaid { get; set; }
    public bool HasOverpayment { get; set; }
    public List<PaymentSummaryDto> Payments { get; set; } = [];
}
