using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to create a new reservation. Includes full accommodation and item details
/// for the initial booking.
/// </summary>
public sealed class CreateReservationCommand : IRequest<ReservationDto>
{
    public string TenantId { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; }
    public int NumberOfAdults { get; set; }
    public int NumberOfChildren { get; set; }
    public string Source { get; set; } = "Direct";
    public string? SpecialRequests { get; set; }
    public string? InternalNotes { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public List<CreateReservationAccommodationDto> Accommodations { get; set; } = [];
    public List<CreateReservationItemDto> Items { get; set; } = [];
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="CreateReservationCommand"/>. Resolves tenant settings, creates
/// the reservation entity, calculates totals with tenant-specific pricing rules (including VAT),
/// persists the reservation, and publishes domain events.
/// </summary>
public sealed class CreateReservationCommandHandler : IRequestHandler<CreateReservationCommand, ReservationDto>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IAccommodationRepository _accommodationRepository;
    private readonly IPricingService _pricingService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateReservationCommandHandler> _logger;

    public CreateReservationCommandHandler(
        IReservationRepository reservationRepository,
        IAccommodationRepository accommodationRepository,
        IPricingService pricingService,
        ITenantContext tenantContext,
        ILogger<CreateReservationCommandHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _accommodationRepository = accommodationRepository;
        _pricingService = pricingService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ReservationDto> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var currency = tenantSettings.Currency ?? "THB";

        _logger.LogInformation(
            "Creating reservation for tenant {TenantId}, customer {CustomerName}, check-in {CheckIn}",
            tenantSettings.TenantId, request.CustomerName, request.CheckInDate);

        // Generate unique reservation number
        var reservationNumber = await GenerateReservationNumberAsync(tenantSettings.TenantId, cancellationToken);

        // Calculate number of nights
        var numberOfNights = (request.CheckOutDate.Date - request.CheckInDate.Date).Days;
        if (numberOfNights <= 0)
        {
            throw new InvalidOperationException("Check-out date must be after check-in date.");
        }

        // Validate and resolve accommodations
        var accommodationEntries = new List<CreateReservationAccommodationData>();
        decimal accommodationSubTotal = 0;

        foreach (var accDto in request.Accommodations)
        {
            var accommodation = await _accommodationRepository.GetByIdAsync(accDto.AccommodationId, cancellationToken)
                ?? throw new InvalidOperationException($"Accommodation {accDto.AccommodationId} not found.");

            var accCheckIn = accDto.CheckInDate ?? request.CheckInDate;
            var accCheckOut = accDto.CheckOutDate ?? request.CheckOutDate;
            var accNights = (accCheckOut.Date - accCheckIn.Date).Days;

            // Check availability
            var isAvailable = await _accommodationRepository.IsAvailableAsync(
                accDto.AccommodationId, accCheckIn, accCheckOut, cancellationToken);

            if (!isAvailable)
            {
                throw new InvalidOperationException(
                    $"Accommodation '{accommodation.Name}' is not available for the requested dates.");
            }

            // Calculate rate: use override or calculate based on tenant pricing rules
            var ratePerNight = accDto.RateOverride
                ?? await _pricingService.CalculateAccommodationPriceAsync(
                    accDto.AccommodationId, accCheckIn, accCheckOut, tenantSettings, cancellationToken);

            var totalRate = ratePerNight * accNights;
            accommodationSubTotal += totalRate;

            accommodationEntries.Add(new CreateReservationAccommodationData
            {
                AccommodationId = accDto.AccommodationId,
                AccommodationName = accommodation.Name,
                AccommodationType = accommodation.AccommodationType,
                RoomNumber = accommodation.RoomNumber,
                CheckInDate = accCheckIn,
                CheckOutDate = accCheckOut,
                NumberOfNights = accNights,
                RatePerNight = ratePerNight,
                TotalRate = totalRate,
                Occupancy = accDto.Occupancy > 0 ? accDto.Occupancy : accommodation.DefaultOccupancy,
                Currency = currency
            });
        }

        // Calculate item totals
        decimal itemSubTotal = 0;
        var itemEntries = new List<CreateReservationItemData>();

        foreach (var itemDto in request.Items)
        {
            var totalPrice = itemDto.UnitPrice * itemDto.Quantity;
            itemSubTotal += totalPrice;

            itemEntries.Add(new CreateReservationItemData
            {
                ItemName = itemDto.ItemName,
                Description = itemDto.Description,
                ItemType = itemDto.ItemType,
                Quantity = itemDto.Quantity,
                UnitPrice = itemDto.UnitPrice,
                TotalPrice = totalPrice,
                ServiceDate = itemDto.ServiceDate,
                Currency = currency
            });
        }

        // Calculate totals with tenant pricing rules
        var priceResult = _pricingService.CalculateTotal(
            accommodationSubTotal, itemSubTotal, request.DiscountAmount ?? 0, tenantSettings);

        // Create the reservation data model
        var reservationId = Guid.NewGuid();
        var reservationData = new CreateReservationData
        {
            Id = reservationId,
            TenantId = tenantSettings.TenantId,
            ReservationNumber = reservationNumber,
            CustomerId = request.CustomerId ?? Guid.Empty,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            CustomerPhone = request.CustomerPhone,
            CheckInDate = request.CheckInDate,
            CheckOutDate = request.CheckOutDate,
            NumberOfGuests = request.NumberOfGuests,
            NumberOfAdults = request.NumberOfAdults,
            NumberOfChildren = request.NumberOfChildren,
            NumberOfNights = numberOfNights,
            Status = "Pending",
            Source = request.Source,
            SpecialRequests = request.SpecialRequests,
            InternalNotes = request.InternalNotes,
            SubTotal = priceResult.SubTotal,
            DiscountAmount = priceResult.DiscountAmount,
            DiscountReason = request.DiscountReason,
            VATAmount = priceResult.VATAmount,
            VATRate = priceResult.VATRate,
            IsVATInclusive = priceResult.IsVATInclusive,
            TotalAmount = priceResult.TotalAmount,
            AmountPaid = 0,
            BalanceDue = priceResult.TotalAmount,
            Currency = currency,
            CreatedBy = request.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            Accommodations = accommodationEntries,
            Items = itemEntries
        };

        // Persist the reservation
        await _reservationRepository.CreateAsync(reservationData, cancellationToken);

        _logger.LogInformation(
            "Reservation {ReservationNumber} created successfully. Total: {Total} {Currency}",
            reservationNumber, priceResult.TotalAmount, currency);

        // Return the full DTO
        return await _reservationRepository.GetReservationDtoByIdAsync(reservationId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve newly created reservation.");
    }

    private async Task<string> GenerateReservationNumberAsync(string tenantId, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow;
        var prefix = $"RES-{today:yyyyMMdd}";
        var count = await _reservationRepository.GetTodayReservationCountAsync(tenantId, cancellationToken);
        return $"{prefix}-{(count + 1):D4}";
    }
}
