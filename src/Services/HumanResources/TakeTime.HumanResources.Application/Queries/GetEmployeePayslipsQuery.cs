using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.HumanResources.Application.DTOs;
using TakeTime.HumanResources.Infrastructure.Repositories;

namespace TakeTime.HumanResources.Application.Queries;

public class GetEmployeePayslipsQuery : IRequest<List<PayrollDto>>
{
    public Guid EmployeeId { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
}

public class GetEmployeePayslipsHandler : IRequestHandler<GetEmployeePayslipsQuery, List<PayrollDto>>
{
    private readonly HRDbContext _db;
    private readonly ILogger<GetEmployeePayslipsHandler> _logger;

    public GetEmployeePayslipsHandler(HRDbContext db, ILogger<GetEmployeePayslipsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<PayrollDto>> Handle(GetEmployeePayslipsQuery request, CancellationToken ct)
    {
        // Build query for payroll records
        var query = _db.PayrollRecords
            .Where(p => p.EmployeeId == request.EmployeeId);

        // Filter by year if provided
        if (request.Year.HasValue)
            query = query.Where(p => p.Year == request.Year.Value);

        // Filter by month if provided
        if (request.Month.HasValue)
            query = query.Where(p => p.Month == request.Month.Value);

        // Order by year descending, month descending
        query = query.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month);

        var payrollRecords = await query.ToListAsync(ct);

        // Load employee info for the DTO
        var employee = await _db.Employees
            .Where(e => e.Id == request.EmployeeId)
            .Select(e => new { e.FullName, e.EmployeeCode, e.Department, e.Position, e.TenantId, e.BankAccount })
            .FirstOrDefaultAsync(ct);

        if (employee is null)
        {
            _logger.LogWarning("Employee with ID '{EmployeeId}' not found for payslip query.", request.EmployeeId);
            return [];
        }

        return payrollRecords.Select(p =>
        {
            var periodStart = new DateTime(p.Year, p.Month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);

            return new PayrollDto
            {
                Id = p.Id,
                TenantId = employee.TenantId ?? string.Empty,
                PayrollNumber = $"PAY-{p.Year}{p.Month:D2}-{p.Id.ToString()[..4].ToUpper()}",
                EmployeeId = p.EmployeeId,
                EmployeeName = employee.FullName,
                EmployeeNumber = employee.EmployeeCode,
                Department = employee.Department,
                Position = employee.Position,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                PayDate = periodEnd,
                PayPeriodType = "Monthly",
                BaseSalary = p.BaseSalary.Amount,
                OvertimePay = p.OTAmount.Amount,
                Allowances = p.Allowances.Amount,
                GrossEarnings = p.GrossPay.Amount,
                IncomeTax = p.TaxDeduction.Amount,
                SocialSecurityEmployee = p.SocialSecurity.Amount,
                SocialSecurityRate = 0.05m,
                TotalDeductions = p.TaxDeduction.Amount + p.SocialSecurity.Amount + p.Deductions.Amount,
                OtherDeductions = p.Deductions.Amount,
                NetPay = p.NetPay.Amount,
                Currency = "THB",
                Status = p.Status.ToString(),
                BankAccountNumber = employee.BankAccount,
                CreatedAt = p.CreatedAt,
                EarningsBreakdown =
                [
                    new PayrollEarningItemDto { Description = "Base Salary", EarningType = "BaseSalary", Amount = p.BaseSalary.Amount },
                    new PayrollEarningItemDto { Description = "Overtime", EarningType = "Overtime", Amount = p.OTAmount.Amount },
                    new PayrollEarningItemDto { Description = "Allowances", EarningType = "Allowances", Amount = p.Allowances.Amount }
                ],
                DeductionsBreakdown =
                [
                    new PayrollDeductionItemDto { Description = "Income Tax", DeductionType = "Tax", Amount = p.TaxDeduction.Amount },
                    new PayrollDeductionItemDto { Description = "Social Security (5%)", DeductionType = "SocialSecurity", Amount = p.SocialSecurity.Amount, Rate = 0.05m },
                    new PayrollDeductionItemDto { Description = "Other Deductions", DeductionType = "Other", Amount = p.Deductions.Amount }
                ]
            };
        }).ToList();
    }
}
