namespace ThinkOnErp.Application.DTOs.Pos;

/// <summary>
/// وسيلة دفع لشاشة نقاط البيع (POS Cashier Payment Method)
/// </summary>
public sealed class PosPaymentMethodDto
{
    /// <summary>
    /// الرقم التعريفي لوسيلة الدفع وهو نفسه الكود الرقمي المستخدم في إنشاء الطلبات وسداد الذمم (1: نقدي، 2: شبكة، 4: على الحساب...)
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// الكود البرمجي (Cash, Card, CustomerAccount...)
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// الاسم المعروض على زر الكاشير (نقدي، بطاقة دفع / مدى، حساب عميل...)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// الاسم بالإنجليزية (Cash, Card, Customer Account...)
    /// </summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>
    /// نوع وسيلة الدفع (CASH, CARD, SPLIT, ACCOUNT, CHEQUE, BANK, WALLET)
    /// </summary>
    public string Type { get; set; } = "CASH";

    /// <summary>
    /// هل تتطلب وسيلة الدفع إدخال رقم مرجعي (مثل رقم العملية في إيصال الشبكة أو الشيك)
    /// </summary>
    public bool RequiresReference { get; set; }

    /// <summary>
    /// هل تتطلب وسيلة الدفع إدخال تاريخ استحقاق (مثل الشيكات أو الذمم)
    /// </summary>
    public bool RequiresDueDate { get; set; }

    /// <summary>
    /// نسبة عمولة الشبكة أو البنك إن وجدت
    /// </summary>
    public decimal CommissionPercent { get; set; }

    /// <summary>
    /// ترتيب ظهور الزر في شاشة الكاشير
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// حالة التفعيل
    /// </summary>
    public bool IsActive { get; set; } = true;
}
