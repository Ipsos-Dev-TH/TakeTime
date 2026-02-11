using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TakeTime.Reservation.API.Controllers;

/// <summary>
/// Rate and pricing management for accommodations.
/// Manages base rates, seasonal rates, promotions, and discount codes.
/// </summary>
[ApiController]
[Route("api/v1/rates")]
[Authorize]
public class RatesController : ControllerBase
{
    /// <summary>Gets all rate plans for the tenant.</summary>
    [HttpGet("plans")]
    public IActionResult GetRatePlans()
    {
        // TODO: Implement GetRatePlansQuery
        return StatusCode(501, new { message = "Rate plans query not yet implemented." });
    }

    /// <summary>Gets a rate plan by ID.</summary>
    [HttpGet("plans/{id:guid}")]
    public IActionResult GetRatePlan(Guid id)
    {
        return StatusCode(501, new { message = "Get rate plan not yet implemented." });
    }

    /// <summary>Creates a new rate plan.</summary>
    [HttpPost("plans")]
    public IActionResult CreateRatePlan([FromBody] CreateRatePlanRequest request)
    {
        // TODO: Implement CreateRatePlanCommand
        return StatusCode(501, new { message = "Create rate plan not yet implemented." });
    }

    /// <summary>Updates a rate plan.</summary>
    [HttpPut("plans/{id:guid}")]
    public IActionResult UpdateRatePlan(Guid id, [FromBody] UpdateRatePlanRequest request)
    {
        return StatusCode(501, new { message = "Update rate plan not yet implemented." });
    }

    /// <summary>Deletes a rate plan.</summary>
    [HttpDelete("plans/{id:guid}")]
    public IActionResult DeleteRatePlan(Guid id)
    {
        return StatusCode(501, new { message = "Delete rate plan not yet implemented." });
    }

    // ─── Seasonal Rates ──────────────────────────────────────────────

    /// <summary>Gets seasonal rates for the tenant.</summary>
    [HttpGet("seasonal")]
    public IActionResult GetSeasonalRates([FromQuery] int? year)
    {
        return StatusCode(501, new { message = "Get seasonal rates not yet implemented." });
    }

    /// <summary>Creates a new seasonal rate.</summary>
    [HttpPost("seasonal")]
    public IActionResult CreateSeasonalRate([FromBody] CreateSeasonalRateRequest request)
    {
        return StatusCode(501, new { message = "Create seasonal rate not yet implemented." });
    }

    /// <summary>Updates a seasonal rate.</summary>
    [HttpPut("seasonal/{id:guid}")]
    public IActionResult UpdateSeasonalRate(Guid id, [FromBody] UpdateSeasonalRateRequest request)
    {
        return StatusCode(501, new { message = "Update seasonal rate not yet implemented." });
    }

    /// <summary>Deletes a seasonal rate.</summary>
    [HttpDelete("seasonal/{id:guid}")]
    public IActionResult DeleteSeasonalRate(Guid id)
    {
        return StatusCode(501, new { message = "Delete seasonal rate not yet implemented." });
    }

    // ─── Promotions / Discount Codes ─────────────────────────────────

    /// <summary>Gets all promotions/discount codes.</summary>
    [HttpGet("promotions")]
    public IActionResult GetPromotions([FromQuery] bool? activeOnly)
    {
        return StatusCode(501, new { message = "Get promotions not yet implemented." });
    }

    /// <summary>Gets a promotion by ID.</summary>
    [HttpGet("promotions/{id:guid}")]
    public IActionResult GetPromotion(Guid id)
    {
        return StatusCode(501, new { message = "Get promotion not yet implemented." });
    }

    /// <summary>Creates a new promotion/discount code.</summary>
    [HttpPost("promotions")]
    public IActionResult CreatePromotion([FromBody] CreatePromotionRequest request)
    {
        return StatusCode(501, new { message = "Create promotion not yet implemented." });
    }

    /// <summary>Updates a promotion.</summary>
    [HttpPut("promotions/{id:guid}")]
    public IActionResult UpdatePromotion(Guid id, [FromBody] UpdatePromotionRequest request)
    {
        return StatusCode(501, new { message = "Update promotion not yet implemented." });
    }

    /// <summary>Deactivates a promotion.</summary>
    [HttpPost("promotions/{id:guid}/deactivate")]
    public IActionResult DeactivatePromotion(Guid id)
    {
        return StatusCode(501, new { message = "Deactivate promotion not yet implemented." });
    }

    /// <summary>Validates a discount code for a given date range.</summary>
    [HttpPost("promotions/validate")]
    public IActionResult ValidateDiscountCode([FromBody] ValidateDiscountRequest request)
    {
        return StatusCode(501, new { message = "Validate discount code not yet implemented." });
    }

    // ─── Bulk Rate Update ────────────────────────────────────────────

    /// <summary>Bulk update rates for multiple accommodations.</summary>
    [HttpPut("bulk-update")]
    public IActionResult BulkUpdateRates([FromBody] BulkRateUpdateRequest request)
    {
        return StatusCode(501, new { message = "Bulk rate update not yet implemented." });
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
