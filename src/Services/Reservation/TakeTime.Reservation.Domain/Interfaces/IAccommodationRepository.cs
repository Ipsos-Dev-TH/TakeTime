using TakeTime.Core.Domain.Interfaces;
using TakeTime.Reservation.Domain.Entities;
using TakeTime.Reservation.Domain.Enums;

namespace TakeTime.Reservation.Domain.Interfaces;

/// <summary>
/// Repository interface for the Accommodation aggregate root.
/// </summary>
public interface IAccommodationRepository : IRepository<Accommodation>
{
    /// <summary>
    /// Retrieves all accommodations of a specific type.
    /// </summary>
    Task<IReadOnlyList<Accommodation>> GetByTypeAsync(
        AccommodationType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all accommodations with a specific status.
    /// </summary>
    Task<IReadOnlyList<Accommodation>> GetByStatusAsync(
        AccommodationStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all accommodations in a specific building.
    /// </summary>
    Task<IReadOnlyList<Accommodation>> GetByBuildingAsync(
        string building,
        CancellationToken cancellationToken = default);
}
