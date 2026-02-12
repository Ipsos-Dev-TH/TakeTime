using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Inventory.Application.Commands;
using TakeTime.Inventory.Application.DTOs;

namespace TakeTime.Inventory.Application.Queries;

/// <summary>
/// Query to retrieve all distinct product categories with counts of products,
/// active products, and low-stock products in each category.
/// </summary>
public sealed class GetProductCategoriesQuery : IRequest<List<CategorySummaryDto>>
{
}

/// <summary>
/// Handler for <see cref="GetProductCategoriesQuery"/>. Retrieves distinct
/// product categories with summary counts, scoped to the current tenant.
/// </summary>
public sealed class GetProductCategoriesQueryHandler
    : IRequestHandler<GetProductCategoriesQuery, List<CategorySummaryDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetProductCategoriesQueryHandler> _logger;

    public GetProductCategoriesQueryHandler(
        IProductRepository productRepository,
        ITenantContext tenantContext,
        ILogger<GetProductCategoriesQueryHandler> logger)
    {
        _productRepository = productRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<CategorySummaryDto>> Handle(
        GetProductCategoriesQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieving product categories for tenant {TenantId}",
            tenantSettings.TenantId);

        var categories = await _productRepository.GetCategoriesAsync(cancellationToken);

        _logger.LogDebug(
            "Found {Count} product categories for tenant {TenantId}",
            categories.Count, tenantSettings.TenantId);

        return categories.OrderBy(c => c.Category).ToList();
    }
}
