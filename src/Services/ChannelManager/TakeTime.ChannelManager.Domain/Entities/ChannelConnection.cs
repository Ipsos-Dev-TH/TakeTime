using TakeTime.Core.Domain.Base;

namespace TakeTime.ChannelManager.Domain.Entities;

public class ChannelConnection : AggregateRoot
{
    public string ChannelType { get; set; } = string.Empty; // Agoda, BookingDotCom, Expedia
    public string PropertyId { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? HotelCode { get; set; }
    public string? ChannelHotelId { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string SyncStatus { get; set; } = "Idle";
    public string? LastSyncError { get; set; }
    public List<ChannelRoomMapping> RoomMappings { get; set; } = [];

    public void Activate() { IsActive = true; }
    public void Deactivate() { IsActive = false; }

    public void RecordSync(bool success, string? error = null)
    {
        LastSyncAt = DateTime.UtcNow;
        SyncStatus = success ? "Success" : "Failed";
        LastSyncError = error;
    }
}
