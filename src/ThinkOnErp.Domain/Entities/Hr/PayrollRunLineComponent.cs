using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class PayrollRunLineComponent
{
    public long Id { get; set; }
    public long PayrollRunLineId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentNameEn { get; set; } = string.Empty;
    public string ComponentNameAr { get; set; } = string.Empty;
    public string ComponentType { get; set; } = "EARNING"; // EARNING, DEDUCTION
    public decimal Amount { get; set; }
    public string? GlAccountCode { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public PayrollRunLine? PayrollRunLine { get; set; }
}
