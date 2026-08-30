namespace ThinkOnErp.Domain.Entities.Accounting;

public sealed class TaxGroup
{
    public long Id { get; set; }
    public string GroupCode { get; set; } = string.Empty; // STD_VAT_15, ZERO_VAT, EXEMPT_VAT
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public ICollection<TaxGroupItem> Items { get; set; } = new List<TaxGroupItem>();
}

public sealed class TaxGroupItem
{
    public long Id { get; set; }
    public long TaxGroupId { get; set; }
    public long TaxRateId { get; set; }
    public int ApplicationOrder { get; set; } = 1;
    public bool IsCompound { get; set; }

    public TaxGroup? Group { get; set; }
    public TaxRate? TaxRate { get; set; }
}
