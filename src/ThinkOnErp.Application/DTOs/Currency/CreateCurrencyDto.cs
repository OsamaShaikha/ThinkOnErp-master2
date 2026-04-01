namespace ThinkOnErp.Application.DTOs.Currency;

/// <summary>
/// Data transfer object for creating a new currency.
/// Used for POST requests to create currency records.
/// </summary>
public class CreateCurrencyDto
{
    /// <summary>
    /// Arabic description of the currency (required)
    /// </summary>
    public string CurrencyNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English description of the currency (required)
    /// </summary>
    public string CurrencyNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Arabic short description (required)
    /// </summary>
    public string ShortDesc { get; set; } = string.Empty;

    /// <summary>
    /// English short description (required)
    /// </summary>
    public string ShortDescE { get; set; } = string.Empty;

    /// <summary>
    /// Arabic singular form (required)
    /// </summary>
    public string SingulerDesc { get; set; } = string.Empty;

    /// <summary>
    /// English singular form (required)
    /// </summary>
    public string SingulerDescE { get; set; } = string.Empty;

    /// <summary>
    /// Arabic dual form (required)
    /// </summary>
    public string DualDesc { get; set; } = string.Empty;

    /// <summary>
    /// English dual form (required)
    /// </summary>
    public string DualDescE { get; set; } = string.Empty;

    /// <summary>
    /// Arabic sum form (required)
    /// </summary>
    public string SumDesc { get; set; } = string.Empty;

    /// <summary>
    /// English sum form (required)
    /// </summary>
    public string SumDescE { get; set; } = string.Empty;

    /// <summary>
    /// Arabic fraction form (required)
    /// </summary>
    public string FracDesc { get; set; } = string.Empty;

    /// <summary>
    /// English fraction form (required)
    /// </summary>
    public string FracDescE { get; set; } = string.Empty;

    /// <summary>
    /// Exchange rate for the currency (optional)
    /// </summary>
    public decimal? CurrRate { get; set; }

    /// <summary>
    /// Date when the exchange rate was set (optional)
    /// </summary>
    public DateTime? CurrRateDate { get; set; }
}
