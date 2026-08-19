namespace ThinkOnErp.Application.DTOs.Accounting.FiscalPeriods;

public sealed class CreateGlFiscalPeriodDto
{
    public long FiscalYearId { get; set; }
    public int PeriodNumber { get; set; }
    public string PeriodNameAr { get; set; } = string.Empty;
    public string PeriodNameEn { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsAdjustment { get; set; }
}
