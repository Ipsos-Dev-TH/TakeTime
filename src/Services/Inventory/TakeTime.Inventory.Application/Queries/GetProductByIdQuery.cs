using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Inventory.Application.Commands;
using TakeTime.Inventory.Application.DTOs;

namespace TakeTime.Inventory.Application.Queries;

/// <summary>
/// Query to retrieve a single product by its unique identifier.
/// </summary>
public sealed class GetProductByIdQuery : IRequest<ProductDto>
{
    public Guid ProductId { get; set; }

    public GetProductByIdQuery()
    {
    }

    public GetProductByIdQuery(Guid productId)
    {
        ProductId = productId;
    }
}

/// <summary>
/// Handler for <see cref="GetProductByIdQuery"/>. Retrieves a product by ID,
/// enriches it with low stock flag, scoped to the current tenant.
/// </summary>
public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDto>
{
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetProductByIdQueryHandler> _logger;

    public GetProductByIdQueryHandler(
        IProductRepository productRepository,
        ITenantContext tenantContext,
        ILogger<GetProductByIdQueryHandler> logger)
    {
        _productRepository = productRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ProductDto> Handle(
        GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieving product {ProductId} for tenant {TenantId}",
            request.ProductId, tenantSettings.TenantId);

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"Product {request.ProductId} not found.");

        // Enrich with low stock flag
        product.IsLowStock = product.TrackInventory && product.CurrentStock <= product.MinimumStock;

        _logger.LogDebug(
            "Found product '{ProductName}' (SKU: {SKU}) for tenant {TenantId}",
            product.Name, product.SKU, tenantSettings.TenantId);

        return product;
    }
}
