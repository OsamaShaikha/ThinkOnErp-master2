using System.ComponentModel.DataAnnotations;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.DTOs.Pos;

public class PosSalesPdfFilterDto
{
    /// <summary>Optional report language override: 1 Arabic, 2 English. Otherwise uses the session language.</summary>
    [Range(1, 2)] public int? Language { get; set; }
    [Range(1, long.MaxValue)] public long? BranchId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    [Range(1, long.MaxValue)] public long? ShiftId { get; set; }
    [Range(1, long.MaxValue)] public long? TillId { get; set; }
    [MaxLength(200)] public string? CashierUsername { get; set; }
    [EnumDataType(typeof(PosPaymentMethod))] public PosPaymentMethod? PaymentMethod { get; set; }
}

public sealed class PosSalesPrintDataDto
{
    public string Language { get; set; } = "en";
    public bool IsRightToLeft => Language == "ar";
    public string CompanyName { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public string Currency { get; set; } = "";
    public string Cashier { get; set; } = "All";
    public string Payment { get; set; } = "All";
    public string PreparedBy { get; set; } = "";
    public string ReportNumber { get; set; } = "";
    public DateTime IssuedAtUtc { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public long? ShiftId { get; set; }
    public long? TillId { get; set; }
    public List<PosSalesDayDto> Days { get; set; } = new();
    public List<PosSalesTenderDto> Payments { get; set; } = new();
    public PosSalesDayDto Totals { get; set; } = new();
    public decimal AverageInvoice => Totals.Invoices == 0 ? 0 : Totals.Total / Totals.Invoices;
}

public sealed class PosSalesReportOptionsDto
{
    public string Language { get; set; } = "en";
    public bool IsRightToLeft => Language == "ar";
    public string AllBranchesLabel { get; set; } = "All branches";
    public string AllCashiersLabel { get; set; } = "All cashiers";
    public List<PosSalesBranchOptionDto> Branches { get; set; } = new();
    public List<PosSalesCashierOptionDto> Cashiers { get; set; } = new();
    public List<PosSalesBranchOptionDto> Floors { get; set; } = new();
}

public sealed class PosSalesBranchOptionDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class PosSalesCashierOptionDto
{
    public string UserName { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class PosSalesDayDto
{
    public DateTime Date { get; set; }
    public int Invoices { get; set; }
    public decimal Sales { get; set; }
    public decimal Discount { get; set; }
    public decimal Returns { get; set; }
    public decimal Net => Sales - Discount - Returns;
    public decimal Tax { get; set; }
    public decimal Charges { get; set; }
    public decimal Total { get; set; }
}

public sealed class PosSalesTenderDto
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Percentage { get; set; }
}
