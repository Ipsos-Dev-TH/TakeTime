using TakeTime.Core.Application.Interfaces;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Infrastructure.Services;

public sealed class TenantContext : ITenantContext
{
    private readonly ICurrentTenantService _currentTenantService;

    public TenantContext(ICurrentTenantService currentTenantService)
    {
        _currentTenantService = currentTenantService;
    }

    public string TenantId => _currentTenantService.TenantId ?? string.Empty;

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
            DynamicPricingEnabled = bool.TryParse(_currentTenantService.GetSetting("DynamicPricingEnabled"), out var dp) && dp,
            BusinessName = _currentTenantService.GetSetting("BusinessName"),
            BusinessAddress = _currentTenantService.GetSetting("BusinessAddress"),
            BusinessTaxId = _currentTenantService.GetSetting("BusinessTaxId"),
            BusinessPhone = _currentTenantService.GetSetting("BusinessPhone"),
            BusinessEmail = _currentTenantService.GetSetting("BusinessEmail")
        };
        return Task.FromResult(settings);
    }
}
