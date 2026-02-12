using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Payment.Application.Commands;
using TakeTime.Payment.Application.DTOs;

namespace TakeTime.Payment.Application.Queries;

/// <summary>
/// Query to search and list payments with filtering, date range, status,
/// payment method, and pagination support.
/// </summary>
public sealed class SearchPaymentsQuery : IRequest<PaginatedPaymentResult>
{
    public string? Status { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? ReservationId { get; set; }
    public bool? IsRefund { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Handler for <see cref="SearchPaymentsQuery"/>. Applies search filters,
/// date ranges, and pagination scoped to the current tenant.
/// </summary>
public sealed class SearchPaymentsQueryHandler
    : IRequestHandler<SearchPaymentsQuery, PaginatedPaymentResult>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SearchPaymentsQueryHandler> _logger;

    public SearchPaymentsQueryHandler(
        IPaymentRepository paymentRepository,
        ITenantContext tenantContext,
        ILogger<SearchPaymentsQueryHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<PaginatedPaymentResult> Handle(
        SearchPaymentsQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        // Sanitize pagination
        if (request.Page < 1) request.Page = 1;
        if (request.PageSize < 1) request.PageSize = 20;
        if (request.PageSize > 100) request.PageSize = 100;

        var criteria = new PaymentSearchCriteria
        {
            Status = request.Status,
            PaymentMethod = request.PaymentMethod,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            ReservationId = request.ReservationId,
            IsRefund = request.IsRefund,
            Page = request.Page,
            PageSize = request.PageSize
        };

        _logger.LogDebug(
            "Searching payments for tenant {TenantId}: Status='{Status}', Method='{Method}', " +
            "From={From}, To={To}, Page={Page}",
            tenantSettings.TenantId, request.Status, request.PaymentMethod,
            request.FromDate, request.ToDate, request.Page);

        var result = await _paymentRepository.SearchAsync(criteria, cancellationToken);

        _logger.LogDebug(
            "Found {TotalCount} payments matching criteria for tenant {TenantId}",
            result.TotalCount, tenantSettings.TenantId);

        return result;
    }
}
