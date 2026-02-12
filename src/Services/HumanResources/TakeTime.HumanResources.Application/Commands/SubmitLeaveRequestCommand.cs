using MediatR;
using TakeTime.HumanResources.Application.DTOs;

namespace TakeTime.HumanResources.Application.Commands;

public class SubmitLeaveRequestCommand : IRequest<LeaveRequestDto>
{
    public Guid EmployeeId { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Reason { get; set; }
    public Guid? ReplacementEmployeeId { get; set; }
}

public class SubmitLeaveRequestHandler : IRequestHandler<SubmitLeaveRequestCommand, LeaveRequestDto>
{
    public async Task<LeaveRequestDto> Handle(SubmitLeaveRequestCommand request, CancellationToken ct)
    {
        var totalDays = (request.EndDate.Date - request.StartDate.Date).Days + 1;

        if (totalDays <= 0)
            throw new InvalidOperationException("End date must be on or after start date.");

        return await Task.FromResult(new LeaveRequestDto
        {
            Id = Guid.NewGuid(),
            EmployeeId = request.EmployeeId,
            LeaveType = request.LeaveType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalDays = totalDays,
            Reason = request.Reason ?? string.Empty,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });
    }
}
