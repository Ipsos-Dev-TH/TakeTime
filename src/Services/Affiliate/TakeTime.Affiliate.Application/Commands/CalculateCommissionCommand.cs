using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Affiliate.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Affiliate.Application.Commands;

/// <summary>
/// Command to calculate and record a commission for an affiliate when a reservation
/// is completed. The commission is calculated based on the affiliate's configured
/// rate and the reservation's total amount.
/// </summary>
public sealed class CalculateCommissionCommand : IRequest<AffiliateCommissionDto>
{
    public string AffiliateCode { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public decimal ReservationAmount { get; set; }
}

/// <summary>
/// Handler for <see cref="CalculateCommissionCommand"/>. Looks up the affiliate by code,
/// calculates the commission amount based on the affiliate's rate, creates a commission
/// record, and updates the affiliate's balance.
/// </summary>
public sealed class CalculateCommissionCommandHandler : IRequestHandler<CalculateCommissionCommand, AffiliateCommissionDto>
{
    private readonly IAffiliateRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<CalculateCommissionCommandHandler> _logger;

    public CalculateCommissionCommandHandler(
        IAffiliateRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<CalculateCommissionCommandHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<AffiliateCommissionDto> Handle(CalculateCommissionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Calculating commission for affiliate {AffiliateCode} on reservation {ReservationNumber}",
            request.AffiliateCode, request.ReservationNumber);

        // Look up the affiliate
        var affiliate = await _repository.GetByCodeAsync(tenantId, request.AffiliateCode, cancellationToken)
            ?? throw new InvalidOperationException($"Affiliate with code '{request.AffiliateCode}' not found.");

        if (affiliate.Status != "Active")
        {
            throw new InvalidOperationException($"Affiliate '{affiliate.Name}' is not active (status: {affiliate.Status}).");
        }

        // Calculate commission
        decimal commissionAmount;
        if (affiliate.CommissionType.Equals("Percentage", StringComparison.OrdinalIgnoreCase))
        {
            commissionAmount = Math.Round(request.ReservationAmount * affiliate.CommissionRatePercent / 100, 2);
        }
        else // Fixed amount per booking
        {
            commissionAmount = affiliate.CommissionRatePercent; // In fixed mode, the rate field holds the fixed amount
        }

        // Create commission record
        var commission = new CommissionEntry
        {
            AffiliateId = affiliate.Id,
            TenantId = tenantId,
            ReservationId = request.ReservationId,
            ReservationNumber = request.ReservationNumber,
            ReservationAmount = request.ReservationAmount,
            CommissionRatePercent = affiliate.CommissionRatePercent,
            CommissionAmount = commissionAmount,
            Status = "Pending"
        };

        var commissionId = await _repository.CreateCommissionAsync(commission, cancellationToken);

        // Update affiliate balances
        affiliate.TotalEarnings += commissionAmount;
        affiliate.PendingBalance += commissionAmount;
        affiliate.SuccessfulReferrals++;
        affiliate.TotalReferrals++;
        affiliate.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(affiliate, cancellationToken);

        _logger.LogInformation(
            "Commission of {Amount} recorded for affiliate {AffiliateName} on reservation {ReservationNumber}",
            commissionAmount, affiliate.Name, request.ReservationNumber);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        return new AffiliateCommissionDto
        {
            Id = commissionId,
            AffiliateId = affiliate.Id,
            AffiliateName = affiliate.Name,
            ReservationId = request.ReservationId,
            ReservationNumber = request.ReservationNumber,
            ReservationAmount = request.ReservationAmount,
            CommissionRatePercent = affiliate.CommissionRatePercent,
            CommissionAmount = commissionAmount,
            Status = "Pending",
            Currency = currency,
            EarnedAt = commission.EarnedAt
        };
    }
}
