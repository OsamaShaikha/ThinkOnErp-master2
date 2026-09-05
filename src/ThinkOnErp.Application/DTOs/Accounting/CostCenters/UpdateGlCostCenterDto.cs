using System.ComponentModel.DataAnnotations;
using ThinkOnErp.Application.DTOs.Translations;

namespace ThinkOnErp.Application.DTOs.Accounting.CostCenters;

public sealed class UpdateGlCostCenterDto
{
    [Required]
    [MaxLength(200)]
    public string NameLocal { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string NameEn { get; set; } = string.Empty;

    [Required]
    public string CostCenterType { get; set; } = "DETAIL";

    public bool IsPostable { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public List<EntityTranslationDto>? Translations { get; set; }
}
