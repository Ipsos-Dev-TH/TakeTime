using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.HumanResources.Application.DTOs;
using TakeTime.HumanResources.Application.Queries;

namespace TakeTime.HumanResources.API.Controllers;

/// <summary>
/// REST API controller for managing employees.
/// Provides CRUD operations, leave balance inquiries, and payslip history.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class EmployeesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<EmployeesController> _logger;

    public EmployeesController(IMediator mediator, ILogger<EmployeesController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Search and list employees with optional filtering by keyword, department, and status.
    /// </summary>
    /// <param name="keyword">Search keyword matching name, employee number, or email.</param>
    /// <param name="department">Filter by department name.</param>
    /// <param name="status">Filter by employment status (Active, OnLeave, Suspended, Terminated).</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Page size (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of employees matching the search criteria.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EmployeeDto>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] string? department,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Searching employees. Keyword: {Keyword}, Department: {Department}, Status: {Status}, Page: {Page}",
            keyword, department, status, page);

        var result = await _mediator.Send(new GetEmployeesQuery
        {
            Keyword = keyword,
            Department = department,
            Status = status,
            Page = page,
            PageSize = pageSize
        }, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Get a specific employee by ID.
    /// </summary>
    /// <param name="id">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The employee details.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        // TODO: Implement GetEmployeeByIdQuery when available
        // Should return full employee details including compensation, leave balances,
        // social security info, and manager information
        _logger.LogDebug("Getting employee {EmployeeId}", id);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Get employee by ID is not yet implemented. Awaiting GetEmployeeByIdQuery."
        });
    }

    /// <summary>
    /// Create a new employee record.
    /// </summary>
    /// <param name="request">The employee creation data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created employee.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        // TODO: Implement CreateEmployeeCommand when available
        // Should auto-generate employee number, validate national ID uniqueness,
        // set default leave balances based on Thai labor law, and initialize
        // social security settings based on tenant HR configuration
        _logger.LogInformation("Creating employee: {FirstName} {LastName}, Department: {Department}",
            request.FirstName, request.LastName, request.Department);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Create employee is not yet implemented. Awaiting CreateEmployeeCommand."
        });
    }

    /// <summary>
    /// Update an existing employee record.
    /// </summary>
    /// <param name="id">The unique identifier of the employee to update.</param>
    /// <param name="request">The employee update data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated employee.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        // TODO: Implement UpdateEmployeeCommand when available
        // Should support partial updates, recalculate leave balances on status change,
        // and track employment history changes
        _logger.LogInformation("Updating employee {EmployeeId}", id);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Update employee is not yet implemented. Awaiting UpdateEmployeeCommand."
        });
    }

    /// <summary>
    /// Delete (deactivate) an employee record.
    /// </summary>
    /// <param name="id">The unique identifier of the employee to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        // TODO: Implement DeleteEmployeeCommand (soft-delete) when available
        // Should set EmploymentStatus to Terminated, record termination date,
        // and process final payroll calculations
        _logger.LogInformation("Deleting employee {EmployeeId}", id);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Delete employee is not yet implemented. Awaiting DeleteEmployeeCommand."
        });
    }

    /// <summary>
    /// Get the leave balance for a specific employee, including annual, sick, and personal leave.
    /// </summary>
    /// <param name="id">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The employee's current leave balances.</returns>
    [HttpGet("{id:guid}/leave-balance")]
    [ProducesResponseType(typeof(LeaveBalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> GetLeaveBalance(Guid id, CancellationToken cancellationToken)
    {
        // TODO: Implement GetEmployeeLeaveBalanceQuery when available
        // Should return annual, sick, personal leave balances and usage history
        // Should calculate remaining days based on Thai labor law entitlements
        _logger.LogDebug("Getting leave balance for employee {EmployeeId}", id);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Get leave balance is not yet implemented. Awaiting GetEmployeeLeaveBalanceQuery."
        });
    }

    /// <summary>
    /// Get the payslip history for a specific employee.
    /// </summary>
    /// <param name="id">The unique identifier of the employee.</param>
    /// <param name="year">Optional year filter.</param>
    /// <param name="month">Optional month filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of payslips for the employee.</returns>
    [HttpGet("{id:guid}/payslips")]
    [ProducesResponseType(typeof(List<PayslipResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> GetPayslips(
        Guid id,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        // TODO: Implement GetEmployeePayslipsQuery when available
        // Should return payslip history with salary breakdown, deductions,
        // OT calculations, social security contributions, and withholding tax
        _logger.LogDebug("Getting payslips for employee {EmployeeId}, Year: {Year}, Month: {Month}",
            id, year, month);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Get payslips is not yet implemented. Awaiting GetEmployeePayslipsQuery."
        });
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────

/// <summary>
/// Request model for creating a new employee.
/// </summary>
public sealed class CreateEmployeeRequest
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

/// <summary>
/// Request model for updating an existing employee.
/// </summary>
public sealed class UpdateEmployeeRequest
{
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

/// <summary>
/// Response model for employee leave balances.
/// </summary>
public sealed class LeaveBalanceResponse
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public decimal AnnualLeaveBalance { get; set; }
    public decimal AnnualLeaveUsed { get; set; }
    public decimal AnnualLeaveTotal { get; set; }
    public decimal SickLeaveBalance { get; set; }
    public decimal SickLeaveUsed { get; set; }
    public decimal SickLeaveTotal { get; set; }
    public decimal PersonalLeaveBalance { get; set; }
    public decimal PersonalLeaveUsed { get; set; }
    public decimal PersonalLeaveTotal { get; set; }
    public DateTime AsOfDate { get; set; }
}

/// <summary>
/// Response model for an employee payslip.
/// </summary>
public sealed class PayslipResponse
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal Allowances { get; set; }
    public decimal GrossIncome { get; set; }
    public decimal SocialSecurityDeduction { get; set; }
    public decimal WithholdingTax { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal NetIncome { get; set; }
    public string Currency { get; set; } = "THB";
    public DateTime PayDate { get; set; }
}
