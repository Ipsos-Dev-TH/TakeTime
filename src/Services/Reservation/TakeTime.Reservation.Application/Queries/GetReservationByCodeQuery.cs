using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to look up a reservation by its confirmation code.
/// Used by the public booking portal for guest self-service lookups.
/// Returns a guest-safe DTO without internal notes or staff-only fields.
/// </summary>
public sealed class GetReservationByCodeQuery : IRequest<GuestReservationLookupDto?>
{
    public string ConfirmationCode { get; set; } = string.Empty;
}

/// <summary>
/// Handler for <see cref="GetReservationByCodeQuery"/>. Searches for a reservation
/// matching the given confirmation code within the current tenant, returning a
/// guest-safe view of the reservation details.
/// </summary>
public sealed class GetReservationByCodeQueryHandler
    : IRequestHandler<GetReservationByCodeQuery, GuestReservationLookupDto?>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetReservationByCodeQueryHandler> _logger;

    public GetReservationByCodeQueryHandler(
        IReservationRepository reservationRepository,
        ITenantContext tenantContext,
        ILogger<GetReservationByCodeQueryHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<GuestReservationLookupDto?> Handle(
        GetReservationByCodeQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug(
            "Looking up reservation by confirmation code {Code} for tenant {TenantId}",
            request.ConfirmationCode, tenantSettings.TenantId);

        // Search by reservation number (used as confirmation code for portal bookings)
        var criteria = new ReservationSearchCriteria
        {
            ReservationNumber = request.ConfirmationCode,
            PageSize = 1
        };

        var searchResult = await _reservationRepository.SearchReservationsAsync(criteria, cancellationToken);

        if (searchResult.Items.Count == 0)
        {
            _logger.LogWarning(
                "Reservation with confirmation code {Code} not found for tenant {TenantId}",
                request.ConfirmationCode, tenantSettings.TenantId);
            return null;
        }

        var summary = searchResult.Items[0];

        // Retrieve full details
        var reservation = await _reservationRepository.GetReservationDtoByIdAsync(summary.Id, cancellationToken);

        if (reservation is null)
        {
            return null;
        }

        return new GuestReservationLookupDto
        {
            ReservationNumber = reservation.ReservationNumber,
            ConfirmationCode = reservation.ReservationNumber,
            GuestName = reservation.CustomerName,
            GuestEmail = reservation.CustomerEmail,
            CheckInDate = reservation.CheckInDate,
            CheckOutDate = reservation.CheckOutDate,
            NumberOfNights = reservation.NumberOfNights,
            NumberOfGuests = reservation.NumberOfGuests,
            Status = reservation.Status,
            TotalAmount = reservation.TotalAmount,
            AmountPaid = reservation.AmountPaid,
            BalanceDue = reservation.BalanceDue,
            Currency = reservation.Currency,
            SpecialRequests = reservation.SpecialRequests,
            Accommodations = reservation.Accommodations.Select(a => new GuestReservationAccommodationDto
            {
                AccommodationName = a.AccommodationName,
                AccommodationType = a.AccommodationType,
                RatePerNight = a.RatePerNight,
                NumberOfNights = a.NumberOfNights,
                TotalRate = a.TotalRate,
                Currency = a.Currency
            }).ToList(),
            CreatedAt = reservation.CreatedAt
        };
    }
}
