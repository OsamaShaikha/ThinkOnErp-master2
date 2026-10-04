using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Pos;

/// <summary>
/// تصنيف مهيأ وخفيف مخصص لنقاط البيع وشاشات اللمس (POS Category)
/// </summary>
public sealed class PosCategoryDto
{
    public long Id { get; set; }
    public long CategoryId => Id;
    public long? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
    public long CategoryCode { get; set; }
    public string CategoryNameLocal { get; set; } = string.Empty;
    public string? CategoryNameEn { get; set; }
    public string DisplayName => !string.IsNullOrWhiteSpace(CategoryNameLocal) ? CategoryNameLocal : (CategoryNameEn ?? string.Empty);
    public int CategoryLevel { get; set; }
    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    public int ItemsCount { get; set; }
    public List<PosCategoryDto> Children { get; set; } = new();
}
