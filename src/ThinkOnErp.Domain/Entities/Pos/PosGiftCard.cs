using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosGiftCard
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string CardCode { get; set; } = string.Empty;
    public string? PinHash { get; set; }
    public decimal InitialBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public SysBranch? Branch { get; set; }
    public ICollection<PosGiftCardTransaction> Transactions { get; set; } = new List<PosGiftCardTransaction>();
}

public class PosGiftCardTransaction
{
    public long Id { get; set; }
    public long GiftCardId { get; set; }
    public long? OrderId { get; set; }
    public string TransactionType { get; set; } = "Redeem"; // Load, Redeem, Refund, Expire
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string CreationUser { get; set; } = string.Empty;

    // Navigation
    public PosGiftCard? GiftCard { get; set; }
    public PosOrderHeader? Order { get; set; }
}
