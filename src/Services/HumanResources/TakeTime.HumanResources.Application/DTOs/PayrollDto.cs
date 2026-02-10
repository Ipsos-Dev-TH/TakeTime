namespace TakeTime.HumanResources.Application.DTOs;

/// <summary>
/// Full payroll record data transfer object with tax and social security breakdown.
/// </summary>
public sealed class PayrollDto
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string PayrollNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;

    // Pay period
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime PayDate { get; set; }
    public string PayPeriodType { get; set; } = "Monthly"; // Monthly, BiWeekly, Weekly

    // Earnings
    public decimal BaseSalary { get; set; }
    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal OvertimeRate { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal HolidayOvertimeHours { get; set; }
    public decimal HolidayOvertimeRate { get; set; }
    public decimal HolidayOvertimePay { get; set; }
    public decimal Allowances { get; set; }
    public decimal Commission { get; set; }
    public decimal Bonus { get; set; }
    public decimal GrossEarnings { get; set; }
    public List<PayrollEarningItemDto> EarningsBreakdown { get; set; } = [];

    // Deductions
    public decimal IncomeTax { get; set; }
    public decimal SocialSecurityEmployee { get; set; }
    public decimal SocialSecurityEmployer { get; set; }
    public decimal SocialSecurityRate { get; set; }
    public decimal ProvidentFund { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public List<PayrollDeductionItemDto> DeductionsBreakdown { get; set; } = [];

    // Net pay
    public decimal NetPay { get; set; }
    public string Currency { get; set; } = "THB";

    // Status
    public string Status { get; set; } = string.Empty; // Draft, Approved, Paid
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? PaidBy { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaymentReference { get; set; }

    // Bank details
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Individual earning line item in a payroll record.
/// </summary>
public sealed class PayrollEarningItemDto
{
    public string Description { get; set; } = string.Empty;
    public string EarningType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? Hours { get; set; }
    public decimal? Rate { get; set; }
}

/// <summary>
/// Individual deduction line item in a payroll record.
/// </summary>
public sealed class PayrollDeductionItemDto
{
    public string Description { get; set; } = string.Empty;
    public string DeductionType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? Rate { get; set; }
    public string? Reference { get; set; }
}

/// <summary>
/// Payroll summary for a pay period across all employees.
/// </summary>
public sealed class PayrollSummaryDto
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public decimal TotalGrossEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNetPay { get; set; }
    public decimal TotalSocialSecurityEmployer { get; set; }
    public decimal TotalSocialSecurityEmployee { get; set; }
    public decimal TotalIncomeTax { get; set; }
    public decimal TotalOvertimePay { get; set; }
    public string Currency { get; set; } = "THB";
    public string Status { get; set; } = string.Empty;
    public List<PayrollDto> Records { get; set; } = [];
}
