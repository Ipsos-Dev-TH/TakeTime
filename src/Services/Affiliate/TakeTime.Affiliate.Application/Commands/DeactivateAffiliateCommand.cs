using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Affiliate.Application.DTOs;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Exceptions;

namespace TakeTime.Affiliate.Application.Commands;

/// <summary>
/// Command to deactivate an affiliate partner.
/// </summary>
public sealed class DeactivateAffiliateCommand : IRequest<AffiliateDto>
{
    public Guid AffiliateId { get; set; }
}

/// <summary>
/// Handler for <see cref="DeactivateAffiliateCommand"/>. Sets the affiliate's status to Inactive.
/// </summary>
public sealed class DeactivateAffiliateCommandHandler : IRequestHandler<DeactivateAffiliateCommand, AffiliateDto>
{
    private readonly IAffiliateRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<DeactivateAffiliateCommandHandler> _logger;

    public DeactivateAffiliateCommandHandler(
        IAffiliateRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<DeactivateAffiliateCommandHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<AffiliateDto> Handle(DeactivateAffiliateCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deactivating affiliate {AffiliateId}", request.AffiliateId);

        var affiliate = await _repository.GetByIdAsync(request.AffiliateId, cancellationToken)
            ?? throw new NotFoundException("Affiliate", request.AffiliateId);

        if (affiliate.Status == "Inactive")
        {
            throw new InvalidOperationException($"Affiliate '{affiliate.Name}' is already inactive.");
        }

        affiliate.Status = "Inactive";
        affiliate.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(affiliate, cancellationToken);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        _logger.LogInformation("Affiliate {AffiliateId} ({Name}) deactivated", request.AffiliateId, affiliate.Name);

        return new AffiliateDto
        {
            Id = affiliate.Id,
            TenantId = affiliate.TenantId,
            Name = affiliate.Name,
            Email = affiliate.Email,
            Phone = affiliate.Phone,
            CompanyName = affiliate.CompanyName,
            AffiliateCode = affiliate.AffiliateCode,
            Status = affiliate.Status,
            CommissionRatePercent = affiliate.CommissionRatePercent,
            CommissionType = affiliate.CommissionType,
            TotalEarnings = affiliate.TotalEarnings,
            PendingBalance = affiliate.PendingBalance,
            PaidBalance = affiliate.PaidBalance,
            TotalReferrals = affiliate.TotalReferrals,
            SuccessfulReferrals = affiliate.SuccessfulReferrals,
            BankAccountName = affiliate.BankAccountName,
            BankAccountNumber = affiliate.BankAccountNumber,
            BankName = affiliate.BankName,
            TaxId = affiliate.TaxId,
            Currency = currency,
            CreatedAt = affiliate.CreatedAt,
            UpdatedAt = affiliate.UpdatedAt
        };
    }
}
