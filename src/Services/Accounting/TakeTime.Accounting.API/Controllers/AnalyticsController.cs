using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Accounting.Application.DTOs;
using TakeTime.Accounting.Application.Queries;

namespace TakeTime.Accounting.API.Controllers;

/// <summary>
/// API controller for analytics and business intelligence dashboards.
/// Provides occupancy overview, revenue/occupancy trends, guest demographics,
/// and booking channel performance analysis.
/// </summary>
[ApiController]
[Route("api/v1/analytics")]
[Authorize]
[Produces("application/json")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(IMediator mediator, ILogger<AnalyticsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get the analytics dashboard overview including occupancy, revenue,
    /// and booking metrics for the current date (or a specified date).
    /// </summary>
    /// <param name="date">Optional date for the overview (defaults to today).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analytics overview with key metrics.</returns>
    /// <response code="200">Overview retrieved successfully.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(AnalyticsOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AnalyticsOverviewDto>> GetOverview(
        [FromQuery] DateTime? date,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching analytics overview for date: {Date}", date?.ToString("yyyy-MM-dd") ?? "today");

        var query = new GetAnalyticsOverviewQuery
        {
            Date = date
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get revenue and occupancy trends over a specified time period.
    /// Supports daily, weekly, and monthly aggregation periods.
    /// </summary>
    /// <param name="startDate">Start date of the trend period.</param>
    /// <param name="endDate">End date of the trend period.</param>
    /// <param name="period">Aggregation period: "daily", "weekly", or "monthly" (default: "daily").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Revenue and occupancy trend data points.</returns>
    /// <response code="200">Trends retrieved successfully.</response>
    /// <response code="400">Invalid date range.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("trends")]
    [ProducesResponseType(typeof(TrendsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TrendsDto>> GetTrends(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string period = "daily",
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
        {
            return BadRequest(new { message = "End date must be on or after start date." });
        }

        _logger.LogDebug(
            "Fetching {Period} trends from {StartDate} to {EndDate}",
            period, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

        var query = new GetRevenueTrendsQuery
        {
            StartDate = startDate,
            EndDate = endDate,
            Period = period
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get guest demographics breakdown by booking source, nationality,
    /// and stay duration for a specified date range.
    /// </summary>
    /// <param name="startDate">Start date of the analysis period.</param>
    /// <param name="endDate">End date of the analysis period.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Guest demographics breakdown.</returns>
    /// <response code="200">Demographics retrieved successfully.</response>
    /// <response code="400">Invalid date range.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("guest-demographics")]
    [ProducesResponseType(typeof(GuestDemographicsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GuestDemographicsDto>> GetGuestDemographics(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
        {
            return BadRequest(new { message = "End date must be on or after start date." });
        }

        _logger.LogDebug(
            "Fetching guest demographics from {StartDate} to {EndDate}",
            startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

        var query = new GetGuestDemographicsQuery
        {
            StartDate = startDate,
            EndDate = endDate
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get revenue performance breakdown by booking channel for a specified date range.
    /// Shows which channels generate the most revenue and bookings.
    /// </summary>
    /// <param name="startDate">Start date of the analysis period.</param>
    /// <param name="endDate">End date of the analysis period.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Channel performance breakdown.</returns>
    /// <response code="200">Channel performance retrieved successfully.</response>
    /// <response code="400">Invalid date range.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("channel-performance")]
    [ProducesResponseType(typeof(ChannelPerformanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ChannelPerformanceDto>> GetChannelPerformance(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
        {
            return BadRequest(new { message = "End date must be on or after start date." });
        }

        _logger.LogDebug(
            "Fetching channel performance from {StartDate} to {EndDate}",
            startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

        var query = new GetChannelPerformanceQuery
        {
            StartDate = startDate,
            EndDate = endDate
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
