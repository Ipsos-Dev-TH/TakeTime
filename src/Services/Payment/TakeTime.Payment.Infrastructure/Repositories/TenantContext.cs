using TakeTime.Core.Application.Interfaces;
using TakeTime.Payment.Application.Commands;

namespace TakeTime.Payment.Infrastructure.Repositories;

/// <summary>
/// Infrastructure implementation of <see cref="ITenantContext"/> for the Payment service.
/// Bridges the application-layer tenant context interface with the core
/// <see cref="ICurrentTenantService"/> to provide tenant-specific settings
/// such as VAT configuration, business details, and currency preferences.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private readonly ICurrentTenantService _currentTenantService;

    public TenantContext(ICurrentTenantService currentTenantService)
    {
        _currentTenantService = currentTenantService;
    }

    /// <inheritdoc />
    public string TenantId => _currentTenantService.TenantId ?? string.Empty;

    /// <inheritdoc />
    public Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = new TenantSettings
        {
            TenantId = _currentTenantService.TenantId ?? string.Empty,
            TenantName = _currentTenantService.TenantName ?? string.Empty,
            Currency = _currentTenantService.GetSetting("DefaultCurrency") ?? "THB",
            IsVATRegistered = bool.TryParse(_currentTenantService.GetSetting("IsVATRegistered"), out var vr) && vr,
            VATRate = decimal.TryParse(_currentTenantService.GetSetting("VATRate"), out var rate) ? rate : 7m,
            IsVATInclusive = bool.TryParse(_currentTenantService.GetSetting("IsVATInclusive"), out var vi) && vi,
            VATRegistrationNumber = _currentTenantService.GetSetting("VATRegistrationNumber"),
            BusinessName = _currentTenantService.GetSetting("BusinessName"),
            BusinessAddress = _currentTenantService.GetSetting("BusinessAddress"),
            BusinessTaxId = _currentTenantService.GetSetting("BusinessTaxId"),
            BusinessPhone = _currentTenantService.GetSetting("BusinessPhone"),
            BusinessEmail = _currentTenantService.GetSetting("BusinessEmail"),
            BusinessBranch = _currentTenantService.GetSetting("BusinessBranch")
        };

        return Task.FromResult(settings);
    }
}
