namespace TakeTime.Reservation.Application.DTOs;

/// <summary>
/// Data transfer object for rate plan information.
/// </summary>
public sealed class RatePlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BaseRate { get; set; }
    public string Currency { get; set; } = "THB";
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Data transfer object for seasonal rate information.
/// </summary>
public sealed class SeasonalRateDto
{
    public Guid Id { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal RateMultiplier { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Data transfer object for promotion/discount code information.
/// </summary>
public sealed class PromotionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int? MaxUses { get; set; }
    public int CurrentUses { get; set; }
    public decimal? MinimumBookingAmount { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Result of validating a discount code against booking parameters.
/// </summary>
public sealed class DiscountValidationResultDto
{
    public bool IsValid { get; set; }
    public string? Message { get; set; }
    public string? PromotionName { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal? CalculatedDiscount { get; set; }
}

/// <summary>
/// Result of a bulk rate update operation.
/// </summary>
public sealed class BulkUpdateResultDto
{
    public int UpdatedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
