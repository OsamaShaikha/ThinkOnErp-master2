namespace ThinkOnErp.Application.DTOs.Pos;

/// <summary>
/// تفاصيل رصيد وسقف ائتمان العميل لنقاط البيع (POS Customer Balance & Credit Details)
/// </summary>
public sealed class PosCustomerBalanceDto
{
    public long CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    
    /// <summary>
    /// صافي الرصيد الحالي للعميل في الذمم (موجب = مدين/مستحق عليه، سالب = دائن/له رصيد)
    /// </summary>
    public decimal CurrentBalance { get; set; }

    /// <summary>
    /// إجمالي الفواتير المفتوحة غير المسددة
    /// </summary>
    public decimal OpenInvoicesBalance { get; set; }

    /// <summary>
    /// سقف الائتمان المسموح به
    /// </summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>
    /// الرصيد المتاح المتبقي للشراء الآجل (CreditLimit - CurrentBalance)
    /// </summary>
    public decimal? AvailableCredit => CreditLimit.HasValue ? CreditLimit.Value - CurrentBalance : null;

    /// <summary>
    /// هل تجاوز العميل سقف الائتمان وأصبح محظوراً من البيع الآجل
    /// </summary>
    public bool IsCreditBlocked => CreditLimit.HasValue && CurrentBalance > CreditLimit.Value;

    /// <summary>
    /// فترة السداد المسموحة بالأيام
    /// </summary>
    public int PaymentTermsDays { get; set; }

    /// <summary>
    /// عدد الفواتير المتأخرة عن تاريخ الاستحقاق
    /// </summary>
    public int OverdueInvoicesCount { get; set; }
}
