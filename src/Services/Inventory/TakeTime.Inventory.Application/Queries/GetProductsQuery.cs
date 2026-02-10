using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Inventory.Application.Commands;
using TakeTime.Inventory.Application.DTOs;

namespace TakeTime.Inventory.Application.Queries;

/// <summary>
/// Query to retrieve products with search, filtering, and pagination.
/// </summary>
public sealed class GetProductsQuery : IRequest<PaginatedResult<ProductDto>>
{
    public ProductSearchCriteria Criteria { get; set; } = new();

    public GetProductsQuery()
    {
    }

    public GetProductsQuery(ProductSearchCriteria criteria)
    {
        Criteria = criteria;
    }
}

/// <summary>
/// Handler for <see cref="GetProductsQuery"/>. Applies search, filtering,
/// sorting, and pagination scoped to the current tenant.
/// </summary>
public sealed class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, PaginatedResult<ProductDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetProductsQueryHandler> _logger;

    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name", "SKU", "Category", "SellingPrice", "CostPrice",
        "CurrentStock", "CreatedAt", "UpdatedAt"
    };

    public GetProductsQueryHandler(
        IProductRepository productRepository,
        ITenantContext tenantContext,
        ILogger<GetProductsQueryHandler> logger)
    {
        _productRepository = productRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<PaginatedResult<ProductDto>> Handle(
        GetProductsQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var criteria = request.Criteria;

        // Sanitize pagination
        if (criteria.Page < 1) criteria.Page = 1;
        if (criteria.PageSize < 1) criteria.PageSize = 20;
        if (criteria.PageSize > 100) criteria.PageSize = 100;

        // Validate sort field
        if (!AllowedSortFields.Contains(criteria.SortBy))
        {
            criteria.SortBy = "Name";
        }

        _logger.LogDebug(
            "Searching products for tenant {TenantId}: Term='{SearchTerm}', Category='{Category}', Page={Page}",
            tenantSettings.TenantId, criteria.SearchTerm, criteria.Category, criteria.Page);

        var result = await _productRepository.SearchAsync(criteria, cancellationToken);

        // Enrich with low stock flag
        foreach (var product in result.Items)
        {
            product.IsLowStock = product.TrackInventory && product.CurrentStock <= product.MinimumStock;
        }

        _logger.LogDebug(
            "Found {TotalCount} products matching criteria for tenant {TenantId}",
            result.TotalCount, tenantSettings.TenantId);

        return result;
    }
}
