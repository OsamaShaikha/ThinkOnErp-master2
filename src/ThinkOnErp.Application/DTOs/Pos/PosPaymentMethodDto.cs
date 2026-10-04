namespace ThinkOnErp.Application.DTOs.Pos;

/// <summary>
/// وسيلة دفع مهيأة ومخصصة لشاشة نقاط البيع (POS Cash Register)
/// </summary>
public sealed class PosPaymentMethodDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string DisplayName => !string.IsNullOrWhiteSpace(NameLocal) ? NameLocal : NameEn;
    public string MethodType { get; set; } = "CASH";
    public decimal CommissionPercent { get; set; }
    public decimal CommissionFixedAmount { get; set; }
    public bool RequiresReference { get; set; }
    public bool RequiresDueDate { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
