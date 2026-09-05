namespace ThinkOnErp.Application.DTOs.Accounting.PostingRules;

public sealed class PostingRuleDto
{
    public long Id { get; set; }
    public long? BranchId { get; set; }
    public string? BranchNameLocal { get; set; }

    public string Module { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EventNameLocal { get; set; } = string.Empty;
    public string EventNameEn { get; set; } = string.Empty;

    public string DebitAccountCode { get; set; } = string.Empty;
    public string? DebitAccountNameLocal { get; set; }

    public string CreditAccountCode { get; set; } = string.Empty;
    public string? CreditAccountNameLocal { get; set; }

    public string? DefaultCostCenterCode { get; set; }
    public string? DefaultCostCenterNameLocal { get; set; }

    public int DefaultVoucherType { get; set; }
    public string? DescriptionTemplate { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreatePostingRuleDto
{
    public long? BranchId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EventNameLocal { get; set; } = string.Empty;
    public string EventNameEn { get; set; } = string.Empty;

    public string DebitAccountCode { get; set; } = string.Empty;
    public string CreditAccountCode { get; set; } = string.Empty;
    public string? DefaultCostCenterCode { get; set; }
    public int DefaultVoucherType { get; set; } = 1;
    public string? DescriptionTemplate { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdatePostingRuleDto
{
    public string DebitAccountCode { get; set; } = string.Empty;
    public string CreditAccountCode { get; set; } = string.Empty;
    public string? DefaultCostCenterCode { get; set; }
    public int DefaultVoucherType { get; set; } = 1;
    public string? DescriptionTemplate { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AutomaticPostingEventRequest
{
    public long BranchId { get; set; }
    public long FiscalYearId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;

    public DateTime VoucherDate { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public long CurrencyId { get; set; } = 1;
    public decimal ExchangeRate { get; set; } = 1.0m;

    public string? PartyType { get; set; } // CUSTOMER, VENDOR
    public string? PartyCode { get; set; }
    public string? CostCenterCode { get; set; }
    public string? ReferenceNo { get; set; }
    public string? CustomDescription { get; set; }
}
