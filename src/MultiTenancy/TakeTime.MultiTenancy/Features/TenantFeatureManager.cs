using TakeTime.MultiTenancy.Services;

namespace TakeTime.MultiTenancy.Features;

/// <summary>
/// Default implementation of <see cref="IFeatureManager"/> that reads feature
/// configuration from the current tenant's business settings.
/// </summary>
public class TenantFeatureManager : IFeatureManager
{
    private readonly ICurrentTenantService _currentTenant;

    public TenantFeatureManager(ICurrentTenantService currentTenant)
    {
        _currentTenant = currentTenant;
    }

    public Task<bool> IsModuleEnabledAsync(FeatureModule module, CancellationToken ct = default)
    {
        var config = GetFeatureConfig();
        return Task.FromResult(config?.IsModuleEnabled(module) ?? false);
    }

    public Task<bool> IsSubFeatureEnabledAsync(
        FeatureModule module, string subFeature, CancellationToken ct = default)
    {
        var config = GetFeatureConfig();
        return Task.FromResult(config?.IsSubFeatureEnabled(module, subFeature) ?? false);
    }

    public Task<bool> IsMenuVisibleAsync(
        FeatureModule module, string menuKey, CancellationToken ct = default)
    {
        var config = GetFeatureConfig();
        return Task.FromResult(config?.IsMenuVisible(module, menuKey) ?? false);
    }

    public Task<IReadOnlyList<MenuItemConfiguration>> GetVisibleMenuItemsAsync(
        string? userRole = null, CancellationToken ct = default)
    {
        var config = GetFeatureConfig();
        IReadOnlyList<MenuItemConfiguration> items = config?.GetVisibleMenuItems(userRole)
            ?? Array.Empty<MenuItemConfiguration>();
        return Task.FromResult(items);
    }

    public Task<TenantFeatureConfiguration?> GetFeatureConfigurationAsync(CancellationToken ct = default)
    {
        return Task.FromResult(GetFeatureConfig());
    }

    public Task<IReadOnlyList<ModuleConfiguration>> GetEnabledModulesAsync(CancellationToken ct = default)
    {
        var config = GetFeatureConfig();
        IReadOnlyList<ModuleConfiguration> modules = config?.GetEnabledModules()
            ?? Array.Empty<ModuleConfiguration>();
        return Task.FromResult(modules);
    }

    private TenantFeatureConfiguration? GetFeatureConfig()
    {
        return _currentTenant.BusinessSettings?.FeatureConfiguration;
    }
}
