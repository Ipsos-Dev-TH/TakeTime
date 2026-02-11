using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace TakeTime.MultiTenancy.Features;

/// <summary>
/// Action filter attribute that gates access to a controller or action based on
/// the current tenant's feature configuration. When applied, the request will
/// receive a 403 Forbidden response if the required module (and optionally
/// sub-feature) is not enabled for the tenant.
///
/// <example>
/// <code>
/// [FeatureGate(FeatureModule.Inventory)]
/// public class ProductsController : ControllerBase { }
///
/// [FeatureGate(FeatureModule.Payment, SubFeature = "TaxInvoice")]
/// public async Task&lt;IActionResult&gt; GenerateTaxInvoice() { }
/// </code>
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class FeatureGateAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>The module that must be enabled.</summary>
    public FeatureModule Module { get; }

    /// <summary>
    /// Optional sub-feature within the module that must be enabled.
    /// If null, only the module-level check is performed.
    /// </summary>
    public string? SubFeature { get; set; }

    public FeatureGateAttribute(FeatureModule module)
    {
        Module = module;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var featureManager = context.HttpContext.RequestServices
            .GetService<IFeatureManager>();

        if (featureManager is null)
        {
            // Feature management not configured; allow through
            await next();
            return;
        }

        var ct = context.HttpContext.RequestAborted;

        bool isEnabled;
        if (SubFeature is not null)
        {
            isEnabled = await featureManager.IsSubFeatureEnabledAsync(Module, SubFeature, ct);
        }
        else
        {
            isEnabled = await featureManager.IsModuleEnabledAsync(Module, ct);
        }

        if (!isEnabled)
        {
            var featureName = SubFeature is not null
                ? $"{Module}.{SubFeature}"
                : Module.ToString();

            context.Result = new ObjectResult(new
            {
                error = "Feature not available",
                feature = featureName,
                message = $"The '{featureName}' feature is not enabled for your organization. " +
                          "Please contact your administrator to enable this feature."
            })
            {
                StatusCode = 403
            };
            return;
        }

        await next();
    }
}
