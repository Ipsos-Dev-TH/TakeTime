using TakeTime.Core.Domain.Base;

namespace TakeTime.GuestExperience.Domain.Entities;

public class RoomServiceOrder : AggregateRoot
{
    public string OrderNumber { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public List<RoomServiceOrderItem> Items { get; set; } = [];
    public decimal SubTotal { get; set; }
    public decimal VATAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "THB";
    public bool ChargeToRoom { get; set; }
    public string Status { get; set; } = "Ordered"; // Ordered, Preparing, Ready, Delivering, Delivered, Cancelled
    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PreparedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? DeliveredBy { get; set; }
    public string? SpecialInstructions { get; set; }

    public void MarkPreparing() { Status = "Preparing"; }
    public void MarkReady() { Status = "Ready"; PreparedAt = DateTime.UtcNow; }

    public void MarkDelivered(string deliveredBy)
    {
        Status = "Delivered";
        DeliveredAt = DateTime.UtcNow;
        DeliveredBy = deliveredBy;
    }

    public void Cancel() { Status = "Cancelled"; }
}

public class RoomServiceOrderItem : Entity
{
    public Guid OrderId { get; set; }
    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Notes { get; set; }
}
