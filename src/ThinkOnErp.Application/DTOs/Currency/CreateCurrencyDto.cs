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
    public string ShortNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English short description (required)
    /// </summary>
    public string ShortNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Arabic singular form (required)
    /// </summary>
    public string SingularNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English singular form (required)
    /// </summary>
    public string SingularNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Arabic dual form (required)
    /// </summary>
    public string DualNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English dual form (required)
    /// </summary>
    public string DualNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Arabic sum form (required)
    /// </summary>
    public string CollectiveNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English sum form (required)
    /// </summary>
    public string CollectiveNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Arabic fraction form (required)
    /// </summary>
    public string FractionNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English fraction form (required)
    /// </summary>
    public string FractionNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Exchange rate for the currency (optional)
    /// </summary>
    public decimal? CurrRate { get; set; }

    /// <summary>
    /// Date when the exchange rate was set (optional)
    /// </summary>
    public DateTime? CurrRateDate { get; set; }
}
