using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Exceptions;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Queries;

/// <summary>
/// Query to retrieve a specific room service order by its unique identifier.
/// </summary>
public sealed class GetRoomServiceOrderByIdQuery : IRequest<RoomServiceOrderDto>
{
    public Guid Id { get; set; }
}

/// <summary>
/// Handler for <see cref="GetRoomServiceOrderByIdQuery"/>. Retrieves a single
/// room service order from the repository and maps it to a DTO.
/// </summary>
public sealed class GetRoomServiceOrderByIdQueryHandler : IRequestHandler<GetRoomServiceOrderByIdQuery, RoomServiceOrderDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ILogger<GetRoomServiceOrderByIdQueryHandler> _logger;

    public GetRoomServiceOrderByIdQueryHandler(
        IGuestExperienceRepository repository,
        ILogger<GetRoomServiceOrderByIdQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<RoomServiceOrderDto> Handle(GetRoomServiceOrderByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving room service order {OrderId}", request.Id);

        var order = await _repository.GetRoomServiceOrderByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("RoomServiceOrder", request.Id);

        return new RoomServiceOrderDto
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
}
