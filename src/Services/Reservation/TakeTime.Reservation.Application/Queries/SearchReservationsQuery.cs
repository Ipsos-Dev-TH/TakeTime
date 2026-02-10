using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to search and filter reservations with pagination and sorting support.
/// Returns a paginated list of reservation summaries matching the specified criteria.
/// </summary>
public sealed class SearchReservationsQuery : IRequest<PaginatedResult<ReservationSummaryDto>>
{
    public ReservationSearchCriteria Criteria { get; set; } = new();

    public SearchReservationsQuery()
    {
    }

    public SearchReservationsQuery(ReservationSearchCriteria criteria)
    {
        Criteria = criteria;
    }
}

/// <summary>
/// Handler for <see cref="SearchReservationsQuery"/>. Applies filtering, sorting,
/// and pagination scoped to the current tenant.
/// </summary>
public sealed class SearchReservationsQueryHandler
    : IRequestHandler<SearchReservationsQuery, PaginatedResult<ReservationSummaryDto>>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SearchReservationsQueryHandler> _logger;

    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "CheckInDate",
        "CheckOutDate",
        "CreatedAt",
        "CustomerName",
        "ReservationNumber",
        "TotalAmount",
        "Status",
        "Source"
    };

    public SearchReservationsQueryHandler(
        IReservationRepository reservationRepository,
        ITenantContext tenantContext,
        ILogger<SearchReservationsQueryHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<PaginatedResult<ReservationSummaryDto>> Handle(
        SearchReservationsQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var criteria = request.Criteria;

        // Validate and sanitize pagination parameters
        if (criteria.Page < 1) criteria.Page = 1;
        if (criteria.PageSize < 1) criteria.PageSize = 20;
        if (criteria.PageSize > 100) criteria.PageSize = 100;

        // Validate sort field to prevent injection
        if (!AllowedSortFields.Contains(criteria.SortBy))
        {
            criteria.SortBy = "CheckInDate";
        }

        _logger.LogDebug(
            "Searching reservations for tenant {TenantId}: Page {Page}, PageSize {PageSize}, SortBy {SortBy}",
            tenantSettings.TenantId, criteria.Page, criteria.PageSize, criteria.SortBy);

        var result = await _reservationRepository.SearchReservationsAsync(criteria, cancellationToken);

        _logger.LogDebug(
            "Found {TotalCount} reservations matching criteria for tenant {TenantId}",
            result.TotalCount, tenantSettings.TenantId);

        return result;
    }
}
