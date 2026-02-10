using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Payment.Domain.Enums;
using TakeTime.Payment.Domain.Events;

namespace TakeTime.Payment.Domain.Entities;

/// <summary>
/// Aggregate root representing a receipt issued for a payment.
/// Supports both VAT and non-VAT businesses with configurable tax calculation.
/// </summary>
public class Receipt : AggregateRoot
{
    private readonly List<ReceiptItem> _items = [];

    /// <summary>
    /// Auto-generated unique receipt number.
    /// </summary>
    public string ReceiptNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Identifier of the associated payment.
    /// </summary>
    public Guid PaymentId { get; private set; }

    /// <summary>
    /// Identifier of the associated reservation.
    /// </summary>
    public Guid ReservationId { get; private set; }

    /// <summary>
    /// Identifier of the customer.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Line items on the receipt.
    /// </summary>
    public IReadOnlyCollection<ReceiptItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Subtotal before tax.
    /// </summary>
    public Money SubTotal { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// VAT amount.
    /// </summary>
    public Money VATAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// VAT rate as a percentage (e.g., 7 for 7%).
    /// </summary>
    public decimal VATRate { get; private set; }

    /// <summary>
    /// Total amount including VAT.
    /// </summary>
    public Money TotalAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Whether the prices already include VAT (VAT-inclusive pricing).
    /// When true, VAT is extracted from the total; when false, VAT is added on top.
    /// </summary>
    public bool IsVATIncluded { get; private set; }

    /// <summary>
    /// Type of receipt.
    /// </summary>
    public ReceiptType ReceiptType { get; private set; }

    /// <summary>
    /// Identifier of the staff member who issued the receipt.
    /// </summary>
    public string IssuedBy { get; private set; } = string.Empty;

    /// <summary>
    /// Timestamp when the receipt was issued.
    /// </summary>
    public DateTime IssuedAt { get; private set; }

    /// <summary>
    /// Identifier of the staff member who voided the receipt, if applicable.
    /// </summary>
    public string? VoidedBy { get; private set; }

    /// <summary>
    /// Timestamp when the receipt was voided, if applicable.
    /// </summary>
    public DateTime? VoidedAt { get; private set; }

    /// <summary>
    /// Reason for voiding the receipt.
    /// </summary>
    public string? VoidReason { get; private set; }

    /// <summary>
    /// Whether this receipt has been voided.
    /// </summary>
    public bool IsVoided => VoidedAt.HasValue;

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private Receipt() { }

    /// <summary>
    /// Creates a new receipt.
    /// </summary>
    public Receipt(
        Guid paymentId,
        Guid reservationId,
        Guid customerId,
        ReceiptType receiptType,
        string issuedBy,
        bool isVATIncluded = true,
        decimal vatRate = 0m,
        string? prefix = null)
    {
        if (string.IsNullOrWhiteSpace(issuedBy))
            throw new ArgumentException("Issued by user is required.", nameof(issuedBy));

        PaymentId = paymentId;
        ReservationId = reservationId;
        CustomerId = customerId;
        ReceiptType = receiptType;
        IssuedBy = issuedBy;
        IssuedAt = DateTime.UtcNow;
        IsVATIncluded = isVATIncluded;
        VATRate = vatRate;

        ReceiptNumber = GenerateReceiptNumber(prefix);
    }

    /// <summary>
    /// Adds an item to the receipt and recalculates totals.
    /// </summary>
    public void AddItem(ReceiptItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (IsVoided)
            throw new InvalidOperationException("Cannot add items to a voided receipt.");

        item.SetReceiptId(Id);
        _items.Add(item);

        RecalculateTotals();
    }

    /// <summary>
    /// Calculates VAT based on whether the business is VAT-registered
    /// and the configured VAT rate.
    /// </summary>
    /// <param name="enableVAT">Whether VAT calculation is enabled for this business.</param>
    /// <param name="vatRate">The VAT rate as a percentage (e.g., 7 for 7%).</param>
    public void CalculateVAT(bool enableVAT, decimal vatRate)
    {
        VATRate = enableVAT ? vatRate : 0m;
        IsVATIncluded = enableVAT && IsVATIncluded;

        RecalculateTotals();
    }

    /// <summary>
    /// Voids the receipt with a reason.
    /// </summary>
    public void Void(string reason, string userId)
    {
        if (IsVoided)
            throw new InvalidOperationException("Receipt is already voided.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Void reason is required.", nameof(reason));

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        VoidedBy = userId;
        VoidedAt = DateTime.UtcNow;
        VoidReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Generates a receipt number with an optional prefix.
    /// </summary>
    public static string GenerateReceiptNumber(string? prefix = null)
    {
        var pre = string.IsNullOrWhiteSpace(prefix) ? "RCT" : prefix;
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Random.Shared.Next(1000, 9999);
        return $"{pre}-{timestamp}-{random}";
    }

    private void RecalculateTotals()
    {
        var itemsTotal = _items.Aggregate(
            Money.ZeroBaht(),
            (sum, item) => sum + item.Amount);

        if (VATRate > 0)
        {
            if (IsVATIncluded)
            {
                // VAT is included in the price: extract VAT from total
                // Total = SubTotal + VAT = SubTotal + SubTotal * VATRate/100
                // Total = SubTotal * (1 + VATRate/100)
                // SubTotal = Total / (1 + VATRate/100)
                var divisor = 1m + (VATRate / 100m);
                SubTotal = Money.InBaht(Math.Round(itemsTotal.Amount / divisor, 2));
                VATAmount = itemsTotal - SubTotal;
                TotalAmount = itemsTotal;
            }
            else
            {
                // VAT is added on top of the price
                SubTotal = itemsTotal;
                VATAmount = itemsTotal.Percentage(VATRate).Round();
                TotalAmount = SubTotal + VATAmount;
            }
        }
        else
        {
            SubTotal = itemsTotal;
            VATAmount = Money.ZeroBaht();
            TotalAmount = itemsTotal;
        }

        AddDomainEvent(new ReceiptGeneratedEvent(
            Id, ReceiptNumber, ReservationId, TotalAmount, ReceiptType));
    }
}
