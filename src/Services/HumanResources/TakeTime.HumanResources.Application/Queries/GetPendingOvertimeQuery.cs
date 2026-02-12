using MediatR;
using TakeTime.HumanResources.Application.DTOs;

namespace TakeTime.HumanResources.Application.Queries;

public class GetPendingOvertimeQuery : IRequest<List<OvertimeDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetPendingOvertimeHandler : IRequestHandler<GetPendingOvertimeQuery, List<OvertimeDto>>
{
    public async Task<List<OvertimeDto>> Handle(GetPendingOvertimeQuery request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Query OvertimeRecords from HRDbContext where Status == OvertimeStatus.Pending
        // 2. Include employee name and department
        // 3. Apply pagination
        // 4. Order by date ascending (oldest first for approval priority)
        // 5. Map to OvertimeDto

        return await Task.FromResult(new List<OvertimeDto>());
    }
}
