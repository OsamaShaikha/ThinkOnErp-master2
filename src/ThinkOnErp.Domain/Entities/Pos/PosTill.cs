using System;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosTill
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string TillCode { get; set; } = string.Empty;
    public string TillName { get; set; } = string.Empty;
    public string? MachineIdentifier { get; set; }
    public string? IpAddress { get; set; }
    public decimal DefaultFloatAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigation
    public SysBranch? Branch { get; set; }
}
