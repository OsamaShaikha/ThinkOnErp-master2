using System;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosShiftCashMovement
{
    public long Id { get; set; }
    public long ShiftId { get; set; }
    public PosCashMovementType MovementType { get; set; }
    public PosPaymentMethod PaymentMethod { get; set; } = PosPaymentMethod.Cash;
    public PosVoucherType VoucherType { get; set; } = PosVoucherType.Payment;
    public string? VoucherNumber { get; set; }
    public string? GlAccountCode { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? TaxInvoiceNumber { get; set; }
    public long? GlVoucherId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public PosShift? Shift { get; set; }
}
