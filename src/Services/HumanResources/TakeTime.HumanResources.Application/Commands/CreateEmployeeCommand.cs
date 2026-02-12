using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.HumanResources.Application.DTOs;
using TakeTime.HumanResources.Domain.Entities;
using TakeTime.HumanResources.Domain.Enums;
using TakeTime.HumanResources.Infrastructure.Repositories;

namespace TakeTime.HumanResources.Application.Commands;

public class CreateEmployeeCommand : IRequest<EmployeeDto>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? NationalId { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = "FullTime";
    public DateTime HireDate { get; set; }
    public Guid? ManagerId { get; set; }
    public decimal BaseSalary { get; set; }
    public string SalaryType { get; set; } = "Monthly";
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? SocialSecurityNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
}

public class CreateEmployeeHandler : IRequestHandler<CreateEmployeeCommand, EmployeeDto>
{
    private readonly HRDbContext _db;
    private readonly ILogger<CreateEmployeeHandler> _logger;

    public CreateEmployeeHandler(HRDbContext db, ILogger<CreateEmployeeHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<EmployeeDto> Handle(CreateEmployeeCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName))
            throw new InvalidOperationException("First name is required.");

        if (string.IsNullOrWhiteSpace(request.LastName))
            throw new InvalidOperationException("Last name is required.");

        if (string.IsNullOrWhiteSpace(request.Department))
            throw new InvalidOperationException("Department is required.");

        if (string.IsNullOrWhiteSpace(request.Position))
            throw new InvalidOperationException("Position is required.");

        // Parse and validate phone number
        PhoneNumber? phone = null;
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            phone = PhoneNumber.TryCreate(request.Phone);
            if (phone is null)
                throw new InvalidOperationException(
                    $"'{request.Phone}' is not a valid Thai phone number. Expected formats: 0x-xxxx-xxxx (mobile) or 0x-xxx-xxxx (landline).");
        }

        // Auto-generate employee code
        var employeeCode = $"EMP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

        // Check uniqueness of employee code
        var codeExists = await _db.Employees.AnyAsync(e => e.EmployeeCode == employeeCode, ct);
        if (codeExists)
        {
            employeeCode = $"EMP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
        }

        // Parse employment type
        if (!Enum.TryParse<EmploymentType>(request.EmploymentType, true, out var employmentType))
            employmentType = EmploymentType.FullTime;

        // Create Employee entity via domain constructor
        var employee = new Employee(
            employeeCode: employeeCode,
            firstName: request.FirstName,
            lastName: request.LastName,
            dateOfBirth: request.DateOfBirth,
            phone: phone ?? PhoneNumber.TryCreate("000-000-0000")!,
            position: request.Position,
            department: request.Department,
            startDate: request.HireDate,
            salary: Money.InBaht(request.BaseSalary),
            employmentType: employmentType,
            nickName: request.NickName,
            email: request.Email,
            address: request.Address,
            bankAccount: request.BankAccountNumber,
            supervisorId: request.ManagerId
        );

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created employee {EmployeeCode} ({FullName})", employee.EmployeeCode, employee.FullName);

        // Default leave entitlements per Thai labor law
        var annualLeave = 6m;
        var sickLeave = 30m;
        var personalLeave = 3m;

        return new EmployeeDto
        {
            Id = employee.Id,
            EmployeeNumber = employee.EmployeeCode,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            NickName = employee.NickName,
            Email = employee.Email,
            Phone = employee.Phone?.ToString(),
            DateOfBirth = employee.DateOfBirth,
            NationalId = request.NationalId,
            TaxId = request.TaxId,
            Address = employee.Address,
            Department = employee.Department,
            Position = employee.Position,
            EmploymentType = employee.EmploymentType.ToString(),
            EmploymentStatus = employee.Status.ToString(),
            HireDate = employee.StartDate,
            ManagerId = employee.SupervisorId,
            BaseSalary = employee.Salary.Amount,
            SalaryType = request.SalaryType,
            Currency = "THB",
            BankName = request.BankName,
            BankAccountNumber = employee.BankAccount,
            SocialSecurityNumber = request.SocialSecurityNumber,
            SocialSecurityRate = 0.05m,
            AnnualLeaveBalance = annualLeave,
            SickLeaveBalance = sickLeave,
            PersonalLeaveBalance = personalLeave,
            ProfileImageUrl = request.ProfileImageUrl,
            IsActive = true,
            CreatedAt = employee.CreatedAt
        };
    }
}
