namespace TakeTime.MultiTenancy.Features;

/// <summary>
/// Root configuration that holds all module and menu configurations for a tenant.
/// This is stored as part of <see cref="TakeTime.MultiTenancy.Core.TenantBusinessSettings"/>
/// and provides granular control over which modules, sub-features, and menu items
/// are available for each tenant.
///
/// Usage examples:
/// - Small guesthouse: Enable only Reservation, Payment, Notification
/// - Full resort: Enable all modules
/// - No VAT business: Disable TaxInvoice sub-feature in Payment
/// - No restaurant: Disable RoomService sub-feature in GuestExperience
/// </summary>
public class TenantFeatureConfiguration
{
    /// <summary>
    /// Per-module configurations indexed by <see cref="FeatureModule"/>.
    /// </summary>
    public Dictionary<FeatureModule, ModuleConfiguration> Modules { get; set; } = [];

    /// <summary>
    /// Custom menu items that the tenant has added beyond the standard set.
    /// Key = custom menu key, Value = configuration.
    /// </summary>
    public List<MenuItemConfiguration> CustomMenuItems { get; set; } = [];

    /// <summary>Checks whether a specific module is enabled for this tenant.</summary>
    public bool IsModuleEnabled(FeatureModule module)
    {
        return Modules.TryGetValue(module, out var config) ? config.IsEnabled : false;
    }

    /// <summary>Checks whether a sub-feature within a module is enabled.</summary>
    public bool IsSubFeatureEnabled(FeatureModule module, string subFeature)
    {
        if (!Modules.TryGetValue(module, out var config)) return false;
        return config.IsSubFeatureEnabled(subFeature);
    }

    /// <summary>Checks whether a menu item is visible for this tenant.</summary>
    public bool IsMenuVisible(FeatureModule module, string menuKey)
    {
        if (!Modules.TryGetValue(module, out var config)) return false;
        return config.IsMenuVisible(menuKey);
    }

    /// <summary>
    /// Gets all visible menu items across all enabled modules, sorted by module
    /// and then by sort order within each module.
    /// </summary>
    public IReadOnlyList<MenuItemConfiguration> GetVisibleMenuItems(string? userRole = null)
    {
        var items = new List<MenuItemConfiguration>();

        foreach (var (module, config) in Modules.Where(m => m.Value.IsEnabled))
        {
            foreach (var menuItem in config.MenuItems
                .Where(m => m.IsVisible)
                .OrderBy(m => m.SortOrder))
            {
                // Check role requirement
                if (menuItem.RequiredRole is not null && userRole is not null
                    && !menuItem.RequiredRole.Equals(userRole, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Check sub-feature requirement
                if (menuItem.RequiredSubFeature is not null
                    && !config.IsSubFeatureEnabled(menuItem.RequiredSubFeature))
                    continue;

                items.Add(menuItem);
            }
        }

        // Add visible custom menu items
        items.AddRange(CustomMenuItems.Where(m => m.IsVisible));

        return items;
    }

    /// <summary>
    /// Gets all enabled modules with their configurations.
    /// </summary>
    public IReadOnlyList<ModuleConfiguration> GetEnabledModules()
    {
        return Modules.Values.Where(m => m.IsEnabled).ToList();
    }

    /// <summary>
    /// Enables a module. If the module doesn't have a configuration yet,
    /// a default one is created.
    /// </summary>
    public void EnableModule(FeatureModule module)
    {
        if (Modules.TryGetValue(module, out var config))
        {
            config.IsEnabled = true;
        }
        else
        {
            Modules[module] = ModuleConfiguration.CreateDefault(module, enabled: true);
        }
    }

    /// <summary>Disables a module and hides all its menu items.</summary>
    public void DisableModule(FeatureModule module)
    {
        if (Modules.TryGetValue(module, out var config))
        {
            config.IsEnabled = false;
        }
    }

    /// <summary>Toggles a sub-feature within a module.</summary>
    public void SetSubFeature(FeatureModule module, string subFeature, bool enabled)
    {
        if (!Modules.TryGetValue(module, out var config))
        {
            config = ModuleConfiguration.CreateDefault(module);
            Modules[module] = config;
        }
        config.SubFeatures[subFeature] = enabled;
    }

    /// <summary>Toggles visibility of a specific menu item.</summary>
    public void SetMenuVisibility(FeatureModule module, string menuKey, bool visible)
    {
        if (!Modules.TryGetValue(module, out var config)) return;

        var item = config.MenuItems.FirstOrDefault(m =>
            m.Key.Equals(menuKey, StringComparison.OrdinalIgnoreCase));

        if (item is not null)
        {
            item.IsVisible = visible;
        }
    }

    /// <summary>
    /// Creates a full-featured configuration with all modules enabled.
    /// Suitable for Enterprise/Professional tier tenants.
    /// </summary>
    public static TenantFeatureConfiguration CreateFullAccess()
    {
        var config = new TenantFeatureConfiguration();
        foreach (FeatureModule module in Enum.GetValues<FeatureModule>())
        {
            config.Modules[module] = ModuleConfiguration.CreateDefault(module, enabled: true);
        }
        return config;
    }

    /// <summary>
    /// Creates a starter configuration with only essential modules enabled.
    /// Suitable for small properties on the Free/Starter tier.
    /// </summary>
    public static TenantFeatureConfiguration CreateStarter()
    {
        var config = new TenantFeatureConfiguration();
        foreach (FeatureModule module in Enum.GetValues<FeatureModule>())
        {
            var isEssential = module is FeatureModule.Reservation
                or FeatureModule.Payment
                or FeatureModule.Notification
                or FeatureModule.Accounting;

            config.Modules[module] = ModuleConfiguration.CreateDefault(module, enabled: isEssential);
        }
        return config;
    }

    /// <summary>
    /// Creates a Thai hospitality default configuration.
    /// Core modules + CRM + GuestExperience enabled.
    /// </summary>
    public static TenantFeatureConfiguration CreateThaiDefault()
    {
        var config = new TenantFeatureConfiguration();
        foreach (FeatureModule module in Enum.GetValues<FeatureModule>())
        {
            var enabled = module is FeatureModule.Reservation
                or FeatureModule.Payment
                or FeatureModule.GuestExperience
                or FeatureModule.CRM
                or FeatureModule.Notification
                or FeatureModule.Accounting;

            config.Modules[module] = ModuleConfiguration.CreateDefault(module, enabled: enabled);
        }

        // Enable LINE notification by default for Thai properties
        if (config.Modules.TryGetValue(FeatureModule.Notification, out var notif))
        {
            notif.SubFeatures["LINE"] = true;
        }

        return config;
    }
}
