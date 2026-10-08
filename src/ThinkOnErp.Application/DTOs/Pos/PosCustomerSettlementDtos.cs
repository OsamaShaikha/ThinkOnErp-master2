using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.DTOs.Pos;

/// <summary>
/// نوع سداد الذمة من نقطة البيع
/// </summary>
public enum PosCustomerSettlementType
{
    /// <summary>
    /// دفعة عامة على الحساب (تُوزع آلياً FIFO على الفواتير المفتوحة وما زاد يبقى رصيد دائن للعميل)
    /// </summary>
    OnAccount = 1,

    /// <summary>
    /// سداد فاتورة محددة (أو فواتير معينة مختارة)
    /// </summary>
    SpecificInvoice = 2,

    /// <summary>
    /// سداد كامل الفواتير المفتوحة دفعة واحدة
    /// </summary>
    AllInvoices = 3
}

/// <summary>
/// فاتورة مفتوحة غير مسددة للعميل معروضة في شاشة نقطة البيع
/// </summary>
public sealed class PosCustomerOpenInvoiceDto
{
    public long TransactionId { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTime.UtcNow && RemainingAmount > 0;
}

/// <summary>
/// طلب سداد ذمة من نقطة البيع
/// </summary>
public sealed class PosCustomerSettlementRequestDto
{
    /// <summary>
    /// رقم الوردية الحالية في نقطة البيع لتسجيل النقدية في الدرج (اختياري)
    /// </summary>
    public long? ShiftId { get; set; }

    /// <summary>
    /// نوع السداد: يمكن تمريره كرقم كود من جدول SYS_CODE (CODE_MGR = 39) أو كنص (1: OnAccount, 2: SpecificInvoice, 3: AllInvoices)
    /// </summary>
    public PosCustomerSettlementType SettlementType { get; set; } = PosCustomerSettlementType.OnAccount;

    /// <summary>
    /// كود نوع السداد الرقمي من جدول SYS_CODE (CODE_MGR = 39): 1 = OnAccount, 2 = SpecificInvoice, 3 = AllInvoices
    /// </summary>
    public int? SettlementTypeCode
    {
        get => (int)SettlementType;
        set
        {
            if (value.HasValue && Enum.IsDefined(typeof(PosCustomerSettlementType), value.Value))
            {
                SettlementType = (PosCustomerSettlementType)value.Value;
            }
        }
    }

    /// <summary>
    /// المبلغ المدفوع. في حال اختيار AllInvoices أو SpecificInvoice يمكن تركه فارغاً أو 0 لسداد كامل القيمة المتبقية تلقائياً.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// طريقة الدفع: Cash (1), Card (2), Cheque (5), BankTransfer (6)
    /// </summary>
    public PosPaymentMethod PaymentMethod { get; set; } = PosPaymentMethod.Cash;

    /// <summary>
    /// رقم العملية المرجعي للبطاقة أو الشيك (اختياري)
    /// </summary>
    public string? ReferenceNo { get; set; }

    /// <summary>
    /// معرف الفاتورة المراد سدادها في حال كان نوع السداد SpecificInvoice
    /// </summary>
    public long? SpecificInvoiceTransactionId { get; set; }

    /// <summary>
    /// قائمة اختيارية لتخصيص سداد عدة فواتير معينة بمبالغ محددة لكل منها
    /// </summary>
    public List<PosSettlementInvoiceItemDto>? Invoices { get; set; }

    /// <summary>
    /// ملاحظات أو بيان العملية
    /// </summary>
    public string? Notes { get; set; }
}

public sealed class PosSettlementInvoiceItemDto
{
    public long InvoiceTransactionId { get; set; }
    public decimal AmountToApply { get; set; }
}

/// <summary>
/// نتيجة وتفاصيل عملية سداد الذمة للطباعة في إيصال الكاشير
/// </summary>
public sealed class PosCustomerSettlementResultDto
{
    public string ReceiptNumber { get; set; } = string.Empty;
    public long VoucherId { get; set; }
    public DateTime SettlementDate { get; set; }
    public long CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public PosCustomerSettlementType SettlementType { get; set; }
    public int SettlementTypeCode => (int)SettlementType;
    public string SettlementTypeName => SettlementType switch
    {
        PosCustomerSettlementType.OnAccount => "دفعة على الحساب",
        PosCustomerSettlementType.SpecificInvoice => "سداد فاتورة محددة",
        PosCustomerSettlementType.AllInvoices => "سداد كامل الفواتير",
        _ => SettlementType.ToString()
    };
    public PosPaymentMethod PaymentMethod { get; set; }
    public int PaymentMethodCode => (int)PaymentMethod;
    public decimal AmountPaid { get; set; }
    public decimal PreviousBalance { get; set; }
    public decimal RemainingBalance { get; set; }
    public decimal AppliedToInvoicesAmount { get; set; }
    public decimal UnappliedCreditAmount { get; set; }
    public int InvoicesSettledCount { get; set; }
    public List<PosSettledInvoiceDetailDto> SettledInvoices { get; set; } = new();
}

public sealed class PosSettledInvoiceDetailDto
{
    public long InvoiceTransactionId { get; set; }
    public string? ReferenceNo { get; set; }
    public decimal AppliedAmount { get; set; }
    public decimal RemainingInvoiceAmount { get; set; }
    public bool IsFullyPaid { get; set; }
}
