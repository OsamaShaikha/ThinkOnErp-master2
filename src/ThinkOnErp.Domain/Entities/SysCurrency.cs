namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a currency definition with exchange rates in the ERP system.
/// Includes bilingual descriptions (Arabic and English) for various currency forms.
/// Maps to the SYS_CURRENCY table in Oracle database.
/// </summary>
public class SysCurrency
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_CURRENCY sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Arabic description of the currency
    /// </summary>
    public string RowDesc { get; set; } = string.Empty;

    /// <summary>
    /// English description of the currency
    /// </summary>
    public string RowDescE { get; set; } = string.Empty;

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
