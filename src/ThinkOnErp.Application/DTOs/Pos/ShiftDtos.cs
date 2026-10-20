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
    public int PaymentMethod { get; set; } = (int)PosPaymentMethod.Cash;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
}

public class BlindCloseShiftDto
{
    public long ShiftId { get; set; }
    public decimal CountedCashAmount { get; set; }
    public decimal? CountedCardAmount { get; set; }
    public string? ClosingNotes { get; set; }
    public List<TenderDeclarationInputDto>? TenderDeclarations { get; set; }
}

public class TenderDeclarationInputDto
{
    public int PaymentMethod { get; set; }
    public decimal CountedAmount { get; set; }
    public string? Notes { get; set; }
}

public class PosTenderReconciliationDto
{
    public int PaymentMethod { get; set; }
    public string PaymentMethodName { get; set; } = string.Empty;
    public decimal ExpectedAmount { get; set; }
    public decimal CountedAmount { get; set; }
    public decimal VarianceAmount { get; set; }
    public bool IsAutoDeclared { get; set; }
    public string? Notes { get; set; }
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
    public int ClosingMode { get; set; } = 1; // 1: EnforceBlindClose (إلزام بالإغلاق الأعمى), 2: StandardOpenClose
    public bool IsBlindCloseEnforced => ClosingMode == 1;

    public DateTime OpenedAt { get; set; }
    public DateTime? BlindClosedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public decimal OpeningFloat { get; set; }
    public decimal TotalCashSales { get; set; }
    public decimal TotalCardSales { get; set; }
    public decimal TotalCustomerAccountSales { get; set; }
    public decimal TotalHospitalitySales { get; set; }
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

    public decimal ExpectedCardAmount { get; set; }
    public decimal? CountedCardAmount { get; set; }
    public decimal? CardVarianceAmount { get; set; }

    public bool IsAudited { get; set; }
    public string? AuditedBy { get; set; }
    public DateTime? AuditedAt { get; set; }
    public string? AuditNotes { get; set; }

    public List<PosShiftCashMovementItemDto> CashMovements { get; set; } = new();
    public List<PosTenderReconciliationDto> TenderReconciliations { get; set; } = new();
    public ZReportSummaryDto? ZReport { get; set; }
}

public class PosShiftCashMovementItemDto
{
    public long Id { get; set; }
    public PosCashMovementType MovementType { get; set; }
    public int PaymentMethod { get; set; }
    public string PaymentMethodName { get; set; } = string.Empty;
    public int VoucherType { get; set; }
    public string VoucherTypeName { get; set; } = string.Empty;
    public string? VoucherNumber { get; set; }
    public string? GlAccountCode { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? TaxInvoiceNumber { get; set; }
    public long? GlVoucherId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime CreationDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}

public class CreatePosVoucherDto
{
    public long ShiftId { get; set; }
    public int VoucherType { get; set; } = (int)PosVoucherType.Payment; // 1: Receipt (قبض), 2: Payment (صرف)
    public int PaymentMethod { get; set; } = (int)PosPaymentMethod.Cash; // 1: Cash نقدي, 2: Card شبكة/مدى, 6: BankTransfer تحويل بنكي, 5: Cheque شيك
    public decimal Amount { get; set; }
    public string? GlAccountCode { get; set; }
    public string? CustomerCode { get; set; }
    public string? VendorCode { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? TaxInvoiceNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string? ChequeNumber { get; set; }
    public string? TransferReference { get; set; }
}

/// <summary>
/// نموذج إنشاء سند قبض لصندوق نقطة البيع (Receipt Voucher) - يعتمد آلياً على الحساب الافتراضي ووسيلة الدفع
/// </summary>
public class CreatePosReceiptVoucherDto
{
    public long ShiftId { get; set; }
    public decimal Amount { get; set; }
    public int PaymentMethod { get; set; } = (int)PosPaymentMethod.Cash; // 1: Cash نقدي, 2: Card شبكة, 6: BankTransfer, 5: Cheque
    public string? GlAccountCode { get; set; } // اختياري - في حال عدم تحديده يعتمد تلقائياً على الحساب الافتراضي
    public string? CustomerCode { get; set; }
    public string? PayerName { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string? ChequeNumber { get; set; }
    public string? TransferReference { get; set; }
}

/// <summary>
/// نموذج إنشاء سند صرف من صندوق نقطة البيع (Payment Voucher) - يعتمد آلياً على الحساب الافتراضي ووسيلة الدفع
/// </summary>
public class CreatePosPaymentVoucherDto
{
    public long ShiftId { get; set; }
    public decimal Amount { get; set; }
    public int PaymentMethod { get; set; } = (int)PosPaymentMethod.Cash; // 1: Cash نقدي, 2: Card شبكة, 6: BankTransfer, 5: Cheque
    public string? GlAccountCode { get; set; } // اختياري - في حال عدم تحديده يعتمد تلقائياً على الحساب الافتراضي
    public string? VendorCode { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? TaxInvoiceNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string? ChequeNumber { get; set; }
    public string? TransferReference { get; set; }
}

public class PosVoucherSummaryDto
{
    public long Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public long ShiftId { get; set; }
    public int VoucherType { get; set; }
    public string VoucherTypeName { get; set; } = string.Empty;
    public int PaymentMethod { get; set; }
    public string PaymentMethodName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? GlAccountCode { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? TaxInvoiceNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public long? GlVoucherId { get; set; }
    public DateTime CreationDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}

public class PosExpenseAccountLookupDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNameAr { get; set; } = string.Empty;
    public string AccountNameEn { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// نموذج الحسابات الافتراضية لنقاط البيع (الصندوق، المصروفات النثرية، المقبوضات المتنوعة)
/// </summary>
public class PosDefaultAccountsDto
{
    /// <summary>
    /// معرّف الفرع (في حال كان الإعداد مخصصاً لفرع معين، أو null إذا كان الإعداد العام للمنشأة)
    /// </summary>
    public long? BranchId { get; set; }
    public string? BranchName { get; set; }

    /// <summary>
    /// حساب صندوق نقطة البيع الافتراضي (مثلاً: 111102)
    /// </summary>
    public string DefaultCashAccount { get; set; } = "111102";
    public string? DefaultCashAccountNameAr { get; set; }

    /// <summary>
    /// حساب المصروفات النثرية الافتراضي لسندات الصرف (مثلاً: 525101)
    /// </summary>
    public string DefaultExpenseAccount { get; set; } = "525101";
    public string? DefaultExpenseAccountNameAr { get; set; }

    /// <summary>
    /// حساب الإيرادات والمقبوضات الافتراضي لسندات القبض (مثلاً: 421101)
    /// </summary>
    public string DefaultReceiptAccount { get; set; } = "421101";
    public string? DefaultReceiptAccountNameAr { get; set; }

    /// <summary>
    /// المستودع الافتراضي لمبيعات الـ POS (اختياري)
    /// </summary>
    public long? DefaultWarehouseId { get; set; }
}

/// <summary>
/// نموذج تعديل الحسابات الافتراضية لنقاط البيع (حسب الفرع أو عام)
/// </summary>
public class UpdatePosDefaultAccountsDto
{
    /// <summary>
    /// معرّف الفرع لتخصيص الإعداد للفرع (إذا كان فارغاً أو صفر يتم تحديث الإعداد العام للمنشأة)
    /// </summary>
    public long? BranchId { get; set; }

    /// <summary>
    /// حساب الصندوق الافتراضي (اختياري)
    /// </summary>
    public string? DefaultCashAccount { get; set; }

    /// <summary>
    /// حساب المصروف الافتراضي لسندات الصرف (اختياري)
    /// </summary>
    public string? DefaultExpenseAccount { get; set; }

    /// <summary>
    /// حساب القبض الافتراضي لسندات القبض (اختياري)
    /// </summary>
    public string? DefaultReceiptAccount { get; set; }

    /// <summary>
    /// المستودع الافتراضي لمبيعات الـ POS للفرع (اختياري)
    /// </summary>
    public long? DefaultWarehouseId { get; set; }
}
