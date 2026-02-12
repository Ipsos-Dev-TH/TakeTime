using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Inventory.Application.DTOs;

namespace TakeTime.Inventory.Application.Commands;

/// <summary>
/// Command to update an existing product's details. Only provided (non-null)
/// fields are updated; null fields are left unchanged.
/// </summary>
public sealed class UpdateProductCommand : IRequest<ProductDto>
{
    public Guid ProductId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Barcode { get; set; }
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? SellingPrice { get; set; }
    public int? MinimumStock { get; set; }
    public int? MaximumStock { get; set; }
    public string? UnitOfMeasure { get; set; }
    public bool? IsActive { get; set; }
    public bool? TrackInventory { get; set; }
    public string? ImageUrl { get; set; }
    public string? Supplier { get; set; }
    public bool? IsVATExempt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="UpdateProductCommand"/>. Retrieves the product,
/// applies the partial update, validates changed fields, and persists the changes.
/// </summary>
public sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, ProductDto>
{
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdateProductCommandHandler> _logger;

    public UpdateProductCommandHandler(
        IProductRepository productRepository,
        ITenantContext tenantContext,
        ILogger<UpdateProductCommandHandler> logger)
    {
        _productRepository = productRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ProductDto> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"Product {request.ProductId} not found.");

        // Apply partial updates - only update provided fields
        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new InvalidOperationException("Product name cannot be empty.");
            product.Name = request.Name;
        }

        if (request.Description is not null)
            product.Description = request.Description;

        if (request.Barcode is not null)
            product.Barcode = request.Barcode;

        if (request.Category is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Category))
                throw new InvalidOperationException("Product category cannot be empty.");
            product.Category = request.Category;
        }

        if (request.SubCategory is not null)
            product.SubCategory = request.SubCategory;

        if (request.CostPrice.HasValue)
        {
            if (request.CostPrice.Value < 0)
                throw new InvalidOperationException("Cost price cannot be negative.");
            product.CostPrice = request.CostPrice.Value;
        }

        if (request.SellingPrice.HasValue)
        {
            if (request.SellingPrice.Value < 0)
                throw new InvalidOperationException("Selling price cannot be negative.");
            product.SellingPrice = request.SellingPrice.Value;
        }

        if (request.MinimumStock.HasValue)
        {
            if (request.MinimumStock.Value < 0)
                throw new InvalidOperationException("Minimum stock cannot be negative.");
            product.MinimumStock = request.MinimumStock.Value;
        }

        if (request.MaximumStock.HasValue)
        {
            if (request.MaximumStock.Value < 0)
                throw new InvalidOperationException("Maximum stock cannot be negative.");
            product.MaximumStock = request.MaximumStock.Value;
        }

        if (request.UnitOfMeasure is not null)
            product.UnitOfMeasure = request.UnitOfMeasure;

        if (request.IsActive.HasValue)
            product.IsActive = request.IsActive.Value;

        if (request.TrackInventory.HasValue)
            product.TrackInventory = request.TrackInventory.Value;

        if (request.ImageUrl is not null)
            product.ImageUrl = request.ImageUrl;

        if (request.Supplier is not null)
            product.Supplier = request.Supplier;

        if (request.IsVATExempt.HasValue)
        {
            product.IsVATExempt = request.IsVATExempt.Value;
            product.VATRate = request.IsVATExempt.Value ? null : tenantSettings.VATRate;
        }

        product.UpdatedAt = DateTime.UtcNow;

        // Recalculate low stock flag
        product.IsLowStock = product.TrackInventory && product.CurrentStock <= product.MinimumStock;

        await _productRepository.UpdateAsync(product, cancellationToken);

        _logger.LogInformation(
            "Product '{ProductName}' (SKU: {SKU}) updated by {UpdatedBy} for tenant {TenantId}",
            product.Name, product.SKU, request.UpdatedBy, tenantSettings.TenantId);

        return product;
    }
}
