using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Inventory.Application.Commands;
using TakeTime.Inventory.Application.DTOs;

namespace TakeTime.Inventory.Application.Queries;

/// <summary>
/// Query to retrieve the stock movement history for a specific product,
/// optionally filtered by date range.
/// </summary>
public sealed class GetStockHistoryQuery : IRequest<List<StockMovementDto>>
{
    public Guid ProductId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

/// <summary>
/// Handler for <see cref="GetStockHistoryQuery"/>. Retrieves stock movement
/// history for a product, validates the product exists, and applies date filters.
/// </summary>
public sealed class GetStockHistoryQueryHandler
    : IRequestHandler<GetStockHistoryQuery, List<StockMovementDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetStockHistoryQueryHandler> _logger;

    public GetStockHistoryQueryHandler(
        IProductRepository productRepository,
        ITenantContext tenantContext,
        ILogger<GetStockHistoryQueryHandler> logger)
    {
        _productRepository = productRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<StockMovementDto>> Handle(
        GetStockHistoryQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        // Validate product exists
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"Product {request.ProductId} not found.");

        _logger.LogDebug(
            "Retrieving stock history for product '{ProductName}' ({ProductId}) " +
            "from {From} to {To} for tenant {TenantId}",
            product.Name, request.ProductId, request.FromDate, request.ToDate,
            tenantSettings.TenantId);

        var history = await _productRepository.GetStockHistoryAsync(
            request.ProductId, request.FromDate, request.ToDate, cancellationToken);

        _logger.LogDebug(
            "Found {Count} stock movements for product '{ProductName}' for tenant {TenantId}",
            history.Count, product.Name, tenantSettings.TenantId);

        return history;
    }
}
