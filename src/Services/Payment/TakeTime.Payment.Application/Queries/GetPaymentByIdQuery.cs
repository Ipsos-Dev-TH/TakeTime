using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Payment.Application.Commands;
using TakeTime.Payment.Application.DTOs;

namespace TakeTime.Payment.Application.Queries;

/// <summary>
/// Query to retrieve a single payment by its unique identifier.
/// </summary>
public sealed class GetPaymentByIdQuery : IRequest<PaymentDto>
{
    public Guid PaymentId { get; set; }

    public GetPaymentByIdQuery()
    {
    }

    public GetPaymentByIdQuery(Guid paymentId)
    {
        PaymentId = paymentId;
    }
}

/// <summary>
/// Handler for <see cref="GetPaymentByIdQuery"/>. Retrieves a payment by ID,
/// scoped to the current tenant.
/// </summary>
public sealed class GetPaymentByIdQueryHandler : IRequestHandler<GetPaymentByIdQuery, PaymentDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetPaymentByIdQueryHandler> _logger;

    public GetPaymentByIdQueryHandler(
        IPaymentRepository paymentRepository,
        ITenantContext tenantContext,
        ILogger<GetPaymentByIdQueryHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<PaymentDto> Handle(
        GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieving payment {PaymentId} for tenant {TenantId}",
            request.PaymentId, tenantSettings.TenantId);

        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        _logger.LogDebug(
            "Found payment {PaymentNumber} with status {Status} for tenant {TenantId}",
            payment.PaymentNumber, payment.Status, tenantSettings.TenantId);

        return payment;
    }
}
