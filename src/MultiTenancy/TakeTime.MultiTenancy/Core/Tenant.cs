namespace TakeTime.MultiTenancy.Core;

/// <summary>
/// Subscription plan tier that determines feature availability and usage limits.
/// </summary>
public enum SubscriptionTier
{
    /// <summary>Free trial or basic tier with limited features.</summary>
    Free = 0,

    /// <summary>Starter plan for small properties.</summary>
    Starter = 1,

    /// <summary>Professional plan for established businesses.</summary>
    Professional = 2,

    /// <summary>Enterprise plan with full features and support.</summary>
    Enterprise = 3,

    /// <summary>Custom/negotiated plan.</summary>
    Custom = 99
}

/// <summary>
/// Represents a tenant (business/property) in the TakeTime multi-tenant platform.
/// Each tenant has its own business configuration, branding, and optionally its
/// own database. This is the aggregate root of the multi-tenancy bounded context.
/// </summary>
public class Tenant
{
    /// <summary>
    /// Unique identifier for the tenant.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Short, URL-friendly code that uniquely identifies the tenant
    /// (e.g., "taketime-bangphra", "sunrise-pattaya"). Used in subdomain
    /// resolution and API routing.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the business (e.g., "TakeTime Bangphra Resort").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Connection string for the tenant's dedicated database.
    /// Null when using a shared database with row-level tenant filtering.
    /// </summary>
    public string? DatabaseConnectionString { get; set; }

    /// <summary>
    /// All configurable business rules and operational settings.
    /// </summary>
    public TenantBusinessSettings BusinessSettings { get; set; } = new();

    /// <summary>
    /// Whether this tenant is currently active. Inactive tenants cannot
    /// be resolved by the middleware and their users cannot log in.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// UTC timestamp when the tenant record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp of the last update to the tenant record.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // ─── Branding ────────────────────────────────────────────────────

    /// <summary>
    /// URL to the tenant's logo image.
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// URL to the tenant's favicon.
    /// </summary>
    public string? FaviconUrl { get; set; }

    /// <summary>
    /// Primary brand color in hex format (e.g., "#1E40AF").
    /// </summary>
    public string? PrimaryColor { get; set; }

    /// <summary>
    /// Secondary/accent brand color.
    /// </summary>
    public string? SecondaryColor { get; set; }

    /// <summary>
    /// Custom domain for the tenant (e.g., "booking.sunriseresort.com").
    /// </summary>
    public string? CustomDomain { get; set; }

    // ─── Contact Information ─────────────────────────────────────────

    /// <summary>
    /// Primary contact email address.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Primary contact phone number.
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// Website URL.
    /// </summary>
    public string? WebsiteUrl { get; set; }

    // ─── Address ─────────────────────────────────────────────────────

    /// <summary>
    /// Street address line 1.
    /// </summary>
    public string? AddressLine1 { get; set; }

    /// <summary>
    /// Street address line 2.
    /// </summary>
    public string? AddressLine2 { get; set; }

    /// <summary>
    /// City or district.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// State, province, or region.
    /// </summary>
    public string? StateProvince { get; set; }

    /// <summary>
    /// Postal or ZIP code.
    /// </summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country code (e.g., "TH", "US").
    /// </summary>
    public string Country { get; set; } = "TH";

    /// <summary>
    /// GPS latitude for mapping.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// GPS longitude for mapping.
    /// </summary>
    public double? Longitude { get; set; }

    // ─── Subscription ────────────────────────────────────────────────

    /// <summary>
    /// Current subscription tier.
    /// </summary>
    public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Free;

    /// <summary>
    /// When the current subscription period started.
    /// </summary>
    public DateTime? SubscriptionStartDate { get; set; }

    /// <summary>
    /// When the current subscription period ends. Null for lifetime/free.
    /// </summary>
    public DateTime? SubscriptionEndDate { get; set; }

    /// <summary>
    /// Maximum number of rooms/units allowed under the current plan.
    /// </summary>
    public int MaxRooms { get; set; } = 10;

    /// <summary>
    /// Maximum number of staff/user accounts allowed.
    /// </summary>
    public int MaxUsers { get; set; } = 5;

    // ─── Metadata ────────────────────────────────────────────────────

    /// <summary>
    /// Identifier of the user who created this tenant record.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Identifier of the user who last updated this tenant record.
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Optional notes or description about this tenant.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Arbitrary key-value metadata for extensibility.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = [];

    // ─── Behavior ────────────────────────────────────────────────────

    /// <summary>
    /// Checks whether the tenant's subscription is still valid.
    /// </summary>
    public bool IsSubscriptionActive()
    {
        if (SubscriptionTier == SubscriptionTier.Free)
            return true;

        if (SubscriptionEndDate is null)
            return true;

        return SubscriptionEndDate.Value > DateTime.UtcNow;
    }

    /// <summary>
    /// Returns true if the tenant is fully operational
    /// (active, subscription valid).
    /// </summary>
    public bool IsOperational()
    {
        return IsActive && IsSubscriptionActive();
    }

    /// <summary>
    /// Marks the tenant as updated with the current UTC timestamp.
    /// </summary>
    public void MarkUpdated(string? updatedBy = null)
    {
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>
    /// Deactivates the tenant. Deactivated tenants cannot be resolved.
    /// </summary>
    public void Deactivate(string? deactivatedBy = null)
    {
        IsActive = false;
        MarkUpdated(deactivatedBy);
    }

    /// <summary>
    /// Reactivates a previously deactivated tenant.
    /// </summary>
    public void Activate(string? activatedBy = null)
    {
        IsActive = true;
        MarkUpdated(activatedBy);
    }
}
