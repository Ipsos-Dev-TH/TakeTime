using MediatR;
using TakeTime.HumanResources.Application.DTOs;

namespace TakeTime.HumanResources.Application.Commands;

public class CalculatePayrollCommand : IRequest<List<PayrollDto>>
{
    public int Month { get; set; }
    public int Year { get; set; }
    public List<Guid>? EmployeeIds { get; set; }
}

public class CalculatePayrollHandler : IRequestHandler<CalculatePayrollCommand, List<PayrollDto>>
{
    // Thai social security rate: 5% of salary (max 750 THB/month)
    private const decimal SocialSecurityRate = 0.05m;
    private const decimal MaxSocialSecurity = 750m;

    public async Task<List<PayrollDto>> Handle(CalculatePayrollCommand request, CancellationToken ct)
    {
        // Placeholder: in real implementation, fetch employees and OT records from DB
        // and use tenant's OT rate settings
        return await Task.FromResult(new List<PayrollDto>());
    }

    public static PayrollDto CalculateForEmployee(
        Guid employeeId, decimal baseSalary, decimal otAmount,
        decimal allowances, decimal deductions, decimal otRateMultiplier)
    {
        var grossPay = baseSalary + (otAmount * otRateMultiplier) + allowances - deductions;

        // Thai social security: 5% capped at 750 THB
        var socialSecurity = Math.Min(baseSalary * SocialSecurityRate, MaxSocialSecurity);

        // Simplified withholding tax (in real system, use Thai progressive tax brackets)
        var annualIncome = grossPay * 12;
        var taxDeduction = CalculateMonthlyWithholdingTax(annualIncome);

        var netPay = grossPay - socialSecurity - taxDeduction;

        return new PayrollDto
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            BaseSalary = baseSalary,
            OvertimePay = otAmount * otRateMultiplier,
            Allowances = allowances,
            OtherDeductions = deductions,
            GrossEarnings = grossPay,
            SocialSecurityEmployee = socialSecurity,
            IncomeTax = taxDeduction,
            TotalDeductions = socialSecurity + taxDeduction + deductions,
            NetPay = netPay,
            Status = "Calculated"
        };
    }

    private static decimal CalculateMonthlyWithholdingTax(decimal annualIncome)
    {
        // Thai progressive tax brackets (simplified)
        decimal annualTax = 0;
        if (annualIncome > 5_000_000) annualTax += (annualIncome - 5_000_000) * 0.35m;
        if (annualIncome > 2_000_000) annualTax += Math.Min(annualIncome - 2_000_000, 3_000_000) * 0.30m;
        if (annualIncome > 1_000_000) annualTax += Math.Min(annualIncome - 1_000_000, 1_000_000) * 0.25m;
        if (annualIncome > 750_000) annualTax += Math.Min(annualIncome - 750_000, 250_000) * 0.20m;
        if (annualIncome > 500_000) annualTax += Math.Min(annualIncome - 500_000, 250_000) * 0.15m;
        if (annualIncome > 300_000) annualTax += Math.Min(annualIncome - 300_000, 200_000) * 0.10m;
        if (annualIncome > 150_000) annualTax += Math.Min(annualIncome - 150_000, 150_000) * 0.05m;

        return Math.Round(annualTax / 12, 2);
    }
}
