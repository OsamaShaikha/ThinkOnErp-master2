using Microsoft.Extensions.Logging;
using System.Text;
using ThinkOnErp.Application.DTOs.Accounting.Tax;
using ThinkOnErp.Application.Services.Accounting.Tax.Declarations;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting.Tax;

public sealed class TaxEngineService : ITaxEngineService
{
    private readonly ITaxRepository _taxRepository;
    private readonly IGlAccountRepository _accountRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<TaxEngineService> _logger;
    private readonly Dictionary<string, ITaxDeclarationProvider> _declarationProviders;

    public TaxEngineService(
        ITaxRepository taxRepository,
        IGlAccountRepository? accountRepository,
        ICurrentTenantContext tenantContext,
        ILogger<TaxEngineService> logger,
        IEnumerable<ITaxDeclarationProvider>? customProviders = null)
    {
        _taxRepository = taxRepository ?? throw new ArgumentNullException(nameof(taxRepository));
        _accountRepository = accountRepository!;
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var defaultProviders = new ITaxDeclarationProvider[]
        {
            new ZatcaGccDeclarationProvider(),
            new JordanSalesTaxDeclarationProvider(),
            new EgyptVatDeclarationProvider(),
            new UkEuVatDeclarationProvider(),
            new GlobalGenericDeclarationProvider()
        };

        _declarationProviders = new Dictionary<string, ITaxDeclarationProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in defaultProviders)
        {
            _declarationProviders[p.TemplateCode] = p;
        }

        if (customProviders != null)
        {
            foreach (var p in customProviders)
            {
                _declarationProviders[p.TemplateCode] = p;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tax Calculation Engine Core
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<TaxCalculationResultDto> CalculateTaxAsync(TaxCalculationRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request == null || request.Lines.Count == 0)
        {
            return new TaxCalculationResultDto();
        }

        decimal exchangeRate = request.ExchangeRate > 0 ? request.ExchangeRate : 1.0m;
        var availableRates = await _taxRepository.GetTaxRatesAsync(includeInactive: false, cancellationToken);
        var defaultStandardRate = availableRates.FirstOrDefault(r => r.TaxRateCode == "VAT_15")
                                  ?? availableRates.FirstOrDefault(r => r.RatePercent == 15.0m)
                                  ?? availableRates.FirstOrDefault();

        var result = new TaxCalculationResultDto();
        var summaryMap = new Dictionary<string, TaxSummaryItemDto>();

        foreach (var reqLine in request.Lines)
        {
            TaxRate? rate = null;
            if (reqLine.TaxRateId.HasValue && reqLine.TaxRateId.Value > 0)
            {
                rate = availableRates.FirstOrDefault(r => r.Id == reqLine.TaxRateId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(reqLine.TaxRateCode))
            {
                rate = availableRates.FirstOrDefault(r => string.Equals(r.TaxRateCode, reqLine.TaxRateCode.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            rate ??= defaultStandardRate;

            decimal grossAmount = Math.Round(reqLine.Quantity * reqLine.UnitPrice, 3, MidpointRounding.AwayFromZero);
            decimal discount = Math.Max(0, reqLine.DiscountAmount);
            decimal netGross = Math.Max(0, grossAmount - discount);

            decimal ratePercent = rate?.RatePercent ?? 0m;
            decimal netBaseAmount;
            decimal taxAmount;

            if (request.IsInclusive && ratePercent > 0)
            {
                // Inclusive: Price includes tax -> NetBase = NetGross / (1 + Rate)
                netBaseAmount = Math.Round(netGross / (1.0m + (ratePercent / 100.0m)), 3, MidpointRounding.AwayFromZero);
                taxAmount = netGross - netBaseAmount;
            }
            else
            {
                // Exclusive or 0%: NetBase = NetGross -> Tax = NetBase * Rate
                netBaseAmount = netGross;
                taxAmount = ratePercent > 0
                    ? Math.Round(netBaseAmount * (ratePercent / 100.0m), 3, MidpointRounding.AwayFromZero)
                    : 0m;
            }

            decimal lineTotal = netBaseAmount + taxAmount;

            var lineResult = new TaxCalculationLineResultDto
            {
                LineNumber = reqLine.LineNumber,
                ItemCode = reqLine.ItemCode,
                Quantity = reqLine.Quantity,
                UnitPrice = reqLine.UnitPrice,
                GrossAmount = grossAmount,
                DiscountAmount = discount,
                NetBaseAmount = netBaseAmount,
                TaxPercent = ratePercent,
                TaxAmount = taxAmount,
                LineTotalAmount = lineTotal,

                LocalNetBaseAmount = Math.Round(netBaseAmount * exchangeRate, 3, MidpointRounding.AwayFromZero),
                LocalTaxAmount = Math.Round(taxAmount * exchangeRate, 3, MidpointRounding.AwayFromZero),
                LocalLineTotalAmount = Math.Round(lineTotal * exchangeRate, 3, MidpointRounding.AwayFromZero),

                TaxRateCode = rate?.TaxRateCode ?? "VAT_15",
                TaxRateNameLocal = rate?.NameLocal ?? "ضريبة القيمة المضافة 15%",
                GlAccountCode = request.IsSales ? rate?.SalesTaxGlAccountCode : rate?.PurchaseTaxGlAccountCode
            };

            result.Lines.Add(lineResult);

            // Accumulate grand totals
            result.SubtotalAmount += netBaseAmount;
            result.TotalDiscountAmount += discount;
            result.TotalTaxAmount += taxAmount;
            result.GrandTotalAmount += lineTotal;

            result.LocalSubtotalAmount += lineResult.LocalNetBaseAmount;
            result.LocalTotalTaxAmount += lineResult.LocalTaxAmount;
            result.LocalGrandTotalAmount += lineResult.LocalLineTotalAmount;

            // Summary map
            string key = lineResult.TaxRateCode;
            if (!summaryMap.TryGetValue(key, out var summaryItem))
            {
                summaryItem = new TaxSummaryItemDto
                {
                    TaxRateCode = lineResult.TaxRateCode,
                    TaxRateNameLocal = lineResult.TaxRateNameLocal,
                    RatePercent = ratePercent,
                    GlAccountCode = lineResult.GlAccountCode
                };
                summaryMap[key] = summaryItem;
            }

            summaryItem.TotalBaseAmount += netBaseAmount;
            summaryItem.TotalTaxAmount += taxAmount;
            summaryItem.LocalTotalBaseAmount += lineResult.LocalNetBaseAmount;
            summaryItem.LocalTotalTaxAmount += lineResult.LocalTaxAmount;
        }

        result.TaxSummary = summaryMap.Values.OrderBy(s => s.TaxRateCode).ToList();

        // Build Suggested GL Journal Lines
        if (request.IsSales)
        {
            // Sales Journal:
            // Cr. Revenue / Base
            result.SuggestedGlLines.Add(new SuggestedGlJournalLineDto
            {
                AccountCode = "410101",
                AccountNameLocal = "إيرادات المبيعات العامة",
                Debit = 0,
                Credit = result.SubtotalAmount,
                LocalDebit = 0,
                LocalCredit = result.LocalSubtotalAmount,
                Description = "صافي مبيعات خاضعة للضريبة"
            });

            // Cr. Output VAT
            if (result.TotalTaxAmount > 0)
            {
                result.SuggestedGlLines.Add(new SuggestedGlJournalLineDto
                {
                    AccountCode = "213101",
                    AccountNameLocal = "حساب ضريبة القيمة المضافة المستحقة (المخرجات)",
                    Debit = 0,
                    Credit = result.TotalTaxAmount,
                    LocalDebit = 0,
                    LocalCredit = result.LocalTotalTaxAmount,
                    Description = "ضريبة القيمة المضافة المستحقة على المبيعات"
                });
            }

            // Dr. AR / Customer
            result.SuggestedGlLines.Add(new SuggestedGlJournalLineDto
            {
                AccountCode = "112101",
                AccountNameLocal = "حساب العملاء / المدينون التجاريون",
                Debit = result.GrandTotalAmount,
                Credit = 0,
                LocalDebit = result.LocalGrandTotalAmount,
                LocalCredit = 0,
                Description = "إجمالي الفاتورة المستحق على العميل"
            });
        }
        else
        {
            // Purchase Journal:
            // Dr. Purchases / Expenses
            result.SuggestedGlLines.Add(new SuggestedGlJournalLineDto
            {
                AccountCode = "510101",
                AccountNameLocal = "تكلفة المشتريات / البضاعة",
                Debit = result.SubtotalAmount,
                Credit = 0,
                LocalDebit = result.LocalSubtotalAmount,
                LocalCredit = 0,
                Description = "صافي المشتريات الخاضعة للضريبة"
            });

            // Dr. Input VAT
            if (result.TotalTaxAmount > 0)
            {
                result.SuggestedGlLines.Add(new SuggestedGlJournalLineDto
                {
                    AccountCode = "113101",
                    AccountNameLocal = "حساب ضريبة القيمة المضافة المدخلات (المستردة)",
                    Debit = result.TotalTaxAmount,
                    Credit = 0,
                    LocalDebit = result.LocalTotalTaxAmount,
                    LocalCredit = 0,
                    Description = "ضريبة القيمة المضافة المدفوعة على المشتريات"
                });
            }

            // Cr. AP / Vendor
            result.SuggestedGlLines.Add(new SuggestedGlJournalLineDto
            {
                AccountCode = "211101",
                AccountNameLocal = "حساب الموردين / الدائنون التجاريون",
                Debit = 0,
                Credit = result.GrandTotalAmount,
                LocalDebit = 0,
                LocalCredit = result.LocalGrandTotalAmount,
                Description = "إجمالي الفاتورة المستحق للمورد"
            });
        }

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tax Categories CRUD
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TaxCategoryDto>> GetCategoriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var list = await _taxRepository.GetTaxCategoriesAsync(includeInactive, cancellationToken);
        return list.Select(c => new TaxCategoryDto
        {
            Id = c.Id,
            CategoryCode = c.CategoryCode,
            NameLocal = c.NameLocal,
            NameEn = c.NameEn,
            Description = c.Description,
            DisplayOrder = c.DisplayOrder,
            IsActive = c.IsActive,
            RatesCount = c.TaxRates?.Count ?? 0
        }).ToList();
    }

    public async Task<TaxCategoryDto?> GetCategoryByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var c = await _taxRepository.GetTaxCategoryByIdAsync(id, cancellationToken);
        if (c == null) return null;

        return new TaxCategoryDto
        {
            Id = c.Id,
            CategoryCode = c.CategoryCode,
            NameLocal = c.NameLocal,
            NameEn = c.NameEn,
            Description = c.Description,
            DisplayOrder = c.DisplayOrder,
            IsActive = c.IsActive,
            RatesCount = c.TaxRates?.Count ?? 0
        };
    }

    public async Task<TaxCategoryDto> CreateCategoryAsync(CreateTaxCategoryDto dto, string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.CategoryCode))
            throw new AccountingException("رمز فئة الضريبة مطلوب.", "TAX_CAT_CODE_REQUIRED");

        var existing = await _taxRepository.GetTaxCategoryByCodeAsync(dto.CategoryCode, cancellationToken);
        if (existing != null)
            throw new AccountingException($"فئة الضريبة بالرمز ({dto.CategoryCode}) موجودة مسبقاً.", "TAX_CAT_DUPLICATE");

        var entity = new TaxCategory
        {
            CategoryCode = dto.CategoryCode.Trim().ToUpper(),
            NameLocal = dto.NameLocal.Trim(),
            NameEn = dto.NameEn.Trim(),
            Description = dto.Description?.Trim(),
            DisplayOrder = dto.DisplayOrder,
            IsActive = true,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        await _taxRepository.AddTaxCategoryAsync(entity, cancellationToken);
        await _taxRepository.SaveChangesAsync(cancellationToken);

        return (await GetCategoryByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<TaxCategoryDto> UpdateCategoryAsync(long id, UpdateTaxCategoryDto dto, string username, CancellationToken cancellationToken = default)
    {
        var entity = await _taxRepository.GetTaxCategoryByIdAsync(id, cancellationToken)
            ?? throw new AccountingNotFoundException($"فئة الضريبة ({id}) غير موجودة.", "TAX_CAT_NOT_FOUND");

        entity.NameLocal = dto.NameLocal.Trim();
        entity.NameEn = dto.NameEn.Trim();
        entity.Description = dto.Description?.Trim();
        entity.DisplayOrder = dto.DisplayOrder;
        entity.IsActive = dto.IsActive;
        entity.UpdateUser = username;
        entity.UpdateDate = DateTime.UtcNow;

        await _taxRepository.SaveChangesAsync(cancellationToken);
        return (await GetCategoryByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task DeleteCategoryAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _taxRepository.GetTaxCategoryByIdAsync(id, cancellationToken)
            ?? throw new AccountingNotFoundException($"فئة الضريبة ({id}) غير موجودة.", "TAX_CAT_NOT_FOUND");

        if (entity.TaxRates.Count > 0)
            throw new AccountingException("لا يمكن حذف فئة الضريبة لوجود نسب ضريبية مرتبطة بها.", "TAX_CAT_HAS_RATES");

        await _taxRepository.DeleteTaxCategoryAsync(entity, cancellationToken);
        await _taxRepository.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tax Rates CRUD
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TaxRateDto>> GetRatesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var list = await _taxRepository.GetTaxRatesAsync(includeInactive, cancellationToken);
        return list.Select(MapRateToDto).ToList();
    }

    public async Task<TaxRateDto?> GetRateByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var r = await _taxRepository.GetTaxRateByIdAsync(id, cancellationToken);
        return r == null ? null : MapRateToDto(r);
    }

    public async Task<TaxRateDto?> GetRateByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var r = await _taxRepository.GetTaxRateByCodeAsync(code, cancellationToken);
        return r == null ? null : MapRateToDto(r);
    }

    public async Task<TaxRateDto> CreateRateAsync(CreateTaxRateDto dto, string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.TaxRateCode))
            throw new AccountingException("رمز النسبة الضريبية مطلوب.", "TAX_RATE_CODE_REQUIRED");

        var existing = await _taxRepository.GetTaxRateByCodeAsync(dto.TaxRateCode, cancellationToken);
        if (existing != null)
            throw new AccountingException($"النسبة الضريبية بالرمز ({dto.TaxRateCode}) موجودة مسبقاً.", "TAX_RATE_DUPLICATE");

        var category = await _taxRepository.GetTaxCategoryByIdAsync(dto.TaxCategoryId, cancellationToken)
            ?? throw new AccountingException($"فئة الضريبة ({dto.TaxCategoryId}) غير موجودة.", "TAX_CAT_INVALID");

        var entity = new TaxRate
        {
            TaxRateCode = dto.TaxRateCode.Trim().ToUpper(),
            TaxCategoryId = dto.TaxCategoryId,
            NameLocal = dto.NameLocal.Trim(),
            NameEn = dto.NameEn.Trim(),
            RatePercent = dto.RatePercent,
            RateType = dto.RateType,
            SalesTaxGlAccountCode = dto.SalesTaxGlAccountCode?.Trim(),
            PurchaseTaxGlAccountCode = dto.PurchaseTaxGlAccountCode?.Trim(),
            IsExempt = dto.IsExempt,
            IsZeroRated = dto.IsZeroRated,
            ExemptionReasonCode = dto.ExemptionReasonCode?.Trim(),
            ExemptionReasonLocal = dto.ExemptionReasonLocal?.Trim(),
            ExemptionReasonEn = dto.ExemptionReasonEn?.Trim(),
            DisplayOrder = dto.DisplayOrder,
            IsActive = true,
            Description = dto.Description?.Trim(),
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        await _taxRepository.AddTaxRateAsync(entity, cancellationToken);
        await _taxRepository.SaveChangesAsync(cancellationToken);

        return (await GetRateByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<TaxRateDto> UpdateRateAsync(long id, UpdateTaxRateDto dto, string username, CancellationToken cancellationToken = default)
    {
        var entity = await _taxRepository.GetTaxRateByIdAsync(id, cancellationToken)
            ?? throw new AccountingNotFoundException($"النسبة الضريبية ({id}) غير موجودة.", "TAX_RATE_NOT_FOUND");

        entity.NameLocal = dto.NameLocal.Trim();
        entity.NameEn = dto.NameEn.Trim();
        entity.RatePercent = dto.RatePercent;
        entity.RateType = dto.RateType;
        entity.SalesTaxGlAccountCode = dto.SalesTaxGlAccountCode?.Trim();
        entity.PurchaseTaxGlAccountCode = dto.PurchaseTaxGlAccountCode?.Trim();
        entity.IsExempt = dto.IsExempt;
        entity.IsZeroRated = dto.IsZeroRated;
        entity.ExemptionReasonCode = dto.ExemptionReasonCode?.Trim();
        entity.ExemptionReasonLocal = dto.ExemptionReasonLocal?.Trim();
        entity.ExemptionReasonEn = dto.ExemptionReasonEn?.Trim();
        entity.DisplayOrder = dto.DisplayOrder;
        entity.IsActive = dto.IsActive;
        entity.Description = dto.Description?.Trim();
        entity.UpdateUser = username;
        entity.UpdateDate = DateTime.UtcNow;

        await _taxRepository.SaveChangesAsync(cancellationToken);
        return (await GetRateByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task DeleteRateAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _taxRepository.GetTaxRateByIdAsync(id, cancellationToken)
            ?? throw new AccountingNotFoundException($"النسبة الضريبية ({id}) غير موجودة.", "TAX_RATE_NOT_FOUND");

        await _taxRepository.DeleteTaxRateAsync(entity, cancellationToken);
        await _taxRepository.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tax Groups CRUD
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TaxGroupDto>> GetGroupsAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var list = await _taxRepository.GetTaxGroupsAsync(includeInactive, cancellationToken);
        return list.Select(MapGroupToDto).ToList();
    }

    public async Task<TaxGroupDto?> GetGroupByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var g = await _taxRepository.GetTaxGroupByIdAsync(id, cancellationToken);
        return g == null ? null : MapGroupToDto(g);
    }

    public async Task<TaxGroupDto> CreateGroupAsync(CreateTaxGroupDto dto, string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.GroupCode))
            throw new AccountingException("رمز المجموعة الضريبية مطلوب.", "TAX_GRP_CODE_REQUIRED");

        var existing = await _taxRepository.GetTaxGroupByCodeAsync(dto.GroupCode, cancellationToken);
        if (existing != null)
            throw new AccountingException($"المجموعة الضريبية بالرمز ({dto.GroupCode}) موجودة مسبقاً.", "TAX_GRP_DUPLICATE");

        var entity = new TaxGroup
        {
            GroupCode = dto.GroupCode.Trim().ToUpper(),
            NameLocal = dto.NameLocal.Trim(),
            NameEn = dto.NameEn.Trim(),
            Description = dto.Description?.Trim(),
            IsActive = true,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        foreach (var item in dto.Items)
        {
            entity.Items.Add(new TaxGroupItem
            {
                TaxRateId = item.TaxRateId,
                ApplicationOrder = item.ApplicationOrder,
                IsCompound = item.IsCompound
            });
        }

        await _taxRepository.AddTaxGroupAsync(entity, cancellationToken);
        await _taxRepository.SaveChangesAsync(cancellationToken);

        return (await GetGroupByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task DeleteGroupAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _taxRepository.GetTaxGroupByIdAsync(id, cancellationToken)
            ?? throw new AccountingNotFoundException($"المجموعة الضريبية ({id}) غير موجودة.", "TAX_GRP_NOT_FOUND");

        await _taxRepository.DeleteTaxGroupAsync(entity, cancellationToken);
        await _taxRepository.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Multi-Country VAT / Tax Return Declarations Framework
    // ─────────────────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<TaxDeclarationTemplateDto>> GetAvailableDeclarationTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var list = _declarationProviders.Values
            .Select(p => p.GetTemplateMetadata())
            .ToList();

        return Task.FromResult<IReadOnlyList<TaxDeclarationTemplateDto>>(list);
    }

    public Task<TaxDeclarationTemplateDto?> GetDeclarationTemplateAsync(string templateCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
            return Task.FromResult<TaxDeclarationTemplateDto?>(null);

        if (_declarationProviders.TryGetValue(templateCode.Trim(), out var provider))
        {
            return Task.FromResult<TaxDeclarationTemplateDto?>(provider.GetTemplateMetadata());
        }

        return Task.FromResult<TaxDeclarationTemplateDto?>(null);
    }

    public async Task<VatDeclarationDto> GetVatDeclarationAsync(VatDeclarationFilterDto filter, CancellationToken cancellationToken = default)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));

        var transactions = await _taxRepository.GetTaxTransactionsAsync(
            filter.BranchId,
            filter.FromDate,
            filter.ToDate,
            isSalesTax: null,
            cancellationToken);

        var rates = await _taxRepository.GetTaxRatesAsync(includeInactive: true, cancellationToken);

        // Resolve Provider
        ITaxDeclarationProvider provider;
        if (!string.IsNullOrWhiteSpace(filter.TemplateCode) && _declarationProviders.TryGetValue(filter.TemplateCode.Trim(), out var customProvider))
        {
            provider = customProvider;
        }
        else
        {
            // Default to GCC_ZATCA_16
            provider = _declarationProviders["GCC_ZATCA_16"];
        }

        string branchNameLocal = "كافة الفروع";
        string branchNameEn = "All Branches";

        return provider.BuildDeclaration(filter, transactions, rates, branchNameLocal, branchNameEn);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ZATCA QR Code Generator (TLV Base64 Format)
    // ─────────────────────────────────────────────────────────────────────────

    public ZatcaQrResultDto GenerateZatcaQrCode(ZatcaQrRequestDto request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var ms = new MemoryStream();

        // Tag 1: Seller Name
        WriteTlvTag(ms, 1, request.SellerName ?? string.Empty);

        // Tag 2: Tax Registration Number
        WriteTlvTag(ms, 2, request.TaxRegistrationNumber ?? string.Empty);

        // Tag 3: Invoice Timestamp (ISO 8601 UTC string: yyyy-MM-ddTHH:mm:ssZ)
        string timeStr = request.InvoiceTimestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        WriteTlvTag(ms, 3, timeStr);

        // Tag 4: Invoice Total with VAT (Formatted to 2 decimals)
        string totalStr = request.InvoiceTotalWithVat.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        WriteTlvTag(ms, 4, totalStr);

        // Tag 5: Total VAT Amount (Formatted to 2 decimals)
        string vatStr = request.VatTotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        WriteTlvTag(ms, 5, vatStr);

        byte[] tlvBytes = ms.ToArray();
        string base64 = Convert.ToBase64String(tlvBytes);

        return new ZatcaQrResultDto
        {
            QrBase64 = base64,
            SellerName = request.SellerName,
            TaxRegistrationNumber = request.TaxRegistrationNumber,
            InvoiceTimestamp = request.InvoiceTimestamp,
            InvoiceTotalWithVat = request.InvoiceTotalWithVat,
            VatTotal = request.VatTotal
        };
    }

    private static void WriteTlvTag(MemoryStream ms, byte tag, string value)
    {
        byte[] valBytes = Encoding.UTF8.GetBytes(value);
        ms.WriteByte(tag);
        ms.WriteByte((byte)valBytes.Length);
        ms.Write(valBytes, 0, valBytes.Length);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static TaxRateDto MapRateToDto(TaxRate r) => new()
    {
        Id = r.Id,
        TaxRateCode = r.TaxRateCode,
        TaxCategoryId = r.TaxCategoryId,
        CategoryCode = r.Category?.CategoryCode ?? string.Empty,
        CategoryNameLocal = r.Category?.NameLocal ?? string.Empty,
        NameLocal = r.NameLocal,
        NameEn = r.NameEn,
        RatePercent = r.RatePercent,
        RateType = r.RateType,
        SalesTaxGlAccountCode = r.SalesTaxGlAccountCode,
        SalesTaxGlAccountNameLocal = r.SalesTaxGlAccount?.AccountNameLocal,
        PurchaseTaxGlAccountCode = r.PurchaseTaxGlAccountCode,
        PurchaseTaxGlAccountNameLocal = r.PurchaseTaxGlAccount?.AccountNameLocal,
        IsExempt = r.IsExempt,
        IsZeroRated = r.IsZeroRated,
        ExemptionReasonCode = r.ExemptionReasonCode,
        ExemptionReasonLocal = r.ExemptionReasonLocal,
        ExemptionReasonEn = r.ExemptionReasonEn,
        DisplayOrder = r.DisplayOrder,
        IsActive = r.IsActive,
        Description = r.Description
    };

    private static TaxGroupDto MapGroupToDto(TaxGroup g) => new()
    {
        Id = g.Id,
        GroupCode = g.GroupCode,
        NameLocal = g.NameLocal,
        NameEn = g.NameEn,
        Description = g.Description,
        IsActive = g.IsActive,
        Items = g.Items.Select(i => new TaxGroupItemDto
        {
            Id = i.Id,
            TaxRateId = i.TaxRateId,
            TaxRateCode = i.TaxRate?.TaxRateCode ?? string.Empty,
            NameLocal = i.TaxRate?.NameLocal ?? string.Empty,
            RatePercent = i.TaxRate?.RatePercent ?? 0m,
            ApplicationOrder = i.ApplicationOrder,
            IsCompound = i.IsCompound
        }).ToList()
    };
}
