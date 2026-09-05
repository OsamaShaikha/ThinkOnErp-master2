using ThinkOnErp.Application.DTOs.Accounting.Tax;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Application.Services.Accounting.Tax.Declarations;

/// <summary>
/// Universal Global Tax Declaration Provider suitable for any country and custom jurisdiction.
/// </summary>
public sealed class GlobalGenericDeclarationProvider : ITaxDeclarationProvider
{
    public string TemplateCode => "GLOBAL_GENERIC";
    public string CountryCode => "GLOBAL";

    public TaxDeclarationTemplateDto GetTemplateMetadata()
    {
        return new TaxDeclarationTemplateDto
        {
            TemplateCode = TemplateCode,
            CountryCode = CountryCode,
            CountryNameLocal = "النموذج الضريبي العالمي الموحد",
            CountryNameEn = "Universal Global Tax Return",
            FlagEmoji = "🌐",
            TitleAr = "الإقرار الضريبي العالمي الموحد (مبيعات، مشتريات، صافي الضريبة)",
            TitleEn = "Universal Tax Return Summary (Output Tax, Input Tax, Net Due)",
            TaxAuthorityNameLocal = "الهيئة الضريبية المختصة",
            TaxAuthorityNameEn = "General Tax Authority",
            DefaultCurrencyCode = "USD",
            FilingFrequency = "CUSTOM",
            Sections = new List<TaxDeclarationTemplateSectionDto>
            {
                new()
                {
                    SectionKey = "GENERIC_OUTPUTS",
                    TitleAr = "المبيعات والإيرادات الخاضعة للضريبة (ضريبة المخرجات)",
                    TitleEn = "Taxable Sales & Revenues (Output Tax)",
                    Boxes = new List<TaxDeclarationTemplateBoxDto>
                    {
                        new() { BoxNumber = 1, BoxKey = "GEN_TAXABLE_SALES", TitleAr = "المبيعات والإيرادات الخاضعة للضريبة", TitleEn = "Total Taxable Sales" },
                        new() { BoxNumber = 2, BoxKey = "GEN_NON_TAXABLE_SALES", TitleAr = "المبيعات الصفرية والمعفاة من الضريبة", TitleEn = "Zero-Rated & Exempt Sales" },
                        new() { BoxNumber = 3, BoxKey = "GEN_TOTAL_OUTPUT_TAX", TitleAr = "إجمالي ضريبة المخرجات المحصلة", TitleEn = "Total Output Tax Collected", IsCalculated = true }
                    }
                },
                new()
                {
                    SectionKey = "GENERIC_INPUTS",
                    TitleAr = "المشتريات والمصروفات القابلة للخصم (ضريبة المدخلات)",
                    TitleEn = "Taxable Purchases & Expenses (Input Tax)",
                    Boxes = new List<TaxDeclarationTemplateBoxDto>
                    {
                        new() { BoxNumber = 4, BoxKey = "GEN_TAXABLE_PURCHASES", TitleAr = "المشتريات والمصروفات الخاضعة للضريبة", TitleEn = "Total Taxable Purchases" },
                        new() { BoxNumber = 5, BoxKey = "GEN_NON_TAXABLE_PURCHASES", TitleAr = "المشتريات الصفرية والمعفاة من الضريبة", TitleEn = "Zero-Rated & Exempt Purchases" },
                        new() { BoxNumber = 6, BoxKey = "GEN_TOTAL_INPUT_TAX", TitleAr = "إجمالي ضريبة المدخلات القابلة للخصم", TitleEn = "Total Deductible Input Tax", IsCalculated = true }
                    }
                },
                new()
                {
                    SectionKey = "GENERIC_NET",
                    TitleAr = "صافي الضريبة واجبة السداد أو الاسترداد",
                    TitleEn = "Net Tax Balance & Settlement",
                    Boxes = new List<TaxDeclarationTemplateBoxDto>
                    {
                        new() { BoxNumber = 7, BoxKey = "GEN_NET_TAX_PAYABLE", TitleAr = "صافي الضريبة واجبة السداد / (الرصيد الدائن المسترد)", TitleEn = "Net Tax Payable / (Tax Credit Refundable)", IsCalculated = true }
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

        decimal taxableSalesBase = 0, outputTax = 0;
        decimal nonTaxableSalesBase = 0;

        decimal taxablePurchBase = 0, inputTax = 0;
        decimal nonTaxablePurchBase = 0;

        foreach (var t in transactions)
        {
            rateMap.TryGetValue(t.TaxRateId, out var rate);
            bool isTaxable = rate != null && rate.RatePercent > 0 && !rate.IsExempt && !rate.IsZeroRated;

            if (t.IsSalesTax)
            {
                if (isTaxable)
                {
                    taxableSalesBase += t.LocalBaseAmount;
                    outputTax += t.LocalTaxAmount;
                }
                else
                {
                    nonTaxableSalesBase += t.LocalBaseAmount;
                }
            }
            else
            {
                if (isTaxable)
                {
                    taxablePurchBase += t.LocalBaseAmount;
                    inputTax += t.LocalTaxAmount;
                }
                else
                {
                    nonTaxablePurchBase += t.LocalBaseAmount;
                }
            }
        }

        decimal totalSalesBase = taxableSalesBase + nonTaxableSalesBase;
        decimal totalPurchBase = taxablePurchBase + nonTaxablePurchBase;
        decimal netTaxDue = outputTax - inputTax;

        var salesBoxes = new List<VatBoxItemDto>
        {
            new() { BoxNumber = 1, BoxKey = "GEN_TAXABLE_SALES", TitleAr = "المبيعات الخاضعة للضريبة", TitleEn = "Taxable Sales", BaseAmount = taxableSalesBase, VatAmount = outputTax },
            new() { BoxNumber = 2, BoxKey = "GEN_NON_TAXABLE_SALES", TitleAr = "المبيعات المعفاة والصفرية", TitleEn = "Exempt / Zero Sales", BaseAmount = nonTaxableSalesBase, VatAmount = 0 },
            new() { BoxNumber = 3, BoxKey = "GEN_TOTAL_OUTPUT_TAX", TitleAr = "إجمالي ضريبة المخرجات", TitleEn = "Total Output Tax", BaseAmount = totalSalesBase, VatAmount = outputTax }
        };

        var purchaseBoxes = new List<VatBoxItemDto>
        {
            new() { BoxNumber = 4, BoxKey = "GEN_TAXABLE_PURCHASES", TitleAr = "المشتريات الخاضعة للضريبة", TitleEn = "Taxable Purchases", BaseAmount = taxablePurchBase, VatAmount = inputTax },
            new() { BoxNumber = 5, BoxKey = "GEN_NON_TAXABLE_PURCHASES", TitleAr = "المشتريات المعفاة والصفرية", TitleEn = "Exempt / Zero Purchases", BaseAmount = nonTaxablePurchBase, VatAmount = 0 },
            new() { BoxNumber = 6, BoxKey = "GEN_TOTAL_INPUT_TAX", TitleAr = "إجمالي ضريبة المدخلات", TitleEn = "Total Input Tax", BaseAmount = totalPurchBase, VatAmount = inputTax }
        };

        var netBoxes = new List<VatBoxItemDto>
        {
            new() { BoxNumber = 7, BoxKey = "GEN_NET_TAX_PAYABLE", TitleAr = "صافي الضريبة واجبة السداد / (المستردة)", TitleEn = "Net Tax Payable / (Refund)", BaseAmount = 0, VatAmount = netTaxDue }
        };

        return new VatDeclarationDto
        {
            TemplateCode = TemplateCode,
            CountryCode = CountryCode,
            TemplateTitleAr = "الإقرار الضريبي العالمي الموحد",
            TemplateTitleEn = "Universal Global Tax Return",
            TaxAuthorityNameLocal = "الهيئة الضريبية العامة",
            TaxAuthorityNameEn = "Tax Authority",
            CurrencyCode = "USD",
            BranchId = filter.BranchId,
            BranchNameLocal = branchNameLocal,
            BranchNameEn = branchNameEn,
            FromDate = filter.FromDate,
            ToDate = filter.ToDate,
            GeneratedAt = DateTime.UtcNow,
            SalesBoxes = salesBoxes,
            TotalSalesBaseAmount = totalSalesBase,
            TotalOutputVatAmount = outputTax,
            PurchaseBoxes = purchaseBoxes,
            TotalPurchasesBaseAmount = totalPurchBase,
            TotalInputVatAmount = inputTax,
            NetVatDueAmount = netTaxDue,
            Sections = new List<TaxDeclarationSectionResultDto>
            {
                new() { SectionKey = "GENERIC_OUTPUTS", TitleAr = "ضريبة المخرجات (المبيعات)", TitleEn = "Output Tax (Sales)", TotalBaseAmount = totalSalesBase, TotalTaxAmount = outputTax, Boxes = salesBoxes },
                new() { SectionKey = "GENERIC_INPUTS", TitleAr = "ضريبة المدخلات (المشتريات)", TitleEn = "Input Tax (Purchases)", TotalBaseAmount = totalPurchBase, TotalTaxAmount = inputTax, Boxes = purchaseBoxes },
                new() { SectionKey = "GENERIC_NET", TitleAr = "صافي الضريبة", TitleEn = "Net Tax Balance", TotalBaseAmount = 0, TotalTaxAmount = netTaxDue, Boxes = netBoxes }
            }
        };
    }
}
