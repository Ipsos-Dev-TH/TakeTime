using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Affiliate.Application.Commands;
using TakeTime.Affiliate.Application.DTOs;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Exceptions;

namespace TakeTime.Affiliate.Application.Queries;

/// <summary>
/// Query to retrieve payout history for a specific affiliate.
/// </summary>
public sealed class GetAffiliatePayoutsQuery : IRequest<List<AffiliatePayoutDto>>
{
    public Guid AffiliateId { get; set; }
}

/// <summary>
/// Handler for <see cref="GetAffiliatePayoutsQuery"/>. Retrieves all payout records
/// for the specified affiliate.
/// </summary>
public sealed class GetAffiliatePayoutsQueryHandler : IRequestHandler<GetAffiliatePayoutsQuery, List<AffiliatePayoutDto>>
{
    private readonly IAffiliateRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetAffiliatePayoutsQueryHandler> _logger;

    public GetAffiliatePayoutsQueryHandler(
        IAffiliateRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetAffiliatePayoutsQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<List<AffiliatePayoutDto>> Handle(GetAffiliatePayoutsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving payouts for affiliate {AffiliateId}", request.AffiliateId);

        var affiliate = await _repository.GetByIdAsync(request.AffiliateId, cancellationToken)
            ?? throw new NotFoundException("Affiliate", request.AffiliateId);

        var payouts = await _repository.GetPayoutsByAffiliateAsync(request.AffiliateId, cancellationToken);

        var result = payouts.Select(p => new AffiliatePayoutDto
        {
            Id = p.Id,
            AffiliateId = p.AffiliateId,
            AffiliateName = affiliate.Name,
            Amount = p.Amount,
            PaymentMethod = p.PaymentMethod,
            TransactionReference = p.TransactionReference,
            Status = p.Status,
            Currency = p.Currency,
            RequestedAt = p.RequestedAt,
            ProcessedAt = p.ProcessedAt,
            Notes = p.Notes
        }).ToList();

        _logger.LogInformation("Retrieved {Count} payouts for affiliate {AffiliateId}", result.Count, request.AffiliateId);

        return result;
    }
}
