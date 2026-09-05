namespace ThinkOnErp.Application.DTOs.Currency;

/// <summary>
/// Data transfer object for updating an existing currency.
/// Used for PUT requests to update currency records.
/// </summary>
public class UpdateCurrencyDto
{
    /// <summary>
    /// Arabic description of the currency (required)
    /// </summary>
    public string CurrencyNameLocal { get; set; } = string.Empty;

    /// <summary>
    /// English description of the currency (required)
    /// </summary>
    public string CurrencyNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Arabic short description (required)
    /// </summary>
    public string ShortNameLocal { get; set; } = string.Empty;

    /// <summary>
    /// English short description (required)
    /// </summary>
    public string ShortNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Arabic singular form (required)
    /// </summary>
    public string SingularNameLocal { get; set; } = string.Empty;

    /// <summary>
    /// English singular form (required)
    /// </summary>
    public string SingularNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Arabic dual form (required)
    /// </summary>
    public string DualNameLocal { get; set; } = string.Empty;

    /// <summary>
    /// English dual form (required)
    /// </summary>
    public string DualNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Arabic sum form (required)
    /// </summary>
    public string CollectiveNameLocal { get; set; } = string.Empty;

    /// <summary>
    /// English sum form (required)
    /// </summary>
    public string CollectiveNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Arabic fraction form (required)
    /// </summary>
    public string FractionNameLocal { get; set; } = string.Empty;

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
