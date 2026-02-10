using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Payment.Application.Commands;
using TakeTime.Payment.Application.DTOs;

namespace TakeTime.Payment.Application.Queries;

/// <summary>
/// Query to retrieve all payments for a specific reservation.
/// </summary>
public sealed class GetPaymentsByReservationQuery : IRequest<List<PaymentSummaryDto>>
{
    public Guid ReservationId { get; set; }
    public bool IncludeRefunds { get; set; } = true;
    public string? StatusFilter { get; set; }

    public GetPaymentsByReservationQuery()
    {
    }

    public GetPaymentsByReservationQuery(Guid reservationId)
    {
        ReservationId = reservationId;
    }
}

/// <summary>
/// Handler for <see cref="GetPaymentsByReservationQuery"/>. Retrieves all payments
/// associated with a reservation, optionally filtering by status and refund type.
/// </summary>
public sealed class GetPaymentsByReservationQueryHandler
    : IRequestHandler<GetPaymentsByReservationQuery, List<PaymentSummaryDto>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetPaymentsByReservationQueryHandler> _logger;

    public GetPaymentsByReservationQueryHandler(
        IPaymentRepository paymentRepository,
        ITenantContext tenantContext,
        ILogger<GetPaymentsByReservationQueryHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<PaymentSummaryDto>> Handle(
        GetPaymentsByReservationQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieving payments for reservation {ReservationId} for tenant {TenantId}",
            request.ReservationId, tenantSettings.TenantId);

        var payments = await _paymentRepository.GetPaymentsByReservationAsync(
            request.ReservationId, cancellationToken);

        // Apply filters
        if (!request.IncludeRefunds)
        {
            payments = payments.Where(p => !p.IsRefund).ToList();
        }

        if (!string.IsNullOrEmpty(request.StatusFilter))
        {
            payments = payments
                .Where(p => p.Status.Equals(request.StatusFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        _logger.LogDebug(
            "Found {Count} payments for reservation {ReservationId}",
            payments.Count, request.ReservationId);

        return payments.OrderByDescending(p => p.PaymentDate).ToList();
    }
}
