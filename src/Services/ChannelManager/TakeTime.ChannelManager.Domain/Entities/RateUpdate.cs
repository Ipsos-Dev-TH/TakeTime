using TakeTime.Core.Domain.Base;

namespace TakeTime.ChannelManager.Domain.Entities;

public class RateUpdate : Entity
{
    public Guid ChannelConnectionId { get; set; }
    public Guid AccommodationId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Rate { get; set; }
    public string Currency { get; set; } = "THB";
    public int Availability { get; set; }
    public int? MinStay { get; set; }
    public int? MaxStay { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Sent, Confirmed, Failed
    public DateTime? SentAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
