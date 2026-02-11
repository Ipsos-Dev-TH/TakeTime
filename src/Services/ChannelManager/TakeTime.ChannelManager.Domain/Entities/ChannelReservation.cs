using TakeTime.Core.Domain.Base;

namespace TakeTime.ChannelManager.Domain.Entities;

public class ChannelReservation : Entity
{
    public Guid ChannelConnectionId { get; set; }
    public string ChannelBookingId { get; set; } = string.Empty;
    public Guid? LocalReservationId { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string? GuestEmail { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public string? RoomType { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "THB";
    public string Status { get; set; } = "New";
    public string? RawData { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SyncedAt { get; set; }
}
