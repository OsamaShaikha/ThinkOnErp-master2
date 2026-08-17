namespace ThinkOnErp.Application.DTOs.Accounting.OpeningBalances;

/// <summary>Response DTO for an Opening Balance header with all detail lines.</summary>
public sealed class OpeningBalanceHeaderDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long FiscalYearId { get; set; }

    public DateTime AsOfDate { get; set; }
    public string? Description { get; set; }

    /// <summary>1 = Draft (editable), 2 = Confirmed (locked)</summary>
    public int Status { get; set; }
    public string StatusLabel => Status == 1 ? "Draft" : "Confirmed";

    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }

    /// <summary>ID of the auto-generated OB journal voucher (populated after Confirm).</summary>
    public long? ObVoucherId { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<OpeningBalanceDetailDto> Details { get; set; } = new();
}
