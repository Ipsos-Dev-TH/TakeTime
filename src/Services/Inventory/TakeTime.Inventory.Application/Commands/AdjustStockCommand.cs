using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Inventory.Application.DTOs;

namespace TakeTime.Inventory.Application.Commands;

/// <summary>
/// Command to adjust the stock level of a product. Supports additions (receiving),
/// subtractions (damage, loss), and corrections.
/// </summary>
public sealed class AdjustStockCommand : IRequest<StockAdjustmentDto>
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string AdjustmentType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? AdjustedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="AdjustStockCommand"/>. Validates the adjustment,
/// updates stock levels, and records the adjustment for audit purposes.
/// </summary>
public sealed class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, StockAdjustmentDto>
{
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<AdjustStockCommandHandler> _logger;

    private static readonly HashSet<string> ValidAdjustmentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Received",
        "Returned",
        "Damaged",
        "Lost",
        "Correction",
        "Transfer",
        "WriteOff",
        "InitialStock"
    };

    public AdjustStockCommandHandler(
        IProductRepository productRepository,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<AdjustStockCommandHandler> logger)
    {
        _productRepository = productRepository;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<StockAdjustmentDto> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        // Validate adjustment type
        if (!ValidAdjustmentTypes.Contains(request.AdjustmentType))
        {
            throw new InvalidOperationException(
                $"Invalid adjustment type '{request.AdjustmentType}'. " +
                $"Valid types: {string.Join(", ", ValidAdjustmentTypes)}.");
        }

        if (request.Quantity == 0)
        {
            throw new InvalidOperationException("Adjustment quantity cannot be zero.");
        }

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"Product {request.ProductId} not found.");

        if (!product.TrackInventory)
        {
            throw new InvalidOperationException(
                $"Product '{product.Name}' does not track inventory. Stock adjustments are not applicable.");
        }

        var currentStock = await _productRepository.GetCurrentStockAsync(request.ProductId, cancellationToken);

        // Determine the actual quantity change based on adjustment type
        var quantityChange = request.AdjustmentType switch
        {
            "Received" or "Returned" or "Correction" or "InitialStock" => Math.Abs(request.Quantity),
            "Damaged" or "Lost" or "WriteOff" or "Transfer" => -Math.Abs(request.Quantity),
            _ => request.Quantity
        };

        var newStock = currentStock + quantityChange;

        // Validate stock won't go negative
        if (newStock < 0)
        {
            throw new InvalidOperationException(
                $"Stock adjustment would result in negative stock for '{product.Name}'. " +
                $"Current: {currentStock}, Adjustment: {quantityChange}.");
        }

        // Apply the adjustment
        await _productRepository.AdjustStockAsync(
            request.ProductId, quantityChange, request.AdjustmentType,
            request.Reference, request.AdjustedBy, cancellationToken);

        _logger.LogInformation(
            "Stock adjusted for product '{ProductName}' ({SKU}): {Before} -> {After} " +
            "({Type}: {Quantity}) by {AdjustedBy} for tenant {TenantId}",
            product.Name, product.SKU, currentStock, newStock,
            request.AdjustmentType, quantityChange,
            request.AdjustedBy, tenantSettings.TenantId);

        // Check if stock is now below minimum
        if (newStock <= product.MinimumStock)
        {
            await _mediator.Publish(new LowStockAlertEvent
            {
                ProductId = product.Id,
                ProductName = product.Name,
                SKU = product.SKU,
                CurrentStock = newStock,
                MinimumStock = product.MinimumStock,
                TenantId = tenantSettings.TenantId,
                OccurredOn = DateTime.UtcNow
            }, cancellationToken);
        }

        return new StockAdjustmentDto
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            ProductName = product.Name,
            SKU = product.SKU,
            AdjustmentType = request.AdjustmentType,
            QuantityBefore = currentStock,
            QuantityAdjusted = quantityChange,
            QuantityAfter = newStock,
            Reason = request.Reason,
            Reference = request.Reference,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.AdjustedBy
        };
    }
}

/// <summary>
/// Domain event published when a product's stock falls below minimum.
/// </summary>
public sealed class LowStockAlertEvent : INotification
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int MinimumStock { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public DateTime OccurredOn { get; set; }
}
