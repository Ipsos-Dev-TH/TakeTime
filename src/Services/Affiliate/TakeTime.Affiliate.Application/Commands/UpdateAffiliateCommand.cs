using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Affiliate.Application.DTOs;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Exceptions;

namespace TakeTime.Affiliate.Application.Commands;

/// <summary>
/// Command to update an affiliate's profile details.
/// </summary>
public sealed class UpdateAffiliateCommand : IRequest<AffiliateDto>
{
    public Guid AffiliateId { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public decimal? CommissionRatePercent { get; set; }
    public string? CommissionType { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? TaxId { get; set; }
}

/// <summary>
/// Handler for <see cref="UpdateAffiliateCommand"/>. Updates the affiliate's profile
/// in the repository with the provided changes.
/// </summary>
public sealed class UpdateAffiliateCommandHandler : IRequestHandler<UpdateAffiliateCommand, AffiliateDto>
{
    private readonly IAffiliateRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<UpdateAffiliateCommandHandler> _logger;

    public UpdateAffiliateCommandHandler(
        IAffiliateRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<UpdateAffiliateCommandHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<AffiliateDto> Handle(UpdateAffiliateCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating affiliate {AffiliateId}", request.AffiliateId);

        var affiliate = await _repository.GetByIdAsync(request.AffiliateId, cancellationToken)
            ?? throw new NotFoundException("Affiliate", request.AffiliateId);

        if (request.Name != null) affiliate.Name = request.Name;
        if (request.Email != null) affiliate.Email = request.Email;
        if (request.Phone != null) affiliate.Phone = request.Phone;
        if (request.CompanyName != null) affiliate.CompanyName = request.CompanyName;
        if (request.CommissionRatePercent.HasValue) affiliate.CommissionRatePercent = request.CommissionRatePercent.Value;
        if (request.CommissionType != null) affiliate.CommissionType = request.CommissionType;
        if (request.BankAccountName != null) affiliate.BankAccountName = request.BankAccountName;
        if (request.BankAccountNumber != null) affiliate.BankAccountNumber = request.BankAccountNumber;
        if (request.BankName != null) affiliate.BankName = request.BankName;
        if (request.TaxId != null) affiliate.TaxId = request.TaxId;
        affiliate.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(affiliate, cancellationToken);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        _logger.LogInformation("Affiliate {AffiliateId} updated successfully", request.AffiliateId);

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
