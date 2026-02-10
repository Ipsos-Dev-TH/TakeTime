namespace TakeTime.Inventory.Domain.Enums;

/// <summary>
/// Determines how a product can be charged to a customer.
/// </summary>
public enum ChargeType
{
    /// <summary>
    /// Normal point-of-sale transaction only.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Can only be charged to a guest's room.
    /// </summary>
    RoomCharge = 1,

    /// <summary>
    /// Can be sold normally or charged to a guest's room.
    /// </summary>
    Both = 2
}
