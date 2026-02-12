namespace TakeTime.GuestExperience.Application.DTOs;

/// <summary>
/// DTO containing hotel information for the guest self-service portal.
/// Includes about info, facilities, nearby places, and emergency contacts.
/// </summary>
public sealed class HotelInfoDto
{
    public string HotelName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? CheckInTime { get; set; }
    public string? CheckOutTime { get; set; }
    public List<FacilityDto> Facilities { get; set; } = [];
    public List<NearbyPlaceDto> NearbyPlaces { get; set; } = [];
    public List<EmergencyContactDto> EmergencyContacts { get; set; } = [];
    public Dictionary<string, string> AdditionalInfo { get; set; } = new();
}

/// <summary>
/// Hotel facility information.
/// </summary>
public sealed class FacilityDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? OperatingHours { get; set; }
    public string? Location { get; set; }
    public bool IsComplimentary { get; set; }
}

/// <summary>
/// Information about a nearby place of interest.
/// </summary>
public sealed class NearbyPlaceDto
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Distance { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Emergency contact information for guests.
/// </summary>
public sealed class EmergencyContactDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// DTO for a room service menu item available through the guest portal.
/// </summary>
public sealed class RoomServiceMenuItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "THB";
    public bool IsAvailable { get; set; } = true;
    public string? ImageUrl { get; set; }
}

/// <summary>
/// DTO for submitting a guest review through the portal.
/// </summary>
public sealed class SubmitGuestReviewDto
{
    public string RoomNumber { get; set; } = string.Empty;
    public string? GuestName { get; set; }
    public int OverallRating { get; set; }
    public int? CleanlinessRating { get; set; }
    public int? ServiceRating { get; set; }
    public int? FacilitiesRating { get; set; }
    public int? LocationRating { get; set; }
    public int? ValueRating { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// Confirmation DTO returned after a guest review is submitted.
/// </summary>
public sealed class GuestReviewConfirmationDto
{
    public Guid ReviewId { get; set; }
    public string? GuestName { get; set; }
    public int OverallRating { get; set; }
    public string Status { get; set; } = "Submitted";
    public string Message { get; set; } = "Thank you for your review!";
    public DateTime SubmittedAt { get; set; }
}

/// <summary>
/// Request DTO for guest housekeeping request through the portal.
/// Simplified compared to the staff-facing command.
/// </summary>
public sealed class GuestHousekeepingRequestDto
{
    public string RoomNumber { get; set; } = string.Empty;
    public string? RequestType { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Request DTO for guest room service order through the portal.
/// </summary>
public sealed class GuestRoomServiceRequestDto
{
    public string RoomNumber { get; set; } = string.Empty;
    public string? GuestName { get; set; }
    public List<GuestRoomServiceItemDto> Items { get; set; } = [];
    public string? SpecialInstructions { get; set; }
}

/// <summary>
/// Simplified room service item DTO for guest portal orders.
/// </summary>
public sealed class GuestRoomServiceItemDto
{
    public string ItemName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string? SpecialRequest { get; set; }
}

/// <summary>
/// Request DTO for guest maintenance report through the portal.
/// </summary>
public sealed class GuestMaintenanceRequestDto
{
    public string RoomNumber { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
}
