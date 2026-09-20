using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Inventory.ItemCategories;

#region Main Category DTOs

public class CreateMainCategoryDto
{
    public long? BranchId { get; set; }

    [Required]
    public long CategoryCode { get; set; }
    public long GroupCode
    {
        get => CategoryCode;
        set => CategoryCode = value;
    }

    [Required]
    [MaxLength(150)]
    public string CategoryNameLocal { get; set; } = string.Empty;
    public string GroupNameLocal
    {
        get => CategoryNameLocal;
        set => CategoryNameLocal = value;
    }

    [MaxLength(150)]
    public string? CategoryNameEn { get; set; }
    public string? GroupNameEn
    {
        get => CategoryNameEn;
        set => CategoryNameEn = value;
    }

    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    public bool ShowInPos { get; set; } = true;
}

public class UpdateMainCategoryDto
{
    [Required]
    [MaxLength(150)]
    public string CategoryNameLocal { get; set; } = string.Empty;
    public string GroupNameLocal
    {
        get => CategoryNameLocal;
        set => CategoryNameLocal = value;
    }

    [MaxLength(150)]
    public string? CategoryNameEn { get; set; }
    public string? GroupNameEn
    {
        get => CategoryNameEn;
        set => CategoryNameEn = value;
    }

    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    public bool ShowInPos { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class InvMainCategoryDto
{
    public long Id { get; set; }
    public long CategoryId => Id;
    public long? BranchId { get; set; }
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
    public int CategoryLevel { get; set; } = 1;
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
    public int SubCategoriesCount { get; set; }
    public int SubGroupsCount
    {
        get => SubCategoriesCount;
        set => SubCategoriesCount = value;
    }
    public int ItemsCount { get; set; }
    public List<InvSubCategoryDto> SubCategories { get; set; } = new();
    public List<InvSubCategoryDto> SubGroups => SubCategories;
}

#endregion

#region Sub Category DTOs

public class CreateSubCategoryDto
{
    [Required]
    public long MainCategoryId { get; set; }
    public long MainGroupId
    {
        get => MainCategoryId;
        set => MainCategoryId = value;
    }

    public long? BranchId { get; set; }

    [Required]
    public long CategoryCode { get; set; }
    public long GroupCode
    {
        get => CategoryCode;
        set => CategoryCode = value;
    }

    [Required]
    [MaxLength(150)]
    public string CategoryNameLocal { get; set; } = string.Empty;
    public string GroupNameLocal
    {
        get => CategoryNameLocal;
        set => CategoryNameLocal = value;
    }

    [MaxLength(150)]
    public string? CategoryNameEn { get; set; }
    public string? GroupNameEn
    {
        get => CategoryNameEn;
        set => CategoryNameEn = value;
    }

    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    public bool ShowInPos { get; set; } = true;
}

public class UpdateSubCategoryDto
{
    public long? MainCategoryId { get; set; }
    public long? MainGroupId
    {
        get => MainCategoryId;
        set => MainCategoryId = value;
    }

    [Required]
    [MaxLength(150)]
    public string CategoryNameLocal { get; set; } = string.Empty;
    public string GroupNameLocal
    {
        get => CategoryNameLocal;
        set => CategoryNameLocal = value;
    }

    [MaxLength(150)]
    public string? CategoryNameEn { get; set; }
    public string? GroupNameEn
    {
        get => CategoryNameEn;
        set => CategoryNameEn = value;
    }

    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    public bool ShowInPos { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class InvSubCategoryDto
{
    public long Id { get; set; }
    public long CategoryId => Id;
    public long MainCategoryId { get; set; }
    public long MainGroupId
    {
        get => MainCategoryId;
        set => MainCategoryId = value;
    }
    public string? MainCategoryNameLocal { get; set; }
    public string? MainGroupNameLocal
    {
        get => MainCategoryNameLocal;
        set => MainCategoryNameLocal = value;
    }
    public string? MainCategoryNameEn { get; set; }
    public string? MainGroupNameEn
    {
        get => MainCategoryNameEn;
        set => MainCategoryNameEn = value;
    }
    public long? BranchId { get; set; }
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
    public int CategoryLevel { get; set; } = 2;
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
}

#endregion

#region Tree Hierarchy DTOs

public class InvCategoryTreeNodeDto
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
    public bool IsActive { get; set; } = true;
    public int DirectChildrenCount => Children.Count;
    public int ItemsCount { get; set; }
    public List<InvCategoryTreeNodeDto> Children { get; set; } = new();
    public List<InvCategoryTreeNodeDto> SubCategories => Children;
    public List<InvCategoryTreeNodeDto> SubGroups => Children;
}

#endregion
