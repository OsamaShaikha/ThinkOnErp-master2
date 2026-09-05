using ThinkOnErp.Application.DTOs.Accounting.Tax;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Application.Services.Accounting.Tax.Declarations;

/// <summary>
/// GCC and Saudi Arabia ZATCA 16-Box Official VAT Return Declaration Provider.
/// </summary>
public sealed class ZatcaGccDeclarationProvider : ITaxDeclarationProvider
{
    public string TemplateCode => "GCC_ZATCA_16";
    public string CountryCode => "SA";

    public TaxDeclarationTemplateDto GetTemplateMetadata()
    {
        return new TaxDeclarationTemplateDto
        {
            TemplateCode = TemplateCode,
            CountryCode = CountryCode,
            CountryNameLocal = "المملكة العربية السعودية ودول الخليج",
            CountryNameEn = "Saudi Arabia & GCC Countries",
            FlagEmoji = "🇸🇦",
            TitleAr = "إقرار ضريبة القيمة المضافة (16 مربعاً) - هيئة الزكاة والضريبة والجمارك",
            TitleEn = "Official 16-Box VAT Declaration - ZATCA & GCC",
            TaxAuthorityNameLocal = "هيئة الزكاة والضريبة والجمارك (ZATCA)",
            TaxAuthorityNameEn = "Zakat, Tax and Customs Authority (ZATCA)",
            DefaultCurrencyCode = "SAR",
            FilingFrequency = "MONTHLY_OR_QUARTERLY",
            Sections = new List<TaxDeclarationTemplateSectionDto>
            {
                new()
                {
                    SectionKey = "OUTPUT_VAT_SALES",
                    TitleAr = "ضريبة المخرجات (المبيعات الخاضعة للضريبة)",
                    TitleEn = "Output VAT (Taxable Sales)",
                    Boxes = new List<TaxDeclarationTemplateBoxDto>
                    {
                        new() { BoxNumber = 1, BoxKey = "VAT_BOX_STD_SALES", TitleAr = "المبيعات الخاضعة للنسبة الأساسية (15%)", TitleEn = "Standard Rated Sales (15%)" },
                        new() { BoxNumber = 2, BoxKey = "VAT_BOX_CITIZEN_SALES", TitleAr = "المبيعات للمواطنين (الخدمات الصحية / التعليم)", TitleEn = "Private Healthcare/Education Sales to Citizens" },
                        new() { BoxNumber = 3, BoxKey = "VAT_BOX_ZERO_SALES", TitleAr = "المبيعات الخاضعة للنسبة الصفرية (0%)", TitleEn = "Zero-Rated Domestic Sales (0%)" },
                        new() { BoxNumber = 4, BoxKey = "VAT_BOX_EXPORT_SALES", TitleAr = "الصادرات", TitleEn = "Exports" },
                        new() { BoxNumber = 5, BoxKey = "VAT_BOX_EXEMPT_SALES", TitleAr = "المبيعات المعفاة من الضريبة", TitleEn = "Exempt Sales" },
                        new() { BoxNumber = 6, BoxKey = "VAT_BOX_TOTAL_OUTPUT", TitleAr = "إجمالي ضريبة المخرجات", TitleEn = "Total Output VAT", IsCalculated = true, CalculationFormula = "Box 1..5" }
                    }
                },
                new()
                {
                    SectionKey = "INPUT_VAT_PURCHASES",
                    TitleAr = "ضريبة المدخلات (المشتريات الخاضعة للضريبة)",
                    TitleEn = "Input VAT (Taxable Purchases)",
                    Boxes = new List<TaxDeclarationTemplateBoxDto>
                    {
                        new() { BoxNumber = 7, BoxKey = "VAT_BOX_STD_PURCH", TitleAr = "المشتريات الخاضعة للنسبة الأساسية (15%)", TitleEn = "Standard Rated Domestic Purchases (15%)" },
                        new() { BoxNumber = 8, BoxKey = "VAT_BOX_IMPORT_CUSTOMS", TitleAr = "الاستيرادات الخاضعة لضريبة القيمة المضافة المدفوعة بالجمارك", TitleEn = "Imports Subject to VAT Paid at Customs" },
                        new() { BoxNumber = 9, BoxKey = "VAT_BOX_IMPORT_REVERSE", TitleAr = "الاستيرادات الخاضعة لآلية الاحتساب العكسي", TitleEn = "Imports Subject to Reverse Charge Mechanism" },
                        new() { BoxNumber = 10, BoxKey = "VAT_BOX_ZERO_PURCH", TitleAr = "المشتريات الخاضعة للنسبة الصفرية (0%)", TitleEn = "Zero-Rated Purchases (0%)" },
                        new() { BoxNumber = 11, BoxKey = "VAT_BOX_EXEMPT_PURCH", TitleAr = "المشتريات المعفاة من الضريبة", TitleEn = "Exempt Purchases" },
                        new() { BoxNumber = 12, BoxKey = "VAT_BOX_TOTAL_INPUT", TitleAr = "إجمالي ضريبة المدخلات القابلة للخصم", TitleEn = "Total Deductible Input VAT", IsCalculated = true, CalculationFormula = "Box 7..11" }
                    }
                },
                new()
                {
                    SectionKey = "NET_VAT_SETTLEMENT",
                    TitleAr = "صافي الضريبة المستحقة والسداد",
                    TitleEn = "Net VAT Due & Settlement",
                    Boxes = new List<TaxDeclarationTemplateBoxDto>
                    {
                        new() { BoxNumber = 13, BoxKey = "VAT_BOX_NET_DUE", TitleAr = "صافي الضريبة المستحقة للفترة الحالية", TitleEn = "Net VAT Due / (Refund) for Current Period", IsCalculated = true, CalculationFormula = "Box 6 - Box 12" },
                        new() { BoxNumber = 14, BoxKey = "VAT_BOX_CORRECTIONS", TitleAr = "تصحيحات وتعديلات من فترات سابقة", TitleEn = "Prior Period Adjustments" },
                        new() { BoxNumber = 15, BoxKey = "VAT_BOX_CARRIED_FORWARD", TitleAr = "رصيد دائن مرحل من فترات سابقة", TitleEn = "VAT Credit Carried Forward" },
                        new() { BoxNumber = 16, BoxKey = "VAT_BOX_TOTAL_PAYABLE", TitleAr = "صافي الضريبة الواجب سدادها / (المستردة)", TitleEn = "Total VAT Payable / (Refundable)", IsCalculated = true, CalculationFormula = "Box 13 - Box 14 - Box 15" }
                    }
                }
            }
        };
    }

    public VatDeclarationDto BuildDeclaration(
        VatDeclarationFilterDto filter,
        IReadOnlyList<TaxTransaction> transactions,
        IReadOnlyList<TaxRate> availableRates,
        string branchNameLocal,
        string branchNameEn)
    {
        var rateMap = availableRates.ToDictionary(r => r.Id);

        decimal stdSalesBase = 0, stdSalesVat = 0;
        decimal zeroSalesBase = 0;
        decimal exemptSalesBase = 0;
        decimal exportSalesBase = 0;

        decimal stdPurchBase = 0, stdPurchVat = 0;
        decimal zeroPurchBase = 0;
        decimal exemptPurchBase = 0;
        decimal importCustomsBase = 0, importCustomsVat = 0;
        decimal importReverseBase = 0, importReverseVat = 0;

        foreach (var t in transactions)
        {
            rateMap.TryGetValue(t.TaxRateId, out var rate);
            bool isZero = rate?.IsZeroRated == true || rate?.RatePercent == 0;
            bool isExempt = rate?.IsExempt == true;
            bool isStandard = !isZero && !isExempt && (rate?.RatePercent > 0);

            if (t.IsSalesTax)
            {
                if (isStandard)
                {
                    stdSalesBase += t.LocalBaseAmount;
                    stdSalesVat += t.LocalTaxAmount;
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
                if (isStandard)
                {
                    stdPurchBase += t.LocalBaseAmount;
                    stdPurchVat += t.LocalTaxAmount;
                }
                else if (isZero)
                {
                    zeroPurchBase += t.LocalBaseAmount;
                }
                else if (isExempt)
                {
                    exemptPurchBase += t.LocalBaseAmount;
                }
            }
        }

        var salesBoxes = new List<VatBoxItemDto>
        {
            new() { BoxNumber = 1, BoxKey = "VAT_BOX_STD_SALES", TitleAr = "المبيعات الخاضعة للنسبة الأساسية (15%)", TitleEn = "Standard Rated Sales (15%)", BaseAmount = stdSalesBase, VatAmount = stdSalesVat },
            new() { BoxNumber = 2, BoxKey = "VAT_BOX_CITIZEN_SALES", TitleAr = "المبيعات للمواطنين (الخدمات الصحية / التعليم)", TitleEn = "Private Healthcare/Education Sales", BaseAmount = 0, VatAmount = 0 },
            new() { BoxNumber = 3, BoxKey = "VAT_BOX_ZERO_SALES", TitleAr = "المبيعات الخاضعة للنسبة الصفرية (0%)", TitleEn = "Zero-Rated Sales (0%)", BaseAmount = zeroSalesBase, VatAmount = 0 },
            new() { BoxNumber = 4, BoxKey = "VAT_BOX_EXPORT_SALES", TitleAr = "الصادرات", TitleEn = "Exports", BaseAmount = exportSalesBase, VatAmount = 0 },
            new() { BoxNumber = 5, BoxKey = "VAT_BOX_EXEMPT_SALES", TitleAr = "المبيعات المعفاة من الضريبة", TitleEn = "Exempt Sales", BaseAmount = exemptSalesBase, VatAmount = 0 },
            new() { BoxNumber = 6, BoxKey = "VAT_BOX_TOTAL_OUTPUT", TitleAr = "إجمالي ضريبة المخرجات", TitleEn = "Total Output VAT", BaseAmount = stdSalesBase + zeroSalesBase + exportSalesBase + exemptSalesBase, VatAmount = stdSalesVat }
        };

        var purchaseBoxes = new List<VatBoxItemDto>
        {
            new() { BoxNumber = 7, BoxKey = "VAT_BOX_STD_PURCH", TitleAr = "المشتريات الخاضعة للنسبة الأساسية (15%)", TitleEn = "Standard Rated Domestic Purchases (15%)", BaseAmount = stdPurchBase, VatAmount = stdPurchVat },
            new() { BoxNumber = 8, BoxKey = "VAT_BOX_IMPORT_CUSTOMS", TitleAr = "الاستيرادات الخاضعة للضريبة المدفوعة بالجمارك", TitleEn = "Imports Paid at Customs", BaseAmount = importCustomsBase, VatAmount = importCustomsVat },
            new() { BoxNumber = 9, BoxKey = "VAT_BOX_IMPORT_REVERSE", TitleAr = "الاستيرادات الخاضعة لآلية الاحتساب العكسي", TitleEn = "Imports Subject to Reverse Charge", BaseAmount = importReverseBase, VatAmount = importReverseVat },
            new() { BoxNumber = 10, BoxKey = "VAT_BOX_ZERO_PURCH", TitleAr = "المشتريات الخاضعة للنسبة الصفرية (0%)", TitleEn = "Zero-Rated Purchases (0%)", BaseAmount = zeroPurchBase, VatAmount = 0 },
            new() { BoxNumber = 11, BoxKey = "VAT_BOX_EXEMPT_PURCH", TitleAr = "المشتريات المعفاة من الضريبة", TitleEn = "Exempt Purchases", BaseAmount = exemptPurchBase, VatAmount = 0 },
            new() { BoxNumber = 12, BoxKey = "VAT_BOX_TOTAL_INPUT", TitleAr = "إجمالي ضريبة المدخلات القابلة للخصم", TitleEn = "Total Deductible Input VAT", BaseAmount = stdPurchBase + importCustomsBase + importReverseBase + zeroPurchBase + exemptPurchBase, VatAmount = stdPurchVat + importCustomsVat + importReverseVat }
        };

        decimal totalOutputVat = stdSalesVat;
        decimal totalInputVat = stdPurchVat + importCustomsVat + importReverseVat;
        decimal netVatDue = totalOutputVat - totalInputVat;

        var settlementBoxes = new List<VatBoxItemDto>
        {
            new() { BoxNumber = 13, BoxKey = "VAT_BOX_NET_DUE", TitleAr = "صافي الضريبة المستحقة للفترة الحالية", TitleEn = "Net VAT Due / (Refund)", BaseAmount = 0, VatAmount = netVatDue },
            new() { BoxNumber = 14, BoxKey = "VAT_BOX_CORRECTIONS", TitleAr = "تصحيحات وتعديلات من فترات سابقة", TitleEn = "Prior Period Adjustments", BaseAmount = 0, VatAmount = 0 },
            new() { BoxNumber = 15, BoxKey = "VAT_BOX_CARRIED_FORWARD", TitleAr = "رصيد دائن مرحل من فترات سابقة", TitleEn = "VAT Credit Carried Forward", BaseAmount = 0, VatAmount = 0 },
            new() { BoxNumber = 16, BoxKey = "VAT_BOX_TOTAL_PAYABLE", TitleAr = "صافي الضريبة الواجب سدادها / (المستردة)", TitleEn = "Total VAT Payable / (Refundable)", BaseAmount = 0, VatAmount = netVatDue }
        };

        return new VatDeclarationDto
        {
            TemplateCode = TemplateCode,
            CountryCode = CountryCode,
            TemplateTitleAr = "إقرار ضريبة القيمة المضافة (ZATCA 16 Boxes)",
            TemplateTitleEn = "VAT Declaration Return (ZATCA 16 Boxes)",
            TaxAuthorityNameLocal = "هيئة الزكاة والضريبة والجمارك (ZATCA)",
            TaxAuthorityNameEn = "Zakat, Tax and Customs Authority (ZATCA)",
            CurrencyCode = "SAR",
            BranchId = filter.BranchId,
            BranchNameLocal = branchNameLocal,
            BranchNameEn = branchNameEn,
            FromDate = filter.FromDate,
            ToDate = filter.ToDate,
            GeneratedAt = DateTime.UtcNow,
            SalesBoxes = salesBoxes,
            TotalSalesBaseAmount = salesBoxes.First(b => b.BoxNumber == 6).BaseAmount,
            TotalOutputVatAmount = totalOutputVat,
            PurchaseBoxes = purchaseBoxes,
            TotalPurchasesBaseAmount = purchaseBoxes.First(b => b.BoxNumber == 12).BaseAmount,
            TotalInputVatAmount = totalInputVat,
            NetVatDueAmount = netVatDue,
            Sections = new List<TaxDeclarationSectionResultDto>
            {
                new()
                {
                    SectionKey = "OUTPUT_VAT_SALES",
                    TitleAr = "ضريبة المخرجات (المبيعات)",
                    TitleEn = "Output VAT (Sales)",
                    TotalBaseAmount = salesBoxes.First(b => b.BoxNumber == 6).BaseAmount,
                    TotalTaxAmount = totalOutputVat,
                    Boxes = salesBoxes
                },
                new()
                {
                    SectionKey = "INPUT_VAT_PURCHASES",
                    TitleAr = "ضريبة المدخلات (المشتريات)",
                    TitleEn = "Input VAT (Purchases)",
                    TotalBaseAmount = purchaseBoxes.First(b => b.BoxNumber == 12).BaseAmount,
                    TotalTaxAmount = totalInputVat,
                    Boxes = purchaseBoxes
                },
                new()
                {
                    SectionKey = "NET_VAT_SETTLEMENT",
                    TitleAr = "صافي الضريبة والسداد",
                    TitleEn = "Net VAT Settlement",
                    TotalBaseAmount = 0,
                    TotalTaxAmount = netVatDue,
                    Boxes = settlementBoxes
                }
            }
        };
    }
}
