namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class PayrollCurrencyPrecisionDto
{
    public long CompanyId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; }
}
