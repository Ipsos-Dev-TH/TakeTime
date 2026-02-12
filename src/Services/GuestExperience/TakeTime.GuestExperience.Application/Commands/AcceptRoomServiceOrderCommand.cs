using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Exceptions;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to accept a room service order and begin processing.
/// </summary>
public sealed class AcceptRoomServiceOrderCommand : IRequest<RoomServiceOrderDto>
{
    public Guid OrderId { get; set; }
}

/// <summary>
/// Handler for <see cref="AcceptRoomServiceOrderCommand"/>. Validates the order is in
/// Received status and sets it to Accepted.
/// </summary>
public sealed class AcceptRoomServiceOrderCommandHandler : IRequestHandler<AcceptRoomServiceOrderCommand, RoomServiceOrderDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ILogger<AcceptRoomServiceOrderCommandHandler> _logger;

    public AcceptRoomServiceOrderCommandHandler(
        IGuestExperienceRepository repository,
        ILogger<AcceptRoomServiceOrderCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<RoomServiceOrderDto> Handle(AcceptRoomServiceOrderCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Accepting room service order {OrderId}", request.OrderId);

        var order = await _repository.GetRoomServiceOrderByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("RoomServiceOrder", request.OrderId);

        if (order.Status != "Received")
        {
            throw new InvalidOperationException(
                $"Cannot accept room service order in '{order.Status}' status. Order must be in Received status.");
        }

        order.Status = "Accepted";

        await _repository.UpdateRoomServiceOrderAsync(order, cancellationToken);

        _logger.LogInformation("Room service order {OrderId} accepted", request.OrderId);

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
