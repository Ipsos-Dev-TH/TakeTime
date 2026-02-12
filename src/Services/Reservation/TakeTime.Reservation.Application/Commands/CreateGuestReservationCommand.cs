using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to create a reservation through the public booking portal.
/// Simplified compared to the staff-facing CreateReservationCommand, this
/// accepts guest-provided details and creates a Pending reservation with
/// a confirmation code for self-service lookup.
/// </summary>
public sealed class CreateGuestReservationCommand : IRequest<GuestReservationConfirmationDto>
{
    public string GuestName { get; set; } = string.Empty;
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; } = 1;
    public int NumberOfAdults { get; set; } = 1;
    public int NumberOfChildren { get; set; }
    public string? SpecialRequests { get; set; }
    public List<Guid> AccommodationIds { get; set; } = [];
}

/// <summary>
/// Handler for <see cref="CreateGuestReservationCommand"/>. Validates accommodation
/// availability, calculates pricing using tenant rules, creates the reservation
/// with status Pending and source "Website", and returns a confirmation code.
/// </summary>
public sealed class CreateGuestReservationCommandHandler
    : IRequestHandler<CreateGuestReservationCommand, GuestReservationConfirmationDto>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IAccommodationRepository _accommodationRepository;
    private readonly IPricingService _pricingService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateGuestReservationCommandHandler> _logger;

    public CreateGuestReservationCommandHandler(
        IReservationRepository reservationRepository,
        IAccommodationRepository accommodationRepository,
        IPricingService pricingService,
        ITenantContext tenantContext,
        ILogger<CreateGuestReservationCommandHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _accommodationRepository = accommodationRepository;
        _pricingService = pricingService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<GuestReservationConfirmationDto> Handle(
        CreateGuestReservationCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var currency = tenantSettings.Currency ?? "THB";

        _logger.LogInformation(
            "Creating guest reservation for tenant {TenantId}, guest {GuestName}, check-in {CheckIn}",
            tenantSettings.TenantId, request.GuestName, request.CheckInDate);

        // Validate dates
        var numberOfNights = (request.CheckOutDate.Date - request.CheckInDate.Date).Days;
        if (numberOfNights <= 0)
        {
            throw new InvalidOperationException("Check-out date must be after check-in date.");
        }

        if (request.CheckInDate.Date < DateTime.UtcNow.Date)
        {
            throw new InvalidOperationException("Check-in date cannot be in the past.");
        }

        if (request.AccommodationIds.Count == 0)
        {
            throw new InvalidOperationException("At least one accommodation must be selected.");
        }

        // Generate reservation number
        var today = DateTime.UtcNow;
        var count = await _reservationRepository.GetTodayReservationCountAsync(
            tenantSettings.TenantId, cancellationToken);
        var reservationNumber = $"WEB-{today:yyyyMMdd}-{(count + 1):D4}";

        // Validate and calculate accommodation pricing
        var accommodationEntries = new List<CreateReservationAccommodationData>();
        decimal accommodationSubTotal = 0;
        var accommodationNames = new List<string>();

        foreach (var accommodationId in request.AccommodationIds)
        {
            var accommodation = await _accommodationRepository.GetByIdAsync(accommodationId, cancellationToken)
                ?? throw new InvalidOperationException($"Accommodation {accommodationId} not found.");

            // Check availability
            var isAvailable = await _accommodationRepository.IsAvailableAsync(
                accommodationId, request.CheckInDate, request.CheckOutDate, cancellationToken);

            if (!isAvailable)
            {
                throw new InvalidOperationException(
                    $"Accommodation '{accommodation.Name}' is not available for the requested dates.");
            }

            // Calculate rate using tenant pricing rules
            var ratePerNight = await _pricingService.CalculateAccommodationPriceAsync(
                accommodationId, request.CheckInDate, request.CheckOutDate,
                tenantSettings, cancellationToken);

            var totalRate = ratePerNight * numberOfNights;
            accommodationSubTotal += totalRate;
            accommodationNames.Add(accommodation.Name);

            accommodationEntries.Add(new CreateReservationAccommodationData
            {
                AccommodationId = accommodationId,
                AccommodationName = accommodation.Name,
                AccommodationType = accommodation.AccommodationType,
                RoomNumber = accommodation.RoomNumber,
                CheckInDate = request.CheckInDate,
                CheckOutDate = request.CheckOutDate,
                NumberOfNights = numberOfNights,
                RatePerNight = ratePerNight,
                TotalRate = totalRate,
                Occupancy = accommodation.DefaultOccupancy,
                Currency = currency
            });
        }

        // Calculate totals with tenant pricing rules (no discount for portal bookings)
        var priceResult = _pricingService.CalculateTotal(accommodationSubTotal, 0, 0, tenantSettings);

        // Create the reservation
        var reservationId = Guid.NewGuid();
        var reservationData = new CreateReservationData
        {
            Id = reservationId,
            TenantId = tenantSettings.TenantId,
            ReservationNumber = reservationNumber,
            CustomerId = Guid.Empty,
            CustomerName = request.GuestName,
            CustomerEmail = request.GuestEmail,
            CustomerPhone = request.GuestPhone,
            CheckInDate = request.CheckInDate,
            CheckOutDate = request.CheckOutDate,
            NumberOfGuests = request.NumberOfGuests,
            NumberOfAdults = request.NumberOfAdults,
            NumberOfChildren = request.NumberOfChildren,
            NumberOfNights = numberOfNights,
            Status = "Pending",
            Source = "Website",
            SpecialRequests = request.SpecialRequests,
            SubTotal = priceResult.SubTotal,
            DiscountAmount = 0,
            VATAmount = priceResult.VATAmount,
            VATRate = priceResult.VATRate,
            IsVATInclusive = priceResult.IsVATInclusive,
            TotalAmount = priceResult.TotalAmount,
            AmountPaid = 0,
            BalanceDue = priceResult.TotalAmount,
            Currency = currency,
            CreatedBy = "Guest Portal",
            CreatedAt = DateTime.UtcNow,
            Accommodations = accommodationEntries,
            Items = []
        };

        await _reservationRepository.CreateAsync(reservationData, cancellationToken);

        _logger.LogInformation(
            "Guest reservation {ReservationNumber} created successfully. Total: {Total} {Currency}",
            reservationNumber, priceResult.TotalAmount, currency);

        return new GuestReservationConfirmationDto
        {
            ReservationId = reservationId,
            ReservationNumber = reservationNumber,
            ConfirmationCode = reservationNumber,
            GuestName = request.GuestName,
            CheckInDate = request.CheckInDate,
            CheckOutDate = request.CheckOutDate,
            NumberOfNights = numberOfNights,
            NumberOfGuests = request.NumberOfGuests,
            TotalAmount = priceResult.TotalAmount,
            Currency = currency,
            Status = "Pending",
            AccommodationNames = accommodationNames,
            CreatedAt = DateTime.UtcNow
        };
    }
}
