namespace TakeTime.Payment.Domain.Enums;

/// <summary>
/// Types of receipts that can be issued.
/// </summary>
public enum ReceiptType
{
    /// <summary>
    /// Receipt for a deposit payment.
    /// </summary>
    Deposit = 0,

    /// <summary>
    /// Regular receipt for standard payments.
    /// </summary>
    Regular = 1,

    /// <summary>
    /// Full tax invoice with VAT details.
    /// </summary>
    TaxInvoice = 2,

    /// <summary>
    /// Credit note for refunds or corrections.
    /// </summary>
    CreditNote = 3
}
