using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Payment.Application.Commands;
using TakeTime.Payment.Application.DTOs;
using TakeTime.Payment.Application.Services;

namespace TakeTime.Payment.Application.Queries;

/// <summary>
/// Query to calculate the total paid vs total due for a reservation,
/// including VAT breakdown.
/// </summary>
public sealed class GetReservationBalanceQuery : IRequest<ReservationBalanceDto>
{
    public Guid ReservationId { get; set; }

    public GetReservationBalanceQuery()
    {
    }

    public GetReservationBalanceQuery(Guid reservationId)
    {
        ReservationId = reservationId;
    }
}

/// <summary>
/// Handler for <see cref="GetReservationBalanceQuery"/>. Calculates the complete
/// balance summary for a reservation, including all payments, refunds, and VAT.
/// </summary>
public sealed class GetReservationBalanceQueryHandler : IRequestHandler<GetReservationBalanceQuery, ReservationBalanceDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IReservationServiceClient _reservationClient;
    private readonly ITenantContext _tenantContext;
    private readonly VATCalculationService _vatService;
    private readonly ILogger<GetReservationBalanceQueryHandler> _logger;

    public GetReservationBalanceQueryHandler(
        IPaymentRepository paymentRepository,
        IReservationServiceClient reservationClient,
        ITenantContext tenantContext,
        VATCalculationService vatService,
        ILogger<GetReservationBalanceQueryHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _reservationClient = reservationClient;
        _tenantContext = tenantContext;
        _vatService = vatService;
        _logger = logger;
    }

    public async Task<ReservationBalanceDto> Handle(
        GetReservationBalanceQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        // Get reservation info
        var reservation = await _reservationClient.GetReservationAsync(
            request.ReservationId, cancellationToken)
            ?? throw new InvalidOperationException($"Reservation {request.ReservationId} not found.");

        // Get all payments for this reservation
        var payments = await _paymentRepository.GetPaymentsByReservationAsync(
            request.ReservationId, cancellationToken);

        // Only count verified/completed payments
        var verifiedPayments = payments
            .Where(p => p.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase)
                     || p.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var totalPaid = verifiedPayments
            .Where(p => !p.IsRefund)
            .Sum(p => p.Amount);

        var totalRefunded = verifiedPayments
            .Where(p => p.IsRefund)
            .Sum(p => p.Amount);

        var netPaid = totalPaid - totalRefunded;
        var balanceDue = reservation.TotalAmount - netPaid;

        // Calculate VAT on total amount
        var vatResult = _vatService.CalculateVAT(reservation.TotalAmount, tenantSettings);

        var balance = new ReservationBalanceDto
        {
            ReservationId = request.ReservationId,
            ReservationNumber = reservation.ReservationNumber,
            TotalAmount = reservation.TotalAmount,
            TotalPaid = totalPaid,
            TotalRefunded = totalRefunded,
            NetPaid = netPaid,
            BalanceDue = balanceDue,
            VATAmount = vatResult.VATAmount,
            Currency = reservation.Currency,
            IsFullyPaid = balanceDue <= 0,
            HasOverpayment = balanceDue < 0,
            Payments = payments.OrderByDescending(p => p.PaymentDate).ToList()
        };

        _logger.LogDebug(
            "Balance for reservation {ReservationNumber}: Total {Total}, Paid {Paid}, " +
            "Refunded {Refunded}, Balance {Balance} for tenant {TenantId}",
            reservation.ReservationNumber, reservation.TotalAmount,
            totalPaid, totalRefunded, balanceDue, tenantSettings.TenantId);

        return balance;
    }
}
