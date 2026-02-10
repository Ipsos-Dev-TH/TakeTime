namespace TakeTime.ChannelManager.Application.DTOs;

/// <summary>
/// DTO representing a connection to an OTA (Online Travel Agency) channel.
/// </summary>
public sealed class ChannelConnectionDto
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string ChannelType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ApiEndpoint { get; set; }
    public string? PropertyId { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncStatus { get; set; }
    public int SyncErrorCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for creating a new channel connection.
/// </summary>
public sealed class CreateChannelConnectionDto
{
    public string ChannelName { get; set; } = string.Empty;
    public string ChannelType { get; set; } = string.Empty;
    public string? ApiEndpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? PropertyId { get; set; }
    public Dictionary<string, string> Settings { get; set; } = new();
}

/// <summary>
/// DTO for inventory sync results.
/// </summary>
public sealed class InventorySyncResultDto
{
    public Guid ChannelConnectionId { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int RoomsSynced { get; set; }
    public int RatesSynced { get; set; }
    public int AvailabilityDaysSynced { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO for an imported OTA reservation.
/// </summary>
public sealed class ImportedReservationDto
{
    public Guid Id { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public string ExternalReservationId { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; }
    public string? RoomType { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "THB";
    public string Status { get; set; } = string.Empty;
    public Guid? LocalReservationId { get; set; }
    public DateTime ImportedAt { get; set; }
}

/// <summary>
/// DTO for room availability data to be synced to channels.
/// </summary>
public sealed class RoomAvailabilityDto
{
    public Guid AccommodationId { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int AvailableCount { get; set; }
    public decimal Rate { get; set; }
    public string Currency { get; set; } = "THB";
    public int MinStayNights { get; set; } = 1;
    public bool IsClosed { get; set; }
}

/// <summary>
/// DTO for channel sync log entries.
/// </summary>
public sealed class ChannelSyncLogDto
{
    public Guid Id { get; set; }
    public Guid ChannelConnectionId { get; set; }
    public string SyncType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
