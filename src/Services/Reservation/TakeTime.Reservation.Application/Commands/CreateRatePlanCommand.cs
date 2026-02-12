using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;
using TakeTime.Reservation.Domain.Entities;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to create a new rate plan for the tenant.
/// </summary>
public sealed class CreateRatePlanCommand : IRequest<RatePlanDto>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BaseRate { get; set; }
    public string Currency { get; set; } = "THB";
    public bool IsDefault { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="CreateRatePlanCommand"/>. Creates a new rate plan
/// entity and persists it for the current tenant.
/// </summary>
public sealed class CreateRatePlanCommandHandler : IRequestHandler<CreateRatePlanCommand, RatePlanDto>
{
    private readonly IRateManagementRepository _rateRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateRatePlanCommandHandler> _logger;

    public CreateRatePlanCommandHandler(
        IRateManagementRepository rateRepository,
        ITenantContext tenantContext,
        ILogger<CreateRatePlanCommandHandler> logger)
    {
        _rateRepository = rateRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<RatePlanDto> Handle(CreateRatePlanCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var currency = request.Currency ?? tenantSettings.Currency ?? "THB";

        _logger.LogInformation(
            "Creating rate plan '{Name}' for tenant {TenantId}",
            request.Name, tenantSettings.TenantId);

        var baseRate = Money.Create(request.BaseRate, currency);
        var ratePlan = new RatePlan(
            request.Name,
            baseRate,
            request.Description,
            request.IsDefault);

        ratePlan.TenantId = tenantSettings.TenantId;
        ratePlan.CreatedBy = request.CreatedBy;

        await _rateRepository.CreateRatePlanAsync(ratePlan, cancellationToken);

        _logger.LogInformation(
            "Rate plan '{Name}' (ID: {RatePlanId}) created for tenant {TenantId}",
            request.Name, ratePlan.Id, tenantSettings.TenantId);

        return await _rateRepository.GetRatePlanDtoByIdAsync(ratePlan.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve newly created rate plan.");
    }
}
