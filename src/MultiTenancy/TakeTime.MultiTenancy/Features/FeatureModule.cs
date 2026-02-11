namespace TakeTime.MultiTenancy.Features;

/// <summary>
/// All system modules that can be independently enabled or disabled per tenant.
/// Each module maps to a bounded context / microservice in the system.
/// </summary>
public enum FeatureModule
{
    /// <summary>Reservation / booking management (core module).</summary>
    Reservation = 1,

    /// <summary>Payment processing, receipts, tax invoices.</summary>
    Payment = 2,

    /// <summary>Product inventory, POS, stock management.</summary>
    Inventory = 3,

    /// <summary>Human resources, leave, payroll, overtime.</summary>
    HumanResources = 4,

    /// <summary>Customer relationship management, loyalty, reviews.</summary>
    CRM = 5,

    /// <summary>OTA channel integration management.</summary>
    ChannelManager = 6,

    /// <summary>Housekeeping, room service, maintenance.</summary>
    GuestExperience = 7,

    /// <summary>Financial reports, P&L, revenue summaries.</summary>
    Accounting = 8,

    /// <summary>Email, LINE, Telegram, SMS notifications.</summary>
    Notification = 9,

    /// <summary>Affiliate / partner referral program.</summary>
    Affiliate = 10
}
