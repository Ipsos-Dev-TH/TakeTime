using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to soft-delete a rate plan.
/// </summary>
public sealed class DeleteRatePlanCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
    public string? DeletedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="DeleteRatePlanCommand"/>. Validates the rate plan exists
/// and performs a soft-delete.
/// </summary>
public sealed class DeleteRatePlanCommandHandler : IRequestHandler<DeleteRatePlanCommand, Unit>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DeleteRatePlanCommandHandler> _logger;

    public DeleteRatePlanCommandHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<DeleteRatePlanCommandHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeleteRatePlanCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogInformation(
            "Deleting rate plan {RatePlanId} for tenant {TenantId}",
            request.Id, tenantSettings.TenantId);

        var ratePlan = await _rateRepository.GetRatePlanEntityByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Rate plan with ID '{request.Id}' not found.");

        await _rateRepository.DeleteRatePlanAsync(request.Id, request.DeletedBy, cancellationToken);

        _logger.LogInformation(
            "Rate plan '{RatePlanName}' (ID: {RatePlanId}) soft-deleted for tenant {TenantId}",
            ratePlan.Name, request.Id, tenantSettings.TenantId);

        return Unit.Value;
    }
}
