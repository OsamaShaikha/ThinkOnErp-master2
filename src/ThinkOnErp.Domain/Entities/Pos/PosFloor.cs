using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosFloor
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string FloorCode { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public SysBranch? Branch { get; set; }
    public ICollection<PosTable> Tables { get; set; } = new List<PosTable>();
}

public class PosTable
{
    public long Id { get; set; }
    public long FloorId { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public int Capacity { get; set; } = 4;
    public PosTableStatus Status { get; set; } = PosTableStatus.Available;

    // Visual layout editor coordinates
    public decimal PositionX { get; set; }
    public decimal PositionY { get; set; }
    public decimal Width { get; set; } = 80;
    public decimal Height { get; set; } = 80;
    public string Shape { get; set; } = "Square"; // Square, Round, Rectangle

    // Active order association
    public long? ActiveOrderId { get; set; }
    public DateTime? StatusChangedAt { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public PosFloor? Floor { get; set; }
    public PosOrderHeader? ActiveOrder { get; set; }
}
