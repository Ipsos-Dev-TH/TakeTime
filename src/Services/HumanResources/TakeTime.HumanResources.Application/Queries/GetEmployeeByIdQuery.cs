using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.HumanResources.Application.DTOs;
using TakeTime.HumanResources.Domain.Enums;
using TakeTime.HumanResources.Infrastructure.Repositories;

namespace TakeTime.HumanResources.Application.Queries;

public class GetEmployeeByIdQuery : IRequest<EmployeeDto?>
{
    public Guid EmployeeId { get; set; }
}

public class GetEmployeeByIdHandler : IRequestHandler<GetEmployeeByIdQuery, EmployeeDto?>
{
    private readonly HRDbContext _db;
    private readonly ILogger<GetEmployeeByIdHandler> _logger;

    // Thai labor law default entitlements
    private const decimal DefaultAnnualLeave = 6m;
    private const decimal DefaultSickLeave = 30m;
    private const decimal DefaultPersonalLeave = 3m;

    public GetEmployeeByIdHandler(HRDbContext db, ILogger<GetEmployeeByIdHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<EmployeeDto?> Handle(GetEmployeeByIdQuery request, CancellationToken ct)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct);
        if (employee is null)
        {
            _logger.LogWarning("Employee with ID '{EmployeeId}' not found.", request.EmployeeId);
            return null;
        }

        // Look up manager name
        string? managerName = null;
        if (employee.SupervisorId.HasValue)
        {
            var manager = await _db.Employees
                .Where(e => e.Id == employee.SupervisorId.Value)
                .Select(e => new { e.FirstName, e.LastName })
                .FirstOrDefaultAsync(ct);
            managerName = manager is not null ? $"{manager.FirstName} {manager.LastName}" : null;
        }

        // Calculate leave balances from approved/pending leave requests for the current year
        var currentYear = DateTime.UtcNow.Year;
        var yearStart = new DateTime(currentYear, 1, 1);
        var yearEnd = new DateTime(currentYear, 12, 31);

        var leaveRequests = await _db.LeaveRequests
            .Where(lr => lr.EmployeeId == request.EmployeeId &&
                         lr.StartDate >= yearStart &&
                         lr.StartDate <= yearEnd &&
                         (lr.Status == LeaveRequestStatus.Approved || lr.Status == LeaveRequestStatus.Pending))
            .ToListAsync(ct);

        var annualUsed = leaveRequests
            .Where(lr => lr.LeaveType == LeaveType.Annual && lr.Status == LeaveRequestStatus.Approved)
            .Sum(lr => lr.TotalDays);
        var sickUsed = leaveRequests
            .Where(lr => lr.LeaveType == LeaveType.Sick && lr.Status == LeaveRequestStatus.Approved)
            .Sum(lr => lr.TotalDays);
        var personalUsed = leaveRequests
            .Where(lr => lr.LeaveType == LeaveType.Personal && lr.Status == LeaveRequestStatus.Approved)
            .Sum(lr => lr.TotalDays);

        return new EmployeeDto
        {
            Id = employee.Id,
            TenantId = employee.TenantId ?? string.Empty,
            EmployeeNumber = employee.EmployeeCode,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            NickName = employee.NickName,
            Email = employee.Email,
            Phone = employee.Phone?.ToString(),
            DateOfBirth = employee.DateOfBirth,
            Address = employee.Address,
            Department = employee.Department,
            Position = employee.Position,
            EmploymentType = employee.EmploymentType.ToString(),
            EmploymentStatus = employee.Status.ToString(),
            HireDate = employee.StartDate,
            TerminationDate = employee.EndDate,
            ManagerId = employee.SupervisorId,
            ManagerName = managerName,
            BaseSalary = employee.Salary.Amount,
            SalaryType = "Monthly",
            Currency = "THB",
            BankAccountNumber = employee.BankAccount,
            AnnualLeaveBalance = DefaultAnnualLeave - annualUsed,
            SickLeaveBalance = DefaultSickLeave - sickUsed,
            PersonalLeaveBalance = DefaultPersonalLeave - personalUsed,
            SocialSecurityRate = 0.05m,
            IsActive = employee.Status == EmployeeStatus.Active,
            CreatedAt = employee.CreatedAt,
            UpdatedAt = employee.UpdatedAt
        };
    }
}
