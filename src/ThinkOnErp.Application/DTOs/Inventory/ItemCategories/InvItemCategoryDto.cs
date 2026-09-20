using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.ItemCategories;

public sealed class InvItemCategoryDto
{
    public long Id { get; set; }
    public long CategoryId => Id;
    public long? BranchId { get; set; }
    public long? ParentCategoryId { get; set; }
    public long? ParentGroupId
    {
        get => ParentCategoryId;
        set => ParentCategoryId = value;
    }
    public string? ParentCategoryName { get; set; }
    public string? ParentGroupName
    {
        get => ParentCategoryName;
        set => ParentCategoryName = value;
    }
    public bool IsMainCategory { get; set; }
    public bool IsMainGroup
    {
        get => IsMainCategory;
        set => IsMainCategory = value;
    }
    public bool IsSubCategory { get; set; }
    public bool IsSubGroup
    {
        get => IsSubCategory;
        set => IsSubCategory = value;
    }
    public long CategoryCode { get; set; }
    public long GroupCode
    {
        get => CategoryCode;
        set => CategoryCode = value;
    }
    public string CategoryNameLocal { get; set; } = string.Empty;
    public string GroupNameLocal
    {
        get => CategoryNameLocal;
        set => CategoryNameLocal = value;
    }
    public string? CategoryNameEn { get; set; }
    public string? GroupNameEn
    {
        get => CategoryNameEn;
        set => CategoryNameEn = value;
    }
    public int CategoryLevel { get; set; }
    public int GroupLevel
    {
        get => CategoryLevel;
        set => CategoryLevel = value;
    }
    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    public bool ShowInPos { get; set; } = true;
    public bool IsActive { get; set; }
    public int ItemsCount { get; set; }

    public List<InvItemCategoryDto> SubCategories { get; set; } = new();
    public List<InvItemCategoryDto> Children => SubCategories;
    public List<InvItemCategoryDto> SubGroups => SubCategories;
}
