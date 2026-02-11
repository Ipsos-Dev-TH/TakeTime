using TakeTime.Core.Domain.Base;

namespace TakeTime.ChannelManager.Domain.Entities;

public class ChannelRoomMapping : Entity
{
    public Guid ChannelConnectionId { get; set; }
    public Guid LocalAccommodationId { get; set; }
    public string ChannelRoomTypeId { get; set; } = string.Empty;
    public string ChannelRoomTypeName { get; set; } = string.Empty;
    public string? ChannelRatePlanId { get; set; }
    public bool IsActive { get; set; } = true;

    public void Activate() { IsActive = true; }
    public void Deactivate() { IsActive = false; }
}
