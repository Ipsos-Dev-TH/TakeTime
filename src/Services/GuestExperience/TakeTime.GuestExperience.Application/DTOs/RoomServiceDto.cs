namespace TakeTime.GuestExperience.Application.DTOs;

/// <summary>
/// DTO representing a room service order.
/// </summary>
public sealed class RoomServiceOrderDto
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public Guid? ReservationId { get; set; }
    public string? GuestName { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<RoomServiceItemDto> Items { get; set; } = [];
    public decimal SubTotal { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal VATAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "THB";
    public bool ChargeToRoom { get; set; }
    public string? SpecialInstructions { get; set; }
    public DateTime? EstimatedDeliveryTime { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for a single item in a room service order.
/// </summary>
public sealed class RoomServiceItemDto
{
    public Guid Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? SpecialRequest { get; set; }
}

/// <summary>
/// DTO for creating a new room service order.
/// </summary>
public sealed class CreateRoomServiceOrderDto
{
    public string RoomNumber { get; set; } = string.Empty;
    public Guid? ReservationId { get; set; }
    public string? GuestName { get; set; }
    public List<CreateRoomServiceItemDto> Items { get; set; } = [];
    public bool ChargeToRoom { get; set; } = true;
    public string? SpecialInstructions { get; set; }
}

/// <summary>
/// DTO for creating a room service item.
/// </summary>
public sealed class CreateRoomServiceItemDto
{
    public string ItemName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string? SpecialRequest { get; set; }
}

/// <summary>
/// DTO representing a maintenance request.
/// </summary>
public sealed class MaintenanceRequestDto
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string RequestNumber { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public Guid? AccommodationId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string Currency { get; set; } = "THB";
    public string? ResolutionNotes { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for creating a maintenance request.
/// </summary>
public sealed class CreateMaintenanceRequestDto
{
    public string RoomNumber { get; set; } = string.Empty;
    public Guid? AccommodationId { get; set; }
    public string Category { get; set; } = "General";
    public string Priority { get; set; } = "Normal";
    public string Description { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public decimal? EstimatedCost { get; set; }
}
