using ThinkOnErp.Application.DTOs.Accounting.Tax;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Application.Services.Accounting.Tax.Declarations;

/// <summary>
/// Egypt VAT Return Declaration Provider - Form 10 (مصلحة الضرائب المصرية - ETA).
/// </summary>
public sealed class EgyptVatDeclarationProvider : ITaxDeclarationProvider
{
    public string TemplateCode => "EG_VAT_FORM_10";
    public string CountryCode => "EG";

    public TaxDeclarationTemplateDto GetTemplateMetadata()
    {
        return new TaxDeclarationTemplateDto
        {
            TemplateCode = TemplateCode,
            CountryCode = CountryCode,
            CountryNameAr = "جمهورية مصر العربية",
            CountryNameEn = "Arab Republic of Egypt",
            FlagEmoji = "🇪🇬",
            TitleAr = "إقرار ضريبة القيمة المضافة وضريبة الجدول (نموذج 10) - مصلحة الضرائب المصرية",
            TitleEn = "VAT & Schedule Tax Return (Form 10) - Egyptian Tax Authority (ETA)",
            TaxAuthorityNameAr = "مصلحة الضرائب المصرية (ETA)",
            TaxAuthorityNameEn = "Egyptian Tax Authority (ETA)",
            DefaultCurrencyCode = "EGP",
            FilingFrequency = "MONTHLY",
            Sections = new List<TaxDeclarationTemplateSectionDto>
            {
                new()
                {
                    SectionKey = "EG_SALES_SECTION",
                    TitleAr = "المبيعات من السلع والخدمات (المخرجات)",
                    TitleEn = "Sales of Goods & Services (Outputs)",
                    Boxes = new List<TaxDeclarationTemplateBoxDto>
                    {
                        new() { BoxNumber = 1, BoxKey = "EG_SALES_GENERAL_14", TitleAr = "مبيعات سلع وخدمات خاضعة للنسبة العامة (14%)", TitleEn = "General Goods & Services Sales (14%)" },
                        new() { BoxNumber = 2, BoxKey = "EG_SALES_SCHEDULE_TAX", TitleAr = "مبيعات سلع وخدمات خاضعة لضريبة الجدول", TitleEn = "Schedule Tax Items Sales" },
                        new() { BoxNumber = 3, BoxKey = "EG_SALES_ZERO_EXPORTS", TitleAr = "الصادرات والمبيعات الخاضعة لسعر (صفر %)", TitleEn = "Exports & Zero-Rated Sales (0%)" },
                        new() { BoxNumber = 4, BoxKey = "EG_SALES_EXEMPT", TitleAr = "المبيعات المعفاة من الضريبة (قائمة الإعفاءات)", TitleEn = "Exempt Sales" },
                        new() { BoxNumber = 5, BoxKey = "EG_TOTAL_OUTPUT_VAT", TitleAr = "إجمالي ضريبة المبيعات المستحقة", TitleEn = "Total Sales VAT Due", IsCalculated = true }
                    }
                },
                new()
                {
                    SectionKey = "EG_PURCHASES_SECTION",
                    TitleAr = "المدخلات والمشتريات القابلة للخصم",
                    TitleEn = "Deductible Inputs & Purchases",
                    Boxes = new List<TaxDeclarationTemplateBoxDto>
                    {
                        new() { BoxNumber = 6, BoxKey = "EG_PURCH_LOCAL_14", TitleAr = "مشتريات محلية خاضعة للسعر العام (14%)", TitleEn = "Local Domestic Purchases (14%)" },
                        new() { BoxNumber = 7, BoxKey = "EG_PURCH_IMPORTS", TitleAr = "السلع المستوردة المسدد عنها الضريبة بالإفراج الجمركي", TitleEn = "Imported Goods (Customs Release)" },
                        new() { BoxNumber = 8, BoxKey = "EG_PURCH_SCHEDULE_TAX", TitleAr = "المدخلات الخاضعة لضريبة الجدول", TitleEn = "Schedule Tax Inputs" },
                        new() { BoxNumber = 9, BoxKey = "EG_PURCH_EXEMPT_ZERO", TitleAr = "المشتريات المعفاة والواردة بسعر الصفر", TitleEn = "Exempt & Zero Purchases" },
                        new() { BoxNumber = 10, BoxKey = "EG_TOTAL_INPUT_VAT", TitleAr = "إجمالي الضريبة القابلة للخصم والتسوية", TitleEn = "Total Deductible Input VAT", IsCalculated = true }
                    }
                },
                new()
                {
                    SectionKey = "EG_SETTLEMENT_SECTION",
                    TitleAr = "تسوية حساب الضريبة وسداد الإقرار",
                    TitleEn = "Tax Settlement & Final Due",
                    Boxes = new List<TaxDeclarationTemplateBoxDto>
                    {
                        new() { BoxNumber = 11, BoxKey = "EG_NET_TAX_DUE", TitleAr = "صافي الضريبة المستحقة للفترة", TitleEn = "Net Tax Due", IsCalculated = true },
                        new() { BoxNumber = 12, BoxKey = "EG_WHT_DEDUCTIONS", TitleAr = "الخصم والتحصيل تحت حساب الضريبة (أ.ت.ص)", TitleEn = "Advance WHT Deductions / Credit" },
                        new() { BoxNumber = 13, BoxKey = "EG_FINAL_PAYABLE", TitleAr = "الضريبة واجبة السداد لمصلحة الضرائب / (الرصيد الدائن)", TitleEn = "Final Payable to ETA / (Credit Balance)", IsCalculated = true }
                    }
                }
            }
        };
    }

    public VatDeclarationDto BuildDeclaration(
        VatDeclarationFilterDto filter,
        IReadOnlyList<TaxTransaction> transactions,
        IReadOnlyList<TaxRate> availableRates,
        string branchNameAr,
        string branchNameEn)
    {
        var rateMap = availableRates.ToDictionary(r => r.Id);

        decimal genSalesBase = 0, genSalesVat = 0;
        decimal schedSalesBase = 0, schedSalesVat = 0;
        decimal zeroSalesBase = 0;
        decimal exemptSalesBase = 0;

        decimal genPurchBase = 0, genPurchVat = 0;
        decimal importPurchBase = 0, importPurchVat = 0;
        decimal exemptZeroPurchBase = 0;

        foreach (var t in transactions)
        {
            rateMap.TryGetValue(t.TaxRateId, out var rate);
            decimal pct = rate?.RatePercent ?? 0;
            bool isZero = rate?.IsZeroRated == true || pct == 0;
            bool isExempt = rate?.IsExempt == true;

            if (t.IsSalesTax)
            {
                if (pct >= 13.0m) // Standard Egypt Rate (14%)
                {
                    genSalesBase += t.LocalBaseAmount;
                    genSalesVat += t.LocalTaxAmount;
                }
                else if (pct > 0 && pct < 13.0m)
                {
                    schedSalesBase += t.LocalBaseAmount;
                    schedSalesVat += t.LocalTaxAmount;
                }
                else if (isZero)
                {
                    zeroSalesBase += t.LocalBaseAmount;
                }
                else if (isExempt)
                {
                    exemptSalesBase += t.LocalBaseAmount;
                }
            }
            else
            {
                if (pct >= 13.0m)
                {
                    genPurchBase += t.LocalBaseAmount;
                    genPurchVat += t.LocalTaxAmount;
                }
                else if (isZero || isExempt)
                {
                    exemptZeroPurchBase += t.LocalBaseAmount;
                }
            }
        }

        decimal totalSalesTax = genSalesVat + schedSalesVat;
        decimal totalSalesBase = genSalesBase + schedSalesBase + zeroSalesBase + exemptSalesBase;

        decimal totalPurchTax = genPurchVat + importPurchVat;
        decimal totalPurchBase = genPurchBase + importPurchBase + exemptZeroPurchBase;

        decimal netDue = totalSalesTax - totalPurchTax;

        var salesBoxes = new List<VatBoxItemDto>
        {
            new() { BoxNumber = 1, BoxKey = "EG_SALES_GENERAL_14", TitleAr = "مبيعات سلع وخدمات خاضعة للنسبة العامة (14%)", TitleEn = "General Goods & Services Sales (14%)", BaseAmount = genSalesBase, VatAmount = genSalesVat },
            new() { BoxNumber = 2, BoxKey = "EG_SALES_SCHEDULE_TAX", TitleAr = "مبيعات خاضعة لضريبة الجدول", TitleEn = "Schedule Tax Sales", BaseAmount = schedSalesBase, VatAmount = schedSalesVat },
            new() { BoxNumber = 3, BoxKey = "EG_SALES_ZERO_EXPORTS", TitleAr = "الصادرات ونسبة الصفر (0%)", TitleEn = "Exports & Zero-Rated Sales", BaseAmount = zeroSalesBase, VatAmount = 0 },
            new() { BoxNumber = 4, BoxKey = "EG_SALES_EXEMPT", TitleAr = "المبيعات المعفاة", TitleEn = "Exempt Sales", BaseAmount = exemptSalesBase, VatAmount = 0 },
            new() { BoxNumber = 5, BoxKey = "EG_TOTAL_OUTPUT_VAT", TitleAr = "إجمالي ضريبة المبيعات المستحقة", TitleEn = "Total Output VAT", BaseAmount = totalSalesBase, VatAmount = totalSalesTax }
        };

        var purchaseBoxes = new List<VatBoxItemDto>
        {
            new() { BoxNumber = 6, BoxKey = "EG_PURCH_LOCAL_14", TitleAr = "مشتريات محلية خاضعة للسعر العام (14%)", TitleEn = "Local Domestic Purchases (14%)", BaseAmount = genPurchBase, VatAmount = genPurchVat },
            new() { BoxNumber = 7, BoxKey = "EG_PURCH_IMPORTS", TitleAr = "السلع المستوردة (إفراج جمركي)", TitleEn = "Imported Goods (Customs)", BaseAmount = importPurchBase, VatAmount = importPurchVat },
            new() { BoxNumber = 8, BoxKey = "EG_PURCH_SCHEDULE_TAX", TitleAr = "مدخلات ضريبة الجدول", TitleEn = "Schedule Tax Inputs", BaseAmount = 0, VatAmount = 0 },
            new() { BoxNumber = 9, BoxKey = "EG_PURCH_EXEMPT_ZERO", TitleAr = "مشتريات معفاة وصفرية", TitleEn = "Exempt & Zero Purchases", BaseAmount = exemptZeroPurchBase, VatAmount = 0 },
            new() { BoxNumber = 10, BoxKey = "EG_TOTAL_INPUT_VAT", TitleAr = "إجمالي الضريبة القابلة للخصم", TitleEn = "Total Deductible Input VAT", BaseAmount = totalPurchBase, VatAmount = totalPurchTax }
        };

        var settlementBoxes = new List<VatBoxItemDto>
        {
            new() { BoxNumber = 11, BoxKey = "EG_NET_TAX_DUE", TitleAr = "صافي الضريبة المستحقة للفترة", TitleEn = "Net Tax Due", BaseAmount = 0, VatAmount = netDue },
            new() { BoxNumber = 12, BoxKey = "EG_WHT_DEDUCTIONS", TitleAr = "الخصم والتحصيل (أ.ت.ص)", TitleEn = "Advance WHT Deductions", BaseAmount = 0, VatAmount = 0 },
            new() { BoxNumber = 13, BoxKey = "EG_FINAL_PAYABLE", TitleAr = "الضريبة واجبة السداد لمصلحة الضرائب", TitleEn = "Final Payable to ETA", BaseAmount = 0, VatAmount = netDue }
        };

        return new VatDeclarationDto
        {
            TemplateCode = TemplateCode,
            CountryCode = CountryCode,
            TemplateTitleAr = "إقرار ضريبة القيمة المضافة (مصر - نموذج 10)",
            TemplateTitleEn = "Egypt VAT Return (Form 10 - ETA)",
            TaxAuthorityNameAr = "مصلحة الضرائب المصرية (ETA)",
            TaxAuthorityNameEn = "Egyptian Tax Authority (ETA)",
            CurrencyCode = "EGP",
            BranchId = filter.BranchId,
            BranchNameAr = branchNameAr,
            BranchNameEn = branchNameEn,
            FromDate = filter.FromDate,
            ToDate = filter.ToDate,
            GeneratedAt = DateTime.UtcNow,
            SalesBoxes = salesBoxes,
            TotalSalesBaseAmount = totalSalesBase,
            TotalOutputVatAmount = totalSalesTax,
            PurchaseBoxes = purchaseBoxes,
            TotalPurchasesBaseAmount = totalPurchBase,
            TotalInputVatAmount = totalPurchTax,
            NetVatDueAmount = netDue,
            Sections = new List<TaxDeclarationSectionResultDto>
            {
                new() { SectionKey = "EG_SALES_SECTION", TitleAr = "المبيعات والخدمات", TitleEn = "Sales & Services", TotalBaseAmount = totalSalesBase, TotalTaxAmount = totalSalesTax, Boxes = salesBoxes },
                new() { SectionKey = "EG_PURCHASES_SECTION", TitleAr = "المدخلات والمشتريات", TitleEn = "Inputs & Purchases", TotalBaseAmount = totalPurchBase, TotalTaxAmount = totalPurchTax, Boxes = purchaseBoxes },
                new() { SectionKey = "EG_SETTLEMENT_SECTION", TitleAr = "التسوية والسداد", TitleEn = "Settlement & Payment", TotalBaseAmount = 0, TotalTaxAmount = netDue, Boxes = settlementBoxes }
            }
        };
    }
}
