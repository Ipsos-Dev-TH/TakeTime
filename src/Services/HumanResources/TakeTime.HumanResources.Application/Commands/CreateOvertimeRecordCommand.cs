using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.HumanResources.Application.DTOs;
using TakeTime.HumanResources.Domain.Entities;
using TakeTime.HumanResources.Infrastructure.Repositories;

namespace TakeTime.HumanResources.Application.Commands;

public class CreateOvertimeRecordCommand : IRequest<OvertimeDto>
{
    public Guid EmployeeId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal OTRate { get; set; } = 1.5m;
}

public class CreateOvertimeRecordHandler : IRequestHandler<CreateOvertimeRecordCommand, OvertimeDto>
{
    private readonly HRDbContext _db;
    private readonly ILogger<CreateOvertimeRecordHandler> _logger;

    public CreateOvertimeRecordHandler(HRDbContext db, ILogger<CreateOvertimeRecordHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<OvertimeDto> Handle(CreateOvertimeRecordCommand request, CancellationToken ct)
    {
        if (request.EndTime <= request.StartTime)
            throw new InvalidOperationException("End time must be after start time.");

        if (request.OTRate <= 0)
            throw new InvalidOperationException("OT rate must be positive.");

        // Load employee to verify existence and calculate hourly rate
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct);
        if (employee is null)
            throw new InvalidOperationException($"Employee with ID '{request.EmployeeId}' not found.");

        // Calculate hourly rate from monthly salary: salary / 30 days / 8 hours
        var hourlyRate = Money.InBaht(Math.Round(employee.Salary.Amount / 30m / 8m, 2));

        // Create OvertimeRecord entity via domain constructor
        var otRecord = new OvertimeRecord(
            employeeId: request.EmployeeId,
            date: request.Date,
            startTime: request.StartTime,
            endTime: request.EndTime,
            otRate: request.OTRate,
            hourlyRate: hourlyRate
        );

        _db.OvertimeRecords.Add(otRecord);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created overtime record for employee {EmployeeCode} on {Date}: {Hours}h at {Rate}x",
            employee.EmployeeCode, request.Date.Date, otRecord.TotalHours, request.OTRate);

        return new OvertimeDto
        {
            Id = otRecord.Id,
            EmployeeId = otRecord.EmployeeId,
            EmployeeName = employee.FullName,
            EmployeeNumber = employee.EmployeeCode,
            Department = employee.Department,
            Date = otRecord.Date,
            StartTime = otRecord.StartTime,
            EndTime = otRecord.EndTime,
            TotalHours = otRecord.TotalHours,
            OTRate = otRecord.OTRate,
            Amount = otRecord.Amount.Amount,
            Currency = "THB",
            Status = otRecord.Status.ToString(),
            CreatedAt = otRecord.CreatedAt
        };
    }
}
