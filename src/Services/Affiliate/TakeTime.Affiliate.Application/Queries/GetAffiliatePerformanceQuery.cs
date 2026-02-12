using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Affiliate.Application.Commands;
using TakeTime.Affiliate.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Affiliate.Application.Queries;

/// <summary>
/// Query to retrieve affiliate performance metrics (bookings, commission, conversion rate).
/// </summary>
public sealed class GetAffiliatePerformanceQuery : IRequest<AffiliatePerformanceDto>
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

/// <summary>
/// Handler for <see cref="GetAffiliatePerformanceQuery"/>. Aggregates affiliate performance
/// data including total commissions, bookings, and conversion rate.
/// </summary>
public sealed class GetAffiliatePerformanceQueryHandler : IRequestHandler<GetAffiliatePerformanceQuery, AffiliatePerformanceDto>
{
    private readonly IAffiliateRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetAffiliatePerformanceQueryHandler> _logger;

    public GetAffiliatePerformanceQueryHandler(
        IAffiliateRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetAffiliatePerformanceQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<AffiliatePerformanceDto> Handle(GetAffiliatePerformanceQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Retrieving affiliate performance for tenant {TenantId} from {From} to {To}",
            tenantId, request.From, request.To);

        var (totalCommission, totalBookings, totalReferrals, conversionRate) =
            await _repository.GetPerformanceMetricsAsync(tenantId, request.From, request.To, cancellationToken);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        var averageCommission = totalBookings > 0
            ? Math.Round(totalCommission / totalBookings, 2)
            : 0;

        _logger.LogInformation(
            "Performance: {TotalBookings} bookings, {TotalCommission} {Currency} commission, {ConversionRate}% conversion",
            totalBookings, totalCommission, currency, conversionRate);

        return new AffiliatePerformanceDto
        {
            TenantId = tenantId,
            FromDate = request.From,
            ToDate = request.To,
            TotalCommission = totalCommission,
            TotalBookings = totalBookings,
            TotalReferrals = totalReferrals,
            ConversionRatePercent = conversionRate,
            AverageCommissionPerBooking = averageCommission,
            Currency = currency
        };
    }
}
