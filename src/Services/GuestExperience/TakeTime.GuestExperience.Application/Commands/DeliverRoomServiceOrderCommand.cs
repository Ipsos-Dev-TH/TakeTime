using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Exceptions;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to mark a room service order as delivered to the guest.
/// </summary>
public sealed class DeliverRoomServiceOrderCommand : IRequest<RoomServiceOrderDto>
{
    public Guid OrderId { get; set; }
}

/// <summary>
/// Handler for <see cref="DeliverRoomServiceOrderCommand"/>. Validates the order is in
/// Preparing status, sets it to Delivered, and records the DeliveredAt timestamp.
/// </summary>
public sealed class DeliverRoomServiceOrderCommandHandler : IRequestHandler<DeliverRoomServiceOrderCommand, RoomServiceOrderDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ILogger<DeliverRoomServiceOrderCommandHandler> _logger;

    public DeliverRoomServiceOrderCommandHandler(
        IGuestExperienceRepository repository,
        ILogger<DeliverRoomServiceOrderCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<RoomServiceOrderDto> Handle(DeliverRoomServiceOrderCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Marking room service order {OrderId} as delivered", request.OrderId);

        var order = await _repository.GetRoomServiceOrderByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("RoomServiceOrder", request.OrderId);

        if (order.Status != "Preparing")
        {
            throw new InvalidOperationException(
                $"Cannot deliver room service order in '{order.Status}' status. Order must be in Preparing status.");
        }

        order.Status = "Delivered";
        order.DeliveredAt = DateTime.UtcNow;

        await _repository.UpdateRoomServiceOrderAsync(order, cancellationToken);

        _logger.LogInformation(
            "Room service order {OrderId} delivered at {DeliveredAt}",
            request.OrderId, order.DeliveredAt);

        return MapToDto(order);
    }

    private static RoomServiceOrderDto MapToDto(RoomServiceOrderEntry order) => new()
    {
        Id = order.Id,
        TenantId = order.TenantId,
        OrderNumber = order.OrderNumber,
        RoomNumber = order.RoomNumber,
        ReservationId = order.ReservationId,
        GuestName = order.GuestName,
        Status = order.Status,
        Items = order.Items.Select(i => new RoomServiceItemDto
        {
            Id = i.Id,
            ItemName = i.ItemName,
            Category = i.Category,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            TotalPrice = i.TotalPrice,
            SpecialRequest = i.SpecialRequest
        }).ToList(),
        SubTotal = order.SubTotal,
        ServiceCharge = order.ServiceCharge,
        VATAmount = order.VATAmount,
        TotalAmount = order.TotalAmount,
        Currency = order.Currency,
        ChargeToRoom = order.ChargeToRoom,
        SpecialInstructions = order.SpecialInstructions,
        EstimatedDeliveryTime = order.EstimatedDeliveryTime,
        DeliveredAt = order.DeliveredAt,
        CreatedAt = order.CreatedAt
    };
}
