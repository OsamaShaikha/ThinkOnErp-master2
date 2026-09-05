namespace ThinkOnErp.Domain.Entities.Accounting;

public sealed class TaxCategory
{
    public long Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty; // VAT, WHT, EXCISE, CUSTOMS
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public ICollection<TaxRate> TaxRates { get; set; } = new List<TaxRate>();
}
