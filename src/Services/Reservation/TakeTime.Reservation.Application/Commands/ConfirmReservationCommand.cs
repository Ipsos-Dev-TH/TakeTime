using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to confirm a pending reservation. Transitions the reservation status
/// from Pending to Confirmed.
/// </summary>
public sealed class ConfirmReservationCommand : IRequest<ReservationDto>
{
    public Guid ReservationId { get; set; }
    public string? ConfirmedBy { get; set; }
    public string? ConfirmationNotes { get; set; }
    public bool SendConfirmationEmail { get; set; } = true;
}

/// <summary>
/// Handler for <see cref="ConfirmReservationCommand"/>. Validates the reservation
/// exists and is in a confirmable state, updates the status, and publishes
/// a ReservationConfirmed domain event.
/// </summary>
public sealed class ConfirmReservationCommandHandler : IRequestHandler<ConfirmReservationCommand, ReservationDto>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<ConfirmReservationCommandHandler> _logger;

    public ConfirmReservationCommandHandler(
        IReservationRepository reservationRepository,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<ConfirmReservationCommandHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<ReservationDto> Handle(ConfirmReservationCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        var reservation = await _reservationRepository.GetByIdAsync(request.ReservationId, cancellationToken)
            ?? throw new InvalidOperationException($"Reservation {request.ReservationId} not found.");

        // Validate current status allows confirmation
        var allowedStatuses = new[] { "Pending", "Tentative" };
        if (!allowedStatuses.Contains(reservation.Status, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Reservation {reservation.ReservationNumber} cannot be confirmed. " +
                $"Current status is '{reservation.Status}'. Only Pending or Tentative reservations can be confirmed.");
        }

        // Validate check-in date is in the future
        if (reservation.CheckInDate.Date < DateTime.UtcNow.Date)
        {
            throw new InvalidOperationException(
                $"Cannot confirm reservation {reservation.ReservationNumber}. " +
                $"Check-in date {reservation.CheckInDate:yyyy-MM-dd} is in the past.");
        }

        // Re-verify accommodation availability
        var accommodationsAvailable = await _reservationRepository.VerifyAccommodationsAvailableAsync(
            request.ReservationId, cancellationToken);

        if (!accommodationsAvailable)
        {
            throw new InvalidOperationException(
                $"Cannot confirm reservation {reservation.ReservationNumber}. " +
                "One or more accommodations are no longer available for the requested dates.");
        }

        // Update status
        await _reservationRepository.UpdateStatusAsync(
            request.ReservationId,
            "Confirmed",
            request.ConfirmedBy,
            request.ConfirmationNotes,
            cancellationToken);

        _logger.LogInformation(
            "Reservation {ReservationNumber} confirmed by {ConfirmedBy} for tenant {TenantId}",
            reservation.ReservationNumber, request.ConfirmedBy, tenantSettings.TenantId);

        // Publish domain event for downstream processing (notifications, channel sync, etc.)
        await _mediator.Publish(new ReservationConfirmedEvent
        {
            ReservationId = request.ReservationId,
            ReservationNumber = reservation.ReservationNumber,
            TenantId = tenantSettings.TenantId,
            CustomerName = reservation.CustomerName,
            CustomerEmail = null, // CustomerEmail not available in SummaryDto
            CheckInDate = reservation.CheckInDate,
            CheckOutDate = reservation.CheckOutDate,
            SendConfirmationEmail = request.SendConfirmationEmail,
            OccurredOn = DateTime.UtcNow
        }, cancellationToken);

        // Return updated reservation
        return await _reservationRepository.GetReservationDtoByIdAsync(request.ReservationId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve confirmed reservation.");
    }
}

/// <summary>
/// Domain event published when a reservation is confirmed.
/// </summary>
public sealed class ReservationConfirmedEvent : INotification
{
    public Guid ReservationId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public bool SendConfirmationEmail { get; set; }
    public DateTime OccurredOn { get; set; }
}
