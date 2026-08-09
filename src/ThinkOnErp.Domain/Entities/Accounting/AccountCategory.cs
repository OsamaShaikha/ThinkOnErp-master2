namespace ThinkOnErp.Domain.Entities.Accounting;

public class AccountCategory
{
    public long Id { get; set; }
    public int CategoryCode { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NormalBalance { get; set; } = string.Empty;
    public string FinancialStatement { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public ICollection<GlAccount> Accounts { get; set; } = new List<GlAccount>();
}
