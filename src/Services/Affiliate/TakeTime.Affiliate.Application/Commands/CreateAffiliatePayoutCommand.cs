using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Affiliate.Application.DTOs;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Exceptions;

namespace TakeTime.Affiliate.Application.Commands;

/// <summary>
/// Command to create a payout for an affiliate partner.
/// </summary>
public sealed class CreateAffiliatePayoutCommand : IRequest<AffiliatePayoutDto>
{
    public Guid AffiliateId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// Handler for <see cref="CreateAffiliatePayoutCommand"/>. Validates the affiliate
/// has sufficient pending balance, creates a payout record, and updates balances.
/// </summary>
public sealed class CreateAffiliatePayoutCommandHandler : IRequestHandler<CreateAffiliatePayoutCommand, AffiliatePayoutDto>
{
    private readonly IAffiliateRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<CreateAffiliatePayoutCommandHandler> _logger;

    public CreateAffiliatePayoutCommandHandler(
        IAffiliateRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<CreateAffiliatePayoutCommandHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<AffiliatePayoutDto> Handle(CreateAffiliatePayoutCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Creating payout for affiliate {AffiliateId}, amount: {Amount}",
            request.AffiliateId, request.Amount);

        var affiliate = await _repository.GetByIdAsync(request.AffiliateId, cancellationToken)
            ?? throw new NotFoundException("Affiliate", request.AffiliateId);

        if (affiliate.Status != "Active")
        {
            throw new InvalidOperationException(
                $"Cannot create payout for affiliate in '{affiliate.Status}' status.");
        }

        if (request.Amount <= 0)
        {
            throw new InvalidOperationException("Payout amount must be positive.");
        }

        if (request.Amount > affiliate.PendingBalance)
        {
            throw new InvalidOperationException(
                $"Payout amount ({request.Amount}) exceeds pending balance ({affiliate.PendingBalance}).");
        }

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        var payout = new PayoutEntry
        {
            AffiliateId = request.AffiliateId,
            TenantId = tenantId,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            Status = "Pending",
            Currency = currency,
            Notes = request.Notes
        };

        var payoutId = await _repository.CreatePayoutAsync(payout, cancellationToken);

        // Update affiliate balances
        affiliate.PendingBalance -= request.Amount;
        affiliate.PaidBalance += request.Amount;
        affiliate.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(affiliate, cancellationToken);

        _logger.LogInformation(
            "Payout {PayoutId} of {Amount} {Currency} created for affiliate {AffiliateName}",
            payoutId, request.Amount, currency, affiliate.Name);

        return new AffiliatePayoutDto
        {
            Id = payoutId,
            AffiliateId = affiliate.Id,
            AffiliateName = affiliate.Name,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            Status = "Pending",
            Currency = currency,
            RequestedAt = payout.RequestedAt,
            Notes = request.Notes
        };
    }
}
