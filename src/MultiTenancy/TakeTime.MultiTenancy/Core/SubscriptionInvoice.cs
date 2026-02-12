namespace TakeTime.MultiTenancy.Core;

/// <summary>
/// Status of a subscription invoice.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Invoice is being prepared.</summary>
    Draft = 0,

    /// <summary>Invoice has been issued to the tenant.</summary>
    Issued = 1,

    /// <summary>Invoice has been paid.</summary>
    Paid = 2,

    /// <summary>Invoice is past due date and unpaid.</summary>
    Overdue = 3,

    /// <summary>Invoice has been cancelled/voided.</summary>
    Cancelled = 4
}

/// <summary>
/// Represents a tax invoice for a subscription payment, following Thai tax
/// invoice requirements including seller/buyer tax IDs and 7% VAT.
/// </summary>
public class SubscriptionInvoice
{
    /// <summary>
    /// Unique identifier for this invoice.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The tenant this invoice is for.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// The subscription this invoice relates to.
    /// </summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>
    /// The payment associated with this invoice (null if unpaid).
    /// </summary>
    public Guid? PaymentId { get; set; }

    /// <summary>
    /// Auto-generated invoice number (e.g., "SUB-INV-20260201-0001").
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Date the invoice was issued.
    /// </summary>
    public DateTime InvoiceDate { get; set; }

    /// <summary>
    /// Payment due date.
    /// </summary>
    public DateTime DueDate { get; set; }

    /// <summary>
    /// Name of the plan being billed.
    /// </summary>
    public string PlanName { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable billing cycle description (e.g., "Monthly", "Yearly").
    /// </summary>
    public string BillingCycleDescription { get; set; } = string.Empty;

    /// <summary>
    /// Start date of the billing period.
    /// </summary>
    public DateTime BillingPeriodStart { get; set; }

    /// <summary>
    /// End date of the billing period.
    /// </summary>
    public DateTime BillingPeriodEnd { get; set; }

    /// <summary>
    /// Subtotal before discounts and tax.
    /// </summary>
    public decimal SubTotal { get; set; }

    /// <summary>
    /// Discount amount applied.
    /// </summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// VAT rate percentage (standard Thai rate: 7%).
    /// </summary>
    public decimal VatRate { get; set; } = 7m;

    /// <summary>
    /// Calculated VAT amount.
    /// </summary>
    public decimal VatAmount { get; set; }

    /// <summary>
    /// Total amount including VAT and after discounts.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Currency code (default THB).
    /// </summary>
    public string Currency { get; set; } = "THB";

    /// <summary>
    /// Current status of the invoice.
    /// </summary>
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    // ─── Seller Information ───────────────────────────────────────────

    /// <summary>
    /// Name of the seller/provider (TakeTime platform).
    /// </summary>
    public string? SellerName { get; set; }

    /// <summary>
    /// Tax identification number of the seller.
    /// </summary>
    public string? SellerTaxId { get; set; }

    /// <summary>
    /// Address of the seller.
    /// </summary>
    public string? SellerAddress { get; set; }

    // ─── Buyer Information ────────────────────────────────────────────

    /// <summary>
    /// Name of the buyer (tenant business name).
    /// </summary>
    public string? BuyerName { get; set; }

    /// <summary>
    /// Tax identification number of the buyer.
    /// </summary>
    public string? BuyerTaxId { get; set; }

    /// <summary>
    /// Address of the buyer.
    /// </summary>
    public string? BuyerAddress { get; set; }

    /// <summary>
    /// UTC timestamp when the invoice was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the invoice was paid.
    /// </summary>
    public DateTime? PaidAt { get; set; }
}
