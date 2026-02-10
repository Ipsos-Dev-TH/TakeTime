using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Infrastructure.Database;
using TakeTime.Reservation.Domain.Entities;
using TakeTime.Reservation.Domain.Enums;
using TakeTime.Reservation.Domain.Interfaces;

namespace TakeTime.Reservation.Infrastructure.Repositories;

/// <summary>
/// Implementation of IReservationRepository providing data access for the Reservation aggregate.
/// Extends BaseRepository for standard CRUD and adds reservation-specific query methods.
/// </summary>
public class ReservationRepository : BaseRepository<Domain.Entities.Reservation>, IReservationRepository
{
    private readonly ReservationDbContext _dbContext;

    public ReservationRepository(
        ReservationDbContext context,
        ICurrentTenantService currentTenantService)
        : base(context, currentTenantService)
    {
        _dbContext = context;
    }

    /// <inheritdoc />
    public override async Task<Domain.Entities.Reservation?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .Include(r => r.Items)
            .Include(r => r.Payments)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Domain.Entities.Reservation?> GetByReservationNumberAsync(
        string reservationNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .Include(r => r.Items)
            .Include(r => r.Payments)
            .FirstOrDefaultAsync(r => r.ReservationNumber == reservationNumber, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.Entities.Reservation>> GetByDateRangeAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .Where(r => r.CheckInDate <= endDate.Date && r.CheckOutDate >= startDate.Date)
            .OrderBy(r => r.CheckInDate)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.Entities.Reservation>> GetByCustomerIdAsync(
        Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.CheckInDate)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.Entities.Reservation>> GetActiveReservationsAsync(
        CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            ReservationStatus.Pending,
            ReservationStatus.Confirmed,
            ReservationStatus.CheckedIn,
            ReservationStatus.PostponedCheckIn
        };

        return await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .Where(r => activeStatuses.Contains(r.Status))
            .OrderBy(r => r.CheckInDate)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.Entities.Reservation>> GetTodayCheckInsAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        return await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .Where(r => r.CheckInDate == today &&
                        (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Pending))
            .OrderBy(r => r.CustomerName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.Entities.Reservation>> GetTodayCheckOutsAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        return await _dbContext.Reservations
            .Include(r => r.Accommodations)
            .Where(r => r.CheckOutDate == today && r.Status == ReservationStatus.CheckedIn)
            .OrderBy(r => r.CustomerName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Accommodation>> GetAvailableAccommodationsAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        // Get IDs of accommodations that have overlapping reservations
        var bookedAccommodationIds = await _dbContext.ReservationAccommodations
            .Where(ra => _dbContext.Reservations
                .Any(r => r.Id == ra.ReservationId &&
                          r.CheckInDate < endDate.Date &&
                          r.CheckOutDate > startDate.Date &&
                          r.Status != ReservationStatus.Cancelled &&
                          !r.IsDeleted))
            .Select(ra => ra.AccommodationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await _dbContext.Accommodations
            .Where(a => a.Status == AccommodationStatus.Available &&
                        !bookedAccommodationIds.Contains(a.Id))
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> CheckAvailabilityAsync(
        Guid accommodationId, DateTime startDate, DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var hasOverlap = await _dbContext.ReservationAccommodations
            .AnyAsync(ra =>
                ra.AccommodationId == accommodationId &&
                _dbContext.Reservations.Any(r =>
                    r.Id == ra.ReservationId &&
                    r.CheckInDate < endDate.Date &&
                    r.CheckOutDate > startDate.Date &&
                    r.Status != ReservationStatus.Cancelled &&
                    !r.IsDeleted),
                cancellationToken);

        return !hasOverlap;
    }
}
