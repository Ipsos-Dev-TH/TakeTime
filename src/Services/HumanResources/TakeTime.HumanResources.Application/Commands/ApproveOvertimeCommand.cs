using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.HumanResources.Application.DTOs;
using TakeTime.HumanResources.Infrastructure.Repositories;

namespace TakeTime.HumanResources.Application.Commands;

public class ApproveOvertimeCommand : IRequest<OvertimeDto>
{
    public Guid OvertimeRecordId { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
}

public class ApproveOvertimeHandler : IRequestHandler<ApproveOvertimeCommand, OvertimeDto>
{
    private readonly HRDbContext _db;
    private readonly ILogger<ApproveOvertimeHandler> _logger;

    public ApproveOvertimeHandler(HRDbContext db, ILogger<ApproveOvertimeHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<OvertimeDto> Handle(ApproveOvertimeCommand request, CancellationToken ct)
    {
        var otRecord = await _db.OvertimeRecords.FirstOrDefaultAsync(e => e.Id == request.OvertimeRecordId, ct);
        if (otRecord is null)
            throw new InvalidOperationException($"Overtime record with ID '{request.OvertimeRecordId}' not found.");

        // Apply approval or rejection via domain methods
        if (request.IsApproved)
        {
            otRecord.Approve(request.ApprovedBy);
        }
        else
        {
            otRecord.Reject(request.ApprovedBy);
        }

        await _db.SaveChangesAsync(ct);

        // Load employee info for the DTO
        var employee = await _db.Employees
            .Where(e => e.Id == otRecord.EmployeeId)
            .Select(e => new { e.FullName, e.EmployeeCode, e.Department })
            .FirstOrDefaultAsync(ct);

        var action = request.IsApproved ? "Approved" : "Rejected";
        _logger.LogInformation(
            "{Action} overtime record {Id} for employee {EmployeeCode} by {ApprovedBy}",
            action, otRecord.Id, employee?.EmployeeCode ?? "unknown", request.ApprovedBy);

        return new OvertimeDto
        {
            Id = otRecord.Id,
            EmployeeId = otRecord.EmployeeId,
            EmployeeName = employee?.FullName ?? string.Empty,
            EmployeeNumber = employee?.EmployeeCode,
            Department = employee?.Department ?? string.Empty,
            Date = otRecord.Date,
            StartTime = otRecord.StartTime,
            EndTime = otRecord.EndTime,
            TotalHours = otRecord.TotalHours,
            OTRate = otRecord.OTRate,
            Amount = otRecord.Amount.Amount,
            Currency = "THB",
            Status = otRecord.Status.ToString(),
            ApprovedBy = otRecord.ApprovedBy,
            CreatedAt = otRecord.CreatedAt,
            UpdatedAt = otRecord.UpdatedAt
        };
    }
}
