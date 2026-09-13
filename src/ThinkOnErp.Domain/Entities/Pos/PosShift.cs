using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosShift
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long TillId { get; set; }
    public long CashierUserId { get; set; }
    public string ShiftNumber { get; set; } = string.Empty;
    public PosShiftStatus Status { get; set; } = PosShiftStatus.Open;

    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? BlindClosedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public decimal OpeningFloat { get; set; }
    public decimal TotalCashSales { get; set; }
    public decimal TotalCardSales { get; set; }
    public decimal TotalOtherSales { get; set; }
    public decimal TotalCashRefunds { get; set; }
    public decimal TotalCardRefunds { get; set; }
    public decimal TotalFloatIn { get; set; }
    public decimal TotalCashDrop { get; set; }
    public decimal TotalPayOut { get; set; }
    public decimal TotalTipPayout { get; set; }

    // Blind close values
    public decimal ExpectedCashInDrawer { get; set; }
    public decimal? CountedCashAmount { get; set; }
    public decimal? VarianceAmount { get; set; }

    // Manager Audit
    public bool IsAudited { get; set; }
    public string? AuditedBy { get; set; }
    public DateTime? AuditedAt { get; set; }
    public string? AuditNotes { get; set; }

    // Audit fields
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigation
    public PosTill? Till { get; set; }
    public SysUser? CashierUser { get; set; }
    public SysBranch? Branch { get; set; }
    public ICollection<PosShiftCashMovement> CashMovements { get; set; } = new List<PosShiftCashMovement>();
    public ICollection<PosOrderHeader> Orders { get; set; } = new List<PosOrderHeader>();
}
