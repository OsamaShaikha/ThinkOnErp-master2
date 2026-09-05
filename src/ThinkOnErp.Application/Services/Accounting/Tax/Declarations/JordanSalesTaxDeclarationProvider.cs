using ThinkOnErp.Application.DTOs.Accounting.Tax;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Application.Services.Accounting.Tax.Declarations;

/// <summary>
/// Jordan General and Special Sales Tax Return Declaration Provider (ISTD).
/// </summary>
public sealed class JordanSalesTaxDeclarationProvider : ITaxDeclarationProvider
{
    public string TemplateCode => "JO_SALES_TAX";
    public string CountryCode => "JO";

    public TaxDeclarationTemplateDto GetTemplateMetadata()
    {
        return new TaxDeclarationTemplateDto
        {
            TemplateCode = TemplateCode,
            CountryCode = CountryCode,
            CountryNameLocal = "المملكة الأردنية الهاشمية",
            CountryNameEn = "Hashemite Kingdom of Jordan",
            FlagEmoji = "🇯🇴",
            TitleAr = "إقرار الضريبة العامة على المبيعات - دائرة ضريبة الدخل والمبيعات",
            TitleEn = "General Sales Tax Return - Income and Sales Tax Department (ISTD)",
            TaxAuthorityNameLocal = "دائرة ضريبة الدخل والمبيعات (ISTD)",
            TaxAuthorityNameEn = "Income & Sales Tax Department (ISTD)",
            DefaultCurrencyCode = "JOD",
            FilingFrequency = "BI_MONTHLY_OR_MONTHLY",
            Sections = new List<TaxDeclarationTemplateSectionDto>
            {
                new()
                {
                    SectionKey = "SALES_SECTION",
                    TitleAr = "المبيعات والخدمات الخاضعة للضريبة",
                    TitleEn = "Taxable Sales & Services",
                    Boxes = new List<TaxDeclarationTemplateBoxDto>
                    {
                        new() { BoxNumber = 1, BoxKey = "JO_SALES_GENERAL_16", TitleAr = "المبيعات الخاضعة للنسبة العامة (16%)", TitleEn = "Sales Subject to General Rate (16%)" },
                        new() { BoxNumber = 2, BoxKey = "JO_SALES_REDUCED_8_4", TitleAr = "المبيعات الخاضعة للنسب الخاصة والمخفضة (4% / 8% / 10%)", TitleEn = "Sales Subject to Reduced/Special Rates (4%, 8%, 10%)" },
                        new() { BoxNumber = 3, BoxKey = "JO_SALES_ZERO_RATED", TitleAr = "المبيعات الخاضعة للنسبة الصفرية (0%)", TitleEn = "Zero-Rated Sales (0%)" },
                        new() { BoxNumber = 4, BoxKey = "JO_SALES_EXEMPT", TitleAr = "المبيعات المعفاة من الضريبة العامة", TitleEn = "Exempt Sales" },
                        new() { BoxNumber = 5, BoxKey = "JO_SALES_EXPORTS", TitleAr = "الصادرات الخارجية", TitleEn = "Foreign Exports" },
                        new() { BoxNumber = 6, BoxKey = "JO_SALES_TOTAL_TAX", TitleAr = "إجمالي الضريبة المستحقة على المبيعات", TitleEn = "Total Sales Tax Due", IsCalculated = true, CalculationFormula = "Sum of Sales Tax" }
                    }
                },
                new()
                {
                    SectionKey = "PURCHASES_SECTION",
                    TitleAr = "المشتريات والمصاريف القابلة للخصم",
                    TitleEn = "Deductible Purchases & Expenses",
                    Boxes = new List<TaxDeclarationTemplateBoxDto>
                    {
                        new() { BoxNumber = 7, BoxKey = "JO_PURCH_LOCAL_16", TitleAr = "المشتريات المحلية الخاضعة للنسبة العامة (16%)", TitleEn = "Local Purchases Subject to General Rate (16%)" },
                        new() { BoxNumber = 8, BoxKey = "JO_PURCH_LOCAL_REDUCED", TitleAr = "المشتريات المحلية الخاضعة لنسب خاصة أو مخفضة", TitleEn = "Local Purchases Subject to Reduced Rates" },
                        new() { BoxNumber = 9, BoxKey = "JO_PURCH_IMPORTS", TitleAr = "المستوردات من الخارج (المسددة جمركياً)", TitleEn = "Imported Goods Paid at Customs" },
                        new() { BoxNumber = 10, BoxKey = "JO_PURCH_EXEMPT_ZERO", TitleAr = "المشتريات المعفاة أو الخاضعة لنسبة الصفر", TitleEn = "Exempt & Zero-Rated Purchases" },
                        new() { BoxNumber = 11, BoxKey = "JO_PURCH_TOTAL_TAX", TitleAr = "إجمالي الضريبة القابلة للخصم والتنزيل", TitleEn = "Total Deductible Input Tax", IsCalculated = true, CalculationFormula = "Sum of Deductible Purchases" }
                    }
                },
                new()
                {
                    SectionKey = "NET_SETTLEMENT",
                    TitleAr = "احتساب الضريبة واجبة السداد أو الرد",
                    TitleEn = "Net Tax Payable / Refundable",
                    Boxes = new List<TaxDeclarationTemplateBoxDto>
                    {
                        new() { BoxNumber = 12, BoxKey = "JO_NET_PERIOD_TAX", TitleAr = "صافي الضريبة المستحقة للفترة (ضريبة المبيعات - ضريبة المشتريات)", TitleEn = "Net Tax Due (Sales Tax - Input Tax)", IsCalculated = true },
                        new() { BoxNumber = 13, BoxKey = "JO_PREVIOUS_CREDIT", TitleAr = "رصيد ضريبي مدور من إقرارات سابقة", TitleEn = "Tax Credit Carried from Previous Period" },
                        new() { BoxNumber = 14, BoxKey = "JO_FINAL_DUE_PAYABLE", TitleAr = "الضريبة النهائية واجبة الدفع للدائرة / (الرصيد المدور للفترة القادمة)", TitleEn = "Final Tax Payable to ISTD / (Credit to Next Period)", IsCalculated = true }
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

        decimal genSalesBase = 0, genSalesVat = 0;
        decimal reducedSalesBase = 0, reducedSalesVat = 0;
        decimal zeroSalesBase = 0;
        decimal exemptSalesBase = 0;

        decimal genPurchBase = 0, genPurchVat = 0;
        decimal reducedPurchBase = 0, reducedPurchVat = 0;
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
                if (pct >= 15.0m) // General Jordan rate (16%)
                {
                    genSalesBase += t.LocalBaseAmount;
                    genSalesVat += t.LocalTaxAmount;
                }
                else if (pct > 0 && pct < 15.0m) // Reduced (4%, 8%, 10%)
                {
                    reducedSalesBase += t.LocalBaseAmount;
                    reducedSalesVat += t.LocalTaxAmount;
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
                if (pct >= 15.0m)
                {
                    genPurchBase += t.LocalBaseAmount;
                    genPurchVat += t.LocalTaxAmount;
                }
                else if (pct > 0 && pct < 15.0m)
                {
                    reducedPurchBase += t.LocalBaseAmount;
                    reducedPurchVat += t.LocalTaxAmount;
                }
                else if (isZero || isExempt)
                {
                    exemptZeroPurchBase += t.LocalBaseAmount;
                }
            }
        }

        decimal totalSalesTax = genSalesVat + reducedSalesVat;
        decimal totalSalesBase = genSalesBase + reducedSalesBase + zeroSalesBase + exemptSalesBase;

        decimal totalPurchTax = genPurchVat + reducedPurchVat + importPurchVat;
        decimal totalPurchBase = genPurchBase + reducedPurchBase + importPurchBase + exemptZeroPurchBase;

        decimal netDue = totalSalesTax - totalPurchTax;

        var salesBoxes = new List<VatBoxItemDto>
        {
            new() { BoxNumber = 1, BoxKey = "JO_SALES_GENERAL_16", TitleAr = "المبيعات الخاضعة للنسبة العامة (16%)", TitleEn = "General Rate Sales (16%)", BaseAmount = genSalesBase, VatAmount = genSalesVat },
            new() { BoxNumber = 2, BoxKey = "JO_SALES_REDUCED_8_4", TitleAr = "المبيعات الخاضعة للنسب الخاصة والمخفضة (4% / 8% / 10%)", TitleEn = "Special / Reduced Rates Sales", BaseAmount = reducedSalesBase, VatAmount = reducedSalesVat },
            new() { BoxNumber = 3, BoxKey = "JO_SALES_ZERO_RATED", TitleAr = "المبيعات بنسبة الصفر (0%)", TitleEn = "Zero-Rated Sales", BaseAmount = zeroSalesBase, VatAmount = 0 },
            new() { BoxNumber = 4, BoxKey = "JO_SALES_EXEMPT", TitleAr = "المبيعات المعفاة", TitleEn = "Exempt Sales", BaseAmount = exemptSalesBase, VatAmount = 0 },
            new() { BoxNumber = 5, BoxKey = "JO_SALES_EXPORTS", TitleAr = "الصادرات", TitleEn = "Exports", BaseAmount = 0, VatAmount = 0 },
            new() { BoxNumber = 6, BoxKey = "JO_SALES_TOTAL_TAX", TitleAr = "إجمالي الضريبة المستحقة على المبيعات", TitleEn = "Total Sales Tax Due", BaseAmount = totalSalesBase, VatAmount = totalSalesTax }
        };

        var purchaseBoxes = new List<VatBoxItemDto>
        {
            new() { BoxNumber = 7, BoxKey = "JO_PURCH_LOCAL_16", TitleAr = "المشتريات المحلية بنسبة (16%)", TitleEn = "Local Purchases (16%)", BaseAmount = genPurchBase, VatAmount = genPurchVat },
            new() { BoxNumber = 8, BoxKey = "JO_PURCH_LOCAL_REDUCED", TitleAr = "المشتريات بنسب مخفضة", TitleEn = "Reduced Rate Purchases", BaseAmount = reducedPurchBase, VatAmount = reducedPurchVat },
            new() { BoxNumber = 9, BoxKey = "JO_PURCH_IMPORTS", TitleAr = "المستوردات المسددة بالجمارك", TitleEn = "Imports Paid at Customs", BaseAmount = importPurchBase, VatAmount = importPurchVat },
            new() { BoxNumber = 10, BoxKey = "JO_PURCH_EXEMPT_ZERO", TitleAr = "المشتريات المعفاة والصفرية", TitleEn = "Exempt / Zero Purchases", BaseAmount = exemptZeroPurchBase, VatAmount = 0 },
            new() { BoxNumber = 11, BoxKey = "JO_PURCH_TOTAL_TAX", TitleAr = "إجمالي الضريبة القابلة للخصم", TitleEn = "Total Deductible Tax", BaseAmount = totalPurchBase, VatAmount = totalPurchTax }
        };

        var settlementBoxes = new List<VatBoxItemDto>
        {
            new() { BoxNumber = 12, BoxKey = "JO_NET_PERIOD_TAX", TitleAr = "صافي الضريبة المستحقة للفترة", TitleEn = "Net Period Tax", BaseAmount = 0, VatAmount = netDue },
            new() { BoxNumber = 13, BoxKey = "JO_PREVIOUS_CREDIT", TitleAr = "رصيد مدور سابق", TitleEn = "Carried Forward Credit", BaseAmount = 0, VatAmount = 0 },
            new() { BoxNumber = 14, BoxKey = "JO_FINAL_DUE_PAYABLE", TitleAr = "الضريبة النهائية واجبة الدفع / (المستردة)", TitleEn = "Final Tax Payable / (Refundable)", BaseAmount = 0, VatAmount = netDue }
        };

        return new VatDeclarationDto
        {
            TemplateCode = TemplateCode,
            CountryCode = CountryCode,
            TemplateTitleAr = "إقرار الضريبة العامة على المبيعات (الأردن - ISTD)",
            TemplateTitleEn = "Jordan General Sales Tax Return (ISTD)",
            TaxAuthorityNameLocal = "دائرة ضريبة الدخل والمبيعات الأردنية (ISTD)",
            TaxAuthorityNameEn = "Income & Sales Tax Department (ISTD)",
            CurrencyCode = "JOD",
            BranchId = filter.BranchId,
            BranchNameLocal = branchNameLocal,
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
                new() { SectionKey = "SALES_SECTION", TitleAr = "المبيعات والخدمات", TitleEn = "Sales & Services", TotalBaseAmount = totalSalesBase, TotalTaxAmount = totalSalesTax, Boxes = salesBoxes },
                new() { SectionKey = "PURCHASES_SECTION", TitleAr = "المشتريات والمصروفات", TitleEn = "Purchases & Expenses", TotalBaseAmount = totalPurchBase, TotalTaxAmount = totalPurchTax, Boxes = purchaseBoxes },
                new() { SectionKey = "NET_SETTLEMENT", TitleAr = "الصافي والسداد", TitleEn = "Net Settlement", TotalBaseAmount = 0, TotalTaxAmount = netDue, Boxes = settlementBoxes }
            }
        };
    }
}
