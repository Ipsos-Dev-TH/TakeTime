using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Inventory.Application.Commands;
using TakeTime.Inventory.Application.DTOs;

namespace TakeTime.Inventory.Application.Queries;

/// <summary>
/// Query to generate a sales report for a specified date range,
/// including revenue, VAT, profit margins, and product breakdowns.
/// </summary>
public sealed class GetSalesReportQuery : IRequest<SalesReportDto>
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? Category { get; set; }
    public int TopProductsCount { get; set; } = 10;
}

/// <summary>
/// Handler for <see cref="GetSalesReportQuery"/>. Aggregates sales data for
/// the specified period including revenue, VAT, cost analysis, and top products.
/// </summary>
public sealed class GetSalesReportQueryHandler : IRequestHandler<GetSalesReportQuery, SalesReportDto>
{
    private readonly ISalesTransactionRepository _salesRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetSalesReportQueryHandler> _logger;

    public GetSalesReportQueryHandler(
        ISalesTransactionRepository salesRepository,
        ITenantContext tenantContext,
        ILogger<GetSalesReportQueryHandler> logger)
    {
        _salesRepository = salesRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<SalesReportDto> Handle(GetSalesReportQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        if (request.ToDate < request.FromDate)
        {
            throw new InvalidOperationException("End date must be after start date.");
        }

        _logger.LogDebug(
            "Generating sales report for tenant {TenantId}: {FromDate} to {ToDate}",
            tenantSettings.TenantId, request.FromDate, request.ToDate);

        var report = await _salesRepository.GetSalesReportAsync(
            request.FromDate, request.ToDate, cancellationToken);

        // Set tenant-specific fields
        report.TenantId = tenantSettings.TenantId;
        report.Currency = tenantSettings.Currency ?? "THB";

        // Calculate derived metrics
        if (report.TotalSales > 0)
        {
            report.GrossProfit = report.NetSales - report.TotalCostOfGoods;
            report.GrossProfitMargin = Math.Round(report.GrossProfit / report.NetSales * 100, 2);
        }

        if (report.TransactionCount > 0)
        {
            report.AverageTransactionValue = Math.Round(report.TotalSales / report.TransactionCount, 2);
        }

        // Calculate category percentages
        if (report.SalesByCategory.Count > 0 && report.TotalSales > 0)
        {
            foreach (var category in report.SalesByCategory)
            {
                category.Percentage = Math.Round(category.TotalSales / report.TotalSales * 100, 2);
            }
        }

        // Trim top products list
        if (report.TopSellingProducts.Count > request.TopProductsCount)
        {
            report.TopSellingProducts = report.TopSellingProducts
                .Take(request.TopProductsCount)
                .ToList();
        }

        _logger.LogInformation(
            "Sales report generated for tenant {TenantId}: {Transactions} transactions, " +
            "Total: {TotalSales}, Net: {NetSales}, Profit: {Profit}",
            tenantSettings.TenantId, report.TransactionCount,
            report.TotalSales, report.NetSales, report.GrossProfit);

        return report;
    }
}
