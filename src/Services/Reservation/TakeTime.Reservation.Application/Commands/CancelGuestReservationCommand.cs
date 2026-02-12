using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to cancel a reservation through the public booking portal using
/// the confirmation code. Applies the tenant's cancellation policy and
/// validates the guest's email for identity verification.
/// </summary>
public sealed class CancelGuestReservationCommand : IRequest<GuestReservationLookupDto>
{
    public string ConfirmationCode { get; set; } = string.Empty;
    public string? GuestEmail { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Handler for <see cref="CancelGuestReservationCommand"/>. Looks up the reservation
/// by confirmation code, verifies the guest's identity via email, evaluates the
/// cancellation policy, and transitions the reservation to Cancelled status.
/// </summary>
public sealed class CancelGuestReservationCommandHandler
    : IRequestHandler<CancelGuestReservationCommand, GuestReservationLookupDto>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<CancelGuestReservationCommandHandler> _logger;

    public CancelGuestReservationCommandHandler(
        IReservationRepository reservationRepository,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<CancelGuestReservationCommandHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<GuestReservationLookupDto> Handle(
        CancelGuestReservationCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogInformation(
            "Guest cancellation request for confirmation code {Code} in tenant {TenantId}",
            request.ConfirmationCode, tenantSettings.TenantId);

        // Find the reservation by confirmation code
        var criteria = new ReservationSearchCriteria
        {
            ReservationNumber = request.ConfirmationCode,
            PageSize = 1
        };

        var searchResult = await _reservationRepository.SearchReservationsAsync(criteria, cancellationToken);

        if (searchResult.Items.Count == 0)
        {
            throw new InvalidOperationException(
                $"Reservation with confirmation code '{request.ConfirmationCode}' not found.");
        }

        var reservation = searchResult.Items[0];

        // Verify guest identity via email if provided
        var fullReservation = await _reservationRepository.GetReservationDtoByIdAsync(
            reservation.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve reservation details.");

        if (!string.IsNullOrEmpty(request.GuestEmail) &&
            !string.Equals(fullReservation.CustomerEmail, request.GuestEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The provided email does not match the reservation record.");
        }

        // Validate status allows cancellation
        var nonCancellableStatuses = new[] { "Cancelled", "CheckedOut", "CheckedIn", "NoShow" };
        if (nonCancellableStatuses.Contains(reservation.Status, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Reservation cannot be cancelled. Current status is '{reservation.Status}'.");
        }

        // Cancel using the existing CancelReservationCommand
        var cancelCommand = new CancelReservationCommand
        {
            ReservationId = reservation.Id,
            Reason = request.Reason ?? "Cancelled by guest via booking portal",
            CancelledBy = "Guest Portal",
            WaiveCancellationFee = false,
            SendCancellationEmail = true
        };

        await _mediator.Send(cancelCommand, cancellationToken);

        _logger.LogInformation(
            "Guest reservation {ReservationNumber} cancelled via portal for tenant {TenantId}",
            reservation.ReservationNumber, tenantSettings.TenantId);

        // Return updated reservation view
        var updatedReservation = await _reservationRepository.GetReservationDtoByIdAsync(
            reservation.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve cancelled reservation.");

        return new GuestReservationLookupDto
        {
            ReservationNumber = updatedReservation.ReservationNumber,
            ConfirmationCode = updatedReservation.ReservationNumber,
            GuestName = updatedReservation.CustomerName,
            GuestEmail = updatedReservation.CustomerEmail,
            CheckInDate = updatedReservation.CheckInDate,
            CheckOutDate = updatedReservation.CheckOutDate,
            NumberOfNights = updatedReservation.NumberOfNights,
            NumberOfGuests = updatedReservation.NumberOfGuests,
            Status = updatedReservation.Status,
            TotalAmount = updatedReservation.TotalAmount,
            AmountPaid = updatedReservation.AmountPaid,
            BalanceDue = updatedReservation.BalanceDue,
            Currency = updatedReservation.Currency,
            SpecialRequests = updatedReservation.SpecialRequests,
            Accommodations = updatedReservation.Accommodations.Select(a => new GuestReservationAccommodationDto
            {
                AccommodationName = a.AccommodationName,
                AccommodationType = a.AccommodationType,
                RatePerNight = a.RatePerNight,
                NumberOfNights = a.NumberOfNights,
                TotalRate = a.TotalRate,
                Currency = a.Currency
            }).ToList(),
            CreatedAt = updatedReservation.CreatedAt
        };
    }
}
