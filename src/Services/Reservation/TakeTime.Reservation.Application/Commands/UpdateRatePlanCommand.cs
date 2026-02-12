using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to update an existing rate plan. Supports partial updates.
/// </summary>
public sealed class UpdateRatePlanCommand : IRequest<RatePlanDto>
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? BaseRate { get; set; }
    public string? Currency { get; set; }
    public bool? IsActive { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="UpdateRatePlanCommand"/>. Retrieves the rate plan,
/// applies changes, and persists the update.
/// </summary>
public sealed class UpdateRatePlanCommandHandler : IRequestHandler<UpdateRatePlanCommand, RatePlanDto>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdateRatePlanCommandHandler> _logger;

    public UpdateRatePlanCommandHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<UpdateRatePlanCommandHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<RatePlanDto> Handle(UpdateRatePlanCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogInformation(
            "Updating rate plan {RatePlanId} for tenant {TenantId}",
            request.Id, tenantSettings.TenantId);

        var ratePlan = await _rateRepository.GetRatePlanEntityByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Rate plan with ID '{request.Id}' not found.");

        // Apply updates
        var currency = request.Currency ?? tenantSettings.Currency ?? "THB";
        var name = request.Name ?? ratePlan.Name;
        var description = request.Description ?? ratePlan.Description;
        var baseRate = request.BaseRate.HasValue
            ? Money.Create(request.BaseRate.Value, currency)
            : ratePlan.BaseRate;

        ratePlan.Update(name, description, baseRate);
        ratePlan.UpdatedBy = request.UpdatedBy;

        if (request.IsActive.HasValue)
        {
            ratePlan.SetActive(request.IsActive.Value);
        }

        await _rateRepository.UpdateRatePlanAsync(ratePlan, cancellationToken);

        _logger.LogInformation(
            "Rate plan {RatePlanId} updated for tenant {TenantId}",
            request.Id, tenantSettings.TenantId);

        return await _rateRepository.GetRatePlanDtoByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve updated rate plan.");
    }
}
