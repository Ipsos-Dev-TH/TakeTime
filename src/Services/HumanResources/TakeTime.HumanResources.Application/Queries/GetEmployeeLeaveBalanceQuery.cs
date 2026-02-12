using MediatR;
using TakeTime.HumanResources.Application.DTOs;

namespace TakeTime.HumanResources.Application.Queries;

public class GetEmployeeLeaveBalanceQuery : IRequest<LeaveBalanceDto?>
{
    public Guid EmployeeId { get; set; }
}

public class GetEmployeeLeaveBalanceHandler : IRequestHandler<GetEmployeeLeaveBalanceQuery, LeaveBalanceDto?>
{
    // Thai labor law default entitlements
    private const decimal DefaultAnnualLeave = 6m;  // minimum 6 days after 1 year of service
    private const decimal DefaultSickLeave = 30m;   // up to 30 days per year
    private const decimal DefaultPersonalLeave = 3m; // typical 3 days

    public async Task<LeaveBalanceDto?> Handle(GetEmployeeLeaveBalanceQuery request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Load employee from HRDbContext to get name and verify existence
        // 2. Query LeaveRequests for this employee in the current year
        // 3. Calculate used days per leave type (only Approved requests)
        // 4. Calculate pending days per leave type (Pending requests)
        // 5. Calculate remaining = entitled - used - pending
        // 6. Adjust annual leave entitlement based on years of service (Thai labor law)

        // Placeholder: return default balances with zero usage
        return await Task.FromResult<LeaveBalanceDto?>(new LeaveBalanceDto
        {
            EmployeeId = request.EmployeeId,
            EmployeeName = string.Empty,
            Balances =
            [
                new LeaveTypeBalanceDto
                {
                    LeaveType = "Annual",
                    Entitled = DefaultAnnualLeave,
                    Used = 0,
                    Pending = 0,
                    Remaining = DefaultAnnualLeave
                },
                new LeaveTypeBalanceDto
                {
                    LeaveType = "Sick",
                    Entitled = DefaultSickLeave,
                    Used = 0,
                    Pending = 0,
                    Remaining = DefaultSickLeave
                },
                new LeaveTypeBalanceDto
                {
                    LeaveType = "Personal",
                    Entitled = DefaultPersonalLeave,
                    Used = 0,
                    Pending = 0,
                    Remaining = DefaultPersonalLeave
                }
            ]
        });
    }
}
