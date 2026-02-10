using Microsoft.Extensions.Logging;
using TakeTime.MultiTenancy.Core;

namespace TakeTime.MultiTenancy.Services;

/// <summary>
/// Result of a tenant management operation.
/// </summary>
public class TenantOperationResult
{
    public bool Success { get; init; }
    public Tenant? Tenant { get; init; }
    public string? Error { get; init; }
    public List<string> Warnings { get; init; } = [];

    public static TenantOperationResult Ok(Tenant tenant) =>
        new() { Success = true, Tenant = tenant };

    public static TenantOperationResult Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Service for managing tenant lifecycle operations: provisioning, updating,
/// activating/deactivating, and deletion. This is the primary API for
/// administrative tenant management.
/// </summary>
public class TenantManagementService
{
    private readonly ITenantStore _tenantStore;
    private readonly ILogger<TenantManagementService> _logger;

    public TenantManagementService(
        ITenantStore tenantStore,
        ILogger<TenantManagementService> logger)
    {
        _tenantStore = tenantStore;
        _logger = logger;
    }

    /// <summary>
    /// Provisions a new tenant with the given parameters and default Thai business settings.
    /// </summary>
    public async Task<TenantOperationResult> CreateTenantAsync(
        string code,
        string name,
        string? contactEmail = null,
        string? contactPhone = null,
        string? connectionString = null,
        SubscriptionTier tier = SubscriptionTier.Free,
        string? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // Normalize the code
        code = NormalizeTenantCode(code);

        // Check for duplicate code
        var existing = await _tenantStore.GetTenantByCodeAsync(code, cancellationToken);
        if (existing is not null)
        {
            _logger.LogWarning("Attempted to create tenant with duplicate code: {Code}", code);
            return TenantOperationResult.Fail($"A tenant with code '{code}' already exists.");
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            ContactEmail = contactEmail,
            ContactPhone = contactPhone,
            DatabaseConnectionString = connectionString,
            SubscriptionTier = tier,
            SubscriptionStartDate = DateTime.UtcNow,
            BusinessSettings = TenantBusinessSettings.CreateThaiDefault(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            Country = "TH"
        };

        // Set subscription end date based on tier
        if (tier != SubscriptionTier.Free)
        {
            tenant.SubscriptionEndDate = DateTime.UtcNow.AddYears(1);
        }

        // Set limits based on tier
        SetTierLimits(tenant);

        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Provisioned new tenant: {TenantCode} ({TenantId}) with tier {Tier}",
            tenant.Code, tenant.Id, tier);

        return TenantOperationResult.Ok(tenant);
    }

    /// <summary>
    /// Updates a tenant's basic information (name, contact info, branding, address).
    /// Business settings should be updated through <see cref="TenantSettingsService"/>.
    /// </summary>
    public async Task<TenantOperationResult> UpdateTenantAsync(
        string tenantId,
        Action<Tenant> updateAction,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var tenant = await _tenantStore.GetTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");
        }

        // Apply the update
        updateAction(tenant);
        tenant.MarkUpdated(updatedBy);

        // If code was changed, validate uniqueness
        var codeCheck = await _tenantStore.GetTenantByCodeAsync(tenant.Code, cancellationToken);
        if (codeCheck is not null && codeCheck.Id != tenant.Id)
        {
            return TenantOperationResult.Fail($"A tenant with code '{tenant.Code}' already exists.");
        }

        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Updated tenant: {TenantCode} ({TenantId}) by {UpdatedBy}",
            tenant.Code, tenant.Id, updatedBy);

        return TenantOperationResult.Ok(tenant);
    }

    /// <summary>
    /// Activates a previously deactivated tenant.
    /// </summary>
    public async Task<TenantOperationResult> ActivateTenantAsync(
        string tenantId,
        string? activatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantStore.GetTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");
        }

        if (tenant.IsActive)
        {
            var result = TenantOperationResult.Ok(tenant);
            result.Warnings.Add("Tenant is already active.");
            return result;
        }

        tenant.Activate(activatedBy);
        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Activated tenant: {TenantCode} ({TenantId}) by {ActivatedBy}",
            tenant.Code, tenant.Id, activatedBy);

        return TenantOperationResult.Ok(tenant);
    }

    /// <summary>
    /// Deactivates a tenant. Deactivated tenants cannot be resolved by the
    /// middleware, effectively disabling all access.
    /// </summary>
    public async Task<TenantOperationResult> DeactivateTenantAsync(
        string tenantId,
        string? deactivatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantStore.GetTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");
        }

        if (!tenant.IsActive)
        {
            var result = TenantOperationResult.Ok(tenant);
            result.Warnings.Add("Tenant is already inactive.");
            return result;
        }

        tenant.Deactivate(deactivatedBy);
        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogWarning(
            "Deactivated tenant: {TenantCode} ({TenantId}) by {DeactivatedBy}",
            tenant.Code, tenant.Id, deactivatedBy);

        return TenantOperationResult.Ok(tenant);
    }

    /// <summary>
    /// Deletes a tenant permanently. This is a destructive operation and should
    /// be used with extreme caution. Consider deactivating instead.
    /// </summary>
    public async Task<TenantOperationResult> DeleteTenantAsync(
        string tenantId,
        string? deletedBy = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantStore.GetTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");
        }

        var deleted = await _tenantStore.DeleteTenantAsync(tenantId, cancellationToken);
        if (!deleted)
        {
            return TenantOperationResult.Fail("Failed to delete tenant from the store.");
        }

        _logger.LogWarning(
            "DELETED tenant: {TenantCode} ({TenantId}) by {DeletedBy}",
            tenant.Code, tenant.Id, deletedBy);

        return TenantOperationResult.Ok(tenant);
    }

    /// <summary>
    /// Retrieves a tenant by ID or code.
    /// </summary>
    public async Task<Tenant?> GetTenantAsync(
        string tenantIdOrCode,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantStore.GetTenantAsync(tenantIdOrCode, cancellationToken);
        if (tenant is null)
        {
            tenant = await _tenantStore.GetTenantByCodeAsync(tenantIdOrCode, cancellationToken);
        }
        return tenant;
    }

    /// <summary>
    /// Retrieves all tenants.
    /// </summary>
    public async Task<IReadOnlyList<Tenant>> GetAllTenantsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _tenantStore.GetAllTenantsAsync(cancellationToken);
    }

    /// <summary>
    /// Updates the subscription tier for a tenant and adjusts limits accordingly.
    /// </summary>
    public async Task<TenantOperationResult> UpdateSubscriptionAsync(
        string tenantId,
        SubscriptionTier newTier,
        DateTime? endDate = null,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantStore.GetTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");
        }

        var oldTier = tenant.SubscriptionTier;
        tenant.SubscriptionTier = newTier;
        tenant.SubscriptionStartDate = DateTime.UtcNow;
        tenant.SubscriptionEndDate = endDate ?? (newTier == SubscriptionTier.Free ? null : DateTime.UtcNow.AddYears(1));

        SetTierLimits(tenant);
        tenant.MarkUpdated(updatedBy);

        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Updated subscription for tenant {TenantCode}: {OldTier} -> {NewTier}",
            tenant.Code, oldTier, newTier);

        return TenantOperationResult.Ok(tenant);
    }

    /// <summary>
    /// Normalizes a tenant code to lowercase with hyphens instead of spaces/underscores.
    /// </summary>
    private static string NormalizeTenantCode(string code)
    {
        return code
            .Trim()
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('_', '-');
    }

    /// <summary>
    /// Sets room and user limits based on subscription tier.
    /// </summary>
    private static void SetTierLimits(Tenant tenant)
    {
        (tenant.MaxRooms, tenant.MaxUsers) = tenant.SubscriptionTier switch
        {
            SubscriptionTier.Free => (5, 2),
            SubscriptionTier.Starter => (20, 5),
            SubscriptionTier.Professional => (100, 20),
            SubscriptionTier.Enterprise => (int.MaxValue, int.MaxValue),
            SubscriptionTier.Custom => (tenant.MaxRooms, tenant.MaxUsers), // Keep existing
            _ => (10, 5)
        };
    }
}
