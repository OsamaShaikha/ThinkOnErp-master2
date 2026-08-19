namespace ThinkOnErp.Domain.Entities.Accounting;

public sealed class GlPostingRule
{
    public long Id { get; set; }
    public long? BranchId { get; set; } // Null for default/company-wide fallback
    
    /// <summary>
    /// Module: SALES, PURCHASES, CASH_BANK, PDC, INVENTORY, PAYROLL, FIXED_ASSETS, TAX, FX
    /// </summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// EventType: SALES_INVOICE, SALES_RETURN, AR_RECEIPT, AP_BILL, AP_PAYMENT, 
    /// PDC_INWARD_RECEIPT, PDC_INWARD_CLEAR, PDC_INWARD_BOUNCE, PDC_OUTWARD_ISSUE, PDC_OUTWARD_CLEAR,
    /// INVENTORY_RECEIPT, INVENTORY_ISSUE, COGS, MONTHLY_DEPRECIATION, FX_GAIN, FX_LOSS, BANK_CHARGES
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    public string EventNameAr { get; set; } = string.Empty;
    public string EventNameEn { get; set; } = string.Empty;

    public string DebitAccountCode { get; set; } = string.Empty;
    public string CreditAccountCode { get; set; } = string.Empty;
    public string? DefaultCostCenterCode { get; set; }
    public int DefaultVoucherType { get; set; } = 1; // 1: JV, 2: RV, 3: PV
    public string? DescriptionTemplate { get; set; }

    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public SysBranch? Branch { get; set; }
    public GlAccount? DebitAccount { get; set; }
    public GlAccount? CreditAccount { get; set; }
    public GlCostCenter? DefaultCostCenter { get; set; }
}
