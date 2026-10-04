namespace ThinkOnErp.Application.DTOs.Pos;

/// <summary>
/// بيانات العميل المهيأة والمخصصة لشاشات ونقاط البيع (POS Customer)
/// </summary>
public sealed class PosCustomerDto
{
    public long Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string DisplayName => !string.IsNullOrWhiteSpace(NameLocal) ? NameLocal : NameEn;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxNumber { get; set; }
    public string? Address { get; set; }
    public decimal? CreditLimit { get; set; }
    public int PaymentTermsDays { get; set; }
    public long? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
}
