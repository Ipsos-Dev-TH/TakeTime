namespace TakeTime.HumanResources.Application.DTOs;

/// <summary>
/// Full employee data transfer object.
/// </summary>
public sealed class EmployeeDto
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string? NickName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? NationalId { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }

    // Employment
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty; // FullTime, PartTime, Contract, Temporary
    public string EmploymentStatus { get; set; } = string.Empty; // Active, OnLeave, Suspended, Terminated
    public DateTime HireDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public Guid? ManagerId { get; set; }
    public string? ManagerName { get; set; }

    // Compensation
    public decimal BaseSalary { get; set; }
    public string SalaryType { get; set; } = "Monthly"; // Monthly, Daily, Hourly
    public string Currency { get; set; } = "THB";
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }

    // Leave balances
    public decimal AnnualLeaveBalance { get; set; }
    public decimal SickLeaveBalance { get; set; }
    public decimal PersonalLeaveBalance { get; set; }

    // Social security
    public string? SocialSecurityNumber { get; set; }
    public decimal SocialSecurityRate { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public bool IsActive { get; set; }
    public string? ProfileImageUrl { get; set; }
}

/// <summary>
/// Lightweight employee summary DTO for list views.
/// </summary>
public sealed class EmployeeSummaryDto
{
    public Guid Id { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string EmploymentStatus { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public string? Phone { get; set; }
    public string? ProfileImageUrl { get; set; }
}

/// <summary>
/// Search criteria for filtering employees.
/// </summary>
public sealed class EmployeeSearchCriteria
{
    public string? SearchTerm { get; set; }
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string? EmploymentStatus { get; set; }
    public string? EmploymentType { get; set; }
    public Guid? ManagerId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "FullName";
    public bool SortDescending { get; set; }
}

/// <summary>
/// Paginated result wrapper for HR queries.
/// </summary>
public sealed class PaginatedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
