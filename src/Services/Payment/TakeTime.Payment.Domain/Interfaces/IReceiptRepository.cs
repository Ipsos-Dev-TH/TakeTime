using TakeTime.Core.Domain.Interfaces;
using TakeTime.Payment.Domain.Entities;
using TakeTime.Payment.Domain.Enums;

namespace TakeTime.Payment.Domain.Interfaces;

/// <summary>
/// Repository interface for the Receipt aggregate root.
/// </summary>
public interface IReceiptRepository : IRepository<Receipt>
{
    /// <summary>
    /// Retrieves a receipt by its unique receipt number.
    /// </summary>
    Task<Receipt?> GetByReceiptNumberAsync(
        string receiptNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all receipts for a specific reservation.
    /// </summary>
    Task<IReadOnlyList<Receipt>> GetByReservationIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all receipts for a specific payment.
    /// </summary>
    Task<Receipt?> GetByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all receipts of a specific type.
    /// </summary>
    Task<IReadOnlyList<Receipt>> GetByTypeAsync(
        ReceiptType receiptType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves receipts issued within a date range.
    /// </summary>
    Task<IReadOnlyList<Receipt>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
