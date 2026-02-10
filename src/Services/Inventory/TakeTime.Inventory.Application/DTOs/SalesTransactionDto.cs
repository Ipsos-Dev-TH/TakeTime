namespace TakeTime.Inventory.Application.DTOs;

/// <summary>
/// Full sales transaction (POS) data transfer object.
/// </summary>
public sealed class SalesTransactionDto
{
    public Guid Id { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public Guid? ReservationId { get; set; }
    public string? ReservationNumber { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VATAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "THB";
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsVATInclusive { get; set; }
    public decimal VATRate { get; set; }
    public List<SalesTransactionItemDto> Items { get; set; } = [];
    public DateTime TransactionDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>
/// DTO for a line item in a sales transaction.
/// </summary>
public sealed class SalesTransactionItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? SKU { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? VATAmount { get; set; }
    public string Currency { get; set; } = "THB";
}

/// <summary>
/// DTO for creating a new sales transaction (POS checkout).
/// </summary>
public sealed class CreateSalesTransactionDto
{
    public Guid? ReservationId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public decimal? DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public List<POSCartItemDto> Items { get; set; } = [];
}

/// <summary>
/// DTO for a cart item in a POS transaction.
/// </summary>
public sealed class POSCartItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal? PriceOverride { get; set; }
    public decimal? DiscountAmount { get; set; }
}

/// <summary>
/// Sales report summary DTO.
/// </summary>
public sealed class SalesReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public decimal TotalVAT { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal NetSales { get; set; }
    public decimal TotalCostOfGoods { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossProfitMargin { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageTransactionValue { get; set; }
    public string Currency { get; set; } = "THB";
    public List<SalesByCategoryDto> SalesByCategory { get; set; } = [];
    public List<TopSellingProductDto> TopSellingProducts { get; set; } = [];
    public List<DailySalesDto> DailySales { get; set; } = [];
}

/// <summary>
/// Sales aggregated by product category.
/// </summary>
public sealed class SalesByCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public int ItemsSold { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// Top selling product summary.
/// </summary>
public sealed class TopSellingProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? SKU { get; set; }
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

/// <summary>
/// Daily sales summary for trend reporting.
/// </summary>
public sealed class DailySalesDto
{
    public DateTime Date { get; set; }
    public decimal TotalSales { get; set; }
    public int TransactionCount { get; set; }
}

/// <summary>
/// Stock adjustment data transfer object.
/// </summary>
public sealed class StockAdjustmentDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? SKU { get; set; }
    public string AdjustmentType { get; set; } = string.Empty;
    public int QuantityBefore { get; set; }
    public int QuantityAdjusted { get; set; }
    public int QuantityAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
