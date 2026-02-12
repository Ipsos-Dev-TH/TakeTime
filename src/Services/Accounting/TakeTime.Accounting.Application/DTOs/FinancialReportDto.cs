namespace TakeTime.Accounting.Application.DTOs;

/// <summary>
/// Daily revenue report showing income breakdown for a specific date.
/// </summary>
public sealed class DailyRevenueDto
{
    public DateTime Date { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public decimal AccommodationRevenue { get; set; }
    public decimal FoodBeverageRevenue { get; set; }
    public decimal ServiceRevenue { get; set; }
    public decimal OtherRevenue { get; set; }
    public int TransactionCount { get; set; }
    public int CheckInsCount { get; set; }
    public int CheckOutsCount { get; set; }
    public string Currency { get; set; } = "THB";
    public List<RevenueBreakdownDto> Breakdown { get; set; } = [];
}

/// <summary>
/// Profit and loss statement for a specified date range.
/// </summary>
public sealed class ProfitLossDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string TenantId { get; set; } = string.Empty;

    // Revenue
    public decimal TotalRevenue { get; set; }
    public decimal AccommodationRevenue { get; set; }
    public decimal FoodBeverageRevenue { get; set; }
    public decimal ServiceRevenue { get; set; }
    public decimal OtherRevenue { get; set; }

    // Expenses
    public decimal TotalExpenses { get; set; }
    public decimal StaffExpenses { get; set; }
    public decimal UtilityExpenses { get; set; }
    public decimal SupplyExpenses { get; set; }
    public decimal MaintenanceExpenses { get; set; }
    public decimal MarketingExpenses { get; set; }
    public decimal OtherExpenses { get; set; }

    // Profit
    public decimal GrossProfit { get; set; }
    public decimal NetProfit { get; set; }
    public decimal ProfitMarginPercent { get; set; }

    // Tax
    public decimal VATCollected { get; set; }
    public decimal VATPayable { get; set; }

    public string Currency { get; set; } = "THB";
}

/// <summary>
/// Occupancy report showing room utilization metrics for a specified period.
/// </summary>
public sealed class OccupancyReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public int TotalRooms { get; set; }
    public int TotalRoomNightsAvailable { get; set; }
    public int TotalRoomNightsSold { get; set; }
    public decimal OccupancyRatePercent { get; set; }
    public decimal AverageDailyRate { get; set; }
    public decimal RevPAR { get; set; }
    public decimal AverageStayDuration { get; set; }
    public string Currency { get; set; } = "THB";
    public List<DailyOccupancyDto> DailyBreakdown { get; set; } = [];
}

/// <summary>
/// Daily occupancy snapshot within an occupancy report.
/// </summary>
public sealed class DailyOccupancyDto
{
    public DateTime Date { get; set; }
    public int OccupiedRooms { get; set; }
    public int AvailableRooms { get; set; }
    public decimal OccupancyRatePercent { get; set; }
    public decimal Revenue { get; set; }
}

/// <summary>
/// Revenue breakdown by category or source.
/// </summary>
public sealed class RevenueBreakdownDto
{
    public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
    public decimal PercentageOfTotal { get; set; }
    public string Currency { get; set; } = "THB";
}

/// <summary>
/// Key performance indicators dashboard DTO.
/// </summary>
public sealed class KpiDashboardDto
{
    public DateTime AsOfDate { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public decimal TodayRevenue { get; set; }
    public decimal MonthToDateRevenue { get; set; }
    public decimal YearToDateRevenue { get; set; }
    public decimal CurrentOccupancyPercent { get; set; }
    public decimal MonthAverageOccupancyPercent { get; set; }
    public decimal AverageDailyRate { get; set; }
    public decimal RevPAR { get; set; }
    public int TotalReservationsThisMonth { get; set; }
    public int CancellationsThisMonth { get; set; }
    public decimal CancellationRatePercent { get; set; }
    public decimal AverageStayNights { get; set; }
    public string Currency { get; set; } = "THB";
}

/// <summary>
/// Monthly income/expense summary DTO.
/// </summary>
public sealed class MonthlySummaryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetIncome { get; set; }
    public int TransactionCount { get; set; }
    public List<CategorySummaryDto> IncomeByCategory { get; set; } = [];
    public List<CategorySummaryDto> ExpenseByCategory { get; set; } = [];
    public string Currency { get; set; } = "THB";
}

/// <summary>
/// Category-level summary.
/// </summary>
public sealed class CategorySummaryDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

/// <summary>
/// Revenue breakdown by room type.
/// </summary>
public sealed class RevenueByRoomTypeDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public List<RoomTypeRevenueDto> RoomTypes { get; set; } = [];
    public string Currency { get; set; } = "THB";
}

/// <summary>
/// Revenue for a single room type.
/// </summary>
public sealed class RoomTypeRevenueDto
{
    public string RoomType { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int BookingCount { get; set; }
    public decimal AverageRate { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

/// <summary>
/// Tax (VAT) report DTO for tax filing.
/// </summary>
public sealed class TaxReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public decimal TotalSalesBeforeVAT { get; set; }
    public decimal TotalVATCollected { get; set; }
    public decimal TotalPurchasesBeforeVAT { get; set; }
    public decimal TotalInputVAT { get; set; }
    public decimal NetVATPayable { get; set; }
    public decimal VATRate { get; set; }
    public bool IsVATRegistered { get; set; }
    public int SalesTransactionCount { get; set; }
    public int PurchaseTransactionCount { get; set; }
    public string Currency { get; set; } = "THB";
}

/// <summary>
/// Report export result DTO.
/// </summary>
public sealed class ReportExportResultDto
{
    public string FileName { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Financial transaction list DTO.
/// </summary>
public sealed class TransactionListDto
{
    public List<TransactionItemDto> Transactions { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string Currency { get; set; } = "THB";
}

/// <summary>
/// Individual financial transaction DTO.
/// </summary>
public sealed class TransactionItemDto
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public string Currency { get; set; } = "THB";
    public DateTime CreatedAt { get; set; }
}
