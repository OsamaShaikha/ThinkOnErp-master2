using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Entities.Accounting;

/// <summary>
/// Pre-aggregated account balances per period and branch for high-performance financial reporting.
/// </summary>
public sealed class GlAccountBalance
{
    public long Id { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public long BranchId { get; set; }
    public long FiscalYearId { get; set; }
    public long FiscalPeriodId { get; set; }
    public long CurrencyId { get; set; }

    // Foreign currency amounts
    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }
    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }

    // Local / Base currency amounts
    public decimal LocalOpeningDebit { get; set; }
    public decimal LocalOpeningCredit { get; set; }
    public decimal LocalPeriodDebit { get; set; }
    public decimal LocalPeriodCredit { get; set; }
    public decimal LocalClosingDebit { get; set; }
    public decimal LocalClosingCredit { get; set; }

    // Audit fields
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigation properties
    public GlAccount Account { get; set; } = null!;
    public SysBranch Branch { get; set; } = null!;
    public SysFiscalYear FiscalYear { get; set; } = null!;
    public GlFiscalPeriod FiscalPeriod { get; set; } = null!;
    public SysCurrency Currency { get; set; } = null!;
}
