using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.DTOs.Pos;

public class OpenShiftDto
{
    public long BranchId { get; set; }
    public long TillId { get; set; }
    public long CashierUserId { get; set; }
    public decimal OpeningFloat { get; set; }
}

public class CashMovementDto
{
    public long ShiftId { get; set; }
    public PosCashMovementType MovementType { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
}

public class BlindCloseShiftDto
{
    public long ShiftId { get; set; }
    public decimal CountedCashAmount { get; set; }
    public string? ClosingNotes { get; set; }
}

public class AuditShiftDto
{
    public long ShiftId { get; set; }
    public string AuditedBy { get; set; } = string.Empty;
    public string? AuditNotes { get; set; }
    public bool CreateSettlementGlVoucher { get; set; }
}

public class PosShiftSummaryDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long TillId { get; set; }
    public string TillCode { get; set; } = string.Empty;
    public string TillName { get; set; } = string.Empty;
    public long CashierUserId { get; set; }
    public string CashierUserName { get; set; } = string.Empty;
    public string ShiftNumber { get; set; } = string.Empty;
    public PosShiftStatus Status { get; set; }

    public DateTime OpenedAt { get; set; }
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

    public decimal ExpectedCashInDrawer { get; set; }
    public decimal? CountedCashAmount { get; set; }
    public decimal? VarianceAmount { get; set; }

    public bool IsAudited { get; set; }
    public string? AuditedBy { get; set; }
    public DateTime? AuditedAt { get; set; }
    public string? AuditNotes { get; set; }

    public List<PosShiftCashMovementItemDto> CashMovements { get; set; } = new();
}

public class PosShiftCashMovementItemDto
{
    public long Id { get; set; }
    public PosCashMovementType MovementType { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime CreationDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
