using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Pos;

public class GenerateZReportDto
{
    public long BranchId { get; set; }
    public long? TillId { get; set; }
    public long? ShiftId { get; set; }
}

public class PosOrderTypeBreakdownDto
{
    public int OrderType { get; set; }
    public string OrderTypeName { get; set; } = string.Empty;
    public int OrdersCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

public class PosTaxBreakdownSummaryDto
{
    public decimal TaxableAmountStandardRate { get; set; }
    public decimal StandardRateTaxAmount { get; set; }
    public decimal ZeroRatedAmount { get; set; }
    public decimal ExemptAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
}

public class PosCashMovementSummaryDto
{
    public decimal OpeningFloat { get; set; }
    public decimal CashSales { get; set; }
    public decimal CashRefunds { get; set; }
    public decimal FloatInReceiptVouchers { get; set; }
    public decimal PayOutPaymentVouchers { get; set; }
    public decimal CashDropTotal { get; set; }
    public decimal ExpectedDrawerCash { get; set; }
    public decimal ActualCountedCash { get; set; }
    public decimal CashVariance { get; set; }
}

public class PosOperationalStatsDto
{
    public int TotalInvoiceCount { get; set; }
    public int TotalReturnCount { get; set; }
    public int TotalGuestsCovers { get; set; }
    public decimal AverageTicketAmount { get; set; }
    public decimal AverageSpendPerGuest { get; set; }
    public string FirstInvoiceNumber { get; set; } = string.Empty;
    public string LastInvoiceNumber { get; set; } = string.Empty;
    public DateTime? ShiftOpenedAt { get; set; }
    public DateTime? ReportGeneratedAt { get; set; }
    public string? TillName { get; set; }
    public string? CashierName { get; set; }
    public string? ShiftNumber { get; set; }
}

public class PosCategorySalesSummaryDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public decimal PercentageOfTotal { get; set; }
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
    public decimal HospitalityPaymentsTotal { get; set; }
    public decimal OtherPaymentsTotal { get; set; }

    public decimal OpeningFloat { get; set; }
    public decimal CashDropTotal { get; set; }
    public decimal PayOutTotal { get; set; }
    public decimal ExpectedDrawerCash { get; set; }
    public decimal ActualCountedCash { get; set; }
    public decimal CashVariance { get; set; }

    public decimal ExpectedCardAmount { get; set; }
    public decimal ActualCountedCard { get; set; }
    public decimal CardVariance { get; set; }

    public List<PosTenderReconciliationDto> TenderReconciliations { get; set; } = new();
    public List<PosOrderTypeBreakdownDto> OrderTypeBreakdowns { get; set; } = new();
    public PosTaxBreakdownSummaryDto TaxBreakdown { get; set; } = new();
    public PosCashMovementSummaryDto CashMovementSummary { get; set; } = new();
    public PosOperationalStatsDto OperationalStats { get; set; } = new();
    public List<PosCategorySalesSummaryDto> CategorySales { get; set; } = new();

    public bool IsPostedToGl { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
}

public class XReportSummaryDto
{
    public string ReportType { get; set; } = "X-REPORT";
    public string ReportTitle { get; set; } = "تقرير X - قراءة لحظية للوردية (Live Snapshot)";
    public bool IsLiveSnapshot { get; set; } = true;
    public long BranchId { get; set; }
    public long ShiftId { get; set; }
    public string ShiftNumber { get; set; } = string.Empty;
    public int ShiftStatus { get; set; }
    public string ShiftStatusName { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string GeneratedBy { get; set; } = string.Empty;

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
    public decimal HospitalityPaymentsTotal { get; set; }
    public decimal OtherPaymentsTotal { get; set; }

    public decimal OpeningFloat { get; set; }
    public decimal CashDropTotal { get; set; }
    public decimal PayOutTotal { get; set; }
    public decimal ExpectedDrawerCash { get; set; }
    public decimal? ActualCountedCash { get; set; }
    public decimal? CashVariance { get; set; }

    public decimal ExpectedCardAmount { get; set; }
    public decimal? ActualCountedCard { get; set; }
    public decimal? CardVariance { get; set; }

    public List<PosTenderReconciliationDto> TenderReconciliations { get; set; } = new();
    public List<PosOrderTypeBreakdownDto> OrderTypeBreakdowns { get; set; } = new();
    public PosTaxBreakdownSummaryDto TaxBreakdown { get; set; } = new();
    public PosCashMovementSummaryDto CashMovementSummary { get; set; } = new();
    public PosOperationalStatsDto OperationalStats { get; set; } = new();
    public List<PosCategorySalesSummaryDto> CategorySales { get; set; } = new();
}
