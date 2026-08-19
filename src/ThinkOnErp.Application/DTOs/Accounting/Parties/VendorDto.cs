namespace ThinkOnErp.Application.DTOs.Accounting.Parties;

public sealed class VendorDto
{
    public long Id { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string ApControlAccountCode { get; set; } = "211101";
    public string? ApControlAccountNameAr { get; set; }
    public string? ApControlAccountNameEn { get; set; }
    public long? DefaultCurrencyId { get; set; }
    public string? DefaultCurrencyNameAr { get; set; }
    public string? DefaultCurrencyNameEn { get; set; }
    public int PaymentTermsDays { get; set; }
    public long? BranchId { get; set; }
    public string? BranchNameAr { get; set; }
    public string? BranchNameEn { get; set; }
    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
}

public sealed class CreateVendorDto
{
    public string VendorCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? ApControlAccountCode { get; set; } = "211101";
    public long? DefaultCurrencyId { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
    public long? BranchId { get; set; }
    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
}

public sealed class UpdateVendorDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? ApControlAccountCode { get; set; }
    public long? DefaultCurrencyId { get; set; }
    public int PaymentTermsDays { get; set; }
    public long? BranchId { get; set; }
    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PartyFilterDto
{
    public string? SearchTerm { get; set; }
    public long? BranchId { get; set; }
    public bool? IsActive { get; set; }
}
