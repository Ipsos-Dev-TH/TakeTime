using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to check in a guest for a reservation. Transitions the reservation
/// status to CheckedIn and records the actual check-in time.
/// </summary>
public sealed class CheckInCommand : IRequest<ReservationDto>
{
    public Guid ReservationId { get; set; }
    public string? CheckedInBy { get; set; }
    public string? Notes { get; set; }
    public DateTime? ActualCheckInTime { get; set; }
    public int? ActualNumberOfGuests { get; set; }
}

/// <summary>
/// Handler for <see cref="CheckInCommand"/>. Validates that the reservation is in
/// a valid state for check-in, records the actual check-in time, and publishes
/// a GuestCheckedIn domain event.
/// </summary>
public sealed class CheckInCommandHandler : IRequestHandler<CheckInCommand, ReservationDto>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<CheckInCommandHandler> _logger;

    public CheckInCommandHandler(
        IReservationRepository reservationRepository,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<CheckInCommandHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<ReservationDto> Handle(CheckInCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        var reservation = await _reservationRepository.GetByIdAsync(request.ReservationId, cancellationToken)
            ?? throw new InvalidOperationException($"Reservation {request.ReservationId} not found.");

        // Validate current status allows check-in
        var allowedStatuses = new[] { "Confirmed", "Pending" };
        if (!allowedStatuses.Contains(reservation.Status, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Reservation {reservation.ReservationNumber} cannot be checked in. " +
                $"Current status is '{reservation.Status}'. Only Confirmed or Pending reservations can be checked in.");
        }

        // Validate check-in date is today or within allowed early check-in window
        var today = DateTime.UtcNow.Date;
        var allowedEarlyCheckInDays = tenantSettings.AllowEarlyCheckInDays;
        var earliestAllowedDate = reservation.CheckInDate.Date.AddDays(-allowedEarlyCheckInDays);

        if (today < earliestAllowedDate)
        {
            throw new InvalidOperationException(
                $"Check-in for reservation {reservation.ReservationNumber} is not yet allowed. " +
                $"Scheduled check-in date is {reservation.CheckInDate:yyyy-MM-dd}.");
        }

        var actualCheckInTime = request.ActualCheckInTime ?? DateTime.UtcNow;

        // Perform check-in
        await _reservationRepository.CheckInAsync(
            request.ReservationId,
            actualCheckInTime,
            request.ActualNumberOfGuests,
            request.CheckedInBy,
            request.Notes,
            cancellationToken);

        _logger.LogInformation(
            "Guest checked in for reservation {ReservationNumber} at {CheckInTime} by {StaffMember} for tenant {TenantId}",
            reservation.ReservationNumber, actualCheckInTime, request.CheckedInBy, tenantSettings.TenantId);

        // Publish domain event
        await _mediator.Publish(new GuestCheckedInEvent
        {
            ReservationId = request.ReservationId,
            ReservationNumber = reservation.ReservationNumber,
            TenantId = tenantSettings.TenantId,
            CustomerName = reservation.CustomerName,
            ActualCheckInTime = actualCheckInTime,
            ScheduledCheckInDate = reservation.CheckInDate,
            CheckOutDate = reservation.CheckOutDate,
            OccurredOn = DateTime.UtcNow
        }, cancellationToken);

        // Return updated reservation
        return await _reservationRepository.GetReservationDtoByIdAsync(request.ReservationId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve checked-in reservation.");
    }
}

/// <summary>
/// Domain event published when a guest checks in.
/// </summary>
public sealed class GuestCheckedInEvent : INotification
{
    public Guid ReservationId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime ActualCheckInTime { get; set; }
    public DateTime ScheduledCheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public DateTime OccurredOn { get; set; }
}
