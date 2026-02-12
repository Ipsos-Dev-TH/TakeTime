using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Exceptions;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to mark a room service order as being prepared.
/// </summary>
public sealed class PrepareRoomServiceOrderCommand : IRequest<RoomServiceOrderDto>
{
    public Guid OrderId { get; set; }
    public DateTime? EstimatedDeliveryTime { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Handler for <see cref="PrepareRoomServiceOrderCommand"/>. Validates the order is in
/// Accepted status, sets it to Preparing, and optionally updates estimated delivery time.
/// </summary>
public sealed class PrepareRoomServiceOrderCommandHandler : IRequestHandler<PrepareRoomServiceOrderCommand, RoomServiceOrderDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ILogger<PrepareRoomServiceOrderCommandHandler> _logger;

    public PrepareRoomServiceOrderCommandHandler(
        IGuestExperienceRepository repository,
        ILogger<PrepareRoomServiceOrderCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<RoomServiceOrderDto> Handle(PrepareRoomServiceOrderCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Marking room service order {OrderId} as preparing", request.OrderId);

        var order = await _repository.GetRoomServiceOrderByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("RoomServiceOrder", request.OrderId);

        if (order.Status != "Accepted")
        {
            throw new InvalidOperationException(
                $"Cannot prepare room service order in '{order.Status}' status. Order must be in Accepted status.");
        }

        order.Status = "Preparing";
        if (request.EstimatedDeliveryTime.HasValue)
        {
            order.EstimatedDeliveryTime = request.EstimatedDeliveryTime.Value;
        }

        await _repository.UpdateRoomServiceOrderAsync(order, cancellationToken);

        _logger.LogInformation("Room service order {OrderId} is now preparing", request.OrderId);

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
