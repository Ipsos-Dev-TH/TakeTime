using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.CRM.Application.DTOs;
using TakeTime.CRM.Domain.Enums;
using TakeTime.CRM.Infrastructure.Repositories;

namespace TakeTime.CRM.Application.Queries;

public class ExportCustomersQuery : IRequest<CustomerExportDto>
{
    public string Format { get; set; } = "csv";
    public string? Tier { get; set; }
    public string? Source { get; set; }
}

public class ExportCustomersHandler : IRequestHandler<ExportCustomersQuery, CustomerExportDto>
{
    private readonly CRMDbContext _db;
    private readonly ILogger<ExportCustomersHandler> _logger;

    public ExportCustomersHandler(CRMDbContext db, ILogger<ExportCustomersHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomerExportDto> Handle(ExportCustomersQuery request, CancellationToken ct)
    {
        var query = _db.Customers.AsQueryable();

        // Apply optional filters
        if (!string.IsNullOrWhiteSpace(request.Tier) &&
            Enum.TryParse<LoyaltyTier>(request.Tier, true, out var tierFilter))
        {
            query = query.Where(c => c.LoyaltyTier == tierFilter);
        }

        if (!string.IsNullOrWhiteSpace(request.Source) &&
            Enum.TryParse<CustomerSource>(request.Source, true, out var sourceFilter))
        {
            query = query.Where(c => c.Source == sourceFilter);
        }

        var customers = await query
            .OrderBy(c => c.CustomerCode)
            .ToListAsync(ct);

        var format = request.Format.ToLowerInvariant();
        var contentType = format switch
        {
            "csv" => "text/csv",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "text/csv"
        };

        var fileName = $"customers-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{format}";

        // Generate CSV content from actual customer data
        var sb = new StringBuilder();
        sb.AppendLine("CustomerCode,FirstName,LastName,Phone,Email,LoyaltyTier,LoyaltyPoints,TotalSpent,TotalVisits,Source,CreatedAt");

        foreach (var c in customers)
        {
            var email = EscapeCsvField(c.Email ?? string.Empty);
            var firstName = EscapeCsvField(c.FirstName);
            var lastName = EscapeCsvField(c.LastName);

            sb.AppendLine(
                $"{c.CustomerCode},{firstName},{lastName},{c.Phone},{email}," +
                $"{c.LoyaltyTier},{c.LoyaltyPoints},{c.TotalSpent.Amount:F2},{c.TotalVisits}," +
                $"{c.Source},{c.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }

        _logger.LogInformation("Exported {Count} customers to {Format}", customers.Count, format);

        return new CustomerExportDto
        {
            FileName = fileName,
            ContentType = contentType,
            Data = Encoding.UTF8.GetBytes(sb.ToString()),
            TotalRecords = customers.Count
        };
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
