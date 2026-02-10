using TakeTime.Core.Domain.Interfaces;
using TakeTime.Payment.Domain.Enums;

namespace TakeTime.Payment.Domain.Interfaces;

/// <summary>
/// Repository interface for the Payment aggregate root.
/// </summary>
public interface IPaymentRepository : IRepository<Entities.Payment>
{
    /// <summary>
    /// Retrieves a payment by its unique payment number.
    /// </summary>
    Task<Entities.Payment?> GetByPaymentNumberAsync(
        string paymentNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all payments for a specific reservation.
    /// </summary>
    Task<IReadOnlyList<Entities.Payment>> GetByReservationIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all payments for a specific customer.
    /// </summary>
    Task<IReadOnlyList<Entities.Payment>> GetByCustomerIdAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all payments with a specific status.
    /// </summary>
    Task<IReadOnlyList<Entities.Payment>> GetByStatusAsync(
        PaymentStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all pending payments awaiting verification.
    /// </summary>
    Task<IReadOnlyList<Entities.Payment>> GetPendingPaymentsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves payments within a date range.
    /// </summary>
    Task<IReadOnlyList<Entities.Payment>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
