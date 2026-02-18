using Microsoft.EntityFrameworkCore;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;
using TakeTime.Reservation.Domain.Entities;
using TakeTime.Reservation.Domain.Enums;

namespace TakeTime.Reservation.Infrastructure.Repositories;

/// <summary>
/// Application-level repository implementation for accommodation operations.
/// Works with DTOs and lightweight info objects rather than domain entities directly.
/// Implements the Application.Interfaces.IAccommodationRepository.
/// </summary>
public class AccommodationDtoRepository : Application.Interfaces.IAccommodationRepository
{
    private readonly ReservationDbContext _dbContext;

    public AccommodationDtoRepository(ReservationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<AccommodationInfo?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Accommodations
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (entity is null)
            return null;

        return MapToInfo(entity);
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(
        Guid accommodationId, DateTime checkIn, DateTime checkOut,
        CancellationToken cancellationToken = default)
    {
        var accommodation = await _dbContext.Accommodations
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accommodationId, cancellationToken);

        if (accommodation is null)
            return false;

        // Accommodation must not be out of order or under maintenance
        if (accommodation.Status == AccommodationStatus.OutOfOrder ||
            accommodation.Status == AccommodationStatus.Maintenance)
            return false;

        // Check for overlapping reservations
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

    /// <inheritdoc />
    public async Task<List<AccommodationInfo>> GetActiveAccommodationsAsync(
        string? type, int? minOccupancy, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Accommodations
            .AsNoTracking()
            .Where(a => a.Status != AccommodationStatus.OutOfOrder);

        if (!string.IsNullOrWhiteSpace(type) &&
            Enum.TryParse<AccommodationType>(type, true, out var accommodationType))
        {
            query = query.Where(a => a.Type == accommodationType);
        }

        if (minOccupancy.HasValue && minOccupancy.Value > 0)
            query = query.Where(a => a.MaxGuests >= minOccupancy.Value);

        var entities = await query
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToInfo).ToList();
    }

    /// <inheritdoc />
    public async Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Accommodations
            .CountAsync(a => a.Status != AccommodationStatus.OutOfOrder, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<decimal> GetBaseRateAsync(
        Guid accommodationId, CancellationToken cancellationToken = default)
    {
        var accommodation = await _dbContext.Accommodations
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accommodationId, cancellationToken);

        if (accommodation is null)
            throw new InvalidOperationException(
                $"Accommodation with ID '{accommodationId}' not found.");

        return accommodation.BasePrice.Amount;
    }

    /// <inheritdoc />
    public async Task<List<AccommodationDto>> GetAllAccommodationDtosAsync(
        string? type, string? status, int? minOccupancy,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Accommodations
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(type) &&
            Enum.TryParse<AccommodationType>(type, true, out var accommodationType))
        {
            query = query.Where(a => a.Type == accommodationType);
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<AccommodationStatus>(status, true, out var accommodationStatus))
        {
            query = query.Where(a => a.Status == accommodationStatus);
        }

        if (minOccupancy.HasValue && minOccupancy.Value > 0)
            query = query.Where(a => a.MaxGuests >= minOccupancy.Value);

        var entities = await query
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<AccommodationDto?> GetAccommodationDtoByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Accommodations
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (entity is null)
            return null;

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<Accommodation?> GetEntityByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Accommodations
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateAsync(
        Accommodation accommodation,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Accommodations.Add(accommodation);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        Accommodation accommodation,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Accommodations.Update(accommodation);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasActiveReservationsAsync(
        Guid accommodationId, CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            ReservationStatus.Pending,
            ReservationStatus.Confirmed,
            ReservationStatus.CheckedIn,
            ReservationStatus.PostponedCheckIn
        };

        return await _dbContext.ReservationAccommodations
            .AnyAsync(ra =>
                ra.AccommodationId == accommodationId &&
                _dbContext.Reservations.Any(r =>
                    r.Id == ra.ReservationId &&
                    activeStatuses.Contains(r.Status) &&
                    !r.IsDeleted),
                cancellationToken);
    }

    // ─── Private Mapping Methods ────────────────────────────────────────

    private static AccommodationInfo MapToInfo(Accommodation entity)
    {
        return new AccommodationInfo
        {
            Id = entity.Id,
            Name = entity.Name,
            AccommodationType = entity.Type.ToString(),
            RoomNumber = entity.Building,
            MaxOccupancy = entity.MaxGuests,
            DefaultOccupancy = Math.Max(1, entity.MaxGuests / 2),
            BaseRatePerNight = entity.BasePrice.Amount,
            BedConfiguration = null,
            Amenities = entity.Amenities.ToList()
        };
    }

    private static AccommodationDto MapToDto(Accommodation entity)
    {
        return new AccommodationDto
        {
            Id = entity.Id,
            Name = entity.Name,
            AccommodationType = entity.Type.ToString(),
            RoomNumber = entity.Building,
            Floor = entity.Floor,
            Building = entity.Building,
            Description = entity.Description,
            MaxOccupancy = entity.MaxGuests,
            DefaultOccupancy = Math.Max(1, entity.MaxGuests / 2),
            BaseRatePerNight = entity.BasePrice.Amount,
            Currency = entity.BasePrice.Currency,
            Status = entity.Status.ToString(),
            IsActive = entity.Status != AccommodationStatus.OutOfOrder,
            Amenities = entity.Amenities.ToList(),
            ImageUrls = entity.Images.ToList(),
            BedConfiguration = null,
            SortOrder = 0,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
