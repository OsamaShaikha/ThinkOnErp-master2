namespace ThinkOnErp.Domain.Entities.Inventory.Enums;

/// <summary>
/// Defines the types of warehouse zones.
/// Corresponds to SysCode CODE_MGR = 23.
/// </summary>
public enum ZoneType
{
    Storage = 1,
    Receiving = 2,
    Staging = 3,
    Quarantine = 4,
    Damaged = 5,
    Returns = 6
}
