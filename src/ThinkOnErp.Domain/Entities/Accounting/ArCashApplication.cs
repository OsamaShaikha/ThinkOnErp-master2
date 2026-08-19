namespace ThinkOnErp.Domain.Entities.Accounting;

/// <summary>
/// Settlement / Matching record between an AR payment (Receipt) and an AR invoice.
/// </summary>
public sealed class ArCashApplication
{
    public long Id { get; set; }
    public long PaymentTransactionId { get; set; }
    public long InvoiceTransactionId { get; set; }
    public decimal AppliedAmount { get; set; }
    public decimal LocalAppliedAmount { get; set; }
    public DateTime AppliedDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    // Audit fields
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ArSubledgerTransaction PaymentTransaction { get; set; } = null!;
    public ArSubledgerTransaction InvoiceTransaction { get; set; } = null!;
}
