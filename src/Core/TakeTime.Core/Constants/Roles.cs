namespace TakeTime.Core.Constants;

/// <summary>
/// Static constants for application roles. These role names are used in
/// authorization policies, JWT claims, and role-based access checks
/// throughout the application.
/// </summary>
public static class Roles
{
    /// <summary>
    /// System administrator with full access to all features and tenants.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Branch or department manager with operational oversight.
    /// </summary>
    public const string Manager = "Manager";

    /// <summary>
    /// Supervisor with team-level management capabilities.
    /// </summary>
    public const string Supervisor = "Supervisor";

    /// <summary>
    /// HR manager with access to employee, leave, and payroll modules.
    /// </summary>
    public const string HRManager = "HRManager";

    /// <summary>
    /// Accountant with access to financial, payment, and reporting modules.
    /// </summary>
    public const string Accountant = "Accountant";

    /// <summary>
    /// Regular staff member with standard operational access.
    /// </summary>
    public const string Staff = "Staff";

    /// <summary>
    /// External affiliate or partner with limited access.
    /// </summary>
    public const string Affiliate = "Affiliate";

    /// <summary>
    /// Guest or walk-in user with minimal, read-only access.
    /// </summary>
    public const string Guest = "Guest";

    /// <summary>
    /// Returns all defined roles as a collection.
    /// </summary>
    public static IReadOnlyCollection<string> All =>
    [
        Admin,
        Manager,
        Supervisor,
        HRManager,
        Accountant,
        Staff,
        Affiliate,
        Guest
    ];

    /// <summary>
    /// Returns roles that have management-level privileges.
    /// </summary>
    public static IReadOnlyCollection<string> ManagementRoles =>
    [
        Admin,
        Manager,
        Supervisor,
        HRManager,
        Accountant
    ];

    /// <summary>
    /// Returns roles that represent internal (non-external) users.
    /// </summary>
    public static IReadOnlyCollection<string> InternalRoles =>
    [
        Admin,
        Manager,
        Supervisor,
        HRManager,
        Accountant,
        Staff
    ];
}
