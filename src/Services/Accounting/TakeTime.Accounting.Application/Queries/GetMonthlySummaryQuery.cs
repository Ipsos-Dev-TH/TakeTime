using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Accounting.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Accounting.Application.Queries;

/// <summary>
/// Query to retrieve a monthly income/expense summary for a specific year and month.
/// </summary>
public sealed class GetMonthlySummaryQuery : IRequest<MonthlySummaryDto>
{
    public int Year { get; set; }
    public int Month { get; set; }
}

/// <summary>
/// Handler for <see cref="GetMonthlySummaryQuery"/>. Aggregates income and expense
/// transactions for the specified month, grouped by category.
/// </summary>
public sealed class GetMonthlySummaryQueryHandler : IRequestHandler<GetMonthlySummaryQuery, MonthlySummaryDto>
{
    private readonly IAccountingQueryRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetMonthlySummaryQueryHandler> _logger;

    public GetMonthlySummaryQueryHandler(
        IAccountingQueryRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetMonthlySummaryQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<MonthlySummaryDto> Handle(GetMonthlySummaryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Generating monthly summary for tenant {TenantId}, {Year}-{Month:D2}",
            tenantId, request.Year, request.Month);

        var transactions = await _repository.GetTransactionsByMonthAsync(
            tenantId, request.Year, request.Month, cancellationToken);

        var expenses = await _repository.GetExpensesByMonthAsync(
            tenantId, request.Year, request.Month, cancellationToken);

        // Calculate income by category
        var incomeByCategory = transactions
            .GroupBy(t => t.Category ?? "Other")
            .Select(g => new CategorySummaryDto
            {
                Category = g.Key,
                Amount = g.Sum(t => t.Amount),
                Count = g.Count()
            })
            .ToList();

        var totalIncome = incomeByCategory.Sum(c => c.Amount);
        foreach (var cat in incomeByCategory)
        {
            cat.PercentageOfTotal = totalIncome > 0
                ? Math.Round(cat.Amount / totalIncome * 100, 2)
                : 0;
        }

        // Calculate expenses by category
        var expenseByCategory = expenses
            .GroupBy(e => e.Category ?? "Other")
            .Select(g => new CategorySummaryDto
            {
                Category = g.Key,
                Amount = g.Sum(e => e.Amount),
                Count = g.Count()
            })
            .ToList();

        var totalExpenses = expenseByCategory.Sum(c => c.Amount);
        foreach (var cat in expenseByCategory)
        {
            cat.PercentageOfTotal = totalExpenses > 0
                ? Math.Round(cat.Amount / totalExpenses * 100, 2)
                : 0;
        }

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        return new MonthlySummaryDto
        {
            Year = request.Year,
            Month = request.Month,
            TenantId = tenantId,
            TotalIncome = totalIncome,
            TotalExpenses = totalExpenses,
            NetIncome = totalIncome - totalExpenses,
            TransactionCount = transactions.Count + expenses.Count,
            IncomeByCategory = incomeByCategory,
            ExpenseByCategory = expenseByCategory,
            Currency = currency
        };
    }
}
