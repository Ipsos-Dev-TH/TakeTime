using MediatR;
using TakeTime.HumanResources.Application.DTOs;

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
    public async Task<OvertimeDto> Handle(CreateOvertimeRecordCommand request, CancellationToken ct)
    {
        // Validate times
        if (request.EndTime <= request.StartTime)
            throw new InvalidOperationException("End time must be after start time.");

        if (request.OTRate <= 0)
            throw new InvalidOperationException("OT rate must be positive.");

        // In real implementation:
        // 1. Load employee from HRDbContext to verify existence and get hourly rate
        // 2. Calculate hourly rate from monthly salary (salary / 30 / 8 for standard)
        // 3. Create OvertimeRecord entity via domain constructor
        // 4. Save to HRDbContext

        var totalHours = Math.Round((decimal)(request.EndTime - request.StartTime).TotalHours, 2);
        var id = Guid.NewGuid();

        return await Task.FromResult(new OvertimeDto
        {
            Id = id,
            EmployeeId = request.EmployeeId,
            Date = request.Date.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            TotalHours = totalHours,
            OTRate = request.OTRate,
            Amount = 0, // Calculated from employee's hourly rate in real implementation
            Currency = "THB",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });
    }
}
