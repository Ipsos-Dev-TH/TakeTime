namespace TakeTime.Affiliate.Application.DTOs;

/// <summary>
/// Full affiliate profile data transfer object.
/// </summary>
public sealed class AffiliateDto
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public string AffiliateCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal CommissionRatePercent { get; set; }
    public string CommissionType { get; set; } = string.Empty;
    public decimal TotalEarnings { get; set; }
    public decimal PendingBalance { get; set; }
    public decimal PaidBalance { get; set; }
    public int TotalReferrals { get; set; }
    public int SuccessfulReferrals { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? TaxId { get; set; }
    public string Currency { get; set; } = "THB";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for registering a new affiliate partner.
/// </summary>
public sealed class RegisterAffiliateDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public decimal CommissionRatePercent { get; set; } = 10;
    public string CommissionType { get; set; } = "Percentage";
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? TaxId { get; set; }
}

/// <summary>
/// DTO representing a commission earned by an affiliate.
/// </summary>
public sealed class AffiliateCommissionDto
{
    public Guid Id { get; set; }
    public Guid AffiliateId { get; set; }
    public string AffiliateName { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public decimal ReservationAmount { get; set; }
    public decimal CommissionRatePercent { get; set; }
    public decimal CommissionAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = "THB";
    public DateTime EarnedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

/// <summary>
/// DTO representing a payout to an affiliate.
/// </summary>
public sealed class AffiliatePayoutDto
{
    public Guid Id { get; set; }
    public Guid AffiliateId { get; set; }
    public string AffiliateName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? TransactionReference { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = "THB";
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for affiliate search/filter criteria.
/// </summary>
public sealed class AffiliateSearchCriteria
{
    public string? Status { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? AffiliateCode { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// DTO for updating an affiliate's profile.
/// </summary>
public sealed class UpdateAffiliateDto
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public decimal? CommissionRatePercent { get; set; }
    public string? CommissionType { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? TaxId { get; set; }
}

/// <summary>
/// DTO for creating an affiliate payout request.
/// </summary>
public sealed class CreatePayoutDto
{
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for affiliate performance metrics.
/// </summary>
public sealed class AffiliatePerformanceDto
{
    public string TenantId { get; set; } = string.Empty;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public decimal TotalCommission { get; set; }
    public int TotalBookings { get; set; }
    public int TotalReferrals { get; set; }
    public decimal ConversionRatePercent { get; set; }
    public decimal AverageCommissionPerBooking { get; set; }
    public string Currency { get; set; } = "THB";
}
