using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Affiliate.Application.Commands;
using TakeTime.Affiliate.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Affiliate.Application.Queries;

/// <summary>
/// Query to retrieve all affiliates with optional filtering and pagination.
/// </summary>
public sealed class GetAllAffiliatesQuery : IRequest<List<AffiliateDto>>
{
    public bool? ActiveOnly { get; set; }
    public string? Name { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Handler for <see cref="GetAllAffiliatesQuery"/>. Retrieves all affiliates
/// for the current tenant with optional filtering.
/// </summary>
public sealed class GetAllAffiliatesQueryHandler : IRequestHandler<GetAllAffiliatesQuery, List<AffiliateDto>>
{
    private readonly IAffiliateRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetAllAffiliatesQueryHandler> _logger;

    public GetAllAffiliatesQueryHandler(
        IAffiliateRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetAllAffiliatesQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<List<AffiliateDto>> Handle(GetAllAffiliatesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Retrieving affiliates for tenant {TenantId}. ActiveOnly: {ActiveOnly}, Page: {Page}",
            tenantId, request.ActiveOnly, request.Page);

        var status = request.ActiveOnly == true ? "Active" : null;

        var affiliates = await _repository.SearchAsync(
            tenantId, status, request.Name, request.Page, request.PageSize, cancellationToken);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        var result = affiliates.Select(a => new AffiliateDto
        {
            Id = a.Id,
            TenantId = a.TenantId,
            Name = a.Name,
            Email = a.Email,
            Phone = a.Phone,
            CompanyName = a.CompanyName,
            AffiliateCode = a.AffiliateCode,
            Status = a.Status,
            CommissionRatePercent = a.CommissionRatePercent,
            CommissionType = a.CommissionType,
            TotalEarnings = a.TotalEarnings,
            PendingBalance = a.PendingBalance,
            PaidBalance = a.PaidBalance,
            TotalReferrals = a.TotalReferrals,
            SuccessfulReferrals = a.SuccessfulReferrals,
            BankAccountName = a.BankAccountName,
            BankAccountNumber = a.BankAccountNumber,
            BankName = a.BankName,
            TaxId = a.TaxId,
            Currency = currency,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        }).ToList();

        _logger.LogInformation("Retrieved {Count} affiliates", result.Count);

        return result;
    }
}
