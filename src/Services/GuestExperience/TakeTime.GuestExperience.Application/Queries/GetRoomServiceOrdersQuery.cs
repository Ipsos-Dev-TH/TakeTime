using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Queries;

/// <summary>
/// Query to retrieve room service orders with optional filtering by status and room number.
/// </summary>
public sealed class GetRoomServiceOrdersQuery : IRequest<List<RoomServiceOrderDto>>
{
    public string? Status { get; set; }
    public string? RoomNumber { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Handler for <see cref="GetRoomServiceOrdersQuery"/>. Retrieves room service orders
/// from the repository with filtering and pagination support.
/// </summary>
public sealed class GetRoomServiceOrdersQueryHandler : IRequestHandler<GetRoomServiceOrdersQuery, List<RoomServiceOrderDto>>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetRoomServiceOrdersQueryHandler> _logger;

    public GetRoomServiceOrdersQueryHandler(
        IGuestExperienceRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetRoomServiceOrdersQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<List<RoomServiceOrderDto>> Handle(GetRoomServiceOrdersQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Retrieving room service orders for tenant {TenantId}. Status: {Status}, Room: {RoomNumber}",
            tenantId, request.Status, request.RoomNumber);

        var orders = await _repository.GetRoomServiceOrdersAsync(
            tenantId, request.Status, request.Page, request.PageSize, cancellationToken);

        var result = orders.Select(o => new RoomServiceOrderDto
        {
            Id = o.Id,
            TenantId = o.TenantId,
            OrderNumber = o.OrderNumber,
            RoomNumber = o.RoomNumber,
            ReservationId = o.ReservationId,
            GuestName = o.GuestName,
            Status = o.Status,
            Items = o.Items.Select(i => new RoomServiceItemDto
            {
                Id = i.Id,
                ItemName = i.ItemName,
                Category = i.Category,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                SpecialRequest = i.SpecialRequest
            }).ToList(),
            SubTotal = o.SubTotal,
            ServiceCharge = o.ServiceCharge,
            VATAmount = o.VATAmount,
            TotalAmount = o.TotalAmount,
            Currency = o.Currency,
            ChargeToRoom = o.ChargeToRoom,
            SpecialInstructions = o.SpecialInstructions,
            EstimatedDeliveryTime = o.EstimatedDeliveryTime,
            DeliveredAt = o.DeliveredAt,
            CreatedAt = o.CreatedAt
        }).ToList();

        // Apply room number filter in-memory if specified
        if (!string.IsNullOrWhiteSpace(request.RoomNumber))
        {
            result = result.Where(o => o.RoomNumber.Equals(request.RoomNumber, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        _logger.LogInformation("Retrieved {Count} room service orders", result.Count);

        return result;
    }
}
