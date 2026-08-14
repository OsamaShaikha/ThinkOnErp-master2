using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Accounting.CostCenters;

public sealed class UpdateGlCostCenterDto
{
    [Required(ErrorMessage = "اسم مركز التكلفة بالعربي مطلوب")]
    [MaxLength(200)]
    public string NameAr { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم مركز التكلفة بالإنجليزي مطلوب")]
    [MaxLength(200)]
    public string NameEn { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع مركز التكلفة مطلوب (HEADER / DETAIL)")]
    public string CostCenterType { get; set; } = "DETAIL";

    public bool IsPostable { get; set; } = true;
    public bool IsActive { get; set; } = true;
}
