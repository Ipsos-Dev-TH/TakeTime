using TakeTime.Core.Application.Interfaces;
using TakeTime.Inventory.Application.Commands;

namespace TakeTime.Inventory.Infrastructure.Repositories;

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
            Currency = _currentTenantService.GetSetting("DefaultCurrency") ?? "THB",
            IsVATRegistered = bool.TryParse(_currentTenantService.GetSetting("IsVATRegistered"), out var vr) && vr,
            VATRate = decimal.TryParse(_currentTenantService.GetSetting("VATRate"), out var rate) ? rate : 7m,
            IsVATInclusive = bool.TryParse(_currentTenantService.GetSetting("IsVATInclusive"), out var vi) && vi
        };
        return Task.FromResult(settings);
    }
}
