using MediatR;
using TakeTime.HumanResources.Application.DTOs;

namespace TakeTime.HumanResources.Application.Commands;

public class ApproveOvertimeCommand : IRequest<OvertimeDto>
{
    public Guid OvertimeRecordId { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
}

public class ApproveOvertimeHandler : IRequestHandler<ApproveOvertimeCommand, OvertimeDto>
{
    public async Task<OvertimeDto> Handle(ApproveOvertimeCommand request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Load OvertimeRecord from HRDbContext by ID
        // 2. If not found, throw NotFoundException
        // 3. Call record.Approve(approvedBy) or record.Reject(approvedBy)
        //    based on IsApproved flag
        // 4. Save changes

        var status = request.IsApproved ? "Approved" : "Rejected";

        return await Task.FromResult(new OvertimeDto
        {
            Id = request.OvertimeRecordId,
            Status = status,
            ApprovedBy = request.ApprovedBy,
            UpdatedAt = DateTime.UtcNow
        });
    }
}
