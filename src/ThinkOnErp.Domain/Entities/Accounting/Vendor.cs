using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Entities.Accounting;

/// <summary>
/// Vendor / Supplier entity for Accounts Payable (AP) subledger.
/// </summary>
public sealed class Vendor
{
    public long Id { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string ApControlAccountCode { get; set; } = "211101"; // Default to AP Control
    public long? DefaultCurrencyId { get; set; }
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
    public GlAccount? ApControlAccount { get; set; }
    public SysCurrency? DefaultCurrency { get; set; }
    public SysBranch? Branch { get; set; }
}
