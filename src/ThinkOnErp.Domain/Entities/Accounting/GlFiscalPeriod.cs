using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Entities.Accounting;

public sealed class GlFiscalPeriod
{
    public long Id { get; set; }
    public long FiscalYearId { get; set; }
    public int PeriodNumber { get; set; } // 1..12, 13 (Year-end adjustments)
    public string PeriodNameLocal { get; set; } = string.Empty;
    public string PeriodNameEn { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Status: OPEN, SOFT_CLOSE, HARD_CLOSE
    /// </summary>
    public string Status { get; set; } = "OPEN";

    public bool IsAdjustment { get; set; }
    public string? CloseReason { get; set; }
    public string? ClosedBy { get; set; }
    public DateTime? ClosedDate { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public SysFiscalYear FiscalYear { get; set; } = null!;
}
