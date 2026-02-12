using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Accounting.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Accounting.Application.Queries;

/// <summary>
/// Query to generate a VAT report for tax filing for a specific date range.
/// </summary>
public sealed class GetTaxReportQuery : IRequest<TaxReportDto>
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

/// <summary>
/// Handler for <see cref="GetTaxReportQuery"/>. Calculates output VAT from sales,
/// input VAT from purchases, and net VAT payable based on tenant VAT settings.
/// </summary>
public sealed class GetTaxReportQueryHandler : IRequestHandler<GetTaxReportQuery, TaxReportDto>
{
    private readonly IAccountingQueryRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetTaxReportQueryHandler> _logger;

    public GetTaxReportQueryHandler(
        IAccountingQueryRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetTaxReportQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<TaxReportDto> Handle(GetTaxReportQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Generating tax report for tenant {TenantId} from {StartDate} to {EndDate}",
            tenantId, request.StartDate.ToString("yyyy-MM-dd"), request.EndDate.ToString("yyyy-MM-dd"));

        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;

        // Get VAT configuration
        var vatRateStr = _currentTenantService.GetSetting("VATRate");
        var vatRate = decimal.TryParse(vatRateStr, out var vr) ? vr : 7m;
        var isVATRegistered = _currentTenantService.GetSetting("IsVATRegistered")
            ?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

        // Retrieve transactions (sales) and expenses (purchases)
        var transactions = await _repository.GetTransactionsByDateRangeAsync(
            tenantId, startDate, endDate, cancellationToken);
        var expenses = await _repository.GetExpensesByDateRangeAsync(
            tenantId, startDate, endDate, cancellationToken);

        var totalSalesIncludingVAT = transactions.Sum(t => t.Amount);
        var totalPurchasesIncludingVAT = expenses.Sum(e => e.Amount);

        decimal totalSalesBeforeVAT, totalVATCollected;
        decimal totalPurchasesBeforeVAT, totalInputVAT;

        if (isVATRegistered)
        {
            // Extract VAT from VAT-inclusive amounts
            totalSalesBeforeVAT = Math.Round(totalSalesIncludingVAT * 100 / (100 + vatRate), 2);
            totalVATCollected = totalSalesIncludingVAT - totalSalesBeforeVAT;

            totalPurchasesBeforeVAT = Math.Round(totalPurchasesIncludingVAT * 100 / (100 + vatRate), 2);
            totalInputVAT = totalPurchasesIncludingVAT - totalPurchasesBeforeVAT;
        }
        else
        {
            totalSalesBeforeVAT = totalSalesIncludingVAT;
            totalVATCollected = 0;
            totalPurchasesBeforeVAT = totalPurchasesIncludingVAT;
            totalInputVAT = 0;
        }

        var netVATPayable = totalVATCollected - totalInputVAT;
        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        return new TaxReportDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TenantId = tenantId,
            TotalSalesBeforeVAT = totalSalesBeforeVAT,
            TotalVATCollected = totalVATCollected,
            TotalPurchasesBeforeVAT = totalPurchasesBeforeVAT,
            TotalInputVAT = totalInputVAT,
            NetVATPayable = netVATPayable,
            VATRate = vatRate,
            IsVATRegistered = isVATRegistered,
            SalesTransactionCount = transactions.Count,
            PurchaseTransactionCount = expenses.Count,
            Currency = currency
        };
    }
}
