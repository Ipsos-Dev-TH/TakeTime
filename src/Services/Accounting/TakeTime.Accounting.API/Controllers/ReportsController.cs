using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Accounting.Application.Commands;
using TakeTime.Accounting.Application.DTOs;
using TakeTime.Accounting.Application.Queries;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Accounting.API.Controllers;

/// <summary>
/// API controller for financial reports, including daily revenue, profit and loss,
/// occupancy metrics, and key performance indicators.
/// </summary>
[ApiController]
[Route("api/v1/accounting")]
[Authorize]
[Produces("application/json")]
public sealed class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentTenantService _currentTenantService;

    public ReportsController(IMediator mediator, ICurrentTenantService currentTenantService)
    {
        _mediator = mediator;
        _currentTenantService = currentTenantService;
    }

    /// <summary>
    /// Retrieves the daily revenue report for a specified date.
    /// </summary>
    /// <param name="date">The date for the revenue report (defaults to today).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Daily revenue breakdown by category.</returns>
    /// <response code="200">Revenue report retrieved successfully.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("daily-revenue")]
    [ProducesResponseType(typeof(DailyRevenueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DailyRevenueDto>> GetDailyRevenue(
        [FromQuery] DateTime? date,
        CancellationToken cancellationToken)
    {
        var query = new GetDailyRevenueQuery
        {
            Date = date ?? DateTime.UtcNow.Date
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves the profit and loss statement for a specified date range.
    /// </summary>
    /// <param name="startDate">Start date of the P&L period.</param>
    /// <param name="endDate">End date of the P&L period.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Profit and loss statement with revenue and expense breakdowns.</returns>
    /// <response code="200">P&L report retrieved successfully.</response>
    /// <response code="400">Invalid date range.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("profit-loss")]
    [ProducesResponseType(typeof(ProfitLossDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfitLossDto>> GetProfitLoss(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        if (endDate < startDate)
            return BadRequest("End date must be on or after start date.");

        var query = new GetProfitLossQuery
        {
            StartDate = startDate,
            EndDate = endDate
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves the occupancy report for a specified date range.
    /// </summary>
    /// <param name="startDate">Start date of the occupancy period.</param>
    /// <param name="endDate">End date of the occupancy period.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Occupancy metrics including ADR, RevPAR, and daily breakdown.</returns>
    /// <response code="200">Occupancy report retrieved successfully.</response>
    /// <response code="400">Invalid date range.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("occupancy")]
    [ProducesResponseType(typeof(OccupancyReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OccupancyReportDto>> GetOccupancy(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        if (endDate < startDate)
            return BadRequest("End date must be on or after start date.");

        var query = new GetOccupancyReportQuery
        {
            StartDate = startDate,
            EndDate = endDate
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves key performance indicators for the current tenant.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>KPI dashboard with current operational metrics.</returns>
    /// <response code="200">KPIs retrieved successfully.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("kpis")]
    [ProducesResponseType(typeof(KpiDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<KpiDashboardDto>> GetKpis(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var yearStart = new DateTime(today.Year, 1, 1);

        // Get today's revenue
        var dailyQuery = new GetDailyRevenueQuery { Date = today };
        var dailyRevenue = await _mediator.Send(dailyQuery, cancellationToken);

        // Get month-to-date P&L for revenue
        var mtdQuery = new GetProfitLossQuery { StartDate = monthStart, EndDate = today };
        var mtdPnl = await _mediator.Send(mtdQuery, cancellationToken);

        // Get year-to-date P&L for revenue
        var ytdQuery = new GetProfitLossQuery { StartDate = yearStart, EndDate = today };
        var ytdPnl = await _mediator.Send(ytdQuery, cancellationToken);

        // Get occupancy metrics for the current month
        var monthOccupancy = new GetOccupancyReportQuery { StartDate = monthStart, EndDate = today };
        var occupancy = await _mediator.Send(monthOccupancy, cancellationToken);

        // Get today's occupancy
        var todayOccupancy = occupancy.DailyBreakdown
            .FirstOrDefault(d => d.Date == today);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        return Ok(new KpiDashboardDto
        {
            AsOfDate = today,
            TenantId = tenantId,
            TodayRevenue = dailyRevenue.TotalRevenue,
            MonthToDateRevenue = mtdPnl.TotalRevenue,
            YearToDateRevenue = ytdPnl.TotalRevenue,
            CurrentOccupancyPercent = todayOccupancy?.OccupancyRatePercent ?? 0,
            MonthAverageOccupancyPercent = occupancy.OccupancyRatePercent,
            AverageDailyRate = occupancy.AverageDailyRate,
            RevPAR = occupancy.RevPAR,
            TotalReservationsThisMonth = 0,
            CancellationsThisMonth = 0,
            CancellationRatePercent = 0,
            AverageStayNights = occupancy.AverageStayDuration,
            Currency = currency
        });
    }

    // ─── Additional Report Endpoints ─────────────────────────────────

    /// <summary>
    /// Gets monthly income/expense summary.
    /// </summary>
    /// <param name="year">Year for the summary (defaults to current year).</param>
    /// <param name="month">Month for the summary (1-12, defaults to current month).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Monthly income/expense summary with category breakdowns.</returns>
    [HttpGet("monthly-summary")]
    [ProducesResponseType(typeof(MonthlySummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlySummaryDto>> GetMonthlySummary(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var query = new GetMonthlySummaryQuery
        {
            Year = year ?? now.Year,
            Month = month ?? now.Month
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets revenue breakdown by room type for a date range.
    /// </summary>
    /// <param name="startDate">Start date for the report.</param>
    /// <param name="endDate">End date for the report.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Revenue breakdown by room type.</returns>
    [HttpGet("revenue-by-room-type")]
    [ProducesResponseType(typeof(RevenueByRoomTypeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RevenueByRoomTypeDto>> GetRevenueByRoomType(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        if (endDate < startDate)
            return BadRequest("End date must be on or after start date.");

        var query = new GetRevenueByRoomTypeQuery
        {
            StartDate = startDate,
            EndDate = endDate
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets VAT/tax report for tax filing purposes.
    /// </summary>
    /// <param name="month">Month for the tax period.</param>
    /// <param name="year">Year for the tax period.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tax report with VAT calculations.</returns>
    [HttpGet("tax-report")]
    [ProducesResponseType(typeof(TaxReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaxReportDto>> GetTaxReport(
        [FromQuery] int month,
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var query = new GetTaxReportQuery
        {
            StartDate = startDate,
            EndDate = endDate
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Exports a report in the specified format (CSV).
    /// </summary>
    /// <param name="reportType">Type of report to export (daily-revenue, profit-loss, transactions).</param>
    /// <param name="format">Export format (csv). Defaults to csv.</param>
    /// <param name="startDate">Start date for the report.</param>
    /// <param name="endDate">End date for the report.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exported report file.</returns>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(
        [FromQuery] string reportType,
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var command = new ReportExportCommand
        {
            ReportType = reportType,
            StartDate = startDate ?? new DateTime(now.Year, now.Month, 1),
            EndDate = endDate ?? now.Date,
            Format = format
        };

        var result = await _mediator.Send(command, cancellationToken);
        return File(result.Data, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Gets financial transactions with filters and pagination.
    /// </summary>
    /// <param name="startDate">Filter by start date.</param>
    /// <param name="endDate">Filter by end date.</param>
    /// <param name="category">Filter by transaction category.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Page size (default: 50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of financial transactions.</returns>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(TransactionListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TransactionListDto>> GetTransactions(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetTransactionsQuery
        {
            StartDate = startDate,
            EndDate = endDate,
            Category = category,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
