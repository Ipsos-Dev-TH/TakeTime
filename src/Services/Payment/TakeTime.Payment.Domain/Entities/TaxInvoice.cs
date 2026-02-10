using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Payment.Domain.ValueObjects;

namespace TakeTime.Payment.Domain.Entities;

/// <summary>
/// Aggregate root representing a tax invoice (ใบกำกับภาษี).
/// Required for VAT-registered businesses in Thailand.
/// </summary>
public class TaxInvoice : AggregateRoot
{
    private readonly List<ReceiptItem> _items = [];

    /// <summary>
    /// Auto-generated unique invoice number.
    /// </summary>
    public string InvoiceNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Identifier of the associated receipt.
    /// </summary>
    public Guid ReceiptId { get; private set; }

    /// <summary>
    /// Seller (property) company information.
    /// </summary>
    public CompanyInfo SellerInfo { get; private set; } = null!;

    /// <summary>
    /// Buyer (customer/company) information.
    /// </summary>
    public CompanyInfo BuyerInfo { get; private set; } = null!;

    /// <summary>
    /// Line items on the tax invoice.
    /// </summary>
    public IReadOnlyCollection<ReceiptItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Subtotal before VAT.
    /// </summary>
    public Money SubTotal { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// VAT amount.
    /// </summary>
    public Money VATAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// VAT rate as a percentage.
    /// </summary>
    public decimal VATRate { get; private set; }

    /// <summary>
    /// Total amount including VAT.
    /// </summary>
    public Money TotalAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Date the invoice was issued.
    /// </summary>
    public DateTime IssuedDate { get; private set; }

    /// <summary>
    /// Payment due date.
    /// </summary>
    public DateTime? DueDate { get; private set; }

    /// <summary>
    /// Current status of the invoice (e.g., Issued, Paid, Voided).
    /// </summary>
    public string Status { get; private set; } = "Issued";

    /// <summary>
    /// URL of the generated PDF document.
    /// </summary>
    public string? PDFUrl { get; private set; }

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private TaxInvoice() { }

    /// <summary>
    /// Creates a new tax invoice.
    /// </summary>
    public TaxInvoice(
        Guid receiptId,
        CompanyInfo sellerInfo,
        CompanyInfo buyerInfo,
        decimal vatRate,
        DateTime? dueDate = null)
    {
        ArgumentNullException.ThrowIfNull(sellerInfo);
        ArgumentNullException.ThrowIfNull(buyerInfo);

        ReceiptId = receiptId;
        SellerInfo = sellerInfo;
        BuyerInfo = buyerInfo;
        VATRate = vatRate;
        IssuedDate = DateTime.UtcNow;
        DueDate = dueDate;

        InvoiceNumber = GenerateInvoiceNumber();
    }

    /// <summary>
    /// Adds an item to the tax invoice and recalculates totals.
    /// </summary>
    public void AddItem(ReceiptItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.SetReceiptId(Id);
        _items.Add(item);

        RecalculateTotals();
    }

    /// <summary>
    /// Sets the URL of the generated PDF.
    /// </summary>
    public void SetPDFUrl(string pdfUrl)
    {
        if (string.IsNullOrWhiteSpace(pdfUrl))
            throw new ArgumentException("PDF URL is required.", nameof(pdfUrl));

        PDFUrl = pdfUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the invoice as paid.
    /// </summary>
    public void MarkAsPaid()
    {
        Status = "Paid";
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Voids the tax invoice.
    /// </summary>
    public void Void(string reason)
    {
        if (Status == "Voided")
            throw new InvalidOperationException("Invoice is already voided.");

        Status = "Voided";
        UpdatedAt = DateTime.UtcNow;
    }

    private void RecalculateTotals()
    {
        SubTotal = _items.Aggregate(
            Money.ZeroBaht(),
            (sum, item) => sum + item.Amount);

        VATAmount = SubTotal.Percentage(VATRate).Round();
        TotalAmount = SubTotal + VATAmount;
    }

    private static string GenerateInvoiceNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Random.Shared.Next(1000, 9999);
        return $"INV-{timestamp}-{random}";
    }
}
