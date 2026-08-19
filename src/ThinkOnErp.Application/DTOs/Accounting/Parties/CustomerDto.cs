namespace ThinkOnErp.Application.DTOs.Accounting.Parties;

public sealed class CustomerDto
{
    public long Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string ArControlAccountCode { get; set; } = "112101";
    public string? ArControlAccountNameAr { get; set; }
    public string? ArControlAccountNameEn { get; set; }
    public long? DefaultCurrencyId { get; set; }
    public string? DefaultCurrencyNameAr { get; set; }
    public string? DefaultCurrencyNameEn { get; set; }
    public decimal? CreditLimit { get; set; }
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

public sealed class CreateCustomerDto
{
    public string CustomerCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? ArControlAccountCode { get; set; } = "112101";
    public long? DefaultCurrencyId { get; set; }
    public decimal? CreditLimit { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
    public long? BranchId { get; set; }
    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
}

public sealed class UpdateCustomerDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? ArControlAccountCode { get; set; }
    public long? DefaultCurrencyId { get; set; }
    public decimal? CreditLimit { get; set; }
    public int PaymentTermsDays { get; set; }
    public long? BranchId { get; set; }
    public string? TaxNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}
