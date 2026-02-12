using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.HumanResources.Application.DTOs;
using TakeTime.HumanResources.Domain.Enums;
using TakeTime.HumanResources.Infrastructure.Repositories;

namespace TakeTime.HumanResources.Application.Queries;

public class GetEmployeeOvertimeQuery : IRequest<List<OvertimeDto>>
{
    public Guid EmployeeId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Status { get; set; }
}

public class GetEmployeeOvertimeHandler : IRequestHandler<GetEmployeeOvertimeQuery, List<OvertimeDto>>
{
    private readonly HRDbContext _db;
    private readonly ILogger<GetEmployeeOvertimeHandler> _logger;

    public GetEmployeeOvertimeHandler(HRDbContext db, ILogger<GetEmployeeOvertimeHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<OvertimeDto>> Handle(GetEmployeeOvertimeQuery request, CancellationToken ct)
    {
        var query = _db.OvertimeRecords
            .Where(ot => ot.EmployeeId == request.EmployeeId);

        // Filter by date range if provided
        if (request.FromDate.HasValue)
            query = query.Where(ot => ot.Date >= request.FromDate.Value.Date);

        if (request.ToDate.HasValue)
            query = query.Where(ot => ot.Date <= request.ToDate.Value.Date);

        // Filter by status if provided
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<OvertimeStatus>(request.Status, true, out var statusFilter))
        {
            query = query.Where(ot => ot.Status == statusFilter);
        }

        // Order by date descending
        query = query.OrderByDescending(ot => ot.Date);

        var records = await query.ToListAsync(ct);

        // Load employee info
        var employee = await _db.Employees
            .Where(e => e.Id == request.EmployeeId)
            .Select(e => new { e.FullName, e.EmployeeCode, e.Department })
            .FirstOrDefaultAsync(ct);

        return records.Select(ot => new OvertimeDto
        {
            Id = ot.Id,
            EmployeeId = ot.EmployeeId,
            EmployeeName = employee?.FullName ?? string.Empty,
            EmployeeNumber = employee?.EmployeeCode,
            Department = employee?.Department ?? string.Empty,
            Date = ot.Date,
            StartTime = ot.StartTime,
            EndTime = ot.EndTime,
            TotalHours = ot.TotalHours,
            OTRate = ot.OTRate,
            Amount = ot.Amount.Amount,
            Currency = "THB",
            Status = ot.Status.ToString(),
            ApprovedBy = ot.ApprovedBy,
            CreatedAt = ot.CreatedAt,
            UpdatedAt = ot.UpdatedAt
        }).ToList();
    }
}
