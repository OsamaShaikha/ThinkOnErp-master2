namespace ThinkOnErp.Application.DTOs.Accounting.FiscalPeriods;

public sealed class GenerateFiscalPeriodsDto
{
    /// <summary>
    /// Whether to generate an extra 13th adjustment period for year-end closing entries.
    /// </summary>
    public bool IncludeAdjustmentPeriod { get; set; } = true;
}
