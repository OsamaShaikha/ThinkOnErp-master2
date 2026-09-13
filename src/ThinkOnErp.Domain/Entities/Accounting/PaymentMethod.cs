using System;

namespace ThinkOnErp.Domain.Entities.Accounting;

/// <summary>
/// كيان تعريف وسيلة وطريقة الدفع المالي ومحدداتها المحاسبية والتشغيلية
/// </summary>
public sealed class PaymentMethod
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    /// <summary>
    /// نوع وسيلة الدفع (CASH, BANK, CARD, CHEQUE, CREDIT_ACCOUNT, DIGITAL_WALLET)
    /// </summary>
    public string MethodType { get; set; } = "CASH";

    /// <summary>
    /// حساب الأستاذ العام المرتبط (مثل: 111101 الصندوق، 111201 البنك، 111301 شيكات برسم التحصيل)
    /// </summary>
    public string GlAccountCode { get; set; } = "111101";

    /// <summary>
    /// ربط اختياري بالحساب البنكي المحدد في جدول BANK_ACCOUNT
    /// </summary>
    public long? BankAccountId { get; set; }

    /// <summary>
    /// ربط اختياري بصندوق الكاشير أو الخزينة المحددة في جدول CASH_REGISTER
    /// </summary>
    public long? CashRegisterId { get; set; }

    /// <summary>
    /// نسبة عمولة الشبكة أو البنك المقتطعة (مثال: 1.5% لبطاقات الائتمان)
    /// </summary>
    public decimal CommissionPercent { get; set; }

    /// <summary>
    /// مبلغ عمولة ثابت لكل عملية
    /// </summary>
    public decimal CommissionFixedAmount { get; set; }

    /// <summary>
    /// حساب الأستاذ العام لمصروف العمولة البنكية (مثل: 531201 عمولات بنكية)
    /// </summary>
    public string? CommissionGlAccountCode { get; set; }

    /// <summary>
    /// هل يلزم إدخال رقم مرجع / تفويض الشبكة / رقم الإيصال؟
    /// </summary>
    public bool RequiresReference { get; set; }

    /// <summary>
    /// هل يلزم تحديد تاريخ استحقاق (للشيكات أو الفواتير الآجلة)؟
    /// </summary>
    public bool RequiresDueDate { get; set; }

    /// <summary>
    /// ترحيل قيد محاسبي مباشر وتلقائي فور تأكيد الدفعة
    /// </summary>
    public bool AutoPostGl { get; set; } = true;

    /// <summary>
    /// إتاحة وسيلة الدفع في شاشات نقاط البيع والكاشير POS
    /// </summary>
    public bool ShowInPos { get; set; } = true;

    /// <summary>
    /// إتاحة وسيلة الدفع في فواتير المبيعات والمشتريات
    /// </summary>
    public bool ShowInInvoices { get; set; } = true;

    /// <summary>
    /// إتاحة وسيلة الدفع في سندات القبض والصرف
    /// </summary>
    public bool ShowInVouchers { get; set; } = true;

    /// <summary>
    /// حالة التفعيل
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// ترتيب العرض في الواجهات والأزرار
    /// </summary>
    public int DisplayOrder { get; set; } = 1;

    // بيانات التدقيق (Audit)
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // العلاقات المحاسبية
    public SysBranch? Branch { get; set; }
    public GlAccount? GlAccount { get; set; }
    public GlAccount? CommissionGlAccount { get; set; }
    public BankAccount? BankAccount { get; set; }
    public CashRegister? CashRegister { get; set; }
}
