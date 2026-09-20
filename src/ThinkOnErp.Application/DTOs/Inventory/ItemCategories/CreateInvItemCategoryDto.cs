using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Inventory.ItemCategories;

public sealed class CreateInvItemCategoryDto
{
    public long? BranchId { get; set; }
    public long? ParentCategoryId { get; set; }
    public long? ParentGroupId
    {
        get => ParentCategoryId;
        set => ParentCategoryId = value ?? ParentCategoryId;
    }

    [Required]
    public long CategoryCode { get; set; }
    public long? GroupCode
    {
        get => CategoryCode;
        set { if (value.HasValue) CategoryCode = value.Value; }
    }

    [Required]
    [MaxLength(150)]
    public string CategoryNameLocal { get; set; } = string.Empty;
    public string? GroupNameLocal
    {
        get => CategoryNameLocal;
        set { if (!string.IsNullOrEmpty(value)) CategoryNameLocal = value; }
    }

    [MaxLength(150)]
    public string? CategoryNameEn { get; set; }
    public string? GroupNameEn
    {
        get => CategoryNameEn;
        set { if (!string.IsNullOrEmpty(value)) CategoryNameEn = value; }
    }

    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    public bool ShowInPos { get; set; } = true;
}
