using MediatR;
using TakeTime.HumanResources.Application.DTOs;

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
    public async Task<EmployeeDto> Handle(UpdateEmployeeCommand request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Load employee from HRDbContext by ID
        // 2. If not found, throw NotFoundException
        // 3. Apply partial updates using Employee domain methods:
        //    - UpdatePersonalInfo() for name, phone, email, address
        //    - UpdateEmployment() for position, department, salary, type
        //    - Resign()/Terminate()/Deactivate()/Reactivate() for status changes
        // 4. Recalculate leave balances if status changed
        // 5. Track employment history changes
        // 6. Save changes

        // Validate phone format if provided
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var phone = TakeTime.Core.Domain.ValueObjects.PhoneNumber.TryCreate(request.Phone);
            if (phone is null)
                throw new InvalidOperationException(
                    $"'{request.Phone}' is not a valid Thai phone number.");
        }

        return await Task.FromResult(new EmployeeDto
        {
            Id = request.EmployeeId,
            FirstName = request.FirstName ?? string.Empty,
            LastName = request.LastName ?? string.Empty,
            NickName = request.NickName,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            Department = request.Department ?? string.Empty,
            Position = request.Position ?? string.Empty,
            EmploymentType = request.EmploymentType ?? "FullTime",
            EmploymentStatus = request.EmploymentStatus ?? "Active",
            ManagerId = request.ManagerId,
            BaseSalary = request.BaseSalary ?? 0,
            SalaryType = request.SalaryType ?? "Monthly",
            Currency = "THB",
            BankName = request.BankName,
            BankAccountNumber = request.BankAccountNumber,
            SocialSecurityNumber = request.SocialSecurityNumber,
            ProfileImageUrl = request.ProfileImageUrl,
            IsActive = request.EmploymentStatus is null or "Active",
            UpdatedAt = DateTime.UtcNow
        });
    }
}
