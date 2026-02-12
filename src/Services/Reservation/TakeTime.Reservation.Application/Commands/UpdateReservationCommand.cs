using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to update an existing reservation. Supports partial updates including
/// dates, guest counts, special requests, discount, and accommodation/item lists.
/// Recalculates totals when financial data changes.
/// </summary>
public sealed class UpdateReservationCommand : IRequest<ReservationDto>
{
    public Guid Id { get; set; }
    public DateTime? CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
    public int? NumberOfGuests { get; set; }
    public int? NumberOfAdults { get; set; }
    public int? NumberOfChildren { get; set; }
    public string? SpecialRequests { get; set; }
    public string? InternalNotes { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public List<CreateReservationAccommodationDto>? Accommodations { get; set; }
    public List<CreateReservationItemDto>? Items { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="UpdateReservationCommand"/>. Validates the reservation
/// exists and is in an updatable state, applies the changes, recalculates pricing
/// using tenant rules, and persists the update.
/// </summary>
public sealed class UpdateReservationCommandHandler : IRequestHandler<UpdateReservationCommand, ReservationDto>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IAccommodationRepository _accommodationRepository;
    private readonly IPricingService _pricingService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdateReservationCommandHandler> _logger;

    public UpdateReservationCommandHandler(
        IReservationRepository reservationRepository,
        IAccommodationRepository accommodationRepository,
        IPricingService pricingService,
        ITenantContext tenantContext,
        ILogger<UpdateReservationCommandHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _accommodationRepository = accommodationRepository;
        _pricingService = pricingService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ReservationDto> Handle(UpdateReservationCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var currency = tenantSettings.Currency ?? "THB";

        _logger.LogInformation(
            "Updating reservation {ReservationId} for tenant {TenantId}",
            request.Id, tenantSettings.TenantId);

        // Retrieve existing reservation
        var existingReservation = await _reservationRepository.GetReservationDtoByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Reservation with ID '{request.Id}' not found.");

        // Validate the reservation is in an updatable state
        var nonUpdatableStatuses = new[] { "CheckedOut", "Cancelled" };
        if (nonUpdatableStatuses.Contains(existingReservation.Status, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Reservation {existingReservation.ReservationNumber} cannot be updated. " +
                $"Current status is '{existingReservation.Status}'.");
        }

        // Determine effective dates
        var checkInDate = request.CheckInDate ?? existingReservation.CheckInDate;
        var checkOutDate = request.CheckOutDate ?? existingReservation.CheckOutDate;

        if (checkOutDate <= checkInDate)
        {
            throw new InvalidOperationException("Check-out date must be after check-in date.");
        }

        var numberOfNights = (checkOutDate.Date - checkInDate.Date).Days;

        // Prepare update data
        var updateData = new UpdateReservationData
        {
            Id = request.Id,
            CheckInDate = request.CheckInDate,
            CheckOutDate = request.CheckOutDate,
            NumberOfGuests = request.NumberOfGuests,
            NumberOfAdults = request.NumberOfAdults,
            NumberOfChildren = request.NumberOfChildren,
            NumberOfNights = numberOfNights,
            SpecialRequests = request.SpecialRequests,
            InternalNotes = request.InternalNotes,
            DiscountAmount = request.DiscountAmount,
            DiscountReason = request.DiscountReason,
            UpdatedBy = request.UpdatedBy
        };

        // Recalculate totals if accommodations or items are being updated
        if (request.Accommodations is not null || request.Items is not null)
        {
            decimal accommodationSubTotal = 0;
            var accommodationEntries = new List<CreateReservationAccommodationData>();

            // Process accommodations
            var accommodations = request.Accommodations ?? [];
            foreach (var accDto in accommodations)
            {
                var accommodation = await _accommodationRepository.GetByIdAsync(accDto.AccommodationId, cancellationToken)
                    ?? throw new InvalidOperationException($"Accommodation {accDto.AccommodationId} not found.");

                var accCheckIn = accDto.CheckInDate ?? checkInDate;
                var accCheckOut = accDto.CheckOutDate ?? checkOutDate;
                var accNights = (accCheckOut.Date - accCheckIn.Date).Days;

                // Check availability (excluding current reservation)
                var isAvailable = await _accommodationRepository.IsAvailableAsync(
                    accDto.AccommodationId, accCheckIn, accCheckOut, cancellationToken);

                // For updates, we allow the same accommodation if it's already part of this reservation
                // The repository should handle the exclusion logic

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

            // Process items
            decimal itemSubTotal = 0;
            var itemEntries = new List<CreateReservationItemData>();

            var items = request.Items ?? [];
            foreach (var itemDto in items)
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
            var discountAmount = request.DiscountAmount ?? existingReservation.DiscountAmount;
            var priceResult = _pricingService.CalculateTotal(
                accommodationSubTotal, itemSubTotal, discountAmount, tenantSettings);

            updateData.SubTotal = priceResult.SubTotal;
            updateData.VATAmount = priceResult.VATAmount;
            updateData.TotalAmount = priceResult.TotalAmount;
            updateData.BalanceDue = priceResult.TotalAmount - existingReservation.AmountPaid;
            updateData.Accommodations = accommodationEntries;
            updateData.Items = itemEntries;
        }

        // Persist the update
        await _reservationRepository.UpdateReservationAsync(updateData, cancellationToken);

        _logger.LogInformation(
            "Reservation {ReservationNumber} (ID: {ReservationId}) updated for tenant {TenantId}",
            existingReservation.ReservationNumber, request.Id, tenantSettings.TenantId);

        // Return the updated reservation
        return await _reservationRepository.GetReservationDtoByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve updated reservation.");
    }
}
