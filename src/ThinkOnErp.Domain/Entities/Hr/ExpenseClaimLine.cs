using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class ExpenseClaimLine
{
    public long Id { get; set; }
    public long ClaimId { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string Category { get; set; } = "TRAVEL"; // TRAVEL, MEALS, LODGING, MILEAGE, OFFICE_SUPPLIES, OTHER
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReceiptFileReference { get; set; }
    public string? ExpenseGlAccountCode { get; set; }
    public string? CostCenterCode { get; set; }

    public ExpenseClaim? Claim { get; set; }
}
