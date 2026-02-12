using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.HumanResources.Application.DTOs;
using TakeTime.HumanResources.Domain.Enums;
using TakeTime.HumanResources.Infrastructure.Repositories;

namespace TakeTime.HumanResources.Application.Queries;

public class GetPendingOvertimeQuery : IRequest<List<OvertimeDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetPendingOvertimeHandler : IRequestHandler<GetPendingOvertimeQuery, List<OvertimeDto>>
{
    private readonly HRDbContext _db;
    private readonly ILogger<GetPendingOvertimeHandler> _logger;

    public GetPendingOvertimeHandler(HRDbContext db, ILogger<GetPendingOvertimeHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<OvertimeDto>> Handle(GetPendingOvertimeQuery request, CancellationToken ct)
    {
        // Query pending overtime records, ordered by date ascending (oldest first for approval priority)
        var skip = (Math.Max(1, request.Page) - 1) * request.PageSize;

        var records = await _db.OvertimeRecords
            .Where(ot => ot.Status == OvertimeStatus.Pending)
            .OrderBy(ot => ot.Date)
            .Skip(skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        if (records.Count == 0)
            return [];

        // Load employee info for all returned records in a single query
        var employeeIds = records.Select(r => r.EmployeeId).Distinct().ToList();
        var employees = await _db.Employees
            .Where(e => employeeIds.Contains(e.Id))
            .Select(e => new { e.Id, e.FullName, e.EmployeeCode, e.Department })
            .ToDictionaryAsync(e => e.Id, ct);

        return records.Select(ot =>
        {
            employees.TryGetValue(ot.EmployeeId, out var employee);

            return new OvertimeDto
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
            };
        }).ToList();
    }
}
