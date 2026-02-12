namespace TakeTime.MultiTenancy.Core;

/// <summary>
/// Payment method used for subscription payments.
/// </summary>
public enum SubscriptionPaymentMethod
{
    /// <summary>Thai bank transfer.</summary>
    BankTransfer = 0,

    /// <summary>PromptPay (Thai instant payment system).</summary>
    PromptPay = 1,

    /// <summary>Credit card payment.</summary>
    CreditCard = 2,

    /// <summary>Debit card payment.</summary>
    DebitCard = 3
}

/// <summary>
/// Status of a subscription payment.
/// </summary>
public enum SubscriptionPaymentStatus
{
    /// <summary>Payment has been initiated but not yet processed.</summary>
    Pending = 0,

    /// <summary>Payment is being processed by the gateway/bank.</summary>
    Processing = 1,

    /// <summary>Payment was successfully completed.</summary>
    Completed = 2,

    /// <summary>Payment failed.</summary>
    Failed = 3,

    /// <summary>Payment was refunded.</summary>
    Refunded = 4
}

/// <summary>
/// Records a payment made for a tenant's subscription, including Thai-specific
/// payment methods (PromptPay, bank transfer) and VAT calculation at 7%.
/// </summary>
public class SubscriptionPayment
{
    /// <summary>
    /// Unique identifier for this payment record.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The tenant who made this payment.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// The subscription this payment is for.
    /// </summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>
    /// Auto-generated payment number (e.g., "SUB-PAY-20260201-0001").
    /// </summary>
    public string PaymentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Base amount before VAT.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// VAT amount at 7%.
    /// </summary>
    public decimal VatAmount { get; set; }

    /// <summary>
    /// Total amount including VAT (Amount + VatAmount).
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Currency code (default THB).
    /// </summary>
    public string Currency { get; set; } = "THB";

    /// <summary>
    /// Payment method used.
    /// </summary>
    public SubscriptionPaymentMethod PaymentMethod { get; set; }

    /// <summary>
    /// Current status of the payment.
    /// </summary>
    public SubscriptionPaymentStatus Status { get; set; } = SubscriptionPaymentStatus.Pending;

    /// <summary>
    /// Bank name (for bank transfer payments).
    /// </summary>
    public string? BankName { get; set; }

    /// <summary>
    /// Transfer reference number (for bank transfer/PromptPay payments).
    /// </summary>
    public string? TransferReference { get; set; }

    /// <summary>
    /// URL to the uploaded payment slip image (for bank transfer verification).
    /// </summary>
    public string? SlipImageUrl { get; set; }

    /// <summary>
    /// Last 4 digits of the card used (for credit/debit card payments).
    /// </summary>
    public string? CardLast4 { get; set; }

    /// <summary>
    /// Card brand (e.g., "Visa", "Mastercard") for card payments.
    /// </summary>
    public string? CardBrand { get; set; }

    /// <summary>
    /// Transaction ID from the payment gateway.
    /// </summary>
    public string? GatewayTransactionId { get; set; }

    /// <summary>
    /// Associated invoice number.
    /// </summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>
    /// Start date of the billing period this payment covers.
    /// </summary>
    public DateTime BillingPeriodStart { get; set; }

    /// <summary>
    /// End date of the billing period this payment covers.
    /// </summary>
    public DateTime BillingPeriodEnd { get; set; }

    /// <summary>
    /// Timestamp when the payment was confirmed/completed.
    /// </summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>
    /// UTC timestamp when the payment record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ─── Navigation Properties ────────────────────────────────────────

    /// <summary>
    /// Navigation property to the subscription.
    /// </summary>
    public TenantSubscription? Subscription { get; set; }
}
