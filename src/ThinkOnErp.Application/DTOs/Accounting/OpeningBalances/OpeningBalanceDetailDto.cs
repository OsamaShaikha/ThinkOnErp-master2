namespace ThinkOnErp.Application.DTOs.Accounting.OpeningBalances;

/// <summary>Response DTO for an Opening Balance detail line.</summary>
public sealed class OpeningBalanceDetailDto
{
    public long Id { get; set; }
    public long HeaderId { get; set; }
    public int LineSer { get; set; }

    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameAr { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;

    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal LocalDebit { get; set; }
    public decimal LocalCredit { get; set; }

    public string? Description { get; set; }
}
