using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to create a new room service order with support for charging to the guest's room.
/// </summary>
public sealed class CreateRoomServiceOrderCommand : IRequest<RoomServiceOrderDto>
{
    public string RoomNumber { get; set; } = string.Empty;
    public Guid? ReservationId { get; set; }
    public string? GuestName { get; set; }
    public List<CreateRoomServiceItemDto> Items { get; set; } = [];
    public bool ChargeToRoom { get; set; } = true;
    public string? SpecialInstructions { get; set; }
}

/// <summary>
/// Handler for <see cref="CreateRoomServiceOrderCommand"/>. Creates a room service order,
/// calculates totals including service charge and VAT based on tenant settings, and
/// optionally charges the order to the guest's room folio.
/// </summary>
public sealed class CreateRoomServiceOrderCommandHandler : IRequestHandler<CreateRoomServiceOrderCommand, RoomServiceOrderDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<CreateRoomServiceOrderCommandHandler> _logger;

    public CreateRoomServiceOrderCommandHandler(
        IGuestExperienceRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<CreateRoomServiceOrderCommandHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<RoomServiceOrderDto> Handle(CreateRoomServiceOrderCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Creating room service order for room {RoomNumber}",
            request.RoomNumber);

        // Generate order number
        var todayCount = await _repository.GetTodayOrderCountAsync(tenantId, cancellationToken);
        var orderNumber = $"RS-{DateTime.UtcNow:yyyyMMdd}-{(todayCount + 1):D4}";

        // Calculate item totals
        var itemEntries = new List<RoomServiceItemEntry>();
        decimal subTotal = 0;

        foreach (var itemDto in request.Items)
        {
            var totalPrice = itemDto.UnitPrice * itemDto.Quantity;
            subTotal += totalPrice;

            itemEntries.Add(new RoomServiceItemEntry
            {
                ItemName = itemDto.ItemName,
                Category = itemDto.Category,
                Quantity = itemDto.Quantity,
                UnitPrice = itemDto.UnitPrice,
                TotalPrice = totalPrice,
                SpecialRequest = itemDto.SpecialRequest
            });
        }

        // Calculate service charge from tenant settings
        var serviceChargeRateStr = _currentTenantService.GetSetting("ServiceChargeRate");
        var serviceChargeRate = decimal.TryParse(serviceChargeRateStr, out var scr) ? scr : 10m;
        var serviceCharge = Math.Round(subTotal * serviceChargeRate / 100, 2);

        // Calculate VAT
        var vatRateStr = _currentTenantService.GetSetting("VATRate");
        var vatRate = decimal.TryParse(vatRateStr, out var vr) ? vr : 7m;
        var isVATRegistered = _currentTenantService.GetSetting("IsVATRegistered")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

        decimal vatAmount = 0;
        if (isVATRegistered)
        {
            vatAmount = Math.Round((subTotal + serviceCharge) * vatRate / 100, 2);
        }

        var totalAmount = subTotal + serviceCharge + vatAmount;
        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        // Estimate delivery time (30 minutes from now)
        var estimatedDelivery = DateTime.UtcNow.AddMinutes(30);

        var order = new RoomServiceOrderEntry
        {
            TenantId = tenantId,
            OrderNumber = orderNumber,
            RoomNumber = request.RoomNumber,
            ReservationId = request.ReservationId,
            GuestName = request.GuestName,
            Status = "Received",
            SubTotal = subTotal,
            ServiceCharge = serviceCharge,
            VATAmount = vatAmount,
            TotalAmount = totalAmount,
            Currency = currency,
            ChargeToRoom = request.ChargeToRoom,
            SpecialInstructions = request.SpecialInstructions,
            EstimatedDeliveryTime = estimatedDelivery,
            Items = itemEntries
        };

        var id = await _repository.CreateRoomServiceOrderAsync(order, cancellationToken);

        _logger.LogInformation(
            "Room service order {OrderNumber} created for room {RoomNumber}, total: {Total} {Currency}, charge to room: {ChargeToRoom}",
            orderNumber, request.RoomNumber, totalAmount, currency, request.ChargeToRoom);

        return new RoomServiceOrderDto
        {
            Id = id,
            TenantId = tenantId,
            OrderNumber = orderNumber,
            RoomNumber = request.RoomNumber,
            ReservationId = request.ReservationId,
            GuestName = request.GuestName,
            Status = "Received",
            Items = itemEntries.Select(i => new RoomServiceItemDto
            {
                Id = i.Id,
                ItemName = i.ItemName,
                Category = i.Category,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                SpecialRequest = i.SpecialRequest
            }).ToList(),
            SubTotal = subTotal,
            ServiceCharge = serviceCharge,
            VATAmount = vatAmount,
            TotalAmount = totalAmount,
            Currency = currency,
            ChargeToRoom = request.ChargeToRoom,
            SpecialInstructions = request.SpecialInstructions,
            EstimatedDeliveryTime = estimatedDelivery,
            CreatedAt = order.CreatedAt
        };
    }
}
