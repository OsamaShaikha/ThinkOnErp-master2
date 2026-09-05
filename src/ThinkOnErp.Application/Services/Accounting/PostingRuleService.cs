using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Accounting.PostingRules;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class PostingRuleService : IPostingRuleService
{
    private readonly IPostingRuleRepository _ruleRepository;
    private readonly IGlAccountRepository _accountRepository;
    private readonly IGlCostCenterRepository _costCenterRepository;
    private readonly IGlVoucherService _voucherService;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<PostingRuleService> _logger;

    public PostingRuleService(
        IPostingRuleRepository ruleRepository,
        IGlAccountRepository accountRepository,
        IGlCostCenterRepository costCenterRepository,
        IGlVoucherService voucherService,
        ICurrentTenantContext tenantContext,
        ILogger<PostingRuleService> logger)
    {
        _ruleRepository = ruleRepository;
        _accountRepository = accountRepository;
        _costCenterRepository = costCenterRepository;
        _voucherService = voucherService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PostingRuleDto>> GetRulesAsync(string? module, long? branchId, CancellationToken cancellationToken = default)
    {
        var rules = await _ruleRepository.GetRulesAsync(module, branchId, cancellationToken);
        return rules.Select(MapToDto).ToList();
    }

    public async Task<PostingRuleDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var rule = await _ruleRepository.GetByIdAsync(id, cancellationToken);
        if (rule == null)
        {
            throw new AccountingNotFoundException($"قاعدة الترحيل رقم ({id}) غير موجودة.", "POSTING_RULE_NOT_FOUND");
        }
        return MapToDto(rule);
    }

    public async Task<PostingRuleDto> CreateRuleAsync(CreatePostingRuleDto dto, string username, CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.GetRequiredCompanyId();
        
        var debitAcc = await _accountRepository.GetByCodeAsync(companyId, dto.DebitAccountCode, cancellationToken);
        if (debitAcc == null)
        {
            throw new AccountingNotFoundException($"الحساب المدين ({dto.DebitAccountCode}) غير موجود في الدليل.", "DEBIT_ACCOUNT_NOT_FOUND");
        }

        var creditAcc = await _accountRepository.GetByCodeAsync(companyId, dto.CreditAccountCode, cancellationToken);
        if (creditAcc == null)
        {
            throw new AccountingNotFoundException($"الحساب الدائن ({dto.CreditAccountCode}) غير موجود في الدليل.", "CREDIT_ACCOUNT_NOT_FOUND");
        }

        var existing = await _ruleRepository.GetRuleAsync(dto.Module, dto.EventType, dto.BranchId, cancellationToken);
        if (existing != null)
        {
            throw new AccountingException($"يوجد قاعدة ترحيل مسبقة لنفس الحركة ({dto.Module} - {dto.EventType}) لهذا الفرع/الشركة.", "DUPLICATE_POSTING_RULE");
        }

        var rule = new GlPostingRule
        {
            BranchId = dto.BranchId,
            Module = dto.Module.ToUpper(),
            EventType = dto.EventType.ToUpper(),
            EventNameLocal = dto.EventNameLocal,
            EventNameEn = dto.EventNameEn,
            DebitAccountCode = dto.DebitAccountCode,
            CreditAccountCode = dto.CreditAccountCode,
            DefaultCostCenterCode = dto.DefaultCostCenterCode,
            DefaultVoucherType = dto.DefaultVoucherType,
            DescriptionTemplate = dto.DescriptionTemplate,
            IsActive = dto.IsActive,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        await _ruleRepository.AddAsync(rule, cancellationToken);
        await _ruleRepository.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(rule.Id, cancellationToken);
    }

    public async Task<PostingRuleDto> UpdateRuleAsync(long id, UpdatePostingRuleDto dto, string username, CancellationToken cancellationToken = default)
    {
        var rule = await _ruleRepository.GetByIdAsync(id, cancellationToken);
        if (rule == null)
        {
            throw new AccountingNotFoundException($"قاعدة الترحيل رقم ({id}) غير موجودة.", "POSTING_RULE_NOT_FOUND");
        }

        var companyId = _tenantContext.GetRequiredCompanyId();

        var debitAcc = await _accountRepository.GetByCodeAsync(companyId, dto.DebitAccountCode, cancellationToken);
        if (debitAcc == null)
        {
            throw new AccountingNotFoundException($"الحساب المدين ({dto.DebitAccountCode}) غير موجود في الدليل.", "DEBIT_ACCOUNT_NOT_FOUND");
        }

        var creditAcc = await _accountRepository.GetByCodeAsync(companyId, dto.CreditAccountCode, cancellationToken);
        if (creditAcc == null)
        {
            throw new AccountingNotFoundException($"الحساب الدائن ({dto.CreditAccountCode}) غير موجود في الدليل.", "CREDIT_ACCOUNT_NOT_FOUND");
        }

        rule.DebitAccountCode = dto.DebitAccountCode;
        rule.CreditAccountCode = dto.CreditAccountCode;
        rule.DefaultCostCenterCode = dto.DefaultCostCenterCode;
        rule.DefaultVoucherType = dto.DefaultVoucherType;
        rule.DescriptionTemplate = dto.DescriptionTemplate;
        rule.IsActive = dto.IsActive;
        rule.UpdateUser = username;
        rule.UpdateDate = DateTime.UtcNow;

        await _ruleRepository.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteRuleAsync(long id, CancellationToken cancellationToken = default)
    {
        var rule = await _ruleRepository.GetByIdAsync(id, cancellationToken);
        if (rule == null)
        {
            throw new AccountingNotFoundException($"قاعدة الترحيل رقم ({id}) غير موجودة.", "POSTING_RULE_NOT_FOUND");
        }

        _ruleRepository.Remove(rule);
        await _ruleRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<GlVoucherHeaderDto> PostAutomaticEventAsync(AutomaticPostingEventRequest request, string username, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            throw new AccountingException("يجب أن يكون مبلغ الحركة أكبر من الصفر.", "INVALID_AMOUNT");
        }

        var rule = await _ruleRepository.GetRuleAsync(request.Module.ToUpper(), request.EventType.ToUpper(), request.BranchId, cancellationToken);
        if (rule == null)
        {
            throw new AccountingException($"لا توجد قاعدة ترحيل معرفة للحركة ({request.Module} - {request.EventType}). يرجى ضبط قواعد الترحيل أولاً.", "POSTING_RULE_NOT_CONFIGURED");
        }

        string desc = request.CustomDescription ?? rule.DescriptionTemplate ?? $"{rule.EventNameLocal} - مرجع: {request.ReferenceNo}";
        string? cc = request.CostCenterCode ?? rule.DefaultCostCenterCode;

        var voucherDto = new CreateGlVoucherDto
        {
            BranchId = request.BranchId,
            FiscalYearId = request.FiscalYearId,
            VoucherType = rule.DefaultVoucherType,
            VoucherDate = request.VoucherDate,
            Description = desc,
            Details = new List<CreateGlVoucherDetailDto>
            {
                // Debit Line
                new()
                {
                    AccountCode = rule.DebitAccountCode,
                    Debit = request.Amount,
                    Credit = 0,
                    Description = desc,
                    CostCenterCode = cc,
                    PartyType = request.PartyType,
                    PartyCode = request.PartyCode,
                    CurrencyId = request.CurrencyId,
                    ExchangeRate = request.ExchangeRate,
                    BranchId = request.BranchId
                },
                // Credit Line
                new()
                {
                    AccountCode = rule.CreditAccountCode,
                    Debit = 0,
                    Credit = request.Amount,
                    Description = desc,
                    CostCenterCode = cc,
                    PartyType = request.PartyType,
                    PartyCode = request.PartyCode,
                    CurrencyId = request.CurrencyId,
                    ExchangeRate = request.ExchangeRate,
                    BranchId = request.BranchId
                }
            }
        };

        var voucher = await _voucherService.CreateVoucherAsync(voucherDto, username, cancellationToken);
        var posted = await _voucherService.PostVoucherAsync(voucher.Id, username, cancellationToken);

        _logger.LogInformation("Automatic Posting executed for {Module}.{EventType}: Created & Posted GL Voucher #{VoucherNo} (ID: {VoucherId}) by {User}",
            request.Module, request.EventType, posted.VoucherNo, posted.Id, username);

        return posted;
    }

    public async Task SeedDefaultPostingRulesAsync(long? branchId, string username, CancellationToken cancellationToken = default)
    {
        var defaultRules = new List<CreatePostingRuleDto>
        {
            // Sales
            new() { BranchId = branchId, Module = "SALES", EventType = "SALES_INVOICE", EventNameLocal = "فاتورة مبيعات آجل", EventNameEn = "Credit Sales Invoice", DebitAccountCode = "112101", CreditAccountCode = "410101", DefaultVoucherType = 101, DescriptionTemplate = "إيراد فاتورة مبيعات رقم {InvoiceNo}" },
            new() { BranchId = branchId, Module = "SALES", EventType = "CASH_SALES", EventNameLocal = "فاتورة مبيعات نقدي", EventNameEn = "Cash Sales Invoice", DebitAccountCode = "111101", CreditAccountCode = "410101", DefaultVoucherType = 102, DescriptionTemplate = "مبيعات نقدية فاتورة رقم {InvoiceNo}" },
            new() { BranchId = branchId, Module = "SALES", EventType = "SALES_DISCOUNT", EventNameLocal = "خصم مسموح به (مبيعات)", EventNameEn = "Sales Discount Allowed", DebitAccountCode = "410201", CreditAccountCode = "112101", DefaultVoucherType = 101, DescriptionTemplate = "خصم مسموح به للعميل {PartyCode}" },
            
            // Purchases
            new() { BranchId = branchId, Module = "PURCHASES", EventType = "PURCHASE_BILL", EventNameLocal = "فاتورة مشتريات آجل", EventNameEn = "Credit Purchase Bill", DebitAccountCode = "113101", CreditAccountCode = "211101", DefaultVoucherType = 101, DescriptionTemplate = "فاتورة شراء بضاعة من المورد {PartyCode}" },
            new() { BranchId = branchId, Module = "PURCHASES", EventType = "PURCHASE_DISCOUNT", EventNameLocal = "خصم مكتسب (مشتريات)", EventNameEn = "Purchase Discount Received", DebitAccountCode = "211101", CreditAccountCode = "420101", DefaultVoucherType = 101, DescriptionTemplate = "خصم مكتسب من المورد {PartyCode}" },
            
            // Receipts & Payments
            new() { BranchId = branchId, Module = "CASH_BANK", EventType = "CUSTOMER_RECEIPT_CASH", EventNameLocal = "سند قبض نقدي من عميل", EventNameEn = "Customer Cash Receipt", DebitAccountCode = "111101", CreditAccountCode = "112101", DefaultVoucherType = 102, DescriptionTemplate = "قبض نقدي من العميل {PartyCode}" },
            new() { BranchId = branchId, Module = "CASH_BANK", EventType = "CUSTOMER_RECEIPT_BANK", EventNameLocal = "سند قبض حوالة بنكية", EventNameEn = "Customer Bank Receipt", DebitAccountCode = "111201", CreditAccountCode = "112101", DefaultVoucherType = 102, DescriptionTemplate = "حوالة بنكية من العميل {PartyCode}" },
            new() { BranchId = branchId, Module = "CASH_BANK", EventType = "VENDOR_PAYMENT_CASH", EventNameLocal = "سند صرف نقدي لمورد", EventNameEn = "Vendor Cash Payment", DebitAccountCode = "211101", CreditAccountCode = "111101", DefaultVoucherType = 103, DescriptionTemplate = "دفعة نقدية للمورد {PartyCode}" },
            new() { BranchId = branchId, Module = "CASH_BANK", EventType = "VENDOR_PAYMENT_BANK", EventNameLocal = "سند صرف تحويل بنكي لمورد", EventNameEn = "Vendor Bank Payment", DebitAccountCode = "211101", CreditAccountCode = "111201", DefaultVoucherType = 103, DescriptionTemplate = "تحويل بنكي للمورد {PartyCode}" },

            // PDC
            new() { BranchId = branchId, Module = "PDC", EventType = "PDC_INWARD_RECEIPT", EventNameLocal = "استلام شيك آجل من عميل", EventNameEn = "PDC Inward Receipt", DebitAccountCode = "111301", CreditAccountCode = "112101", DefaultVoucherType = 102, DescriptionTemplate = "شيك آجل برسم التحصيل رقم {ChequeNo}" },
            new() { BranchId = branchId, Module = "PDC", EventType = "PDC_INWARD_CLEAR", EventNameLocal = "تحصيل شيك مقبوض بالبنك", EventNameEn = "PDC Inward Clearing", DebitAccountCode = "111201", CreditAccountCode = "111301", DefaultVoucherType = 101, DescriptionTemplate = "تحصيل شيك رقم {ChequeNo} وإيداعه بالبنك" },
            new() { BranchId = branchId, Module = "PDC", EventType = "PDC_INWARD_BOUNCE", EventNameLocal = "ارتداد شيك مقبوض", EventNameEn = "PDC Inward Bounce", DebitAccountCode = "112101", CreditAccountCode = "111301", DefaultVoucherType = 101, DescriptionTemplate = "إلغاء وإعادة فتح مديونية لارتداد الشيك رقم {ChequeNo}" },
            new() { BranchId = branchId, Module = "PDC", EventType = "PDC_OUTWARD_ISSUE", EventNameLocal = "إصدار شيك آجل لمورد", EventNameEn = "PDC Outward Issuance", DebitAccountCode = "211101", CreditAccountCode = "211201", DefaultVoucherType = 103, DescriptionTemplate = "شيك صادر للمورد {PartyCode} رقم {ChequeNo}" },
            new() { BranchId = branchId, Module = "PDC", EventType = "PDC_OUTWARD_CLEAR", EventNameLocal = "صرف شيك صادر من البنك", EventNameEn = "PDC Outward Clearing", DebitAccountCode = "211201", CreditAccountCode = "111201", DefaultVoucherType = 101, DescriptionTemplate = "خصم من البنك لصرف الشيك الصادر رقم {ChequeNo}" },

            // Inventory & COGS
            new() { BranchId = branchId, Module = "INVENTORY", EventType = "COGS_POSTING", EventNameLocal = "إثبات تكلفة البضاعة المباعة", EventNameEn = "Cost of Goods Sold Posting", DebitAccountCode = "510101", CreditAccountCode = "113101", DefaultVoucherType = 101, DescriptionTemplate = "تكلفة مبيعات البضاعة المباعة" },
            new() { BranchId = branchId, Module = "INVENTORY", EventType = "INVENTORY_DAMAGE", EventNameLocal = "إثبات تالف وهالك المخزون", EventNameEn = "Inventory Damage Write-off", DebitAccountCode = "520901", CreditAccountCode = "113101", DefaultVoucherType = 101, DescriptionTemplate = "تسوية هالك مخزون" },

            // Fixed Assets & FX
            new() { BranchId = branchId, Module = "FIXED_ASSETS", EventType = "MONTHLY_DEPRECIATION", EventNameLocal = "قيد الإهلاك الشهري للأصول", EventNameEn = "Monthly Asset Depreciation", DebitAccountCode = "520801", CreditAccountCode = "120901", DefaultVoucherType = 101, DescriptionTemplate = "إهلاك الأصول الثابتة للفترة" },
            new() { BranchId = branchId, Module = "FX", EventType = "FX_UNREALIZED_GAIN", EventNameLocal = "أرباح تقييم فروق العملة", EventNameEn = "Unrealized FX Gain", DebitAccountCode = "111201", CreditAccountCode = "420201", DefaultVoucherType = 101, DescriptionTemplate = "أرباح فروق تقييم أسعار العملات الأجنبية" },
            new() { BranchId = branchId, Module = "FX", EventType = "FX_UNREALIZED_LOSS", EventNameLocal = "خسائر تقييم فروق العملة", EventNameEn = "Unrealized FX Loss", DebitAccountCode = "580101", CreditAccountCode = "111201", DefaultVoucherType = 101, DescriptionTemplate = "خسائر فروق تقييم أسعار العملات الأجنبية" },
            new() { BranchId = branchId, Module = "CASH_BANK", EventType = "BANK_CHARGES", EventNameLocal = "المصاريف والعمولات البنكية", EventNameEn = "Bank Fees & Charges", DebitAccountCode = "510901", CreditAccountCode = "111201", DefaultVoucherType = 101, DescriptionTemplate = "عمولات ومصاريف بنكية" }
        };

        foreach (var r in defaultRules)
        {
            var existing = await _ruleRepository.GetRuleAsync(r.Module, r.EventType, branchId, cancellationToken);
            if (existing == null)
            {
                var rule = new GlPostingRule
                {
                    BranchId = branchId,
                    Module = r.Module,
                    EventType = r.EventType,
                    EventNameLocal = r.EventNameLocal,
                    EventNameEn = r.EventNameEn,
                    DebitAccountCode = r.DebitAccountCode,
                    CreditAccountCode = r.CreditAccountCode,
                    DefaultCostCenterCode = r.DefaultCostCenterCode,
                    DefaultVoucherType = r.DefaultVoucherType,
                    DescriptionTemplate = r.DescriptionTemplate,
                    IsActive = true,
                    CreationUser = username,
                    CreationDate = DateTime.UtcNow
                };
                await _ruleRepository.AddAsync(rule, cancellationToken);
            }
        }

        await _ruleRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} default posting rules for branch {Branch} by {User}", defaultRules.Count, branchId, username);
    }

    private static PostingRuleDto MapToDto(GlPostingRule r)
    {
        return new PostingRuleDto
        {
            Id = r.Id,
            BranchId = r.BranchId,
            BranchNameLocal = r.Branch?.BranchNameLocal,
            Module = r.Module,
            EventType = r.EventType,
            EventNameLocal = r.EventNameLocal,
            EventNameEn = r.EventNameEn,
            DebitAccountCode = r.DebitAccountCode,
            DebitAccountNameLocal = r.DebitAccount?.AccountNameLocal,
            CreditAccountCode = r.CreditAccountCode,
            CreditAccountNameLocal = r.CreditAccount?.AccountNameLocal,
            DefaultCostCenterCode = r.DefaultCostCenterCode,
            DefaultCostCenterNameLocal = r.DefaultCostCenter?.NameLocal,
            DefaultVoucherType = r.DefaultVoucherType,
            DescriptionTemplate = r.DescriptionTemplate,
            IsActive = r.IsActive
        };
    }
}
