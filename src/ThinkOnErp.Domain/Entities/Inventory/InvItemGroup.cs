using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvItemGroup
{
    public long Id { get; set; }
    public long? BranchId { get; set; }
    public long? ParentGroupId { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string GroupNameAr { get; set; } = string.Empty;
    public string? GroupNameEn { get; set; }
    public int GroupLevel { get; set; } = 1;
    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = "SYSTEM";
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public InvItemGroup? ParentGroup { get; set; }
    public List<InvItemGroup> SubGroups { get; set; } = new();
    public List<InvItem> Items { get; set; } = new();
}
