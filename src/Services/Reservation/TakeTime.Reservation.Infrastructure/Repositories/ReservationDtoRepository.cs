using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;
using TakeTime.Reservation.Domain.Entities;
using TakeTime.Reservation.Domain.Enums;

namespace TakeTime.Reservation.Infrastructure.Repositories;

/// <summary>
/// Application-level repository implementation for reservation operations.
/// Works with DTOs and data models rather than domain entities directly.
/// Implements the Application.Interfaces.IReservationRepository.
/// </summary>
public class ReservationDtoRepository : Application.Interfaces.IReservationRepository
{
    private readonly ReservationDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;

    public ReservationDtoRepository(
        ReservationDbContext dbContext,
        ICurrentTenantService currentTenantService)
    {
        _dbContext = dbContext;
        _currentTenantService = currentTenantService;
    }

    /// <inheritdoc />
    public async Task<ReservationSummaryDto?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (entity is null)
            return null;

        return MapToSummaryDto(entity);
    }

    /// <inheritdoc />
    public async Task<ReservationDto?> GetReservationDtoByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .Include(r => r.Items)
            .Include(r => r.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (entity is null)
            return null;

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task CreateAsync(
        CreateReservationData reservationData,
        CancellationToken cancellationToken = default)
    {
        // Parse enums from string values
        if (!Enum.TryParse<BookingSource>(reservationData.Source, true, out var source))
            source = BookingSource.Direct;

        // Create the domain entity
        var reservation = new Domain.Entities.Reservation(
            customerId: reservationData.CustomerId,
            customerName: reservationData.CustomerName,
            customerPhone: reservationData.CustomerPhone ?? string.Empty,
            customerEmail: reservationData.CustomerEmail ?? string.Empty,
            checkInDate: reservationData.CheckInDate,
            checkOutDate: reservationData.CheckOutDate,
            source: source,
            specialRequests: reservationData.SpecialRequests);

        // Override the generated ID and reservation number if provided
        if (reservationData.Id != Guid.Empty)
        {
            // Use reflection to set the Id since it's protected
            typeof(Domain.Entities.Reservation)
                .BaseType!.BaseType! // Entity base class
                .GetProperty(nameof(Domain.Entities.Reservation.Id))!
                .SetValue(reservation, reservationData.Id);
        }

        // Set tenant and audit fields
        reservation.TenantId = reservationData.TenantId;
        reservation.CreatedBy = reservationData.CreatedBy;

        if (!string.IsNullOrWhiteSpace(reservationData.InternalNotes))
            reservation.SetInternalNotes(reservationData.InternalNotes);

        // Add accommodations
        foreach (var accomData in reservationData.Accommodations)
        {
            var currency = accomData.Currency ?? reservationData.Currency ?? "THB";
            var accommodation = new ReservationAccommodation(
                accommodationId: accomData.AccommodationId,
                accommodationName: accomData.AccommodationName,
                roomNumber: accomData.RoomNumber ?? string.Empty,
                checkInDate: accomData.CheckInDate,
                checkOutDate: accomData.CheckOutDate,
                pricePerNight: Money.Create(accomData.RatePerNight, currency),
                guestCount: accomData.Occupancy > 0 ? accomData.Occupancy : 1);

            if (accomData.Id != Guid.Empty)
            {
                typeof(ReservationAccommodation)
                    .BaseType! // Entity base class
                    .GetProperty(nameof(ReservationAccommodation.Id))!
                    .SetValue(accommodation, accomData.Id);
            }

            accommodation.TenantId = reservationData.TenantId;
            reservation.AddAccommodation(accommodation);
        }

        // Add items
        foreach (var itemData in reservationData.Items)
        {
            var currency = itemData.Currency ?? reservationData.Currency ?? "THB";
            var item = new ReservationItem(
                itemId: itemData.Id,
                itemName: itemData.ItemName,
                quantity: itemData.Quantity > 0 ? itemData.Quantity : 1,
                pricePerUnit: Money.Create(itemData.UnitPrice, currency));

            if (itemData.Id != Guid.Empty)
            {
                typeof(ReservationItem)
                    .BaseType! // Entity base class
                    .GetProperty(nameof(ReservationItem.Id))!
                    .SetValue(item, itemData.Id);
            }

            item.TenantId = reservationData.TenantId;
            reservation.AddItem(item);
        }

        _dbContext.Reservations.Add(reservation);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> GetTodayReservationCountAsync(
        string tenantId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        return await _dbContext.Reservations
            .CountAsync(r => r.CreatedAt.Date == today, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateStatusAsync(
        Guid reservationId, string newStatus, string? updatedBy, string? notes,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Reservation with ID '{reservationId}' not found.");

        if (!Enum.TryParse<ReservationStatus>(newStatus, true, out var status))
            throw new ArgumentException($"Invalid reservation status: '{newStatus}'.");

        switch (status)
        {
            case ReservationStatus.Confirmed:
                reservation.Confirm();
                break;
            case ReservationStatus.Cancelled:
                reservation.Cancel(notes);
                break;
            case ReservationStatus.CheckedIn:
                reservation.CheckIn();
                break;
            case ReservationStatus.CheckedOut:
                reservation.CheckOut();
                break;
            default:
                throw new InvalidOperationException(
                    $"Cannot directly transition to status '{newStatus}'. Use the appropriate action method.");
        }

        if (!string.IsNullOrWhiteSpace(notes) && status != ReservationStatus.Cancelled)
            reservation.SetInternalNotes(notes);

        reservation.UpdatedBy = updatedBy;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAccommodationsAvailableAsync(
        Guid reservationId, CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

        if (reservation is null)
            return false;

        foreach (var accom in reservation.Accommodations)
        {
            var hasConflict = await _dbContext.ReservationAccommodations
                .AnyAsync(ra =>
                    ra.AccommodationId == accom.AccommodationId &&
                    ra.ReservationId != reservationId &&
                    _dbContext.Reservations.Any(r =>
                        r.Id == ra.ReservationId &&
                        r.CheckInDate < accom.CheckOutDate &&
                        r.CheckOutDate > accom.CheckInDate &&
                        r.Status != ReservationStatus.Cancelled &&
                        !r.IsDeleted),
                    cancellationToken);

            if (hasConflict)
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public async Task CancelReservationAsync(
        Guid reservationId, string reason, decimal cancellationFee, decimal refundAmount,
        string? cancelledBy, CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Reservation with ID '{reservationId}' not found.");

        var fullReason = reason;
        if (cancellationFee > 0)
            fullReason += $" | Cancellation fee: {cancellationFee:N2}";
        if (refundAmount > 0)
            fullReason += $" | Refund amount: {refundAmount:N2}";

        reservation.Cancel(fullReason);
        reservation.UpdatedBy = cancelledBy;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CheckInAsync(
        Guid reservationId, DateTime actualCheckInTime, int? actualGuests,
        string? checkedInBy, string? notes, CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Reservation with ID '{reservationId}' not found.");

        reservation.CheckIn();
        reservation.UpdatedBy = checkedInBy;

        if (!string.IsNullOrWhiteSpace(notes))
        {
            var existingNotes = reservation.InternalNotes;
            var checkInNote = $"Check-in at {actualCheckInTime:yyyy-MM-dd HH:mm}";
            if (actualGuests.HasValue)
                checkInNote += $" with {actualGuests.Value} guests";
            if (!string.IsNullOrWhiteSpace(notes))
                checkInNote += $". {notes}";

            reservation.SetInternalNotes(
                string.IsNullOrWhiteSpace(existingNotes)
                    ? checkInNote
                    : $"{existingNotes}\n{checkInNote}");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CheckOutAsync(
        Guid reservationId, DateTime actualCheckOutTime,
        decimal additionalCharges, decimal lateCheckOutFee, decimal vatOnAdditional,
        decimal finalTotal, decimal finalBalance,
        string? checkedOutBy, string? notes,
        List<CheckOutAdditionalItem>? additionalItems,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Reservation with ID '{reservationId}' not found.");

        // Add any additional items from check-out
        if (additionalItems is { Count: > 0 })
        {
            var currency = reservation.TotalAmount.Currency;
            foreach (var addItem in additionalItems)
            {
                var itemCurrency = addItem.Currency ?? currency;
                var item = new ReservationItem(
                    itemId: Guid.NewGuid(),
                    itemName: addItem.ItemName,
                    quantity: addItem.Quantity,
                    pricePerUnit: Money.Create(addItem.UnitPrice, itemCurrency));

                item.TenantId = reservation.TenantId;
                reservation.AddItem(item);
            }
        }

        // Add late check-out fee as an item if applicable
        if (lateCheckOutFee > 0)
        {
            var currency = reservation.TotalAmount.Currency;
            var lateItem = new ReservationItem(
                itemId: Guid.NewGuid(),
                itemName: "Late Check-Out Fee",
                quantity: 1,
                pricePerUnit: Money.Create(lateCheckOutFee, currency));

            lateItem.TenantId = reservation.TenantId;
            reservation.AddItem(lateItem);
        }

        reservation.CheckOut();
        reservation.UpdatedBy = checkedOutBy;

        if (!string.IsNullOrWhiteSpace(notes))
        {
            var existingNotes = reservation.InternalNotes;
            var checkOutNote = $"Check-out at {actualCheckOutTime:yyyy-MM-dd HH:mm}. {notes}";
            reservation.SetInternalNotes(
                string.IsNullOrWhiteSpace(existingNotes)
                    ? checkOutNote
                    : $"{existingNotes}\n{checkOutNote}");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PaginatedResult<ReservationSummaryDto>> SearchReservationsAsync(
        ReservationSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Reservations
            .Include(r => r.Accommodations)
            .AsNoTracking()
            .AsQueryable();

        // Apply filters
        if (criteria.CheckInDateFrom.HasValue)
            query = query.Where(r => r.CheckInDate >= criteria.CheckInDateFrom.Value.Date);

        if (criteria.CheckInDateTo.HasValue)
            query = query.Where(r => r.CheckInDate <= criteria.CheckInDateTo.Value.Date);

        if (criteria.CheckOutDateFrom.HasValue)
            query = query.Where(r => r.CheckOutDate >= criteria.CheckOutDateFrom.Value.Date);

        if (criteria.CheckOutDateTo.HasValue)
            query = query.Where(r => r.CheckOutDate <= criteria.CheckOutDateTo.Value.Date);

        if (criteria.CreatedDateFrom.HasValue)
            query = query.Where(r => r.CreatedAt >= criteria.CreatedDateFrom.Value);

        if (criteria.CreatedDateTo.HasValue)
            query = query.Where(r => r.CreatedAt <= criteria.CreatedDateTo.Value);

        if (!string.IsNullOrWhiteSpace(criteria.Status))
        {
            if (Enum.TryParse<ReservationStatus>(criteria.Status, true, out var status))
                query = query.Where(r => r.Status == status);
        }

        if (criteria.Statuses is { Count: > 0 })
        {
            var statusEnums = criteria.Statuses
                .Where(s => Enum.TryParse<ReservationStatus>(s, true, out _))
                .Select(s => Enum.Parse<ReservationStatus>(s, true))
                .ToList();

            if (statusEnums.Count > 0)
                query = query.Where(r => statusEnums.Contains(r.Status));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Source))
        {
            if (Enum.TryParse<BookingSource>(criteria.Source, true, out var source))
                query = query.Where(r => r.Source == source);
        }

        if (!string.IsNullOrWhiteSpace(criteria.CustomerName))
            query = query.Where(r => r.CustomerName.Contains(criteria.CustomerName));

        if (!string.IsNullOrWhiteSpace(criteria.CustomerEmail))
            query = query.Where(r => r.CustomerEmail.Contains(criteria.CustomerEmail));

        if (!string.IsNullOrWhiteSpace(criteria.CustomerPhone))
            query = query.Where(r => r.CustomerPhone.Contains(criteria.CustomerPhone));

        if (!string.IsNullOrWhiteSpace(criteria.ReservationNumber))
            query = query.Where(r => r.ReservationNumber.Contains(criteria.ReservationNumber));

        if (criteria.AccommodationId.HasValue)
        {
            query = query.Where(r =>
                r.Accommodations.Any(a => a.AccommodationId == criteria.AccommodationId.Value));
        }

        if (!string.IsNullOrWhiteSpace(criteria.AccommodationType))
        {
            query = query.Where(r =>
                r.Accommodations.Any(a => a.AccommodationName.Contains(criteria.AccommodationType)));
        }

        if (criteria.MinTotalAmount.HasValue)
            query = query.Where(r => r.TotalAmount.Amount >= criteria.MinTotalAmount.Value);

        if (criteria.MaxTotalAmount.HasValue)
            query = query.Where(r => r.TotalAmount.Amount <= criteria.MaxTotalAmount.Value);

        if (criteria.HasBalance == true)
            query = query.Where(r => r.BalanceAmount.Amount > 0);
        else if (criteria.HasBalance == false)
            query = query.Where(r => r.BalanceAmount.Amount <= 0);

        // Count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply sorting
        query = criteria.SortBy?.ToLowerInvariant() switch
        {
            "checkindate" => criteria.SortDescending
                ? query.OrderByDescending(r => r.CheckInDate)
                : query.OrderBy(r => r.CheckInDate),
            "checkoutdate" => criteria.SortDescending
                ? query.OrderByDescending(r => r.CheckOutDate)
                : query.OrderBy(r => r.CheckOutDate),
            "customername" => criteria.SortDescending
                ? query.OrderByDescending(r => r.CustomerName)
                : query.OrderBy(r => r.CustomerName),
            "totalamount" => criteria.SortDescending
                ? query.OrderByDescending(r => r.TotalAmount.Amount)
                : query.OrderBy(r => r.TotalAmount.Amount),
            "status" => criteria.SortDescending
                ? query.OrderByDescending(r => r.Status)
                : query.OrderBy(r => r.Status),
            "createdat" => criteria.SortDescending
                ? query.OrderByDescending(r => r.CreatedAt)
                : query.OrderBy(r => r.CreatedAt),
            "reservationnumber" => criteria.SortDescending
                ? query.OrderByDescending(r => r.ReservationNumber)
                : query.OrderBy(r => r.ReservationNumber),
            _ => criteria.SortDescending
                ? query.OrderByDescending(r => r.CheckInDate)
                : query.OrderBy(r => r.CheckInDate)
        };

        // Apply pagination
        var page = Math.Max(1, criteria.Page);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 100);

        var entities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<ReservationSummaryDto>
        {
            Items = entities.Select(MapToSummaryDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<List<ReservationSummaryDto>> GetReservationsByCheckInDateAsync(
        DateTime date, List<string> statuses, CancellationToken cancellationToken = default)
    {
        var statusEnums = statuses
            .Where(s => Enum.TryParse<ReservationStatus>(s, true, out _))
            .Select(s => Enum.Parse<ReservationStatus>(s, true))
            .ToList();

        var entities = await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .AsNoTracking()
            .Where(r => r.CheckInDate == date.Date && statusEnums.Contains(r.Status))
            .OrderBy(r => r.CustomerName)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToSummaryDto).ToList();
    }

    /// <inheritdoc />
    public async Task<List<ReservationSummaryDto>> GetReservationsByCheckOutDateAsync(
        DateTime date, List<string> statuses, CancellationToken cancellationToken = default)
    {
        var statusEnums = statuses
            .Where(s => Enum.TryParse<ReservationStatus>(s, true, out _))
            .Select(s => Enum.Parse<ReservationStatus>(s, true))
            .ToList();

        var entities = await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .AsNoTracking()
            .Where(r => r.CheckOutDate == date.Date && statusEnums.Contains(r.Status))
            .OrderBy(r => r.CustomerName)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToSummaryDto).ToList();
    }

    /// <inheritdoc />
    public async Task<List<BookedAccommodationInfo>> GetBookedAccommodationsAsync(
        DateTime startDate, DateTime endDate, Guid? excludeReservationId,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ReservationAccommodations
            .AsNoTracking()
            .Where(ra => _dbContext.Reservations.Any(r =>
                r.Id == ra.ReservationId &&
                r.CheckInDate < endDate.Date &&
                r.CheckOutDate > startDate.Date &&
                r.Status != ReservationStatus.Cancelled &&
                !r.IsDeleted));

        if (excludeReservationId.HasValue)
            query = query.Where(ra => ra.ReservationId != excludeReservationId.Value);

        var results = await query
            .Join(
                _dbContext.Reservations,
                ra => ra.ReservationId,
                r => r.Id,
                (ra, r) => new BookedAccommodationInfo
                {
                    AccommodationId = ra.AccommodationId,
                    ReservationId = r.Id,
                    ReservationNumber = r.ReservationNumber,
                    GuestName = r.CustomerName,
                    CheckInDate = r.CheckInDate,
                    CheckOutDate = r.CheckOutDate
                })
            .ToListAsync(cancellationToken);

        return results;
    }

    /// <inheritdoc />
    public async Task<int> GetCurrentOccupancyCountAsync(
        DateTime date, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reservations
            .CountAsync(r =>
                r.CheckInDate <= date.Date &&
                r.CheckOutDate > date.Date &&
                r.Status == ReservationStatus.CheckedIn,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<decimal> GetRevenueForDateAsync(
        DateTime date, CancellationToken cancellationToken = default)
    {
        var revenue = await _dbContext.Reservations
            .Where(r =>
                r.CheckInDate <= date.Date &&
                r.CheckOutDate > date.Date &&
                r.Status != ReservationStatus.Cancelled)
            .SumAsync(r => r.TotalAmount.Amount, cancellationToken);

        return revenue;
    }

    /// <inheritdoc />
    public async Task<decimal> GetRevenueForPeriodAsync(
        DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        var revenue = await _dbContext.Reservations
            .Where(r =>
                r.CheckInDate < end.Date &&
                r.CheckOutDate > start.Date &&
                r.Status != ReservationStatus.Cancelled)
            .SumAsync(r => r.TotalAmount.Amount, cancellationToken);

        return revenue;
    }

    /// <inheritdoc />
    public async Task<int> GetCountByStatusAsync(
        string status, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ReservationStatus>(status, true, out var statusEnum))
            return 0;

        return await _dbContext.Reservations
            .CountAsync(r => r.Status == statusEnum, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> GetUpcomingConfirmedCountAsync(
        DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reservations
            .CountAsync(r =>
                r.CheckInDate >= fromDate.Date &&
                r.CheckInDate <= toDate.Date &&
                r.Status == ReservationStatus.Confirmed,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<ReservationSummaryDto>> GetUpcomingCheckInsAsync(
        DateTime fromDate, DateTime toDate, int take,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .AsNoTracking()
            .Where(r =>
                r.CheckInDate >= fromDate.Date &&
                r.CheckInDate <= toDate.Date &&
                (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Pending))
            .OrderBy(r => r.CheckInDate)
            .ThenBy(r => r.CustomerName)
            .Take(take)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToSummaryDto).ToList();
    }

    /// <inheritdoc />
    public async Task<List<ReservationSummaryDto>> GetUpcomingCheckOutsAsync(
        DateTime fromDate, DateTime toDate, int take,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .AsNoTracking()
            .Where(r =>
                r.CheckOutDate >= fromDate.Date &&
                r.CheckOutDate <= toDate.Date &&
                r.Status == ReservationStatus.CheckedIn)
            .OrderBy(r => r.CheckOutDate)
            .ThenBy(r => r.CustomerName)
            .Take(take)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToSummaryDto).ToList();
    }

    /// <inheritdoc />
    public async Task UpdateReservationAsync(
        UpdateReservationData data, CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == data.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Reservation with ID '{data.Id}' not found.");

        // Update date fields if changed
        if (data.CheckInDate.HasValue && data.CheckOutDate.HasValue &&
            (data.CheckInDate.Value.Date != reservation.CheckInDate ||
             data.CheckOutDate.Value.Date != reservation.CheckOutDate))
        {
            // Use PostponeCheckIn if check-in is being moved forward
            if (data.CheckInDate.Value.Date > reservation.CheckInDate)
            {
                reservation.PostponeCheckIn(data.CheckInDate.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(data.SpecialRequests))
            reservation.SetSpecialRequests(data.SpecialRequests);

        if (!string.IsNullOrWhiteSpace(data.InternalNotes))
            reservation.SetInternalNotes(data.InternalNotes);

        reservation.UpdatedBy = data.UpdatedBy;

        // Replace accommodations if provided
        if (data.Accommodations is { Count: > 0 })
        {
            // Remove existing accommodations
            var existingAccomIds = reservation.Accommodations
                .Select(a => a.AccommodationId).ToList();
            foreach (var id in existingAccomIds)
            {
                reservation.RemoveAccommodation(id);
            }

            // Add new accommodations
            var currency = reservation.TotalAmount.Currency;
            foreach (var accomData in data.Accommodations)
            {
                var accomCurrency = accomData.Currency ?? currency;
                var accommodation = new ReservationAccommodation(
                    accommodationId: accomData.AccommodationId,
                    accommodationName: accomData.AccommodationName,
                    roomNumber: accomData.RoomNumber ?? string.Empty,
                    checkInDate: accomData.CheckInDate,
                    checkOutDate: accomData.CheckOutDate,
                    pricePerNight: Money.Create(accomData.RatePerNight, accomCurrency),
                    guestCount: accomData.Occupancy > 0 ? accomData.Occupancy : 1);

                accommodation.TenantId = reservation.TenantId;
                reservation.AddAccommodation(accommodation);
            }
        }

        // Replace items if provided
        if (data.Items is { Count: > 0 })
        {
            // Remove existing items
            var existingItemIds = reservation.Items
                .Select(i => i.ItemId).ToList();
            foreach (var id in existingItemIds)
            {
                reservation.RemoveItem(id);
            }

            // Add new items
            var currency = reservation.TotalAmount.Currency;
            foreach (var itemData in data.Items)
            {
                var itemCurrency = itemData.Currency ?? currency;
                var item = new ReservationItem(
                    itemId: itemData.Id,
                    itemName: itemData.ItemName,
                    quantity: itemData.Quantity > 0 ? itemData.Quantity : 1,
                    pricePerUnit: Money.Create(itemData.UnitPrice, itemCurrency));

                item.TenantId = reservation.TenantId;
                reservation.AddItem(item);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ─── Private Mapping Methods ────────────────────────────────────────

    private static ReservationSummaryDto MapToSummaryDto(Domain.Entities.Reservation entity)
    {
        return new ReservationSummaryDto
        {
            Id = entity.Id,
            ReservationNumber = entity.ReservationNumber,
            CustomerName = entity.CustomerName,
            CustomerPhone = entity.CustomerPhone,
            CheckInDate = entity.CheckInDate,
            CheckOutDate = entity.CheckOutDate,
            NumberOfNights = entity.NumberOfNights,
            NumberOfGuests = entity.Accommodations.Sum(a => a.GuestCount),
            Status = entity.Status.ToString(),
            Source = entity.Source.ToString(),
            TotalAmount = entity.TotalAmount.Amount,
            BalanceDue = entity.BalanceAmount.Amount,
            Currency = entity.TotalAmount.Currency,
            AccommodationNames = entity.Accommodations.Count > 0
                ? string.Join(", ", entity.Accommodations.Select(a => a.AccommodationName))
                : null,
            CreatedAt = entity.CreatedAt
        };
    }

    private static ReservationDto MapToDto(Domain.Entities.Reservation entity)
    {
        var accommodationTotal = entity.Accommodations
            .Sum(a => a.TotalPrice.Amount);
        var itemsTotal = entity.Items
            .Sum(i => i.TotalPrice.Amount);
        var subTotal = accommodationTotal + itemsTotal;
        var discountAmount = subTotal - entity.TotalAmount.Amount;
        if (discountAmount < 0) discountAmount = 0;

        return new ReservationDto
        {
            Id = entity.Id,
            ReservationNumber = entity.ReservationNumber,
            TenantId = entity.TenantId ?? string.Empty,
            CustomerId = entity.CustomerId,
            CustomerName = entity.CustomerName,
            CustomerEmail = entity.CustomerEmail,
            CustomerPhone = entity.CustomerPhone,
            CheckInDate = entity.CheckInDate,
            CheckOutDate = entity.CheckOutDate,
            NumberOfGuests = entity.Accommodations.Sum(a => a.GuestCount),
            NumberOfAdults = entity.Accommodations.Sum(a => a.GuestCount),
            NumberOfChildren = 0,
            NumberOfNights = entity.NumberOfNights,
            Status = entity.Status.ToString(),
            Source = entity.Source.ToString(),
            SpecialRequests = entity.SpecialRequests,
            InternalNotes = entity.InternalNotes,
            SubTotal = subTotal,
            DiscountAmount = discountAmount,
            VATAmount = 0,
            VATRate = 0,
            IsVATInclusive = false,
            TotalAmount = entity.TotalAmount.Amount,
            AmountPaid = entity.DepositAmount.Amount,
            BalanceDue = entity.BalanceAmount.Amount,
            Currency = entity.TotalAmount.Currency,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy,
            Accommodations = entity.Accommodations.Select(a => new ReservationAccommodationDto
            {
                Id = a.Id,
                AccommodationId = a.AccommodationId,
                AccommodationName = a.AccommodationName,
                AccommodationType = string.Empty,
                RoomNumber = a.RoomNumber,
                CheckInDate = a.CheckInDate,
                CheckOutDate = a.CheckOutDate,
                NumberOfNights = (a.CheckOutDate - a.CheckInDate).Days,
                RatePerNight = a.PricePerNight.Amount,
                TotalRate = a.TotalPrice.Amount,
                Currency = a.PricePerNight.Currency,
                Occupancy = a.GuestCount
            }).ToList(),
            Items = entity.Items.Select(i => new ReservationItemDto
            {
                Id = i.Id,
                ItemName = i.ItemName,
                Description = null,
                ItemType = string.Empty,
                Quantity = i.Quantity,
                UnitPrice = i.PricePerUnit.Amount,
                TotalPrice = i.TotalPrice.Amount,
                Currency = i.PricePerUnit.Currency
            }).ToList()
        };
    }
}
