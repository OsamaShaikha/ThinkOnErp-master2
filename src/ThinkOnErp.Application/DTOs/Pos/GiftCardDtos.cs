using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Pos;

public class CreateGiftCardDto
{
    public long BranchId { get; set; }
    public string CardCode { get; set; } = string.Empty;
    public string? Pin { get; set; }
    public decimal InitialBalance { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class UpdateGiftCardDto
{
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LoadGiftCardDto
{
    public long GiftCardId { get; set; }
    public decimal Amount { get; set; }
}

public class RedeemGiftCardDto
{
    public long BranchId { get; set; }
    public string CardCode { get; set; } = string.Empty;
    public string? Pin { get; set; }
    public decimal Amount { get; set; }
    public long? OrderId { get; set; }
}

public class GiftCardSummaryDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string CardCode { get; set; } = string.Empty;
    public decimal InitialBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; }
}

public class GiftCardDetailsDto : GiftCardSummaryDto
{
    public List<GiftCardTransactionDto> Transactions { get; set; } = new();
}

public class GiftCardTransactionDto
{
    public long Id { get; set; }
    public long GiftCardId { get; set; }
    public long? OrderId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime TransactionDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
