using MediatR;
using Microsoft.Extensions.Logging;

namespace TakeTime.Inventory.Application.Commands;

/// <summary>
/// Command to soft-delete a product by deactivating it.
/// The product is not physically removed from the database.
/// </summary>
public sealed class DeleteProductCommand : IRequest<Unit>
{
    public Guid ProductId { get; set; }
    public string? DeletedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="DeleteProductCommand"/>. Soft-deletes a product
/// by marking it as inactive and persisting the change.
/// </summary>
public sealed class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Unit>
{
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<DeleteProductCommandHandler> _logger;

    public DeleteProductCommandHandler(
        IProductRepository productRepository,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<DeleteProductCommandHandler> logger)
    {
        _productRepository = productRepository;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"Product {request.ProductId} not found.");

        if (!product.IsActive)
        {
            throw new InvalidOperationException(
                $"Product '{product.Name}' is already inactive/deleted.");
        }

        await _productRepository.DeleteAsync(request.ProductId, cancellationToken);

        _logger.LogInformation(
            "Product '{ProductName}' (SKU: {SKU}) soft-deleted by {DeletedBy} for tenant {TenantId}",
            product.Name, product.SKU, request.DeletedBy, tenantSettings.TenantId);

        // Publish domain event
        await _mediator.Publish(new ProductDeletedEvent
        {
            ProductId = request.ProductId,
            ProductName = product.Name,
            SKU = product.SKU,
            TenantId = tenantSettings.TenantId,
            DeletedBy = request.DeletedBy,
            OccurredOn = DateTime.UtcNow
        }, cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Domain event published when a product is soft-deleted.
/// </summary>
public sealed class ProductDeletedEvent : INotification
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string? DeletedBy { get; set; }
    public DateTime OccurredOn { get; set; }
}
