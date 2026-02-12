using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Affiliate.Application.DTOs;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Exceptions;

namespace TakeTime.Affiliate.Application.Commands;

/// <summary>
/// Command to activate an affiliate partner.
/// </summary>
public sealed class ActivateAffiliateCommand : IRequest<AffiliateDto>
{
    public Guid AffiliateId { get; set; }
}

/// <summary>
/// Handler for <see cref="ActivateAffiliateCommand"/>. Sets the affiliate's status to Active.
/// </summary>
public sealed class ActivateAffiliateCommandHandler : IRequestHandler<ActivateAffiliateCommand, AffiliateDto>
{
    private readonly IAffiliateRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<ActivateAffiliateCommandHandler> _logger;

    public ActivateAffiliateCommandHandler(
        IAffiliateRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<ActivateAffiliateCommandHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<AffiliateDto> Handle(ActivateAffiliateCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activating affiliate {AffiliateId}", request.AffiliateId);

        var affiliate = await _repository.GetByIdAsync(request.AffiliateId, cancellationToken)
            ?? throw new NotFoundException("Affiliate", request.AffiliateId);

        if (affiliate.Status == "Active")
        {
            throw new InvalidOperationException($"Affiliate '{affiliate.Name}' is already active.");
        }

        affiliate.Status = "Active";
        affiliate.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(affiliate, cancellationToken);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        _logger.LogInformation("Affiliate {AffiliateId} ({Name}) activated", request.AffiliateId, affiliate.Name);

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
