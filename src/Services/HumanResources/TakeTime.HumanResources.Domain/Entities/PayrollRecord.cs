using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.HumanResources.Domain.Enums;

namespace TakeTime.HumanResources.Domain.Entities;

/// <summary>
/// Aggregate root representing a monthly payroll record for an employee.
/// Tracks all salary components, deductions, and net pay.
/// </summary>
public class PayrollRecord : AggregateRoot
{
    /// <summary>
    /// Identifier of the employee.
    /// </summary>
    public Guid EmployeeId { get; private set; }

    /// <summary>
    /// Payroll month (1-12).
    /// </summary>
    public int Month { get; private set; }

    /// <summary>
    /// Payroll year.
    /// </summary>
    public int Year { get; private set; }

    /// <summary>
    /// Period display string (e.g., "January 2024").
    /// </summary>
    public string Period => $"{new DateTime(Year, Month, 1):MMMM yyyy}";

    /// <summary>
    /// Base monthly salary.
    /// </summary>
    public Money BaseSalary { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Total overtime amount for the period.
    /// </summary>
    public Money OTAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Total allowances (housing, transport, meal, etc.).
    /// </summary>
    public Money Allowances { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Total deductions (absence, advances, etc.).
    /// </summary>
    public Money Deductions { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Gross pay before statutory deductions.
    /// </summary>
    public Money GrossPay { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Personal income tax deduction.
    /// </summary>
    public Money TaxDeduction { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Social security contribution (employee portion).
    /// </summary>
    public Money SocialSecurity { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Net pay after all deductions.
    /// </summary>
    public Money NetPay { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Current status of the payroll record.
    /// </summary>
    public PayrollStatus Status { get; private set; }

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private PayrollRecord() { }

    /// <summary>
    /// Creates a new payroll record.
    /// </summary>
    public PayrollRecord(
        Guid employeeId,
        int month,
        int year,
        Money baseSalary)
    {
        if (month < 1 || month > 12)
            throw new ArgumentException("Month must be between 1 and 12.", nameof(month));

        if (year < 2000)
            throw new ArgumentException("Year must be 2000 or later.", nameof(year));

        EmployeeId = employeeId;
        Month = month;
        Year = year;
        BaseSalary = baseSalary;
        Status = PayrollStatus.Draft;
    }

    /// <summary>
    /// Sets the overtime amount for the period.
    /// </summary>
    public void SetOTAmount(Money otAmount)
    {
        OTAmount = otAmount;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the allowances amount.
    /// </summary>
    public void SetAllowances(Money allowances)
    {
        Allowances = allowances;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the deductions amount.
    /// </summary>
    public void SetDeductions(Money deductions)
    {
        Deductions = deductions;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates gross pay, tax, social security, and net pay.
    /// </summary>
    public void Calculate()
    {
        if (Status != PayrollStatus.Draft)
            throw new InvalidOperationException(
                $"Cannot calculate payroll in '{Status}' status. Only Draft payrolls can be calculated.");

        // Gross pay = Base Salary + OT + Allowances - Deductions
        GrossPay = BaseSalary + OTAmount + Allowances - Deductions;

        // Social Security: 5% of gross pay, capped at 750 THB/month
        var ssAmount = Math.Min(GrossPay.Amount * 0.05m, 750m);
        SocialSecurity = Money.InBaht(Math.Round(ssAmount, 2));

        // Tax deduction: simplified progressive rate
        // (In production, this would use Thailand's progressive tax tables)
        TaxDeduction = CalculateWithholdingTax(GrossPay);

        // Net pay = Gross - Tax - Social Security
        NetPay = GrossPay - TaxDeduction - SocialSecurity;

        Status = PayrollStatus.Calculated;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Approves the calculated payroll.
    /// </summary>
    public void Approve()
    {
        if (Status != PayrollStatus.Calculated)
            throw new InvalidOperationException(
                $"Cannot approve payroll in '{Status}' status. Payroll must be Calculated first.");

        Status = PayrollStatus.Approved;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the payroll as paid.
    /// </summary>
    public void MarkAsPaid()
    {
        if (Status != PayrollStatus.Approved)
            throw new InvalidOperationException(
                $"Cannot mark payroll as paid in '{Status}' status. Payroll must be Approved first.");

        Status = PayrollStatus.Paid;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Resets the payroll to Draft status for recalculation.
    /// </summary>
    public void ResetToDraft()
    {
        if (Status == PayrollStatus.Paid)
            throw new InvalidOperationException("Cannot reset a paid payroll.");

        Status = PayrollStatus.Draft;
        UpdatedAt = DateTime.UtcNow;
    }

    private static Money CalculateWithholdingTax(Money grossPay)
    {
        // Simplified monthly withholding tax calculation for Thailand
        // Annualize, apply progressive rates, then divide by 12
        var annualIncome = grossPay.Amount * 12m;

        // Deduct personal allowance (60,000 THB) and expenses (100,000 THB)
        var taxableIncome = Math.Max(0, annualIncome - 160000m);

        decimal annualTax;
        if (taxableIncome <= 150000m)
            annualTax = 0m;
        else if (taxableIncome <= 300000m)
            annualTax = (taxableIncome - 150000m) * 0.05m;
        else if (taxableIncome <= 500000m)
            annualTax = 7500m + (taxableIncome - 300000m) * 0.10m;
        else if (taxableIncome <= 750000m)
            annualTax = 27500m + (taxableIncome - 500000m) * 0.15m;
        else if (taxableIncome <= 1000000m)
            annualTax = 65000m + (taxableIncome - 750000m) * 0.20m;
        else if (taxableIncome <= 2000000m)
            annualTax = 115000m + (taxableIncome - 1000000m) * 0.25m;
        else if (taxableIncome <= 5000000m)
            annualTax = 365000m + (taxableIncome - 2000000m) * 0.30m;
        else
            annualTax = 1265000m + (taxableIncome - 5000000m) * 0.35m;

        var monthlyTax = Math.Round(annualTax / 12m, 2);
        return Money.InBaht(monthlyTax);
    }
}
