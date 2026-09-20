namespace ThinkOnErp.Application.DTOs.Inventory.ItemCategories;

public sealed class UpdateInvItemCategoryDto
{
    public string? CategoryNameLocal { get; set; }
    public string? GroupNameLocal
    {
        get => CategoryNameLocal;
        set => CategoryNameLocal = value ?? CategoryNameLocal;
    }
    public string? CategoryNameEn { get; set; }
    public string? GroupNameEn
    {
        get => CategoryNameEn;
        set => CategoryNameEn = value ?? CategoryNameEn;
    }
    public long? ParentCategoryId { get; set; }
    public long? ParentGroupId
    {
        get => ParentCategoryId;
        set => ParentCategoryId = value ?? ParentCategoryId;
    }
    public string? GlControlAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlAdjustmentAccount { get; set; }
    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    public bool? ShowInPos { get; set; }
    public bool? IsActive { get; set; }
}
