namespace ThinkOnErp.Domain.Entities.Hr;

/// <summary>Company-specific payroll precision override for one configured currency code.</summary>
public sealed class PayrollCurrencyPrecision
{
    public long CompanyId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
