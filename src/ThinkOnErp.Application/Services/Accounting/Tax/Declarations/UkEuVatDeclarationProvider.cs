using ThinkOnErp.Application.DTOs.Accounting.Tax;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Application.Services.Accounting.Tax.Declarations;

/// <summary>
/// UK HMRC and European Union Standard 9-Box VAT Return Declaration Provider.
/// </summary>
public sealed class UkEuVatDeclarationProvider : ITaxDeclarationProvider
{
    public string TemplateCode => "UK_EU_VAT_9";
    public string CountryCode => "GB";

    public TaxDeclarationTemplateDto GetTemplateMetadata()
    {
        return new TaxDeclarationTemplateDto
        {
            TemplateCode = TemplateCode,
            CountryCode = CountryCode,
            CountryNameAr = "المملكة المتحدة والاتحاد الأوروبي",
            CountryNameEn = "United Kingdom & European Union",
            FlagEmoji = "🇬🇧",
            TitleAr = "إقرار ضريبة القيمة المضافة القياسي (9 مربعات) - HMRC / EU Standard",
            TitleEn = "Standard 9-Box VAT Return - HMRC & European Standard",
            TaxAuthorityNameAr = "هيئة الإيرادات والجمارك البريطانية (HMRC)",
            TaxAuthorityNameEn = "HM Revenue & Customs (HMRC)",
            DefaultCurrencyCode = "GBP",
            FilingFrequency = "QUARTERLY_OR_MONTHLY",
            Sections = new List<TaxDeclarationTemplateSectionDto>
            {
                new()
                {
                    SectionKey = "UK_VAT_CALCULATION",
                    TitleAr = "احتساب ضريبة القيمة المضافة (المربعات 1 إلى 5)",
                    TitleEn = "VAT Calculation (Boxes 1 to 5)",
                    Boxes = new List<TaxDeclarationTemplateBoxDto>
                    {
                        new() { BoxNumber = 1, BoxKey = "UK_BOX_1_SALES_VAT", TitleAr = "ضريبة القيمة المضافة المستحقة على المبيعات والمخرجات الأخرى", TitleEn = "VAT due in the period on sales and other outputs" },
                        new() { BoxNumber = 2, BoxKey = "UK_BOX_2_ACQUISITION_VAT", TitleAr = "الضريبة المستحقة على الاستحواذات/الاستيرادات", TitleEn = "VAT due on acquisitions from EU member states" },
                        new() { BoxNumber = 3, BoxKey = "UK_BOX_3_TOTAL_VAT_DUE", TitleAr = "إجمالي الضريبة المستحقة (مربع 1 + مربع 2)", TitleEn = "Total VAT due (Box 1 + Box 2)", IsCalculated = true, CalculationFormula = "Box 1 + Box 2" },
                        new() { BoxNumber = 4, BoxKey = "UK_BOX_4_RECLAIMED_VAT", TitleAr = "ضريبة القيمة المضافة القابلة للاسترداد على المشتريات والمدخلات", TitleEn = "VAT reclaimed on purchases and other inputs" },
                        new() { BoxNumber = 5, BoxKey = "UK_BOX_5_NET_VAT_PAYABLE", TitleAr = "صافي الضريبة واجبة السداد أو الاسترداد (مربع 3 - مربع 4)", TitleEn = "Net VAT to be paid or reclaimed (Box 3 - Box 4)", IsCalculated = true, CalculationFormula = "Box 3 - Box 4" }
                    }
                },
                new()
                {
                    SectionKey = "UK_TOTAL_VALUES",
                    TitleAr = "إجمالي قيم المبيعات والمشتريات بدون ضريبة (المربعات 6 إلى 9)",
                    TitleEn = "Total Values excluding VAT (Boxes 6 to 9)",
                    Boxes = new List<TaxDeclarationTemplateBoxDto>
                    {
                        new() { BoxNumber = 6, BoxKey = "UK_BOX_6_TOTAL_SALES_EX_VAT", TitleAr = "إجمالي قيمة المبيعات والمخرجات بدون ضريبة", TitleEn = "Total value of sales and all other outputs excluding any VAT" },
                        new() { BoxNumber = 7, BoxKey = "UK_BOX_7_TOTAL_PURCH_EX_VAT", TitleAr = "إجمالي قيمة المشتريات والمدخلات بدون ضريبة", TitleEn = "Total value of purchases and all other inputs excluding any VAT" },
                        new() { BoxNumber = 8, BoxKey = "UK_BOX_8_TOTAL_EU_SUPPLIES", TitleAr = "إجمالي قيمة توريدات السلع والخدمات ذات الصلة للخارج", TitleEn = "Total value of dispatches of goods to EU member states" },
                        new() { BoxNumber = 9, BoxKey = "UK_BOX_9_TOTAL_EU_ACQUISITIONS", TitleAr = "إجمالي قيمة الاستحواذات للسلع والخدمات من الخارج", TitleEn = "Total value of acquisitions of goods from EU member states" }
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
        decimal totalSalesBase = 0, totalSalesVat = 0;
        decimal totalPurchBase = 0, totalPurchVat = 0;

        foreach (var t in transactions)
        {
            if (t.IsSalesTax)
            {
                totalSalesBase += t.LocalBaseAmount;
                totalSalesVat += t.LocalTaxAmount;
            }
            else
            {
                totalPurchBase += t.LocalBaseAmount;
                totalPurchVat += t.LocalTaxAmount;
            }
        }

        decimal netPayable = totalSalesVat - totalPurchVat;

        var calcBoxes = new List<VatBoxItemDto>
        {
            new() { BoxNumber = 1, BoxKey = "UK_BOX_1_SALES_VAT", TitleAr = "الضريبة المستحقة على المبيعات", TitleEn = "VAT due on sales (Box 1)", BaseAmount = totalSalesBase, VatAmount = totalSalesVat },
            new() { BoxNumber = 2, BoxKey = "UK_BOX_2_ACQUISITION_VAT", TitleAr = "الضريبة على الاستحواذات/الاستيراد", TitleEn = "VAT due on acquisitions (Box 2)", BaseAmount = 0, VatAmount = 0 },
            new() { BoxNumber = 3, BoxKey = "UK_BOX_3_TOTAL_VAT_DUE", TitleAr = "إجمالي الضريبة المستحقة (مربع 1 + 2)", TitleEn = "Total VAT due (Box 3)", BaseAmount = totalSalesBase, VatAmount = totalSalesVat },
            new() { BoxNumber = 4, BoxKey = "UK_BOX_4_RECLAIMED_VAT", TitleAr = "الضريبة القابلة للاسترداد على المشتريات", TitleEn = "VAT reclaimed on purchases (Box 4)", BaseAmount = totalPurchBase, VatAmount = totalPurchVat },
            new() { BoxNumber = 5, BoxKey = "UK_BOX_5_NET_VAT_PAYABLE", TitleAr = "صافي الضريبة الواجب سدادها / (المستردة)", TitleEn = "Net VAT to be paid / (reclaimed) (Box 5)", BaseAmount = 0, VatAmount = netPayable }
        };

        var valueBoxes = new List<VatBoxItemDto>
        {
            new() { BoxNumber = 6, BoxKey = "UK_BOX_6_TOTAL_SALES_EX_VAT", TitleAr = "إجمالي المبيعات بدون ضريبة", TitleEn = "Total value of sales ex VAT (Box 6)", BaseAmount = totalSalesBase, VatAmount = 0 },
            new() { BoxNumber = 7, BoxKey = "UK_BOX_7_TOTAL_PURCH_EX_VAT", TitleAr = "إجمالي المشتريات بدون ضريبة", TitleEn = "Total value of purchases ex VAT (Box 7)", BaseAmount = totalPurchBase, VatAmount = 0 },
            new() { BoxNumber = 8, BoxKey = "UK_BOX_8_TOTAL_EU_SUPPLIES", TitleAr = "التوريدات الخارجية", TitleEn = "Total dispatches to EU (Box 8)", BaseAmount = 0, VatAmount = 0 },
            new() { BoxNumber = 9, BoxKey = "UK_BOX_9_TOTAL_EU_ACQUISITIONS", TitleAr = "الاستحواذات الخارجية", TitleEn = "Total acquisitions from EU (Box 9)", BaseAmount = 0, VatAmount = 0 }
        };

        return new VatDeclarationDto
        {
            TemplateCode = TemplateCode,
            CountryCode = CountryCode,
            TemplateTitleAr = "إقرار ضريبة القيمة المضافة (المملكة المتحدة وأوروبا - 9 مربعات)",
            TemplateTitleEn = "UK / EU Standard 9-Box VAT Return (HMRC)",
            TaxAuthorityNameAr = "هيئة الإيرادات والجمارك البريطانية (HMRC)",
            TaxAuthorityNameEn = "HM Revenue & Customs (HMRC)",
            CurrencyCode = "GBP",
            BranchId = filter.BranchId,
            BranchNameAr = branchNameAr,
            BranchNameEn = branchNameEn,
            FromDate = filter.FromDate,
            ToDate = filter.ToDate,
            GeneratedAt = DateTime.UtcNow,
            SalesBoxes = calcBoxes,
            TotalSalesBaseAmount = totalSalesBase,
            TotalOutputVatAmount = totalSalesVat,
            PurchaseBoxes = valueBoxes,
            TotalPurchasesBaseAmount = totalPurchBase,
            TotalInputVatAmount = totalPurchVat,
            NetVatDueAmount = netPayable,
            Sections = new List<TaxDeclarationSectionResultDto>
            {
                new() { SectionKey = "UK_VAT_CALCULATION", TitleAr = "احتساب الضريبة (مربعات 1-5)", TitleEn = "VAT Calculation (Boxes 1-5)", TotalBaseAmount = totalSalesBase, TotalTaxAmount = totalSalesVat, Boxes = calcBoxes },
                new() { SectionKey = "UK_TOTAL_VALUES", TitleAr = "إجمالي المبالغ بدون ضريبة (مربعات 6-9)", TitleEn = "Total Values ex VAT (Boxes 6-9)", TotalBaseAmount = totalSalesBase + totalPurchBase, TotalTaxAmount = 0, Boxes = valueBoxes }
            }
        };
    }
}
