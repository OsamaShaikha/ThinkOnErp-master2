using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Entities.Accounting;

/// <summary>
/// Customer entity for Accounts Receivable (AR) subledger.
/// </summary>
public sealed class Customer
{
    public long Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string ArControlAccountCode { get; set; } = "112101"; // Default to AR Control
    public long? DefaultCurrencyId { get; set; }
    public decimal? CreditLimit { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
    public long? BranchId { get; set; }
    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    // Audit fields
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigation properties
    public GlAccount? ArControlAccount { get; set; }
    public SysCurrency? DefaultCurrency { get; set; }
    public SysBranch? Branch { get; set; }
}
