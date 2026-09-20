using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvItemCategory
{
    public long Id { get; set; }
    public long? BranchId { get; set; }
    public long? ParentCategoryId { get; set; }
    public long CategoryCode { get; set; }
    public string CategoryNameLocal { get; set; } = string.Empty;
    public string? CategoryNameEn { get; set; }
    public int CategoryLevel { get; set; } = 1;
    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    public bool ShowInPos { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = "SYSTEM";
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public InvItemCategory? ParentCategory { get; set; }
    public List<InvItemCategory> SubCategories { get; set; } = new();
    public List<InvItem> Items { get; set; } = new();

    #region Backward Compatibility Aliases
    public long? ParentGroupId
    {
        get => ParentCategoryId;
        set => ParentCategoryId = value;
    }
    public long GroupCode
    {
        get => CategoryCode;
        set => CategoryCode = value;
    }
    public string GroupNameLocal
    {
        get => CategoryNameLocal;
        set => CategoryNameLocal = value;
    }
    public string? GroupNameEn
    {
        get => CategoryNameEn;
        set => CategoryNameEn = value;
    }
    public int GroupLevel
    {
        get => CategoryLevel;
        set => CategoryLevel = value;
    }
    public InvItemCategory? ParentGroup
    {
        get => ParentCategory;
        set => ParentCategory = value;
    }
    public List<InvItemCategory> SubGroups => SubCategories;
    #endregion
}
