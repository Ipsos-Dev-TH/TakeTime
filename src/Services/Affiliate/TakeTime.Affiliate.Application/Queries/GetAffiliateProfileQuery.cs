using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Affiliate.Application.Commands;
using TakeTime.Affiliate.Application.DTOs;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Exceptions;

namespace TakeTime.Affiliate.Application.Queries;

/// <summary>
/// Query to retrieve an affiliate's full profile by their ID.
/// </summary>
public sealed class GetAffiliateProfileQuery : IRequest<AffiliateDto>
{
    public Guid AffiliateId { get; set; }
}

/// <summary>
/// Handler for <see cref="GetAffiliateProfileQuery"/>. Retrieves the affiliate profile
/// from the repository and maps it to the DTO.
/// </summary>
public sealed class GetAffiliateProfileQueryHandler : IRequestHandler<GetAffiliateProfileQuery, AffiliateDto>
{
    private readonly IAffiliateRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetAffiliateProfileQueryHandler> _logger;

    public GetAffiliateProfileQueryHandler(
        IAffiliateRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetAffiliateProfileQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<AffiliateDto> Handle(GetAffiliateProfileQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving affiliate profile {AffiliateId}", request.AffiliateId);

        var affiliate = await _repository.GetByIdAsync(request.AffiliateId, cancellationToken)
            ?? throw new NotFoundException("Affiliate", request.AffiliateId);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

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
