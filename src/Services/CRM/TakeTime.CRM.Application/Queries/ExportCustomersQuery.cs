using MediatR;
using TakeTime.CRM.Application.DTOs;

namespace TakeTime.CRM.Application.Queries;

public class ExportCustomersQuery : IRequest<CustomerExportDto>
{
    public string Format { get; set; } = "csv";
    public string? Tier { get; set; }
    public string? Source { get; set; }
}

public class ExportCustomersHandler : IRequestHandler<ExportCustomersQuery, CustomerExportDto>
{
    public async Task<CustomerExportDto> Handle(ExportCustomersQuery request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Query all customers from CRMDbContext (with optional filters)
        // 2. Generate export data based on format (csv, xlsx)
        // 3. Return file content as byte array with appropriate content type

        var format = request.Format.ToLowerInvariant();
        var contentType = format switch
        {
            "csv" => "text/csv",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "text/csv"
        };

        var fileName = $"customers-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{format}";

        // Placeholder: generate CSV header
        var csvContent = "CustomerCode,FirstName,LastName,Phone,Email,LoyaltyTier,LoyaltyPoints,TotalSpent,TotalVisits,Source,CreatedAt\n";

        return await Task.FromResult(new CustomerExportDto
        {
            FileName = fileName,
            ContentType = contentType,
            Data = System.Text.Encoding.UTF8.GetBytes(csvContent),
            TotalRecords = 0
        });
    }
}
