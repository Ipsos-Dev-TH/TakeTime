namespace TakeTime.Payment.Application.DTOs;

/// <summary>
/// Full receipt data transfer object. Supports both VAT and non-VAT display
/// based on tenant configuration.
/// </summary>
public sealed class ReceiptDto
{
    public Guid Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public string? ReservationNumber { get; set; }
    public Guid PaymentId { get; set; }
    public string? PaymentNumber { get; set; }

    // Business details (from tenant settings)
    public string BusinessName { get; set; } = string.Empty;
    public string? BusinessAddress { get; set; }
    public string? BusinessTaxId { get; set; }
    public string? BusinessPhone { get; set; }
    public string? BusinessEmail { get; set; }

    // Customer details
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerAddress { get; set; }
    public string? CustomerTaxId { get; set; }

    // Line items
    public List<ReceiptItemDto> Items { get; set; } = [];

    // Totals
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AmountBeforeVAT { get; set; }
    public decimal VATRate { get; set; }
    public decimal VATAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "THB";
    public bool IsVATInclusive { get; set; }
    public bool IsVATRegistered { get; set; }

    // Payment info
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public decimal Change { get; set; }

    // Metadata
    public string ReceiptType { get; set; } = "Receipt"; // Receipt, TaxInvoice, AbbreviatedTaxInvoice
    public DateTime IssuedDate { get; set; }
    public string? IssuedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? VATRegistrationNumber { get; set; }

    // Formatted display strings
    public string FormattedSubTotal { get; set; } = string.Empty;
    public string FormattedVATAmount { get; set; } = string.Empty;
    public string FormattedTotal { get; set; } = string.Empty;
}

/// <summary>
/// DTO for a line item on a receipt.
/// </summary>
public sealed class ReceiptItemDto
{
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ItemCode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? VATAmount { get; set; }
    public string Currency { get; set; } = "THB";
    public string? Category { get; set; }
}

/// <summary>
/// DTO for creating a receipt.
/// </summary>
public sealed class CreateReceiptDto
{
    public Guid ReservationId { get; set; }
    public Guid PaymentId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerTaxId { get; set; }
    public string ReceiptType { get; set; } = "Receipt";
    public string? IssuedBy { get; set; }
    public decimal? AmountPaid { get; set; }
    public List<CreateReceiptItemDto>? Items { get; set; }
}

/// <summary>
/// DTO for creating a receipt line item.
/// </summary>
public sealed class CreateReceiptItemDto
{
    public string Description { get; set; } = string.Empty;
    public string? ItemCode { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string? Category { get; set; }
}

/// <summary>
/// DTO for a full tax invoice.
/// </summary>
public sealed class TaxInvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public string? ReservationNumber { get; set; }

    // Seller information
    public string SellerName { get; set; } = string.Empty;
    public string? SellerAddress { get; set; }
    public string SellerTaxId { get; set; } = string.Empty;
    public string? SellerPhone { get; set; }
    public string? SellerBranch { get; set; }

    // Buyer information
    public string BuyerName { get; set; } = string.Empty;
    public string? BuyerAddress { get; set; }
    public string? BuyerTaxId { get; set; }
    public string? BuyerBranch { get; set; }

    // Line items
    public List<ReceiptItemDto> Items { get; set; } = [];

    // Totals
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AmountBeforeVAT { get; set; }
    public decimal VATRate { get; set; }
    public decimal VATAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "THB";
    public string TotalAmountInWords { get; set; } = string.Empty;

    // Metadata
    public DateTime InvoiceDate { get; set; }
    public string? IssuedBy { get; set; }
    public string? AuthorizedSignatory { get; set; }
    public DateTime CreatedAt { get; set; }
}
