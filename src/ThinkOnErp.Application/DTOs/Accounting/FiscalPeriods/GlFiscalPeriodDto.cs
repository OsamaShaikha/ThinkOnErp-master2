namespace ThinkOnErp.Application.DTOs.Accounting.FiscalPeriods;

public sealed class GlFiscalPeriodDto
{
    public long Id { get; set; }
    public long FiscalYearId { get; set; }
    public int PeriodNumber { get; set; }
    public string PeriodNameLocal { get; set; } = string.Empty;
    public string PeriodNameEn { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "OPEN";
    public bool IsAdjustment { get; set; }
    public string? CloseReason { get; set; }
    public string? ClosedBy { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
