using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Payment.Application.DTOs;
using TakeTime.Payment.Application.Services;

namespace TakeTime.Payment.Application.Commands;

/// <summary>
/// Command to generate a receipt for a payment. The receipt format adjusts
/// based on whether the tenant is VAT registered.
/// </summary>
public sealed class GenerateReceiptCommand : IRequest<ReceiptDto>
{
    public Guid PaymentId { get; set; }
    public Guid ReservationId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerTaxId { get; set; }
    public string ReceiptType { get; set; } = "Receipt";
    public string? IssuedBy { get; set; }
    public decimal? AmountTendered { get; set; }
}

/// <summary>
/// Handler for <see cref="GenerateReceiptCommand"/>. Generates a receipt with
/// correct VAT calculation and display based on tenant settings. Handles both
/// VAT-inclusive and VAT-exclusive pricing models.
/// </summary>
public sealed class GenerateReceiptCommandHandler : IRequestHandler<GenerateReceiptCommand, ReceiptDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IReceiptRepository _receiptRepository;
    private readonly IReservationServiceClient _reservationClient;
    private readonly ITenantContext _tenantContext;
    private readonly VATCalculationService _vatService;
    private readonly ILogger<GenerateReceiptCommandHandler> _logger;

    public GenerateReceiptCommandHandler(
        IPaymentRepository paymentRepository,
        IReceiptRepository receiptRepository,
        IReservationServiceClient reservationClient,
        ITenantContext tenantContext,
        VATCalculationService vatService,
        ILogger<GenerateReceiptCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _receiptRepository = receiptRepository;
        _reservationClient = reservationClient;
        _tenantContext = tenantContext;
        _vatService = vatService;
        _logger = logger;
    }

    public async Task<ReceiptDto> Handle(GenerateReceiptCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var currency = tenantSettings.Currency ?? "THB";

        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        // Get reservation details for line items
        var reservation = await _reservationClient.GetReservationAsync(
            request.ReservationId, cancellationToken)
            ?? throw new InvalidOperationException($"Reservation {request.ReservationId} not found.");

        // Generate receipt number
        var receiptNumber = await GenerateReceiptNumberAsync(tenantSettings.TenantId, cancellationToken);

        // Build line items from reservation
        var lineItems = new List<ReceiptItemDto>();
        var lineNumber = 1;

        foreach (var item in reservation.Items)
        {
            var receiptItem = new ReceiptItemDto
            {
                LineNumber = lineNumber++,
                Description = item.Description,
                ItemCode = item.ItemCode,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice,
                Currency = currency,
                Category = item.Category
            };

            // Calculate per-item VAT if VAT registered
            if (tenantSettings.IsVATRegistered)
            {
                var itemVat = _vatService.CalculateVAT(item.TotalPrice, tenantSettings);
                receiptItem.VATAmount = itemVat.VATAmount;
            }

            lineItems.Add(receiptItem);
        }

        // Calculate receipt totals
        var subTotal = lineItems.Sum(i => i.TotalPrice);
        var vatResult = _vatService.CalculateVAT(payment.Amount, tenantSettings);
        var amountTendered = request.AmountTendered ?? payment.Amount;
        var change = amountTendered - payment.Amount;
        if (change < 0) change = 0;

        // Format display strings
        var vatDisplay = _vatService.FormatVATDisplay(
            vatResult.AmountBeforeVAT, vatResult.VATAmount, tenantSettings);

        var receipt = new ReceiptDto
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = receiptNumber,
            TenantId = tenantSettings.TenantId,
            ReservationId = request.ReservationId,
            ReservationNumber = reservation.ReservationNumber,
            PaymentId = request.PaymentId,
            PaymentNumber = payment.PaymentNumber,

            // Business details
            BusinessName = tenantSettings.BusinessName ?? tenantSettings.TenantName,
            BusinessAddress = tenantSettings.BusinessAddress,
            BusinessTaxId = tenantSettings.BusinessTaxId,
            BusinessPhone = tenantSettings.BusinessPhone,
            BusinessEmail = tenantSettings.BusinessEmail,

            // Customer details
            CustomerName = request.CustomerName ?? reservation.CustomerName,
            CustomerAddress = request.CustomerAddress,
            CustomerTaxId = request.CustomerTaxId,

            // Line items
            Items = lineItems,

            // Totals
            SubTotal = subTotal,
            DiscountAmount = 0,
            AmountBeforeVAT = vatResult.AmountBeforeVAT,
            VATRate = tenantSettings.VATRate,
            VATAmount = vatResult.VATAmount,
            TotalAmount = payment.Amount,
            Currency = currency,
            IsVATInclusive = tenantSettings.IsVATInclusive,
            IsVATRegistered = tenantSettings.IsVATRegistered,

            // Payment info
            PaymentMethod = payment.PaymentMethod,
            AmountPaid = amountTendered,
            Change = change,

            // Metadata
            ReceiptType = request.ReceiptType,
            IssuedDate = DateTime.UtcNow,
            IssuedBy = request.IssuedBy,
            CreatedAt = DateTime.UtcNow,
            VATRegistrationNumber = tenantSettings.VATRegistrationNumber,

            // Formatted strings
            FormattedSubTotal = FormatCurrency(vatResult.AmountBeforeVAT, currency),
            FormattedVATAmount = FormatCurrency(vatResult.VATAmount, currency),
            FormattedTotal = FormatCurrency(payment.Amount, currency)
        };

        // Persist the receipt
        await _receiptRepository.CreateAsync(receipt, cancellationToken);

        _logger.LogInformation(
            "Receipt {ReceiptNumber} generated for payment {PaymentNumber}, tenant {TenantId}. VAT: {VATAmount}",
            receiptNumber, payment.PaymentNumber, tenantSettings.TenantId, vatResult.VATAmount);

        return receipt;
    }

    private async Task<string> GenerateReceiptNumberAsync(string tenantId, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow;
        var prefix = $"RCP-{today:yyyyMMdd}";
        var count = await _receiptRepository.GetTodayReceiptCountAsync(tenantId, cancellationToken);
        return $"{prefix}-{(count + 1):D4}";
    }

    private static string FormatCurrency(decimal amount, string currency)
    {
        return currency == "THB"
            ? $"\u0E3F{amount:N2}"
            : $"{currency} {amount:N2}";
    }
}
