using MediatR;
using TakeTime.HumanResources.Application.DTOs;

namespace TakeTime.HumanResources.Application.Commands;

public class ApproveLeaveRequestCommand : IRequest<LeaveRequestDto>
{
    public Guid LeaveRequestId { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public string? RejectionReason { get; set; }
}

public class ApproveLeaveRequestHandler : IRequestHandler<ApproveLeaveRequestCommand, LeaveRequestDto>
{
    public async Task<LeaveRequestDto> Handle(ApproveLeaveRequestCommand request, CancellationToken ct)
    {
        var status = request.IsApproved ? "Approved" : "Rejected";

        return await Task.FromResult(new LeaveRequestDto
        {
            Id = request.LeaveRequestId,
            Status = status,
            ApproverName = request.ApprovedBy,
            ApprovedAt = DateTime.UtcNow,
            RejectionReason = request.RejectionReason
        });
    }
}
