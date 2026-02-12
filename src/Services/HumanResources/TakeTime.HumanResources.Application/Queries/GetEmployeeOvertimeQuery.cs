using MediatR;
using TakeTime.HumanResources.Application.DTOs;

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
    public async Task<List<OvertimeDto>> Handle(GetEmployeeOvertimeQuery request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Query OvertimeRecords from HRDbContext for the specified employee
        // 2. Filter by date range and status if provided
        // 3. Include employee name and department
        // 4. Order by date descending
        // 5. Map to OvertimeDto

        return await Task.FromResult(new List<OvertimeDto>());
    }
}
