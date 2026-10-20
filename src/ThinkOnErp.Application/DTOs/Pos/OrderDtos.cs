using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.DTOs.Pos;

public class CreatePosOrderDto
{
    public long BranchId { get; set; }
    public long ShiftId { get; set; }
    public long TillId { get; set; }
    /// <summary>
    /// معرّف فريد للطلب يتم توليده من جهاز الكاشير (UUID) لمنع تكرار الفاتورة عند تكرار الإرسال أو العمل بدون اتصال (اختياري - يُنشأ تلقائياً في السيرفر إن تُرك فارغاً).
    /// </summary>
    public string? ClientUuid { get; set; }

    /// <summary>
    /// نوع الطلب كود رقمي (1: DineIn محلي, 2: Takeaway سفري, 3: Delivery توصيل, 4: Aggregator تطبيقات, 5: Kiosk كشك, 6: QrTable طاولة).
    /// </summary>
    public int OrderType { get; set; } = 1;

    /// <summary>
    /// حالة الطلب كود رقمي (1: Draft مسودة, 2: Parked معلق, 3: SentToKitchen للمطبخ, 4: Ready جاهز, 5: Completed مكتمل ومسدد).
    /// </summary>
    public int Status { get; set; } = 1;

    public long? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public long? TableId { get; set; }
    public int Covers { get; set; } = 1;
    public long? PriceListId { get; set; }

    public decimal ManualDiscountAmount { get; set; }
    public decimal ManualDiscountPercent { get; set; }
    public string? DiscountReason { get; set; }

    public decimal ServiceChargeAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TipAmount { get; set; }
    public string? Notes { get; set; }

    public List<CreatePosOrderLineDto> Lines { get; set; } = new();
    public List<CreatePosOrderPaymentDto> Payments { get; set; } = new();
}

public class CreatePosOrderLineDto
{
    public int LineNumber { get; set; }
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public int UomId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public string? PrepStation { get; set; }
    public bool IsScaleItem { get; set; }
    public decimal? ScaleWeight { get; set; }
    public string? ScaleBarcode { get; set; }
    public long? SalesEmployeeId { get; set; }
    public string? SpecialInstructions { get; set; }
    public List<CreatePosOrderLineModifierDto> Modifiers { get; set; } = new();
}

public class CreatePosOrderLineModifierDto
{
    public long ModifierItemId { get; set; }
    public string ModifierName { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal ExtraPrice { get; set; }
}

public class CreatePosOrderPaymentDto
{
    /// <summary>
    /// كود وسيلة الدفع الرقمي من GET /api/pos/payment-methods (1: Cash نقدي, 2: Card شبكة/مدى, 3: Split دفع مجزأ, 4: CustomerAccount على الحساب, 5: Cheque شيك, 6: BankTransfer تحويل بنكي, 11: Hospitality ضيافة...).
    /// </summary>
    public int PaymentMethod { get; set; } = 1;
    public decimal Amount { get; set; }
    public decimal TenderedAmount { get; set; }
    public decimal ChangeAmount { get; set; }

    public string? CardNumberMasked { get; set; }
    public string? CardType { get; set; }
    public string? TransactionReference { get; set; }
    public string? AuthCode { get; set; }
    public string? TerminalId { get; set; }
    public string? ChequeNumber { get; set; }
    public string? GiftCardCode { get; set; }
    public long? LoyaltyPointsRedeemed { get; set; }
}

public class PosOrderSummaryDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long ShiftId { get; set; }
    public long TillId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public string ClientUuid { get; set; } = string.Empty;

    public PosOrderType OrderType { get; set; }
    public int OrderTypeCode => (int)OrderType;
    public PosOrderStatus Status { get; set; }
    public int StatusCode => (int)Status;

    public long? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public long? TableId { get; set; }
    public string? TableNumber { get; set; }
    public int Covers { get; set; }

    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TipAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public bool IsPaid { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public int PaymentStatusCode { get; set; }
    public string PaymentStatusNameAr { get; set; } = string.Empty;

    public bool IsRefund { get; set; }
    public long? OriginalOrderId { get; set; }
    public string? OriginalInvoiceNumber { get; set; }
    public string? OriginalOrderNumber { get; set; }
    public string? RefundReason { get; set; }
    public string? EInvoiceQrCode { get; set; }
    public string? EInvoiceHash { get; set; }
    public DateTime CreationDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;

    // Customer Account & Credit Details
    public bool IsCreditSale { get; set; }
    public decimal CustomerAccountAmount { get; set; }
    public decimal CustomerAdvanceDeducted { get; set; }
    public decimal CustomerReceivableCharged { get; set; }
    public decimal? CustomerBalanceAfter { get; set; }
    public decimal? CustomerAvailableCreditAfter { get; set; }

    // Refund Status & Tracking
    public string RefundStatus { get; set; } = "None"; // None, PartiallyRefunded, FullyRefunded
    public int RefundStatusCode { get; set; } = 0; // 0 = None, 1 = Partial, 2 = Full
    public string RefundStatusNameAr { get; set; } = "بدون مرتجع";
    public decimal RefundedAmount { get; set; }
    public decimal RemainingRefundableAmount { get; set; }
    public List<string> RefundInvoices { get; set; } = new();

    public List<PosOrderLineSummaryDto> Lines { get; set; } = new();
    public List<PosOrderPaymentSummaryDto> Payments { get; set; } = new();
}

public class PosOrderLineSummaryDto
{
    public long Id { get; set; }
    public int LineNumber { get; set; }
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public int UomId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public PosKdsStatus KdsStatus { get; set; }
    public string? PrepStation { get; set; }
    public bool IsScaleItem { get; set; }
    public decimal? ScaleWeight { get; set; }
    public bool IsVoided { get; set; }
    public string? VoidReason { get; set; }

    // Line Refund Tracking
    public decimal RefundedQuantity { get; set; }
    public decimal RemainingRefundableQuantity { get; set; }
    public bool IsFullyRefunded { get; set; }

    public List<PosOrderLineModifierDto> Modifiers { get; set; } = new();
}

public class PosOrderLineModifierDto
{
    public long Id { get; set; }
    public long ModifierItemId { get; set; }
    public string ModifierName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal ExtraPrice { get; set; }
}

public class PosOrderPaymentSummaryDto
{
    public long Id { get; set; }
    public PosPaymentMethod PaymentMethod { get; set; }
    public int PaymentMethodCode => (int)PaymentMethod;
    public decimal Amount { get; set; }
    public decimal TenderedAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public string? CardNumberMasked { get; set; }
    public string? TransactionReference { get; set; }
    public DateTime PaymentDate { get; set; }
}

public class RefundOrderDto
{
    /// <summary>
    /// معرّف الطلب الأصلي (PosOrderHeader.Id). يمكن استخدامه، أو استخدام InvoiceNumber أو OrderNumber بدلاً منه.
    /// </summary>
    public long OrderId { get; set; }

    /// <summary>
    /// رقم الفاتورة الأصلية (مثال: INV-2026-000051). يمكن إرساله مباشرة دون الحاجة لمعرفة OrderId.
    /// </summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>
    /// رقم الطلب الأصلي في الوردية (مثال: 0085).
    /// </summary>
    public string? OrderNumber { get; set; }

    /// <summary>
    /// معرّف الفرع (اختياري للبحث برقم الطلب).
    /// </summary>
    public long? BranchId { get; set; }

    /// <summary>
    /// معرّف الوردية الحالية التي يتم الإرجاع خلالها (إن لم يُحدد، تُستخدم الوردية النشطة للصندوق أو وردية الطلب الأصلي).
    /// </summary>
    public long? ShiftId { get; set; }

    /// <summary>
    /// معرّف الصندوق / نقطة البيع الحالية (اختياري).
    /// </summary>
    public long? TillId { get; set; }

    /// <summary>
    /// سبب الإرجاع.
    /// </summary>
    public string RefundReason { get; set; } = string.Empty;

    public string? ApprovedBy { get; set; }
    public bool ReturnToInventory { get; set; } = true;
    public List<RefundOrderLineDto> RefundLines { get; set; } = new();
    public List<CreatePosOrderPaymentDto> RefundPayments { get; set; } = new();
}

public class RefundOrderLineDto
{
    public long OrderLineId { get; set; }
    public decimal QuantityToRefund { get; set; }
}

public class VoidLineDto
{
    public long OrderId { get; set; }
    public long OrderLineId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
}

public class UpdatePosOrderDto
{
    /// <summary>
    /// حالة الطلب الاختيارية كود رقمي (1: Draft مسودة, 2: Parked معلق, 3: SentToKitchen للمطبخ, 4: Ready جاهز, 5: Completed مكتمل ومسدد).
    /// </summary>
    public int? Status { get; set; }
    public long? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public long? TableId { get; set; }
    public int Covers { get; set; } = 1;
    public decimal ManualDiscountAmount { get; set; }
    public decimal ManualDiscountPercent { get; set; }
    public string? DiscountReason { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TipAmount { get; set; }
    public string? Notes { get; set; }
}

public class UpdatePosOrderStatusDto
{
    /// <summary>
    /// الحالة الجديدة للطلب كود رقمي (1: Draft مسودة, 2: Parked معلق, 3: SentToKitchen مرسل للمطبخ, 4: Ready جاهز, 5: Completed مكتمل, 6: Voided ملغي, 7: Refunded مسترجع).
    /// </summary>
    public int Status { get; set; } = 1;
}

public class AddOrderLinesDto
{
    public List<CreatePosOrderLineDto> Lines { get; set; } = new();
    public bool SendToKitchen { get; set; } = true;
    public string? AdditionalNotes { get; set; }
}

public class UpdateOrderLineDto
{
    public decimal? Quantity { get; set; }
    public string? SpecialInstructions { get; set; }
}

public class SplitPosOrderDto
{
    /// <summary>
    /// قائمة السطور والكميات المراد فصلها عن الفاتورة الأصلية.
    /// </summary>
    public List<SplitOrderLineItemDto> LinesToSplit { get; set; } = new();

    /// <summary>
    /// هل يتم سداد الفاتورة المفصولة فوراً؟ (True = إغلاق وسداد للزبون الأول، False = بقاء الفاتورة مفتوحة/معلقة).
    /// </summary>
    public bool SettleImmediately { get; set; }

    /// <summary>
    /// معلومات السداد للفاتورة المفصولة (في حال تم تفعيل السداد الفوري).
    /// </summary>
    public List<CreatePosOrderPaymentDto> Payments { get; set; } = new();

    /// <summary>
    /// اسم الزبون الأول أو ملاحظة اختيارية للفاتورة المفصولة.
    /// </summary>
    public string? CustomerName { get; set; }

    /// <summary>
    /// ملاحظات الفاتورة المفصولة.
    /// </summary>
    public string? Notes { get; set; }
}

public class SplitOrderLineItemDto
{
    /// <summary>
    /// معرّف السطر في الفاتورة الأصلية (PosOrderLine.Id).
    /// </summary>
    public long LineId { get; set; }

    /// <summary>
    /// الكمية المراد فصلها من هذا السطر (يجب أن تكون أصغر من أو تساوي الكمية الحالية).
    /// </summary>
    public decimal Quantity { get; set; }
}

public class SplitOrderResultDto
{
    /// <summary>
    /// الطلب الأصلي المحدث (بعد خصم أو حذف السطور المفصولة).
    /// </summary>
    public PosOrderSummaryDto ParentOrder { get; set; } = null!;

    /// <summary>
    /// الطلب الجديد المفصول (الذي يخص الزبون الأول).
    /// </summary>
    public PosOrderSummaryDto SplitOrder { get; set; } = null!;
}

public class ExchangeOrderDto
{
    /// <summary>
    /// معرف الفاتورة الأصلية التي يتم استبدال أصناف منها.
    /// </summary>
    public long OriginalOrderId { get; set; }

    /// <summary>
    /// سبب التبديل أو الاسترجاع.
    /// </summary>
    public string ExchangeReason { get; set; } = string.Empty;

    /// <summary>
    /// قائمة الأصناف المراد إرجاعها واستبدالها من الفاتورة الأصلية.
    /// </summary>
    public List<RefundOrderLineDto> ReturnLines { get; set; } = new();

    /// <summary>
    /// قائمة الأصناف البديلة الجديدة المراد شراؤها.
    /// </summary>
    public List<CreatePosOrderLineDto> NewLines { get; set; } = new();

    /// <summary>
    /// دفعات تسوية الفارق (في حال كان الصنف الجديد أغلى من الصنف المرتجع).
    /// </summary>
    public List<CreatePosOrderPaymentDto> DifferencePayments { get; set; } = new();

    /// <summary>
    /// هل يتم إرجاع الصنف المستبدل إلى المخزون تلقائياً؟
    /// </summary>
    public bool ReturnToInventory { get; set; } = true;

    /// <summary>
    /// ملاحظات إضافية على عملية الاستبدال.
    /// </summary>
    public string? Notes { get; set; }
}

public class ExchangeOrderResultDto
{
    /// <summary>
    /// فاتورة الإرجاع / إشعار دائن (Refund / Credit Note) الخاص بالأصناف المعادة.
    /// </summary>
    public PosOrderSummaryDto RefundOrder { get; set; } = null!;

    /// <summary>
    /// فاتورة المبيعات الجديدة الخاصة بالأصناف البديلة.
    /// </summary>
    public PosOrderSummaryDto NewSalesOrder { get; set; } = null!;

    /// <summary>
    /// إجمالي قيمة المرتجع (شامل الضريبة).
    /// </summary>
    public decimal ReturnedTotalAmount { get; set; }

    /// <summary>
    /// إجمالي قيمة الأصناف الجديدة (شامل الضريبة).
    /// </summary>
    public decimal NewSalesTotalAmount { get; set; }

    /// <summary>
    /// فارق السعر الصافي (قيمة المبيعات الجديدة - قيمة المرتجع).
    /// إذا كان موجباً، يدفعه الزبون. وإذا كان سالباً، يُعاد للزبون أو رصيد دائن.
    /// </summary>
    public decimal NetDifferenceAmount { get; set; }

    /// <summary>
    /// المبلغ المسدد إضافياً من الزبون (إن وجد).
    /// </summary>
    public decimal PaidDifferenceAmount { get; set; }

    /// <summary>
    /// المبلغ المتبقي للإرجاع للزبون (في حال كان الصنف الجديد أرخص).
    /// </summary>
    public decimal ChangeDueToCustomer { get; set; }
}

