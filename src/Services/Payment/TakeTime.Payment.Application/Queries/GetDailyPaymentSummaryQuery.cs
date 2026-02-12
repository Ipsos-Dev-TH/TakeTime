using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Payment.Application.Commands;
using TakeTime.Payment.Application.DTOs;

namespace TakeTime.Payment.Application.Queries;

/// <summary>
/// Query to retrieve the daily payment summary, including totals broken
/// down by payment method.
/// </summary>
public sealed class GetDailyPaymentSummaryQuery : IRequest<DailyPaymentSummaryDto>
{
    public DateTime? Date { get; set; }

    public GetDailyPaymentSummaryQuery()
    {
    }

    public GetDailyPaymentSummaryQuery(DateTime date)
    {
        Date = date;
    }
}

/// <summary>
/// Handler for <see cref="GetDailyPaymentSummaryQuery"/>. Retrieves all payments
/// for the specified date (defaults to today) and calculates totals by payment method.
/// </summary>
public sealed class GetDailyPaymentSummaryQueryHandler
    : IRequestHandler<GetDailyPaymentSummaryQuery, DailyPaymentSummaryDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetDailyPaymentSummaryQueryHandler> _logger;

    public GetDailyPaymentSummaryQueryHandler(
        IPaymentRepository paymentRepository,
        ITenantContext tenantContext,
        ILogger<GetDailyPaymentSummaryQueryHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<DailyPaymentSummaryDto> Handle(
        GetDailyPaymentSummaryQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var currency = tenantSettings.Currency ?? "THB";
        var targetDate = request.Date?.Date ?? DateTime.UtcNow.Date;

        _logger.LogDebug(
            "Generating daily payment summary for {Date} for tenant {TenantId}",
            targetDate, tenantSettings.TenantId);

        var payments = await _paymentRepository.GetPaymentsByDateAsync(targetDate, cancellationToken);

        // Only include verified/completed payments in the summary
        var validPayments = payments
            .Where(p => p.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase) ||
                        p.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                        p.Status.Equals("PartiallyRefunded", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var regularPayments = validPayments.Where(p => !p.IsRefund).ToList();
        var refundPayments = validPayments.Where(p => p.IsRefund).ToList();

        var totalAmount = regularPayments.Sum(p => p.Amount);
        var totalRefunded = refundPayments.Sum(p => p.Amount);

        // Group by payment method
        var byMethod = validPayments
            .GroupBy(p => p.PaymentMethod, StringComparer.OrdinalIgnoreCase)
            .Select(g => new PaymentMethodSummaryDto
            {
                PaymentMethod = g.Key,
                TotalAmount = g.Where(p => !p.IsRefund).Sum(p => p.Amount),
                TotalRefunded = g.Where(p => p.IsRefund).Sum(p => p.Amount),
                NetAmount = g.Where(p => !p.IsRefund).Sum(p => p.Amount)
                           - g.Where(p => p.IsRefund).Sum(p => p.Amount),
                TransactionCount = g.Count(p => !p.IsRefund),
                RefundCount = g.Count(p => p.IsRefund)
            })
            .OrderByDescending(m => m.TotalAmount)
            .ToList();

        var summary = new DailyPaymentSummaryDto
        {
            Date = targetDate,
            TenantId = tenantSettings.TenantId,
            TotalAmount = totalAmount,
            TotalRefunded = totalRefunded,
            NetAmount = totalAmount - totalRefunded,
            TransactionCount = regularPayments.Count,
            RefundCount = refundPayments.Count,
            Currency = currency,
            ByMethod = byMethod
        };

        _logger.LogDebug(
            "Daily summary for {Date}: Total {Total}, Refunded {Refunded}, " +
            "Net {Net}, Transactions {Count} for tenant {TenantId}",
            targetDate, totalAmount, totalRefunded, summary.NetAmount,
            summary.TransactionCount, tenantSettings.TenantId);

        return summary;
    }
}
