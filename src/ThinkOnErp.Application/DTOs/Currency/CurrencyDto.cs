namespace ThinkOnErp.Application.DTOs.Currency;

/// <summary>
/// Data transfer object for currency information returned from API endpoints.
/// Used for read operations (GET requests).
/// </summary>
public class CurrencyDto
{
    /// <summary>
    /// Unique identifier for the currency
    /// </summary>
    public Int64 CurrencyId { get; set; }

    /// <summary>
    /// Arabic description of the currency
    /// </summary>
    public string CurrencyNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English description of the currency
    /// </summary>
    public string CurrencyNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Arabic short description
    /// </summary>
    public string ShortDesc { get; set; } = string.Empty;

    /// <summary>
    /// English short description
    /// </summary>
    public string ShortDescE { get; set; } = string.Empty;

    /// <summary>
    /// Arabic singular form
    /// </summary>
    public string SingulerDesc { get; set; } = string.Empty;

    /// <summary>
    /// English singular form
    /// </summary>
    public string SingulerDescE { get; set; } = string.Empty;

    /// <summary>
    /// Arabic dual form
    /// </summary>
    public string DualDesc { get; set; } = string.Empty;

    /// <summary>
    /// English dual form
    /// </summary>
    public string DualDescE { get; set; } = string.Empty;

    /// <summary>
    /// Arabic sum form
    /// </summary>
    public string SumDesc { get; set; } = string.Empty;

    /// <summary>
    /// English sum form
    /// </summary>
    public string SumDescE { get; set; } = string.Empty;

    /// <summary>
    /// Arabic fraction form
    /// </summary>
    public string FracDesc { get; set; } = string.Empty;

    /// <summary>
    /// English fraction form
    /// </summary>
    public string FracDescE { get; set; } = string.Empty;

    /// <summary>
    /// Exchange rate for the currency
    /// </summary>
    public decimal? CurrRate { get; set; }

    /// <summary>
    /// Date when the exchange rate was set
    /// </summary>
    public DateTime? CurrRateDate { get; set; }

    /// <summary>
    /// Username of the user who created this record
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the record was created
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Username of the user who last updated this record
    /// </summary>
    public string? UpdateUser { get; set; }

    /// <summary>
    /// Timestamp when the record was last updated
    /// </summary>
    public DateTime? UpdateDate { get; set; }
}
