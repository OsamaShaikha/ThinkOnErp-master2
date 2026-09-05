namespace ThinkOnErp.Domain.Entities;

public class SysCurrency
{
    public Int64 Id { get; set; }
    public string CurrencyNameLocal { get; set; } = string.Empty;
    public string CurrencyNameEn { get; set; } = string.Empty;
    public string ShortNameLocal { get; set; } = string.Empty;
    public string ShortNameEn { get; set; } = string.Empty;
    public string SingularNameLocal { get; set; } = string.Empty;
    public string SingularNameEn { get; set; } = string.Empty;
    public string DualNameLocal { get; set; } = string.Empty;
    public string DualNameEn { get; set; } = string.Empty;
    public string CollectiveNameLocal { get; set; } = string.Empty;
    public string CollectiveNameEn { get; set; } = string.Empty;
    public string FractionNameLocal { get; set; } = string.Empty;
    public string FractionNameEn { get; set; } = string.Empty;
    public decimal? CurrRate { get; set; }
    public DateTime? CurrRateDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
