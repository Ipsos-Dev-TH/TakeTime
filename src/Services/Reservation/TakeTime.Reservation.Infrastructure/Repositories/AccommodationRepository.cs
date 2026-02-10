using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Domain.Interfaces;
using TakeTime.Infrastructure.Database;
using TakeTime.Reservation.Domain.Entities;
using TakeTime.Reservation.Domain.Enums;

namespace TakeTime.Reservation.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for the Accommodation aggregate root.
/// Provides CRUD operations and accommodation-specific queries.
/// </summary>
public class AccommodationRepository : BaseRepository<Accommodation>
{
    private readonly ReservationDbContext _dbContext;

    public AccommodationRepository(
        ReservationDbContext context,
        ICurrentTenantService currentTenantService)
        : base(context, currentTenantService)
    {
        _dbContext = context;
    }

    /// <summary>
    /// Retrieves an accommodation by its room number.
    /// </summary>
    public async Task<Accommodation?> GetByRoomNumberAsync(
        string roomNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Accommodations
            .FirstOrDefaultAsync(a => a.Name == roomNumber || a.Building == roomNumber,
                cancellationToken);
    }

    /// <summary>
    /// Retrieves all accommodations available for the specified date range and guest count.
    /// Excludes accommodations that have overlapping confirmed/checked-in reservations.
    /// </summary>
    public async Task<IReadOnlyList<Accommodation>> GetAvailableAsync(
        DateTime checkIn, DateTime checkOut, int guests,
        CancellationToken cancellationToken = default)
    {
        var bookedAccommodationIds = await _dbContext.ReservationAccommodations
            .Where(ra => _dbContext.Reservations
                .Any(r => r.Id == ra.ReservationId &&
                          r.CheckInDate < checkOut.Date &&
                          r.CheckOutDate > checkIn.Date &&
                          r.Status != ReservationStatus.Cancelled &&
                          !r.IsDeleted))
            .Select(ra => ra.AccommodationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await _dbContext.Accommodations
            .Where(a => a.Status == AccommodationStatus.Available &&
                        a.MaxGuests >= guests &&
                        !bookedAccommodationIds.Contains(a.Id))
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves all accommodations of a specific type.
    /// </summary>
    public async Task<IReadOnlyList<Accommodation>> GetByTypeAsync(
        AccommodationType type, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Accommodations
            .Where(a => a.Type == type)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves all accommodations with a specific operational status.
    /// </summary>
    public async Task<IReadOnlyList<Accommodation>> GetByStatusAsync(
        AccommodationStatus status, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Accommodations
            .Where(a => a.Status == status)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Checks whether a specific accommodation is available for the given date range.
    /// </summary>
    public async Task<bool> IsAvailableAsync(
        Guid accommodationId, DateTime checkIn, DateTime checkOut,
        CancellationToken cancellationToken = default)
    {
        var accommodation = await GetByIdAsync(accommodationId, cancellationToken);
        if (accommodation is null || accommodation.Status != AccommodationStatus.Available)
            return false;

        var hasOverlap = await _dbContext.ReservationAccommodations
            .AnyAsync(ra =>
                ra.AccommodationId == accommodationId &&
                _dbContext.Reservations.Any(r =>
                    r.Id == ra.ReservationId &&
                    r.CheckInDate < checkOut.Date &&
                    r.CheckOutDate > checkIn.Date &&
                    r.Status != ReservationStatus.Cancelled &&
                    !r.IsDeleted),
                cancellationToken);

        return !hasOverlap;
    }

    /// <summary>
    /// Returns the count of active (non-deleted, available) accommodations.
    /// </summary>
    public async Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Accommodations
            .CountAsync(a => a.Status != AccommodationStatus.OutOfOrder, cancellationToken);
    }
}
