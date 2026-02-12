using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Inventory.Application.DTOs;

namespace TakeTime.Inventory.Application.Commands;

/// <summary>
/// Command to create a new product in the inventory. Validates input,
/// generates a SKU if not provided, and records the initial stock.
/// </summary>
public sealed class CreateProductCommand : IRequest<ProductDto>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SKU { get; set; }
    public string? Barcode { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int InitialStock { get; set; }
    public int MinimumStock { get; set; }
    public int MaximumStock { get; set; } = 9999;
    public string UnitOfMeasure { get; set; } = "Piece";
    public bool TrackInventory { get; set; } = true;
    public string? ImageUrl { get; set; }
    public string? Supplier { get; set; }
    public bool IsVATExempt { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="CreateProductCommand"/>. Validates the product data,
/// generates a SKU if not provided, creates the product record, and publishes
/// a ProductCreatedEvent.
/// </summary>
public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ProductDto>
{
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateProductCommandHandler> _logger;

    public CreateProductCommandHandler(
        IProductRepository productRepository,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<CreateProductCommandHandler> logger)
    {
        _productRepository = productRepository;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var currency = tenantSettings.Currency ?? "THB";

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Product name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            throw new InvalidOperationException("Product category is required.");
        }

        if (request.SellingPrice < 0)
        {
            throw new InvalidOperationException("Selling price cannot be negative.");
        }

        if (request.CostPrice < 0)
        {
            throw new InvalidOperationException("Cost price cannot be negative.");
        }

        if (request.InitialStock < 0)
        {
            throw new InvalidOperationException("Initial stock cannot be negative.");
        }

        if (request.MinimumStock < 0)
        {
            throw new InvalidOperationException("Minimum stock cannot be negative.");
        }

        // Generate SKU if not provided
        var sku = request.SKU;
        if (string.IsNullOrWhiteSpace(sku))
        {
            sku = await _productRepository.GenerateSKUAsync(request.Category, cancellationToken);
        }

        // Determine VAT rate
        decimal? vatRate = null;
        if (tenantSettings.IsVATRegistered && !request.IsVATExempt)
        {
            vatRate = tenantSettings.VATRate;
        }

        var product = new ProductDto
        {
            Id = Guid.NewGuid(),
            TenantId = tenantSettings.TenantId,
            Name = request.Name,
            Description = request.Description,
            SKU = sku,
            Barcode = request.Barcode,
            Category = request.Category,
            SubCategory = request.SubCategory,
            CostPrice = request.CostPrice,
            SellingPrice = request.SellingPrice,
            Currency = currency,
            CurrentStock = request.InitialStock,
            MinimumStock = request.MinimumStock,
            MaximumStock = request.MaximumStock,
            UnitOfMeasure = request.UnitOfMeasure,
            IsActive = true,
            TrackInventory = request.TrackInventory,
            IsLowStock = request.TrackInventory && request.InitialStock <= request.MinimumStock,
            ImageUrl = request.ImageUrl,
            Supplier = request.Supplier,
            VATRate = vatRate,
            IsVATExempt = request.IsVATExempt,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy
        };

        await _productRepository.CreateAsync(product, cancellationToken);

        _logger.LogInformation(
            "Product '{ProductName}' (SKU: {SKU}) created with initial stock {Stock} " +
            "by {CreatedBy} for tenant {TenantId}",
            product.Name, sku, request.InitialStock,
            request.CreatedBy, tenantSettings.TenantId);

        // Publish domain event
        await _mediator.Publish(new ProductCreatedEvent
        {
            ProductId = product.Id,
            ProductName = product.Name,
            SKU = sku,
            Category = request.Category,
            SellingPrice = request.SellingPrice,
            InitialStock = request.InitialStock,
            TenantId = tenantSettings.TenantId,
            Currency = currency,
            OccurredOn = DateTime.UtcNow
        }, cancellationToken);

        return product;
    }
}

/// <summary>
/// Domain event published when a new product is created.
/// </summary>
public sealed class ProductCreatedEvent : INotification
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    public int InitialStock { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Currency { get; set; } = "THB";
    public DateTime OccurredOn { get; set; }
}
