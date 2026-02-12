using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.HumanResources.Application.DTOs;
using TakeTime.HumanResources.Domain.Enums;
using TakeTime.HumanResources.Infrastructure.Repositories;

namespace TakeTime.HumanResources.Application.Commands;

public class UpdateEmployeeCommand : IRequest<EmployeeDto>
{
    public Guid EmployeeId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? NickName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string? EmploymentType { get; set; }
    public string? EmploymentStatus { get; set; }
    public Guid? ManagerId { get; set; }
    public decimal? BaseSalary { get; set; }
    public string? SalaryType { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? SocialSecurityNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
}

public class UpdateEmployeeHandler : IRequestHandler<UpdateEmployeeCommand, EmployeeDto>
{
    private readonly HRDbContext _db;
    private readonly ILogger<UpdateEmployeeHandler> _logger;

    public UpdateEmployeeHandler(HRDbContext db, ILogger<UpdateEmployeeHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<EmployeeDto> Handle(UpdateEmployeeCommand request, CancellationToken ct)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct);
        if (employee is null)
            throw new InvalidOperationException($"Employee with ID '{request.EmployeeId}' not found.");

        // Validate and parse phone if provided
        PhoneNumber? phone = null;
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            phone = PhoneNumber.TryCreate(request.Phone);
            if (phone is null)
                throw new InvalidOperationException(
                    $"'{request.Phone}' is not a valid Thai phone number.");
        }

        // Update personal info if any personal fields are provided
        if (request.FirstName is not null || request.LastName is not null ||
            request.NickName is not null || request.Email is not null ||
            request.Address is not null || request.Phone is not null)
        {
            employee.UpdatePersonalInfo(
                firstName: request.FirstName ?? employee.FirstName,
                lastName: request.LastName ?? employee.LastName,
                nickName: request.NickName ?? employee.NickName,
                email: request.Email ?? employee.Email,
                address: request.Address ?? employee.Address,
                phone: phone ?? employee.Phone
            );
        }

        // Update employment details if any employment fields are provided
        if (request.Position is not null || request.Department is not null ||
            request.BaseSalary.HasValue || request.EmploymentType is not null)
        {
            var empType = employee.EmploymentType;
            if (request.EmploymentType is not null &&
                Enum.TryParse<EmploymentType>(request.EmploymentType, true, out var parsedType))
            {
                empType = parsedType;
            }

            employee.UpdateEmployment(
                position: request.Position ?? employee.Position,
                department: request.Department ?? employee.Department,
                salary: request.BaseSalary.HasValue ? Money.InBaht(request.BaseSalary.Value) : employee.Salary,
                employmentType: empType,
                supervisorId: request.ManagerId ?? employee.SupervisorId
            );
        }

        // Handle status changes via domain methods
        if (request.EmploymentStatus is not null)
        {
            if (Enum.TryParse<EmployeeStatus>(request.EmploymentStatus, true, out var newStatus))
            {
                switch (newStatus)
                {
                    case EmployeeStatus.Inactive when employee.Status == EmployeeStatus.Active:
                        employee.Deactivate();
                        break;
                    case EmployeeStatus.Active when employee.Status == EmployeeStatus.Inactive:
                        employee.Reactivate();
                        break;
                    case EmployeeStatus.Resigned when employee.Status == EmployeeStatus.Active:
                        employee.Resign(DateTime.UtcNow);
                        break;
                    case EmployeeStatus.Terminated when employee.Status == EmployeeStatus.Active:
                        employee.Terminate(DateTime.UtcNow);
                        break;
                }
            }
        }

        // Update bank account if provided
        if (request.BankAccountNumber is not null)
        {
            employee.SetBankAccount(request.BankAccountNumber);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated employee {EmployeeCode} ({FullName})", employee.EmployeeCode, employee.FullName);

        // Look up manager name if available
        string? managerName = null;
        if (employee.SupervisorId.HasValue)
        {
            var manager = await _db.Employees
                .Where(e => e.Id == employee.SupervisorId.Value)
                .Select(e => new { e.FirstName, e.LastName })
                .FirstOrDefaultAsync(ct);
            managerName = manager is not null ? $"{manager.FirstName} {manager.LastName}" : null;
        }

        return new EmployeeDto
        {
            Id = employee.Id,
            EmployeeNumber = employee.EmployeeCode,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            NickName = employee.NickName,
            Email = employee.Email,
            Phone = employee.Phone?.ToString(),
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
            SalaryType = request.SalaryType ?? "Monthly",
            Currency = "THB",
            BankName = request.BankName,
            BankAccountNumber = employee.BankAccount,
            SocialSecurityNumber = request.SocialSecurityNumber,
            SocialSecurityRate = 0.05m,
            ProfileImageUrl = request.ProfileImageUrl,
            IsActive = employee.Status == EmployeeStatus.Active,
            CreatedAt = employee.CreatedAt,
            UpdatedAt = employee.UpdatedAt
        };
    }
}
