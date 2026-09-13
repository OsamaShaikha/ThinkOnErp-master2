using System;

namespace ThinkOnErp.Application.DTOs.Accounting.PaymentMethods;

/// <summary>
/// بيانات وسيلة الدفع مع تفاصيل الربط المحاسبي (GL Account, Bank, Cash Register)
/// </summary>
public sealed class PaymentMethodDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    public string MethodType { get; set; } = "CASH";

    // الربط بحساب الأستاذ العام
    public string GlAccountCode { get; set; } = string.Empty;
    public string? GlAccountName { get; set; }

    // الربط بالحساب البنكي إن وجد
    public long? BankAccountId { get; set; }
    public string? BankAccountName { get; set; }

    // الربط بصندوق الكاشير إن وجد
    public long? CashRegisterId { get; set; }
    public string? CashRegisterName { get; set; }

    // العمولات البنكية ومصروف العمولة
    public decimal CommissionPercent { get; set; }
    public decimal CommissionFixedAmount { get; set; }
    public string? CommissionGlAccountCode { get; set; }
    public string? CommissionGlAccountName { get; set; }

    // المحددات التشغيلية
    public bool RequiresReference { get; set; }
    public bool RequiresDueDate { get; set; }
    public bool AutoPostGl { get; set; }
    public bool ShowInPos { get; set; }
    public bool ShowInInvoices { get; set; }
    public bool ShowInVouchers { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }

    // بيانات التدقيق
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

public sealed class CreatePaymentMethodDto
{
    public long BranchId { get; set; } = 1;
    public string Code { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    public string MethodType { get; set; } = "CASH";
    public string GlAccountCode { get; set; } = "111101";

    public long? BankAccountId { get; set; }
    public long? CashRegisterId { get; set; }

    public decimal CommissionPercent { get; set; }
    public decimal CommissionFixedAmount { get; set; }
    public string? CommissionGlAccountCode { get; set; }

    public bool RequiresReference { get; set; }
    public bool RequiresDueDate { get; set; }
    public bool AutoPostGl { get; set; } = true;
    public bool ShowInPos { get; set; } = true;
    public bool ShowInInvoices { get; set; } = true;
    public bool ShowInVouchers { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 1;
}

public sealed class UpdatePaymentMethodDto
{
    public string? NameLocal { get; set; }
    public string? NameEn { get; set; }

    public string? MethodType { get; set; }
    public string? GlAccountCode { get; set; }

    public long? BankAccountId { get; set; }
    public long? CashRegisterId { get; set; }

    public decimal? CommissionPercent { get; set; }
    public decimal? CommissionFixedAmount { get; set; }
    public string? CommissionGlAccountCode { get; set; }

    public bool? RequiresReference { get; set; }
    public bool? RequiresDueDate { get; set; }
    public bool? AutoPostGl { get; set; }
    public bool? ShowInPos { get; set; }
    public bool? ShowInInvoices { get; set; }
    public bool? ShowInVouchers { get; set; }
    public bool? IsActive { get; set; }
    public int? DisplayOrder { get; set; }
}
