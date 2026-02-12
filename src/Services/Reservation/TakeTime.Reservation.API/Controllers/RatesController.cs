using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Reservation.Application.Commands;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Queries;

namespace TakeTime.Reservation.API.Controllers;

/// <summary>
/// Rate and pricing management for accommodations.
/// Manages base rates, seasonal rates, promotions, and discount codes.
/// </summary>
[ApiController]
[Route("api/v1/rates")]
[Authorize]
[Produces("application/json")]
public class RatesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<RatesController> _logger;

    public RatesController(IMediator mediator, ILogger<RatesController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ─── Rate Plans ──────────────────────────────────────────────

    /// <summary>Gets all rate plans for the tenant.</summary>
    [HttpGet("plans")]
    [ProducesResponseType(typeof(List<RatePlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRatePlans(CancellationToken cancellationToken)
    {
        var query = new GetRatePlansQuery();
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Gets a rate plan by ID.</summary>
    [HttpGet("plans/{id:guid}")]
    [ProducesResponseType(typeof(RatePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRatePlan(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetRatePlanByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result is null)
            return NotFound(new { message = $"Rate plan with ID '{id}' not found." });

        return Ok(result);
    }

    /// <summary>Creates a new rate plan.</summary>
    [HttpPost("plans")]
    [ProducesResponseType(typeof(RatePlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRatePlan(
        [FromBody] CreateRatePlanRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateRatePlanCommand
        {
            Name = request.Name,
            Description = request.Description,
            BaseRate = request.BaseRate,
            Currency = request.Currency,
            IsDefault = request.IsDefault,
            CreatedBy = User.Identity?.Name
        };

        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetRatePlan), new { id = result.Id }, result);
    }

    /// <summary>Updates a rate plan.</summary>
    [HttpPut("plans/{id:guid}")]
    [ProducesResponseType(typeof(RatePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateRatePlan(
        Guid id,
        [FromBody] UpdateRatePlanRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRatePlanCommand
        {
            Id = id,
            Name = request.Name,
            Description = request.Description,
            BaseRate = request.BaseRate,
            Currency = request.Currency,
            IsActive = request.IsActive,
            UpdatedBy = User.Identity?.Name
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Deletes a rate plan.</summary>
    [HttpDelete("plans/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRatePlan(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteRatePlanCommand
        {
            Id = id,
            DeletedBy = User.Identity?.Name
        };

        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    // ─── Seasonal Rates ──────────────────────────────────────────────

    /// <summary>Gets seasonal rates for the tenant.</summary>
    [HttpGet("seasonal")]
    [ProducesResponseType(typeof(List<SeasonalRateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSeasonalRates(
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var query = new GetSeasonalRatesQuery { Year = year };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a new seasonal rate.</summary>
    [HttpPost("seasonal")]
    [ProducesResponseType(typeof(SeasonalRateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSeasonalRate(
        [FromBody] CreateSeasonalRateRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateSeasonalRateCommand
        {
            SeasonName = request.SeasonName,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RateMultiplier = request.RateMultiplier,
            Description = request.Description,
            CreatedBy = User.Identity?.Name
        };

        var result = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Updates a seasonal rate.</summary>
    [HttpPut("seasonal/{id:guid}")]
    [ProducesResponseType(typeof(SeasonalRateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSeasonalRate(
        Guid id,
        [FromBody] UpdateSeasonalRateRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSeasonalRateCommand
        {
            Id = id,
            SeasonName = request.SeasonName,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RateMultiplier = request.RateMultiplier,
            Description = request.Description,
            IsActive = request.IsActive,
            UpdatedBy = User.Identity?.Name
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Deletes a seasonal rate.</summary>
    [HttpDelete("seasonal/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSeasonalRate(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteSeasonalRateCommand
        {
            Id = id,
            DeletedBy = User.Identity?.Name
        };

        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    // ─── Promotions / Discount Codes ─────────────────────────────────

    /// <summary>Gets all promotions/discount codes.</summary>
    [HttpGet("promotions")]
    [ProducesResponseType(typeof(List<PromotionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPromotions(
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken)
    {
        var query = new GetPromotionsQuery { ActiveOnly = activeOnly };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Gets a promotion by ID.</summary>
    [HttpGet("promotions/{id:guid}")]
    [ProducesResponseType(typeof(PromotionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPromotion(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetPromotionByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result is null)
            return NotFound(new { message = $"Promotion with ID '{id}' not found." });

        return Ok(result);
    }

    /// <summary>Creates a new promotion/discount code.</summary>
    [HttpPost("promotions")]
    [ProducesResponseType(typeof(PromotionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePromotion(
        [FromBody] CreatePromotionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreatePromotionCommand
        {
            Name = request.Name,
            Code = request.Code,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            MaxUses = request.MaxUses,
            MinimumBookingAmount = request.MinimumBookingAmount,
            Description = request.Description,
            CreatedBy = User.Identity?.Name
        };

        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetPromotion), new { id = result.Id }, result);
    }

    /// <summary>Updates a promotion.</summary>
    [HttpPut("promotions/{id:guid}")]
    [ProducesResponseType(typeof(PromotionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePromotion(
        Guid id,
        [FromBody] UpdatePromotionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePromotionCommand
        {
            Id = id,
            Name = request.Name,
            Code = request.Code,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            MaxUses = request.MaxUses,
            MinimumBookingAmount = request.MinimumBookingAmount,
            IsActive = request.IsActive,
            UpdatedBy = User.Identity?.Name
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Deactivates a promotion.</summary>
    [HttpPost("promotions/{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivatePromotion(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeactivatePromotionCommand
        {
            Id = id,
            DeactivatedBy = User.Identity?.Name
        };

        await _mediator.Send(command, cancellationToken);
        return Ok(new { message = "Promotion deactivated successfully." });
    }

    /// <summary>Validates a discount code for a given date range.</summary>
    [HttpPost("promotions/validate")]
    [ProducesResponseType(typeof(DiscountValidationResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateDiscountCode(
        [FromBody] ValidateDiscountRequest request,
        CancellationToken cancellationToken)
    {
        var query = new ValidateDiscountCodeQuery
        {
            Code = request.Code,
            CheckInDate = request.CheckInDate,
            CheckOutDate = request.CheckOutDate,
            BookingAmount = request.BookingAmount
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    // ─── Bulk Rate Update ────────────────────────────────────────────

    /// <summary>Bulk update rates for multiple accommodations.</summary>
    [HttpPut("bulk-update")]
    [ProducesResponseType(typeof(BulkUpdateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkUpdateRates(
        [FromBody] BulkRateUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var command = new BulkUpdateRatesCommand
        {
            AccommodationIds = request.AccommodationIds,
            RateAdjustment = request.RateAdjustment,
            RateMultiplier = request.RateMultiplier,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            UpdatedBy = User.Identity?.Name
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────

public record CreateRatePlanRequest(
    string Name, string? Description, decimal BaseRate,
    string Currency = "THB", bool IsDefault = false);

public record UpdateRatePlanRequest(
    string? Name, string? Description, decimal? BaseRate,
    string? Currency, bool? IsActive);

public record CreateSeasonalRateRequest(
    string SeasonName, DateTime StartDate, DateTime EndDate,
    decimal RateMultiplier, string? Description);

public record UpdateSeasonalRateRequest(
    string? SeasonName, DateTime? StartDate, DateTime? EndDate,
    decimal? RateMultiplier, string? Description, bool? IsActive);

public record CreatePromotionRequest(
    string Name, string? Code, string DiscountType,
    decimal DiscountValue, DateTime? ValidFrom, DateTime? ValidTo,
    int? MaxUses, decimal? MinimumBookingAmount, string? Description);

public record UpdatePromotionRequest(
    string? Name, string? Code, string? DiscountType,
    decimal? DiscountValue, DateTime? ValidFrom, DateTime? ValidTo,
    int? MaxUses, decimal? MinimumBookingAmount, bool? IsActive);

public record ValidateDiscountRequest(
    string Code, DateTime CheckInDate, DateTime CheckOutDate,
    decimal BookingAmount);

public record BulkRateUpdateRequest(
    List<Guid> AccommodationIds, decimal? RateAdjustment,
    decimal? RateMultiplier, DateTime? EffectiveFrom, DateTime? EffectiveTo);
