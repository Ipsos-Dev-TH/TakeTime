using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Inventory.Domain.Enums;

namespace TakeTime.Inventory.Domain.Entities;

/// <summary>
/// Aggregate root representing a point-of-sale (POS) sales transaction.
/// Supports both direct cash/card sales and room-charge transactions.
/// </summary>
public class SalesTransaction : AggregateRoot
{
    private readonly List<SalesTransactionItem> _items = [];

    /// <summary>
    /// Auto-generated unique transaction number.
    /// </summary>
    public string TransactionNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Line items in the transaction.
    /// </summary>
    public IReadOnlyCollection<SalesTransactionItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Subtotal before discounts and VAT.
    /// </summary>
    public Money SubTotal { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Total discount applied to the transaction.
    /// </summary>
    public Money DiscountAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// VAT amount.
    /// </summary>
    public Money VATAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Final total amount.
    /// </summary>
    public Money TotalAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Payment method used for the transaction.
    /// </summary>
    public string PaymentMethod { get; private set; } = string.Empty;

    /// <summary>
    /// Identifier of the cashier who processed the transaction.
    /// </summary>
    public string CashierUserId { get; private set; } = string.Empty;

    /// <summary>
    /// If the sale is charged to a room, the associated reservation identifier.
    /// </summary>
    public Guid? RoomChargeReservationId { get; private set; }

    /// <summary>
    /// Current status of the transaction.
    /// </summary>
    public SalesTransactionStatus Status { get; private set; }

    /// <summary>
    /// Whether this transaction is a room charge.
    /// </summary>
    public bool IsRoomCharge => RoomChargeReservationId.HasValue;

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private SalesTransaction() { }

    /// <summary>
    /// Creates a new sales transaction.
    /// </summary>
    public SalesTransaction(
        string cashierUserId,
        string paymentMethod,
        Guid? roomChargeReservationId = null)
    {
        if (string.IsNullOrWhiteSpace(cashierUserId))
            throw new ArgumentException("Cashier user ID is required.", nameof(cashierUserId));

        if (string.IsNullOrWhiteSpace(paymentMethod))
            throw new ArgumentException("Payment method is required.", nameof(paymentMethod));

        CashierUserId = cashierUserId;
        PaymentMethod = paymentMethod;
        RoomChargeReservationId = roomChargeReservationId;
        Status = SalesTransactionStatus.InProgress;

        TransactionNumber = GenerateTransactionNumber();
    }

    /// <summary>
    /// Adds an item to the transaction.
    /// </summary>
    public void AddItem(SalesTransactionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Status != SalesTransactionStatus.InProgress)
            throw new InvalidOperationException(
                $"Cannot add items to transaction in '{Status}' status.");

        item.SetSalesTransactionId(Id);
        _items.Add(item);

        RecalculateTotals();
    }

    /// <summary>
    /// Removes an item from the transaction.
    /// </summary>
    public void RemoveItem(Guid productId)
    {
        if (Status != SalesTransactionStatus.InProgress)
            throw new InvalidOperationException(
                $"Cannot remove items from transaction in '{Status}' status.");

        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null)
            throw new InvalidOperationException($"Product '{productId}' not found in this transaction.");

        _items.Remove(item);

        RecalculateTotals();
    }

    /// <summary>
    /// Applies a discount to the transaction.
    /// </summary>
    public void ApplyDiscount(Money discount)
    {
        if (Status != SalesTransactionStatus.InProgress)
            throw new InvalidOperationException("Cannot apply discount to a completed transaction.");

        DiscountAmount = discount;
        RecalculateTotals();
    }

    /// <summary>
    /// Completes the transaction.
    /// </summary>
    public void Complete()
    {
        if (Status != SalesTransactionStatus.InProgress)
            throw new InvalidOperationException(
                $"Cannot complete transaction in '{Status}' status.");

        if (_items.Count == 0)
            throw new InvalidOperationException("Cannot complete a transaction with no items.");

        Status = SalesTransactionStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Voids the transaction.
    /// </summary>
    public void Void()
    {
        if (Status == SalesTransactionStatus.Voided)
            throw new InvalidOperationException("Transaction is already voided.");

        Status = SalesTransactionStatus.Voided;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the transaction as refunded.
    /// </summary>
    public void Refund()
    {
        if (Status != SalesTransactionStatus.Completed)
            throw new InvalidOperationException(
                $"Cannot refund transaction in '{Status}' status. Only completed transactions can be refunded.");

        Status = SalesTransactionStatus.Refunded;
        UpdatedAt = DateTime.UtcNow;
    }

    private void RecalculateTotals()
    {
        SubTotal = _items.Aggregate(
            Money.ZeroBaht(),
            (sum, item) => sum + item.TotalPrice);

        TotalAmount = SubTotal - DiscountAmount + VATAmount;
    }

    private static string GenerateTransactionNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var random = Random.Shared.Next(100, 999);
        return $"POS-{timestamp}-{random}";
    }
}
