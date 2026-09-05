namespace ThinkOnErp.Application.DTOs.Accounting.Pdc;

public sealed class PdcRegisterDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string? BranchNameLocal { get; set; }
    public string? BranchNameEn { get; set; }
    public long FiscalYearId { get; set; }

    public string ChequeType { get; set; } = string.Empty; // RECEIVED, ISSUED
    public string ChequeNumber { get; set; } = string.Empty;
    public DateTime ChequeDate { get; set; }
    public DateTime DueDate { get; set; }

    public decimal Amount { get; set; }
    public decimal LocalAmount { get; set; }
    public long CurrencyId { get; set; }
    public string? CurrencyName { get; set; }
    public decimal ExchangeRate { get; set; }

    public string DrawerBankName { get; set; } = string.Empty;
    public string? DrawerBankAccountNo { get; set; }
    public string? BeneficiaryName { get; set; }

    public string? PartyType { get; set; }
    public string? PartyCode { get; set; }
    public string? PartyNameLocal { get; set; }
    public string? PartyNameEn { get; set; }

    public string Status { get; set; } = string.Empty; // RECEIVED, DEPOSITED, CLEARED, BOUNCED, CANCELLED
    public string StatusNameLocal { get; set; } = string.Empty;

    public string? IntermediateAccountCode { get; set; }
    public string? DepositBankAccountCode { get; set; }
    public DateTime? DepositDate { get; set; }
    public DateTime? ClearedDate { get; set; }
    public DateTime? BouncedDate { get; set; }
    public string? BounceReason { get; set; }

    public long? OriginatingVoucherId { get; set; }
    public long? ClearingVoucherId { get; set; }
    public long? BounceVoucherId { get; set; }
    public string? Notes { get; set; }
    public int DaysUntilMaturity => (DueDate.Date - DateTime.UtcNow.Date).Days;
}

public sealed class CreatePdcDto
{
    public long BranchId { get; set; }
    public long FiscalYearId { get; set; }

    public string ChequeType { get; set; } = "RECEIVED"; // RECEIVED, ISSUED
    public string ChequeNumber { get; set; } = string.Empty;
    public DateTime ChequeDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }

    public decimal Amount { get; set; }
    public long CurrencyId { get; set; } = 1;
    public decimal ExchangeRate { get; set; } = 1.0m;

    public string DrawerBankName { get; set; } = string.Empty;
    public string? DrawerBankAccountNo { get; set; }
    public string? BeneficiaryName { get; set; }

    public string? PartyType { get; set; } // CUSTOMER, VENDOR
    public string? PartyCode { get; set; }

    public string? IntermediateAccountCode { get; set; } = "111301"; // شيكات برسم التحصيل
    public string? Notes { get; set; }
}

public sealed class DepositPdcDto
{
    public string DepositBankAccountCode { get; set; } = "111201"; // البنك المودع فيه
    public DateTime DepositDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}

public sealed class ClearPdcDto
{
    public string DepositBankAccountCode { get; set; } = "111201";
    public DateTime ClearedDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}

public sealed class BouncePdcDto
{
    public DateTime BouncedDate { get; set; } = DateTime.UtcNow;
    public string BounceReason { get; set; } = string.Empty;
    public decimal BankCharges { get; set; }
    public string? BankChargesAccountCode { get; set; } = "510901"; // مصاريف بنكية
    public string? Notes { get; set; }
}

public sealed class PdcFilterDto
{
    public long? BranchId { get; set; }
    public string? ChequeType { get; set; }
    public string? Status { get; set; }
    public string? PartyCode { get; set; }
    public DateTime? FromDueDate { get; set; }
    public DateTime? ToDueDate { get; set; }
}

public sealed class UpcomingMaturitySummaryDto
{
    public int TotalChequesCount { get; set; }
    public decimal TotalAmount { get; set; }
    public int DueIn7DaysCount { get; set; }
    public decimal DueIn7DaysAmount { get; set; }
    public int DueIn30DaysCount { get; set; }
    public decimal DueIn30DaysAmount { get; set; }
    public List<PdcRegisterDto> Cheques { get; set; } = new();
}
