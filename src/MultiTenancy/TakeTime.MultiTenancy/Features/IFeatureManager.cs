namespace TakeTime.MultiTenancy.Features;

/// <summary>
/// Service for checking feature availability for the current tenant.
/// Used by controllers, middleware, and application services to gate
/// functionality based on per-tenant configuration.
/// </summary>
public interface IFeatureManager
{
    /// <summary>Checks if a module is enabled for the current tenant.</summary>
    Task<bool> IsModuleEnabledAsync(FeatureModule module, CancellationToken ct = default);

    /// <summary>Checks if a sub-feature within a module is enabled.</summary>
    Task<bool> IsSubFeatureEnabledAsync(FeatureModule module, string subFeature, CancellationToken ct = default);

    /// <summary>Checks if a menu item is visible for the current tenant.</summary>
    Task<bool> IsMenuVisibleAsync(FeatureModule module, string menuKey, CancellationToken ct = default);

    /// <summary>Gets all visible menu items for the current tenant and user role.</summary>
    Task<IReadOnlyList<MenuItemConfiguration>> GetVisibleMenuItemsAsync(string? userRole = null, CancellationToken ct = default);

    /// <summary>Gets the full feature configuration for the current tenant.</summary>
    Task<TenantFeatureConfiguration?> GetFeatureConfigurationAsync(CancellationToken ct = default);

    /// <summary>Gets all enabled modules for the current tenant.</summary>
    Task<IReadOnlyList<ModuleConfiguration>> GetEnabledModulesAsync(CancellationToken ct = default);
}
