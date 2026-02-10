namespace TakeTime.Core.Exceptions;

/// <summary>
/// Exception thrown for tenant-related errors such as missing tenant context,
/// invalid tenant configuration, or tenant isolation violations.
/// </summary>
public class TenantException : DomainException
{
    /// <summary>
    /// The tenant identifier involved in the error, if available.
    /// </summary>
    public string? TenantId { get; }

    public TenantException(string message)
        : base(message)
    {
    }

    public TenantException(string message, string tenantId)
        : base(message)
    {
        TenantId = tenantId;
    }

    public TenantException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public TenantException(string message, string tenantId, Exception innerException)
        : base(message, innerException)
    {
        TenantId = tenantId;
    }

    /// <summary>
    /// Creates an exception for when no tenant context has been established.
    /// </summary>
    public static TenantException NoTenantContext()
    {
        return new TenantException(
            "No tenant context has been established for the current request. " +
            "Ensure the tenant resolution middleware has executed.");
    }

    /// <summary>
    /// Creates an exception for when a tenant cannot be resolved.
    /// </summary>
    public static TenantException TenantNotFound(string identifier)
    {
        return new TenantException(
            $"Tenant with identifier '{identifier}' could not be found or is inactive.",
            identifier);
    }

    /// <summary>
    /// Creates an exception for cross-tenant data access attempts.
    /// </summary>
    public static TenantException CrossTenantAccess(string currentTenantId, string targetTenantId)
    {
        return new TenantException(
            $"Cross-tenant data access denied. Current tenant: '{currentTenantId}', " +
            $"target tenant: '{targetTenantId}'. Tenant data isolation has been enforced.",
            currentTenantId);
    }
}
