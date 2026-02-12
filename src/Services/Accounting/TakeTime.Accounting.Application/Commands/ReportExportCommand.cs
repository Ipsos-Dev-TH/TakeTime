using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Accounting.Application.DTOs;
using TakeTime.Accounting.Application.Queries;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Accounting.Application.Commands;

/// <summary>
/// Command to export a report to CSV format.
/// </summary>
public sealed class ReportExportCommand : IRequest<ReportExportResultDto>
{
    public string ReportType { get; set; } = string.Empty; // "daily-revenue", "profit-loss", "occupancy", "monthly-summary", "transactions"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Format { get; set; } = "csv"; // csv or excel
}

/// <summary>
/// Handler for <see cref="ReportExportCommand"/>. Generates the requested report
/// and exports it to the specified format (CSV).
/// </summary>
public sealed class ReportExportCommandHandler : IRequestHandler<ReportExportCommand, ReportExportResultDto>
{
    private readonly IAccountingQueryRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<ReportExportCommandHandler> _logger;

    public ReportExportCommandHandler(
        IAccountingQueryRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<ReportExportCommandHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<ReportExportResultDto> Handle(ReportExportCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Exporting {ReportType} report for tenant {TenantId} from {StartDate} to {EndDate} as {Format}",
            request.ReportType, tenantId, request.StartDate.ToString("yyyy-MM-dd"),
            request.EndDate.ToString("yyyy-MM-dd"), request.Format);

        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;
        var csv = new StringBuilder();

        switch (request.ReportType.ToLowerInvariant())
        {
            case "daily-revenue":
                csv.AppendLine("Date,Category,Amount,TransactionCount");
                var transactions = await _repository.GetTransactionsByDateRangeAsync(
                    tenantId, startDate, endDate, cancellationToken);
                foreach (var group in transactions.GroupBy(t => new { t.TransactionDate, t.Category }))
                {
                    csv.AppendLine($"{group.Key.TransactionDate:yyyy-MM-dd},{EscapeCsv(group.Key.Category)},{group.Sum(t => t.Amount):F2},{group.Count()}");
                }
                break;

            case "profit-loss":
                csv.AppendLine("Type,Category,Amount");
                var plTransactions = await _repository.GetTransactionsByDateRangeAsync(
                    tenantId, startDate, endDate, cancellationToken);
                foreach (var group in plTransactions.GroupBy(t => t.Category))
                {
                    csv.AppendLine($"Income,{EscapeCsv(group.Key)},{group.Sum(t => t.Amount):F2}");
                }
                var plExpenses = await _repository.GetExpensesByDateRangeAsync(
                    tenantId, startDate, endDate, cancellationToken);
                foreach (var group in plExpenses.GroupBy(e => e.Category))
                {
                    csv.AppendLine($"Expense,{EscapeCsv(group.Key)},{group.Sum(e => e.Amount):F2}");
                }
                break;

            case "transactions":
                csv.AppendLine("Date,Category,SubCategory,Amount,Reference");
                var txns = await _repository.GetTransactionsByDateRangeAsync(
                    tenantId, startDate, endDate, cancellationToken);
                foreach (var t in txns.OrderBy(t => t.TransactionDate))
                {
                    csv.AppendLine($"{t.TransactionDate:yyyy-MM-dd},{EscapeCsv(t.Category)},{EscapeCsv(t.SubCategory ?? "")},{t.Amount:F2},{EscapeCsv(t.Reference ?? "")}");
                }
                break;

            default:
                csv.AppendLine("Date,Category,Amount");
                var defaultTxns = await _repository.GetTransactionsByDateRangeAsync(
                    tenantId, startDate, endDate, cancellationToken);
                foreach (var t in defaultTxns.OrderBy(t => t.TransactionDate))
                {
                    csv.AppendLine($"{t.TransactionDate:yyyy-MM-dd},{EscapeCsv(t.Category)},{t.Amount:F2}");
                }
                break;
        }

        var fileName = $"{request.ReportType}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
        var data = Encoding.UTF8.GetBytes(csv.ToString());

        _logger.LogInformation(
            "Report exported: {FileName}, {Size} bytes",
            fileName, data.Length);

        return new ReportExportResultDto
        {
            FileName = fileName,
            Format = "csv",
            ContentType = "text/csv",
            Data = data,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
