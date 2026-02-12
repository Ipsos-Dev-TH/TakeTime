using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Queries;

/// <summary>
/// Query to retrieve the room service menu items available for guest ordering.
/// Returns categorized menu items with pricing and availability.
/// </summary>
public sealed class GetRoomServiceMenuQuery : IRequest<List<RoomServiceMenuItemDto>>
{
    public string? Category { get; set; }
}

/// <summary>
/// Handler for <see cref="GetRoomServiceMenuQuery"/>. Returns the room service menu
/// configured for the current tenant. In a production system, this would pull from
/// the inventory/product catalog; currently provides a configurable default menu.
/// </summary>
public sealed class GetRoomServiceMenuQueryHandler
    : IRequestHandler<GetRoomServiceMenuQuery, List<RoomServiceMenuItemDto>>
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetRoomServiceMenuQueryHandler> _logger;

    public GetRoomServiceMenuQueryHandler(
        ICurrentTenantService currentTenantService,
        ILogger<GetRoomServiceMenuQueryHandler> logger)
    {
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public Task<List<RoomServiceMenuItemDto>> Handle(
        GetRoomServiceMenuQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogDebug("Fetching room service menu for tenant {TenantId}, category: {Category}",
            tenantId, request.Category);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        // Default menu items - in production, these would come from the Inventory service
        var menuItems = new List<RoomServiceMenuItemDto>
        {
            // Breakfast
            new() { Id = Guid.NewGuid(), Name = "American Breakfast", Category = "Breakfast", Description = "Eggs, bacon, toast, and coffee", Price = 350, Currency = currency },
            new() { Id = Guid.NewGuid(), Name = "Thai Breakfast", Category = "Breakfast", Description = "Rice porridge with pork and egg", Price = 250, Currency = currency },
            new() { Id = Guid.NewGuid(), Name = "Continental Breakfast", Category = "Breakfast", Description = "Pastries, fruit, yogurt, and juice", Price = 300, Currency = currency },

            // Main Course
            new() { Id = Guid.NewGuid(), Name = "Pad Thai", Category = "Main Course", Description = "Stir-fried rice noodles with shrimp", Price = 280, Currency = currency },
            new() { Id = Guid.NewGuid(), Name = "Green Curry", Category = "Main Course", Description = "Thai green curry with chicken and rice", Price = 320, Currency = currency },
            new() { Id = Guid.NewGuid(), Name = "Club Sandwich", Category = "Main Course", Description = "Triple-decker club sandwich with fries", Price = 350, Currency = currency },
            new() { Id = Guid.NewGuid(), Name = "Grilled Fish", Category = "Main Course", Description = "Grilled sea bass with vegetables", Price = 450, Currency = currency },

            // Beverages
            new() { Id = Guid.NewGuid(), Name = "Fresh Juice", Category = "Beverages", Description = "Orange, watermelon, or pineapple", Price = 120, Currency = currency },
            new() { Id = Guid.NewGuid(), Name = "Thai Iced Tea", Category = "Beverages", Description = "Traditional Thai iced tea", Price = 80, Currency = currency },
            new() { Id = Guid.NewGuid(), Name = "Coffee", Category = "Beverages", Description = "Espresso, latte, or cappuccino", Price = 100, Currency = currency },
            new() { Id = Guid.NewGuid(), Name = "Bottled Water", Category = "Beverages", Description = "600ml mineral water", Price = 40, Currency = currency },

            // Desserts
            new() { Id = Guid.NewGuid(), Name = "Mango Sticky Rice", Category = "Desserts", Description = "Fresh mango with coconut sticky rice", Price = 180, Currency = currency },
            new() { Id = Guid.NewGuid(), Name = "Ice Cream", Category = "Desserts", Description = "Two scoops - vanilla, chocolate, or strawberry", Price = 120, Currency = currency }
        };

        // Filter by category if specified
        if (!string.IsNullOrEmpty(request.Category))
        {
            menuItems = menuItems
                .Where(m => m.Category?.Equals(request.Category, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
        }

        _logger.LogDebug("Returning {Count} menu items for tenant {TenantId}", menuItems.Count, tenantId);

        return Task.FromResult(menuItems);
    }
}
