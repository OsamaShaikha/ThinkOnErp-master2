namespace ThinkOnErp.Domain.Entities.Accounting;

/// <summary>
/// تصنيف وسيلة الدفع من الناحية المحاسبية والتشغيلية
/// </summary>
public enum PaymentMethodType
{
    /// <summary>
    /// نقدي (صندوق الكاشير أو الخزينة الرئيسية)
    /// </summary>
    Cash = 1,

    /// <summary>
    /// بنكي / تحويل بنكي مباشر
    /// </summary>
    Bank = 2,

    /// <summary>
    /// شبكة / بطاقات ائتمان / نقاط بيع POS (Mada, Visa, MC)
    /// </summary>
    Card = 3,

    /// <summary>
    /// شيكات بنكية (شيكات تحت التحصيل أو شيكات مؤجلة)
    /// </summary>
    Cheque = 4,

    /// <summary>
    /// على الحساب / ذمم دائنة أو مدينة (Credit / Customer Account)
    /// </summary>
    CreditAccount = 5,

    /// <summary>
    /// محافظ إلكترونية ودفع آجل (Apple Pay, STC Pay, Tabby, Tamara)
    /// </summary>
    DigitalWallet = 6
}
