using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Inventory.Application.DTOs;

namespace TakeTime.Inventory.Application.Commands;

/// <summary>
/// Command to create a POS sales transaction. Processes the cart, calculates
/// totals with VAT support, deducts stock, and records the transaction.
/// </summary>
public sealed class CreateSalesTransactionCommand : IRequest<SalesTransactionDto>
{
    public Guid? ReservationId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public decimal? DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public List<POSCartItemDto> Items { get; set; } = [];
    public string? ProcessedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="CreateSalesTransactionCommand"/>. Validates cart items,
/// calculates line totals with VAT based on tenant settings, deducts inventory,
/// and records the transaction.
/// </summary>
public sealed class CreateSalesTransactionCommandHandler
    : IRequestHandler<CreateSalesTransactionCommand, SalesTransactionDto>
{
    private readonly IProductRepository _productRepository;
    private readonly ISalesTransactionRepository _salesRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateSalesTransactionCommandHandler> _logger;

    public CreateSalesTransactionCommandHandler(
        IProductRepository productRepository,
        ISalesTransactionRepository salesRepository,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<CreateSalesTransactionCommandHandler> logger)
    {
        _productRepository = productRepository;
        _salesRepository = salesRepository;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<SalesTransactionDto> Handle(
        CreateSalesTransactionCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var currency = tenantSettings.Currency ?? "THB";

        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Sales transaction must contain at least one item.");
        }

        // Process cart items
        var lineItems = new List<SalesTransactionItemDto>();
        decimal subTotal = 0;

        foreach (var cartItem in request.Items)
        {
            var product = await _productRepository.GetByIdAsync(cartItem.ProductId, cancellationToken)
                ?? throw new InvalidOperationException($"Product {cartItem.ProductId} not found.");

            if (!product.IsActive)
            {
                throw new InvalidOperationException($"Product '{product.Name}' is not active.");
            }

            // Validate stock if tracking is enabled
            if (product.TrackInventory && product.CurrentStock < cartItem.Quantity)
            {
                throw new InvalidOperationException(
                    $"Insufficient stock for '{product.Name}'. " +
                    $"Available: {product.CurrentStock}, Requested: {cartItem.Quantity}.");
            }

            var unitPrice = cartItem.PriceOverride ?? product.SellingPrice;
            var itemDiscount = cartItem.DiscountAmount ?? 0;
            var lineTotal = (unitPrice * cartItem.Quantity) - itemDiscount;

            // Calculate VAT per item if applicable
            decimal? itemVAT = null;
            if (tenantSettings.IsVATRegistered && !product.IsVATExempt)
            {
                if (tenantSettings.IsVATInclusive)
                {
                    itemVAT = lineTotal - (lineTotal / (1 + tenantSettings.VATRate / 100m));
                }
                else
                {
                    itemVAT = lineTotal * tenantSettings.VATRate / 100m;
                }
                itemVAT = Math.Round(itemVAT.Value, 2);
            }

            subTotal += lineTotal;

            lineItems.Add(new SalesTransactionItemDto
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductName = product.Name,
                SKU = product.SKU,
                Quantity = cartItem.Quantity,
                UnitPrice = unitPrice,
                DiscountAmount = itemDiscount,
                TotalPrice = lineTotal,
                VATAmount = itemVAT,
                Currency = currency
            });
        }

        // Calculate transaction totals
        var transactionDiscount = request.DiscountAmount ?? 0;
        var afterDiscount = subTotal - transactionDiscount;

        decimal vatAmount = 0;
        if (tenantSettings.IsVATRegistered)
        {
            var taxableItems = lineItems.Where(i =>
            {
                var product = request.Items.First(ci => ci.ProductId == i.ProductId);
                return i.VATAmount.HasValue;
            });

            vatAmount = taxableItems.Sum(i => i.VATAmount ?? 0);
        }

        var totalAmount = tenantSettings.IsVATInclusive
            ? afterDiscount
            : afterDiscount + vatAmount;

        // Generate transaction number
        var transactionNumber = await GenerateTransactionNumberAsync(
            tenantSettings.TenantId, cancellationToken);

        var transaction = new SalesTransactionDto
        {
            Id = Guid.NewGuid(),
            TransactionNumber = transactionNumber,
            TenantId = tenantSettings.TenantId,
            ReservationId = request.ReservationId,
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            SubTotal = subTotal,
            DiscountAmount = transactionDiscount,
            VATAmount = vatAmount,
            TotalAmount = Math.Round(totalAmount, 2),
            Currency = currency,
            PaymentMethod = request.PaymentMethod,
            Status = "Completed",
            Notes = request.Notes,
            IsVATInclusive = tenantSettings.IsVATInclusive,
            VATRate = tenantSettings.VATRate,
            Items = lineItems,
            TransactionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.ProcessedBy
        };

        // Persist the transaction
        await _salesRepository.CreateAsync(transaction, cancellationToken);

        // Deduct stock for each item
        foreach (var item in lineItems)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken);
            if (product is not null && product.TrackInventory)
            {
                await _productRepository.AdjustStockAsync(
                    item.ProductId, -item.Quantity,
                    "Sale", transactionNumber, request.ProcessedBy,
                    cancellationToken);
            }
        }

        _logger.LogInformation(
            "Sales transaction {TransactionNumber} created: {Total} {Currency} " +
            "({ItemCount} items) for tenant {TenantId}",
            transactionNumber, totalAmount, currency,
            lineItems.Count, tenantSettings.TenantId);

        // Publish domain event
        await _mediator.Publish(new SalesTransactionCreatedEvent
        {
            TransactionId = transaction.Id,
            TransactionNumber = transactionNumber,
            TenantId = tenantSettings.TenantId,
            TotalAmount = totalAmount,
            ReservationId = request.ReservationId,
            Currency = currency,
            OccurredOn = DateTime.UtcNow
        }, cancellationToken);

        return transaction;
    }

    private async Task<string> GenerateTransactionNumberAsync(
        string tenantId, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow;
        var prefix = $"POS-{today:yyyyMMdd}";
        var count = await _salesRepository.GetTodayTransactionCountAsync(tenantId, cancellationToken);
        return $"{prefix}-{(count + 1):D4}";
    }
}

/// <summary>
/// Domain event published when a sales transaction is created.
/// </summary>
public sealed class SalesTransactionCreatedEvent : INotification
{
    public Guid TransactionId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public Guid? ReservationId { get; set; }
    public string Currency { get; set; } = "THB";
    public DateTime OccurredOn { get; set; }
}

/// <summary>
/// Tenant context interface for Inventory service.
/// </summary>
public interface ITenantContext
{
    string TenantId { get; }
    Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Tenant settings relevant to the Inventory service.
/// </summary>
public class TenantSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string? Currency { get; set; } = "THB";
    public bool IsVATRegistered { get; set; }
    public decimal VATRate { get; set; } = 7;
    public bool IsVATInclusive { get; set; }
}

/// <summary>
/// Repository interface for product operations.
/// </summary>
public interface IProductRepository
{
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task CreateAsync(ProductDto product, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProductDto product, CancellationToken cancellationToken = default);

    Task<PaginatedResult<ProductDto>> SearchAsync(
        ProductSearchCriteria criteria, CancellationToken cancellationToken = default);

    Task AdjustStockAsync(
        Guid productId, int quantityChange, string adjustmentType,
        string? reference, string? adjustedBy,
        CancellationToken cancellationToken = default);

    Task<int> GetCurrentStockAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<string> GenerateSKUAsync(string category, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for sales transaction operations.
/// </summary>
public interface ISalesTransactionRepository
{
    Task CreateAsync(SalesTransactionDto transaction, CancellationToken cancellationToken = default);
    Task<int> GetTodayTransactionCountAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<SalesReportDto> GetSalesReportAsync(
        DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    Task<List<SalesTransactionDto>> GetTransactionsAsync(
        DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}
