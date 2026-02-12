using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.HumanResources.Application.DTOs;
using TakeTime.HumanResources.Domain.Enums;
using TakeTime.HumanResources.Infrastructure.Repositories;

namespace TakeTime.HumanResources.Application.Queries;

public class GetEmployeeLeaveBalanceQuery : IRequest<LeaveBalanceDto?>
{
    public Guid EmployeeId { get; set; }
}

public class GetEmployeeLeaveBalanceHandler : IRequestHandler<GetEmployeeLeaveBalanceQuery, LeaveBalanceDto?>
{
    private readonly HRDbContext _db;
    private readonly ILogger<GetEmployeeLeaveBalanceHandler> _logger;

    // Thai labor law default entitlements
    private const decimal DefaultAnnualLeave = 6m;  // minimum 6 days after 1 year of service
    private const decimal DefaultSickLeave = 30m;   // up to 30 days per year
    private const decimal DefaultPersonalLeave = 3m; // typical 3 days

    public GetEmployeeLeaveBalanceHandler(HRDbContext db, ILogger<GetEmployeeLeaveBalanceHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<LeaveBalanceDto?> Handle(GetEmployeeLeaveBalanceQuery request, CancellationToken ct)
    {
        // Load employee to verify existence and get name
        var employee = await _db.Employees
            .Where(e => e.Id == request.EmployeeId)
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.StartDate })
            .FirstOrDefaultAsync(ct);

        if (employee is null)
        {
            _logger.LogWarning("Employee with ID '{EmployeeId}' not found for leave balance query.", request.EmployeeId);
            return null;
        }

        // Query leave requests for the current year
        var currentYear = DateTime.UtcNow.Year;
        var yearStart = new DateTime(currentYear, 1, 1);
        var yearEnd = new DateTime(currentYear, 12, 31);

        var leaveRequests = await _db.LeaveRequests
            .Where(lr => lr.EmployeeId == request.EmployeeId &&
                         lr.StartDate >= yearStart &&
                         lr.StartDate <= yearEnd)
            .ToListAsync(ct);

        // Adjust annual leave entitlement based on years of service (Thai labor law)
        var yearsOfService = (DateTime.UtcNow - employee.StartDate).Days / 365.0;
        var annualLeaveEntitled = yearsOfService >= 1 ? DefaultAnnualLeave : 0m;

        // Calculate used and pending days per leave type
        var balances = new List<LeaveTypeBalanceDto>();

        foreach (var leaveType in new[] { LeaveType.Annual, LeaveType.Sick, LeaveType.Personal })
        {
            var entitled = leaveType switch
            {
                LeaveType.Annual => annualLeaveEntitled,
                LeaveType.Sick => DefaultSickLeave,
                LeaveType.Personal => DefaultPersonalLeave,
                _ => 0m
            };

            var used = leaveRequests
                .Where(lr => lr.LeaveType == leaveType && lr.Status == LeaveRequestStatus.Approved)
                .Sum(lr => lr.TotalDays);

            var pending = leaveRequests
                .Where(lr => lr.LeaveType == leaveType && lr.Status == LeaveRequestStatus.Pending)
                .Sum(lr => lr.TotalDays);

            var remaining = Math.Max(0, entitled - used - pending);

            balances.Add(new LeaveTypeBalanceDto
            {
                LeaveType = leaveType.ToString(),
                Entitled = entitled,
                Used = used,
                Pending = pending,
                Remaining = remaining
            });
        }

        return new LeaveBalanceDto
        {
            EmployeeId = request.EmployeeId,
            EmployeeName = $"{employee.FirstName} {employee.LastName}",
            Balances = balances
        };
    }
}
