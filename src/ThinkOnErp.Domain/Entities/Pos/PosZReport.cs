using System;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosZReport
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long? TillId { get; set; }
    public long? ShiftId { get; set; }
    public long ZSequenceNumber { get; set; }
    public DateTime ReportDate { get; set; } = DateTime.UtcNow;

    public string FirstInvoiceNumber { get; set; } = string.Empty;
    public string LastInvoiceNumber { get; set; } = string.Empty;
    public int TotalInvoiceCount { get; set; }
    public int TotalReturnCount { get; set; }

    // Revenue metrics
    public decimal GrossSalesAmount { get; set; }
    public decimal TotalDiscountAmount { get; set; }
    public decimal NetSalesAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public decimal TotalServiceCharge { get; set; }
    public decimal TotalRefundAmount { get; set; }
    public decimal FinalTotalAmount { get; set; }

    // Tenders breakdown
    public decimal CashPaymentsTotal { get; set; }
    public decimal CardPaymentsTotal { get; set; }
    public decimal CustomerAccountPaymentsTotal { get; set; }
    public decimal OtherPaymentsTotal { get; set; }

    // Cash reconciliation
    public decimal OpeningFloat { get; set; }
    public decimal CashDropTotal { get; set; }
    public decimal PayOutTotal { get; set; }
    public decimal ExpectedDrawerCash { get; set; }
    public decimal ActualCountedCash { get; set; }
    public decimal CashVariance { get; set; }

    public bool IsPostedToGl { get; set; }
    public long? GlVoucherId { get; set; }

    public string GeneratedBy { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public SysBranch? Branch { get; set; }
    public PosTill? Till { get; set; }
    public PosShift? Shift { get; set; }
}
