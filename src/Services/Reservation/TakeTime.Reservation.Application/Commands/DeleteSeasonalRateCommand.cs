using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to soft-delete a seasonal rate.
/// </summary>
public sealed class DeleteSeasonalRateCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
    public string? DeletedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="DeleteSeasonalRateCommand"/>. Validates the seasonal rate
/// exists and performs a soft-delete.
/// </summary>
public sealed class DeleteSeasonalRateCommandHandler : IRequestHandler<DeleteSeasonalRateCommand, Unit>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DeleteSeasonalRateCommandHandler> _logger;

    public DeleteSeasonalRateCommandHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<DeleteSeasonalRateCommandHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeleteSeasonalRateCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogInformation(
            "Deleting seasonal rate {SeasonalRateId} for tenant {TenantId}",
            request.Id, tenantSettings.TenantId);

        var seasonalRate = await _rateRepository.GetSeasonalRateEntityByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Seasonal rate with ID '{request.Id}' not found.");

        await _rateRepository.DeleteSeasonalRateAsync(request.Id, request.DeletedBy, cancellationToken);

        _logger.LogInformation(
            "Seasonal rate '{SeasonName}' (ID: {SeasonalRateId}) soft-deleted for tenant {TenantId}",
            seasonalRate.SeasonName, request.Id, tenantSettings.TenantId);

        return Unit.Value;
    }
}
