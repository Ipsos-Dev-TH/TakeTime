using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.MultiTenancy.Features;
using TakeTime.MultiTenancy.Services;

namespace TakeTime.ApiGateway.Controllers;

/// <summary>
/// API endpoints for managing feature and menu configurations per tenant.
/// These endpoints allow administrators to enable/disable modules,
/// sub-features, and individual menu items.
/// </summary>
[ApiController]
[Route("api/v1/admin/features")]
public class FeatureManagementController : ControllerBase
{
    private readonly TenantSettingsService _settingsService;

    public FeatureManagementController(TenantSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Gets the full feature configuration for a tenant.
    /// </summary>
    [HttpGet("{tenantId}")]
    public async Task<IActionResult> GetFeatureConfiguration(
        string tenantId, CancellationToken ct)
    {
        var config = await _settingsService.GetFeatureConfigurationAsync(tenantId, ct);
        if (config is null)
            return NotFound(new { error = $"Tenant '{tenantId}' not found or has no feature configuration." });

        return Ok(config);
    }

    /// <summary>
    /// Gets all visible menu items for a tenant based on their current configuration.
    /// </summary>
    [HttpGet("{tenantId}/menu")]
    public async Task<IActionResult> GetVisibleMenu(
        string tenantId, [FromQuery] string? role, CancellationToken ct)
    {
        var config = await _settingsService.GetFeatureConfigurationAsync(tenantId, ct);
        if (config is null)
            return NotFound(new { error = $"Tenant '{tenantId}' not found." });

        var menuItems = config.GetVisibleMenuItems(role);
        return Ok(menuItems);
    }

    /// <summary>
    /// Gets all enabled modules for a tenant.
    /// </summary>
    [HttpGet("{tenantId}/modules")]
    public async Task<IActionResult> GetEnabledModules(
        string tenantId, CancellationToken ct)
    {
        var config = await _settingsService.GetFeatureConfigurationAsync(tenantId, ct);
        if (config is null)
            return NotFound(new { error = $"Tenant '{tenantId}' not found." });

        var modules = config.GetEnabledModules();
        return Ok(modules);
    }

    /// <summary>
    /// Initializes feature configuration from a template.
    /// Templates: "Starter", "ThaiDefault", "FullAccess"
    /// </summary>
    [HttpPost("{tenantId}/initialize")]
    public async Task<IActionResult> InitializeConfiguration(
        string tenantId, [FromBody] InitializeRequest request, CancellationToken ct)
    {
        var result = await _settingsService.InitializeFeatureConfigurationAsync(
            tenantId, request.Template, request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = $"Feature configuration initialized from '{request.Template}' template." });
    }

    /// <summary>
    /// Enables or disables a specific module for a tenant.
    /// </summary>
    [HttpPut("{tenantId}/modules/{module}")]
    public async Task<IActionResult> SetModuleEnabled(
        string tenantId, FeatureModule module,
        [FromBody] SetEnabledRequest request, CancellationToken ct)
    {
        var result = await _settingsService.SetModuleEnabledAsync(
            tenantId, module, request.Enabled, request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new
        {
            module = module.ToString(),
            enabled = request.Enabled,
            message = $"Module '{module}' {(request.Enabled ? "enabled" : "disabled")} successfully."
        });
    }

    /// <summary>
    /// Enables or disables multiple modules at once.
    /// </summary>
    [HttpPut("{tenantId}/modules")]
    public async Task<IActionResult> SetModulesEnabled(
        string tenantId, [FromBody] SetModulesBatchRequest request, CancellationToken ct)
    {
        var result = await _settingsService.SetModulesEnabledAsync(
            tenantId, request.Modules, request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = $"Updated {request.Modules.Count} module(s) successfully." });
    }

    /// <summary>
    /// Toggles a sub-feature within a module.
    /// </summary>
    [HttpPut("{tenantId}/modules/{module}/sub-features/{subFeature}")]
    public async Task<IActionResult> SetSubFeatureEnabled(
        string tenantId, FeatureModule module, string subFeature,
        [FromBody] SetEnabledRequest request, CancellationToken ct)
    {
        var result = await _settingsService.SetSubFeatureEnabledAsync(
            tenantId, module, subFeature, request.Enabled, request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new
        {
            module = module.ToString(),
            subFeature,
            enabled = request.Enabled,
            message = $"Sub-feature '{module}.{subFeature}' {(request.Enabled ? "enabled" : "disabled")} successfully."
        });
    }

    /// <summary>
    /// Toggles visibility of a menu item.
    /// </summary>
    [HttpPut("{tenantId}/modules/{module}/menu/{menuKey}")]
    public async Task<IActionResult> SetMenuVisibility(
        string tenantId, FeatureModule module, string menuKey,
        [FromBody] SetVisibilityRequest request, CancellationToken ct)
    {
        var result = await _settingsService.SetMenuVisibilityAsync(
            tenantId, module, menuKey, request.Visible, request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new
        {
            module = module.ToString(),
            menuKey,
            visible = request.Visible,
            message = $"Menu '{menuKey}' set to {(request.Visible ? "visible" : "hidden")} successfully."
        });
    }

    /// <summary>
    /// Batch update menu visibility for multiple items at once.
    /// </summary>
    [HttpPut("{tenantId}/menu/batch")]
    public async Task<IActionResult> SetMenuVisibilityBatch(
        string tenantId, [FromBody] SetMenuBatchRequest request, CancellationToken ct)
    {
        var changes = request.Changes
            .Select(c => (c.Module, c.MenuKey, c.Visible))
            .ToList();

        var result = await _settingsService.SetMenuVisibilityBatchAsync(
            tenantId, changes, request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = $"Updated {changes.Count} menu item(s) successfully." });
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────

public record InitializeRequest(string Template = "ThaiDefault", string? UpdatedBy = null);

public record SetEnabledRequest(bool Enabled, string? UpdatedBy = null);

public record SetVisibilityRequest(bool Visible, string? UpdatedBy = null);

public record SetModulesBatchRequest(
    Dictionary<FeatureModule, bool> Modules,
    string? UpdatedBy = null);

public record MenuVisibilityChange(
    FeatureModule Module,
    string MenuKey,
    bool Visible);

public record SetMenuBatchRequest(
    List<MenuVisibilityChange> Changes,
    string? UpdatedBy = null);
