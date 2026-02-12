using MediatR;
using TakeTime.HumanResources.Application.DTOs;

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
    public async Task<EmployeeDto> Handle(CreateEmployeeCommand request, CancellationToken ct)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.FirstName))
            throw new InvalidOperationException("First name is required.");

        if (string.IsNullOrWhiteSpace(request.LastName))
            throw new InvalidOperationException("Last name is required.");

        if (string.IsNullOrWhiteSpace(request.Department))
            throw new InvalidOperationException("Department is required.");

        if (string.IsNullOrWhiteSpace(request.Position))
            throw new InvalidOperationException("Position is required.");

        // Validate phone format if provided
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var phone = TakeTime.Core.Domain.ValueObjects.PhoneNumber.TryCreate(request.Phone);
            if (phone is null)
                throw new InvalidOperationException(
                    $"'{request.Phone}' is not a valid Thai phone number. Expected formats: 0x-xxxx-xxxx (mobile) or 0x-xxx-xxxx (landline).");
        }

        // In real implementation:
        // 1. Check employee code uniqueness per tenant
        // 2. Auto-generate employee code (e.g., EMP-yyyyMMdd-xxxx)
        // 3. Create Employee entity via domain constructor
        // 4. Set default leave balances based on Thai labor law
        // 5. Save to HRDbContext
        var id = Guid.NewGuid();
        var employeeCode = $"EMP-{DateTime.UtcNow:yyyyMMdd}-{id.ToString()[..4].ToUpper()}";

        // Default leave entitlements per Thai labor law
        var annualLeave = 6m; // minimum 6 days after 1 year
        var sickLeave = 30m;  // up to 30 days per year
        var personalLeave = 3m; // typical 3 days

        return await Task.FromResult(new EmployeeDto
        {
            Id = id,
            EmployeeNumber = employeeCode,
            FirstName = request.FirstName,
            LastName = request.LastName,
            NickName = request.NickName,
            Email = request.Email,
            Phone = request.Phone,
            DateOfBirth = request.DateOfBirth,
            NationalId = request.NationalId,
            TaxId = request.TaxId,
            Address = request.Address,
            Department = request.Department,
            Position = request.Position,
            EmploymentType = request.EmploymentType,
            EmploymentStatus = "Active",
            HireDate = request.HireDate,
            ManagerId = request.ManagerId,
            BaseSalary = request.BaseSalary,
            SalaryType = request.SalaryType,
            Currency = "THB",
            BankName = request.BankName,
            BankAccountNumber = request.BankAccountNumber,
            SocialSecurityNumber = request.SocialSecurityNumber,
            SocialSecurityRate = 0.05m,
            AnnualLeaveBalance = annualLeave,
            SickLeaveBalance = sickLeave,
            PersonalLeaveBalance = personalLeave,
            ProfileImageUrl = request.ProfileImageUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
    }
}
