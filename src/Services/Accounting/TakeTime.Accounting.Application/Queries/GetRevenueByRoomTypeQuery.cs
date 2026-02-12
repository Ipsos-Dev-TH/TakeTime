using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Accounting.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Accounting.Application.Queries;

/// <summary>
/// Query to retrieve revenue breakdown by room type for a date range.
/// </summary>
public sealed class GetRevenueByRoomTypeQuery : IRequest<RevenueByRoomTypeDto>
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

/// <summary>
/// Handler for <see cref="GetRevenueByRoomTypeQuery"/>. Retrieves revenue grouped by
/// room type, including booking count and average rate per room type.
/// </summary>
public sealed class GetRevenueByRoomTypeQueryHandler : IRequestHandler<GetRevenueByRoomTypeQuery, RevenueByRoomTypeDto>
{
    private readonly IAccountingQueryRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetRevenueByRoomTypeQueryHandler> _logger;

    public GetRevenueByRoomTypeQueryHandler(
        IAccountingQueryRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetRevenueByRoomTypeQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<RevenueByRoomTypeDto> Handle(GetRevenueByRoomTypeQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Generating revenue by room type for tenant {TenantId} from {StartDate} to {EndDate}",
            tenantId, request.StartDate.ToString("yyyy-MM-dd"), request.EndDate.ToString("yyyy-MM-dd"));

        var records = await _repository.GetRevenueByRoomTypeAsync(
            tenantId, request.StartDate.Date, request.EndDate.Date, cancellationToken);

        var totalRevenue = records.Sum(r => r.Revenue);
        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        var roomTypes = records.Select(r => new RoomTypeRevenueDto
        {
            RoomType = r.RoomType,
            Revenue = r.Revenue,
            BookingCount = r.BookingCount,
            AverageRate = r.AverageRate,
            PercentageOfTotal = totalRevenue > 0
                ? Math.Round(r.Revenue / totalRevenue * 100, 2)
                : 0
        }).ToList();

        return new RevenueByRoomTypeDto
        {
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            TenantId = tenantId,
            TotalRevenue = totalRevenue,
            RoomTypes = roomTypes,
            Currency = currency
        };
    }
}
