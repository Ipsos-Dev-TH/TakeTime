using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Exceptions;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to cancel a room service order. Only orders that have not been delivered can be cancelled.
/// </summary>
public sealed class CancelRoomServiceOrderCommand : IRequest<RoomServiceOrderDto>
{
    public Guid OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Handler for <see cref="CancelRoomServiceOrderCommand"/>. Validates the order has not
/// been delivered, sets it to Cancelled, and records the cancellation reason.
/// </summary>
public sealed class CancelRoomServiceOrderCommandHandler : IRequestHandler<CancelRoomServiceOrderCommand, RoomServiceOrderDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ILogger<CancelRoomServiceOrderCommandHandler> _logger;

    public CancelRoomServiceOrderCommandHandler(
        IGuestExperienceRepository repository,
        ILogger<CancelRoomServiceOrderCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<RoomServiceOrderDto> Handle(CancelRoomServiceOrderCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Cancelling room service order {OrderId}, reason: {Reason}",
            request.OrderId, request.Reason);

        var order = await _repository.GetRoomServiceOrderByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("RoomServiceOrder", request.OrderId);

        if (order.Status == "Delivered" || order.Status == "Cancelled")
        {
            throw new InvalidOperationException(
                $"Cannot cancel room service order in '{order.Status}' status.");
        }

        order.Status = "Cancelled";
        order.SpecialInstructions = string.IsNullOrWhiteSpace(order.SpecialInstructions)
            ? $"Cancelled: {request.Reason}"
            : $"{order.SpecialInstructions} | Cancelled: {request.Reason}";

        await _repository.UpdateRoomServiceOrderAsync(order, cancellationToken);

        _logger.LogInformation("Room service order {OrderId} cancelled", request.OrderId);

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
