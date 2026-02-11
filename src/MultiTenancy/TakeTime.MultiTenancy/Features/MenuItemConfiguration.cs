namespace TakeTime.MultiTenancy.Features;

/// <summary>
/// Configuration for a single menu item within a module.
/// Allows per-tenant visibility control of individual navigation items.
/// </summary>
public class MenuItemConfiguration
{
    /// <summary>
    /// Unique key for this menu item (e.g., "reservations.calendar", "hr.payroll").
    /// Uses dot notation: "module.item" or "module.group.item".
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Thai display label for the menu item.</summary>
    public string LabelTh { get; set; } = string.Empty;

    /// <summary>English display label for the menu item.</summary>
    public string LabelEn { get; set; } = string.Empty;

    /// <summary>Icon identifier (e.g., Font Awesome class name).</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>Display order within the parent group (lower = first).</summary>
    public int SortOrder { get; set; }

    /// <summary>Whether this menu item is visible to users.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Minimum role required to see this menu item.
    /// Null means all authenticated users can see it.
    /// </summary>
    public string? RequiredRole { get; set; }

    /// <summary>
    /// Sub-feature key that must be enabled for this menu item to appear.
    /// Null means the item only depends on the module being enabled.
    /// </summary>
    public string? RequiredSubFeature { get; set; }

    public MenuItemConfiguration() { }

    public MenuItemConfiguration(
        string key,
        string labelTh,
        string labelEn,
        string icon,
        int sortOrder,
        bool isVisible = true,
        string? requiredRole = null,
        string? requiredSubFeature = null)
    {
        Key = key;
        LabelTh = labelTh;
        LabelEn = labelEn;
        Icon = icon;
        SortOrder = sortOrder;
        IsVisible = isVisible;
        RequiredRole = requiredRole;
        RequiredSubFeature = requiredSubFeature;
    }
}
