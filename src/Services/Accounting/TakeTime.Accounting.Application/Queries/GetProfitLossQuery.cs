using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Accounting.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Accounting.Application.Queries;

/// <summary>
/// Query to retrieve a profit and loss statement for a specified date range.
/// </summary>
public sealed class GetProfitLossQuery : IRequest<ProfitLossDto>
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

/// <summary>
/// Handler for <see cref="GetProfitLossQuery"/>. Aggregates revenue and expense data
/// from the accounting repository to produce a comprehensive P&L statement.
/// </summary>
public sealed class GetProfitLossQueryHandler : IRequestHandler<GetProfitLossQuery, ProfitLossDto>
{
    private readonly IAccountingQueryRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetProfitLossQueryHandler> _logger;

    public GetProfitLossQueryHandler(
        IAccountingQueryRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetProfitLossQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<ProfitLossDto> Handle(GetProfitLossQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Generating P&L report for tenant {TenantId} from {StartDate} to {EndDate}",
            tenantId, request.StartDate.ToString("yyyy-MM-dd"), request.EndDate.ToString("yyyy-MM-dd"));

        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;

        // Retrieve revenue transactions
        var transactions = await _repository.GetTransactionsByDateRangeAsync(tenantId, startDate, endDate, cancellationToken);

        decimal accommodationRevenue = 0, fbRevenue = 0, serviceRevenue = 0, otherRevenue = 0;

        foreach (var transaction in transactions)
        {
            switch (transaction.Category?.ToLowerInvariant())
            {
                case "accommodation":
                case "room":
                    accommodationRevenue += transaction.Amount;
                    break;
                case "food":
                case "beverage":
                case "food_beverage":
                case "f&b":
                    fbRevenue += transaction.Amount;
                    break;
                case "service":
                case "spa":
                case "laundry":
                    serviceRevenue += transaction.Amount;
                    break;
                default:
                    otherRevenue += transaction.Amount;
                    break;
            }
        }

        var totalRevenue = accommodationRevenue + fbRevenue + serviceRevenue + otherRevenue;

        // Retrieve expenses
        var expenses = await _repository.GetExpensesByDateRangeAsync(tenantId, startDate, endDate, cancellationToken);

        decimal staffExpenses = 0, utilityExpenses = 0, supplyExpenses = 0;
        decimal maintenanceExpenses = 0, marketingExpenses = 0, otherExpenses = 0;

        foreach (var expense in expenses)
        {
            switch (expense.Category?.ToLowerInvariant())
            {
                case "staff":
                case "salary":
                case "payroll":
                    staffExpenses += expense.Amount;
                    break;
                case "utility":
                case "utilities":
                    utilityExpenses += expense.Amount;
                    break;
                case "supply":
                case "supplies":
                    supplyExpenses += expense.Amount;
                    break;
                case "maintenance":
                case "repair":
                    maintenanceExpenses += expense.Amount;
                    break;
                case "marketing":
                case "advertising":
                    marketingExpenses += expense.Amount;
                    break;
                default:
                    otherExpenses += expense.Amount;
                    break;
            }
        }

        var totalExpenses = staffExpenses + utilityExpenses + supplyExpenses +
                            maintenanceExpenses + marketingExpenses + otherExpenses;

        var grossProfit = totalRevenue - totalExpenses;

        // VAT calculation
        var vatRateStr = _currentTenantService.GetSetting("VATRate");
        var vatRate = decimal.TryParse(vatRateStr, out var vr) ? vr : 7m;
        var isVATRegistered = _currentTenantService.GetSetting("IsVATRegistered")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

        decimal vatCollected = 0, vatPayable = 0;
        if (isVATRegistered)
        {
            vatCollected = Math.Round(totalRevenue * vatRate / (100 + vatRate), 2);
            vatPayable = vatCollected;
        }

        var netProfit = grossProfit - vatPayable;
        var profitMargin = totalRevenue > 0
            ? Math.Round(netProfit / totalRevenue * 100, 2)
            : 0;

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        return new ProfitLossDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TenantId = tenantId,
            TotalRevenue = totalRevenue,
            AccommodationRevenue = accommodationRevenue,
            FoodBeverageRevenue = fbRevenue,
            ServiceRevenue = serviceRevenue,
            OtherRevenue = otherRevenue,
            TotalExpenses = totalExpenses,
            StaffExpenses = staffExpenses,
            UtilityExpenses = utilityExpenses,
            SupplyExpenses = supplyExpenses,
            MaintenanceExpenses = maintenanceExpenses,
            MarketingExpenses = marketingExpenses,
            OtherExpenses = otherExpenses,
            GrossProfit = grossProfit,
            NetProfit = netProfit,
            ProfitMarginPercent = profitMargin,
            VATCollected = vatCollected,
            VATPayable = vatPayable,
            Currency = currency
        };
    }
}
