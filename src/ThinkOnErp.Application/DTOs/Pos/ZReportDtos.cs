using System;

namespace ThinkOnErp.Application.DTOs.Pos;

public class GenerateZReportDto
{
    public long BranchId { get; set; }
    public long? TillId { get; set; }
    public long? ShiftId { get; set; }
}

public class ZReportSummaryDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long ZSequenceNumber { get; set; }
    public DateTime ReportDate { get; set; }
    public string FirstInvoiceNumber { get; set; } = string.Empty;
    public string LastInvoiceNumber { get; set; } = string.Empty;
    public int TotalInvoiceCount { get; set; }
    public int TotalReturnCount { get; set; }

    public decimal GrossSalesAmount { get; set; }
    public decimal TotalDiscountAmount { get; set; }
    public decimal NetSalesAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public decimal TotalServiceCharge { get; set; }
    public decimal TotalRefundAmount { get; set; }
    public decimal FinalTotalAmount { get; set; }

    public decimal CashPaymentsTotal { get; set; }
    public decimal CardPaymentsTotal { get; set; }
    public decimal CustomerAccountPaymentsTotal { get; set; }
    public decimal OtherPaymentsTotal { get; set; }

    public decimal OpeningFloat { get; set; }
    public decimal CashDropTotal { get; set; }
    public decimal PayOutTotal { get; set; }
    public decimal ExpectedDrawerCash { get; set; }
    public decimal ActualCountedCash { get; set; }
    public decimal CashVariance { get; set; }

    public bool IsPostedToGl { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
}
