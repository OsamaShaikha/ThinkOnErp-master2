using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Mappings.Accounting;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class GlVoucherService : IGlVoucherService
{
    private readonly IGlVoucherRepository _voucherRepository;
    private readonly IGlAccountRepository _accountRepository;
    private readonly IGlCostCenterRepository _costCenterRepository;
    private readonly IGlFiscalPeriodRepository _periodRepository;
    private readonly IGlAccountBalanceRepository _balanceRepository;
    private readonly IArSubledgerRepository _arSubledgerRepository;
    private readonly IApSubledgerRepository _apSubledgerRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<GlVoucherService> _logger;

    public GlVoucherService(
        IGlVoucherRepository voucherRepository,
        IGlAccountRepository accountRepository,
        IGlCostCenterRepository costCenterRepository,
        IGlFiscalPeriodRepository periodRepository,
        IGlAccountBalanceRepository balanceRepository,
        IArSubledgerRepository arSubledgerRepository,
        IApSubledgerRepository apSubledgerRepository,
        ICurrentTenantContext tenantContext,
        ILogger<GlVoucherService> logger)
    {
        _voucherRepository = voucherRepository;
        _accountRepository = accountRepository;
        _costCenterRepository = costCenterRepository;
        _periodRepository = periodRepository;
        _balanceRepository = balanceRepository;
        _arSubledgerRepository = arSubledgerRepository;
        _apSubledgerRepository = apSubledgerRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GlVoucherTypeDto>> GetVoucherTypesAsync(CancellationToken cancellationToken = default)
    {
        var types = await _voucherRepository.GetVoucherTypesAsync(cancellationToken);
        return types.Select(GlVoucherMapper.ToDto).ToList();
    }

    public async Task<GlVoucherTypeDto?> GetVoucherTypeByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var type = await _voucherRepository.GetVoucherTypeByIdAsync(id, cancellationToken);
        return type == null ? null : GlVoucherMapper.ToDto(type);
    }

    public async Task<GlVoucherTypeDto?> GetVoucherTypeByCodeAsync(int typeCode, CancellationToken cancellationToken = default)
    {
        var type = await _voucherRepository.GetVoucherTypeByCodeAsync(typeCode, cancellationToken);
        return type == null ? null : GlVoucherMapper.ToDto(type);
    }

    public async Task<GlVoucherTypeDto> CreateVoucherTypeAsync(
        CreateGlVoucherTypeDto dto,
        string username,
        CancellationToken cancellationToken = default)
    {
        if (dto.TypeCode <= 0)
            throw new AccountingException("Voucher TypeCode must be a positive integer.", ErrorCodes.OutOfRange);

        if (string.IsNullOrWhiteSpace(dto.TypeKey))
            throw new AccountingException("Voucher TypeKey is required.", ErrorCodes.FieldRequired);

        if (string.IsNullOrWhiteSpace(dto.NameLocal))
            throw new AccountingException("Voucher NameLocal is required.", ErrorCodes.FieldRequired);

        var exists = await _voucherRepository.VoucherTypeExistsAsync(dto.TypeCode, dto.TypeKey, null, cancellationToken);
        if (exists)
            throw new AccountingException($"A voucher type with code '{dto.TypeCode}' or key '{dto.TypeKey}' already exists.", ErrorCodes.DuplicateValue);

        var entity = new GlVoucherType
        {
            TypeCode = dto.TypeCode,
            TypeKey = dto.TypeKey.Trim().ToUpper(),
            NameLocal = dto.NameLocal.Trim(),
            NameEn = string.IsNullOrWhiteSpace(dto.NameEn) ? dto.NameLocal.Trim() : dto.NameEn.Trim(),
            Prefix = string.IsNullOrWhiteSpace(dto.Prefix) ? dto.TypeKey.Trim().ToUpper() : dto.Prefix.Trim().ToUpper(),
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "JOURNAL" : dto.Category.Trim().ToUpper(),
            SerialResetPolicy = string.IsNullOrWhiteSpace(dto.SerialResetPolicy) ? "MONTHLY" : dto.SerialResetPolicy.Trim().ToUpper(),
            RequiresReview = dto.RequiresReview,
            AllowManualEntry = dto.AllowManualEntry,
            IsSystem = false,
            DisplayOrder = dto.DisplayOrder,
            IsActive = dto.IsActive,
            Description = dto.Description,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        var created = await _voucherRepository.CreateVoucherTypeAsync(entity, cancellationToken);
        _logger.LogInformation("Voucher type {TypeCode} ({TypeKey}) created successfully by {User}", created.TypeCode, created.TypeKey, username);
        return GlVoucherMapper.ToDto(created);
    }

    public async Task<GlVoucherTypeDto> UpdateVoucherTypeAsync(
        long id,
        UpdateGlVoucherTypeDto dto,
        string username,
        CancellationToken cancellationToken = default)
    {
        var existing = await _voucherRepository.GetVoucherTypeByIdAsync(id, cancellationToken);
        if (existing == null)
            throw new AccountingException($"Voucher type with ID '{id}' was not found.", ErrorCodes.VoucherNotFound);

        if (string.IsNullOrWhiteSpace(dto.NameLocal))
            throw new AccountingException("Voucher NameLocal is required.", ErrorCodes.FieldRequired);

        existing.NameLocal = dto.NameLocal.Trim();
        existing.NameEn = string.IsNullOrWhiteSpace(dto.NameEn) ? dto.NameLocal.Trim() : dto.NameEn.Trim();
        existing.Prefix = string.IsNullOrWhiteSpace(dto.Prefix) ? existing.Prefix : dto.Prefix.Trim().ToUpper();
        existing.Category = string.IsNullOrWhiteSpace(dto.Category) ? existing.Category : dto.Category.Trim().ToUpper();
        existing.SerialResetPolicy = string.IsNullOrWhiteSpace(dto.SerialResetPolicy) ? existing.SerialResetPolicy : dto.SerialResetPolicy.Trim().ToUpper();
        existing.RequiresReview = dto.RequiresReview;
        existing.AllowManualEntry = dto.AllowManualEntry;
        existing.DisplayOrder = dto.DisplayOrder;
        existing.IsActive = dto.IsActive;
        existing.Description = dto.Description;
        existing.UpdateUser = username;
        existing.UpdateDate = DateTime.UtcNow;

        var updated = await _voucherRepository.UpdateVoucherTypeAsync(existing, cancellationToken);
        _logger.LogInformation("Voucher type {Id} updated successfully by {User}", id, username);
        return GlVoucherMapper.ToDto(updated);
    }

    public async Task DeleteVoucherTypeAsync(long id, CancellationToken cancellationToken = default)
    {
        var existing = await _voucherRepository.GetVoucherTypeByIdAsync(id, cancellationToken);
        if (existing == null)
            throw new AccountingException($"Voucher type with ID '{id}' was not found.", ErrorCodes.VoucherNotFound);

        if (existing.IsSystem)
            throw new AccountingException("System voucher types cannot be deleted.", ErrorCodes.InvalidFormat);

        var hasVouchers = await _voucherRepository.HasAssociatedVouchersAsync(existing.TypeCode, cancellationToken);
        if (hasVouchers)
            throw new AccountingException($"Cannot delete voucher type '{existing.TypeCode}' because vouchers are already recorded under it.", ErrorCodes.DuplicateValue);

        await _voucherRepository.DeleteVoucherTypeAsync(id, cancellationToken);
        _logger.LogInformation("Voucher type {Id} deleted successfully", id);
    }

    public async Task<PagedResultDto<GlVoucherHeaderDto>> GetPagedVouchersAsync(
        GlVoucherFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _voucherRepository.GetPagedVouchersAsync(
            filter.BranchId,
            filter.Year,
            filter.Month,
            filter.TypeCode,
            filter.Status,
            filter.FromDate,
            filter.ToDate,
            filter.SearchKeyword,
            filter.PageIndex,
            filter.PageSize,
            cancellationToken);

        var voucherTypes = await _voucherRepository.GetVoucherTypesAsync(cancellationToken);
        var typeDict = voucherTypes.ToDictionary(t => t.TypeCode);

        var dtoItems = items.Select(item =>
        {
            typeDict.TryGetValue(item.VoucherType, out var vType);
            return GlVoucherMapper.ToDto(item, vType);
        }).ToList();

        return new PagedResultDto<GlVoucherHeaderDto>
        {
            Items = dtoItems,
            TotalCount = totalCount,
            PageIndex = filter.PageIndex,
            PageSize = filter.PageSize
        };
    }

    public async Task<GlVoucherHeaderDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _voucherRepository.GetByIdAsync(id, cancellationToken);
        if (entity == null) return null;

        var voucherType = await _voucherRepository.GetVoucherTypeByCodeAsync(entity.VoucherType, cancellationToken);
        return GlVoucherMapper.ToDto(entity, voucherType);
    }

    public async Task<GlVoucherHeaderDto> CreateVoucherAsync(
        CreateGlVoucherDto dto,
        string username,
        CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.GetRequiredCompanyId();

        if (dto.Details == null || dto.Details.Count < 2)
        {
            throw new AccountingException("يجب أن يحتوي القيد المحاسبي على سطرين على الأقل (مدين ودائن).", "GL_VOUCHER_MIN_LINES");
        }

        var voucherType = await _voucherRepository.GetVoucherTypeByCodeAsync(dto.VoucherType, cancellationToken);
        if (voucherType == null || !voucherType.IsActive)
        {
            throw new AccountingException($"نوع القيد المحاسبي ({dto.VoucherType}) غير معرف أو غير نشط.", "GL_VOUCHER_TYPE_INVALID");
        }

        if (!voucherType.AllowManualEntry)
        {
            throw new AccountingException($"نوع القيد ({voucherType.NameLocal}) آلي ومخصص لعمليات النظام فقط.", "GL_VOUCHER_MANUAL_DISALLOWED");
        }

        decimal totalDebit = 0;
        decimal totalCredit = 0;

        foreach (var detail in dto.Details)
        {
            if (string.IsNullOrWhiteSpace(detail.AccountCode))
            {
                throw new AccountingException("رقم الحساب مطلوب لجميع أسطر القيد.", "GL_VOUCHER_LINE_ACCOUNT_REQUIRED");
            }

            var account = await _accountRepository.GetByCodeAsync(companyId, detail.AccountCode, cancellationToken);
            if (account == null || !account.IsActive)
            {
                throw new AccountingException($"الحساب المحاسبي ({detail.AccountCode}) غير موجود أو غير نشط.", "GL_VOUCHER_ACCOUNT_INVALID");
            }

            if (!account.IsPostable || account.AccountType == "HEADER")
            {
                throw new AccountingException($"لا يمكن الترحيل على الحساب الرئيسي ({detail.AccountCode} - {account.AccountNameLocal}). يجب الترحيل على حساب فرعي قابل للترحيل.", "GL_VOUCHER_ACCOUNT_HEADER_NOT_POSTABLE");
            }

            if (!string.IsNullOrWhiteSpace(detail.CostCenterCode))
            {
                var costCenter = await _costCenterRepository.GetByCodeAsync(detail.CostCenterCode, cancellationToken);
                if (costCenter == null || !costCenter.IsActive)
                {
                    throw new AccountingException($"مركز التكلفة ({detail.CostCenterCode}) غير موجود أو غير نشط.", "GL_VOUCHER_COST_CENTER_INVALID");
                }

                if (!costCenter.IsPostable || costCenter.CostCenterType == "HEADER")
                {
                    throw new AccountingException($"لا يمكن الترحيل على مركز التكلفة الرئيسي ({detail.CostCenterCode} - {costCenter.NameLocal}). يجب الترحيل على مركز تكلفة فرعي قابل للترحيل.", "GL_VOUCHER_COST_CENTER_NOT_POSTABLE");
                }
            }

            // --- Control Account Validation: require partyType + partyCode ---
            if (account.IsControlAccount)
            {
                if (string.IsNullOrWhiteSpace(detail.PartyType) || string.IsNullOrWhiteSpace(detail.PartyCode))
                {
                    var expectedParty = account.ControlAccountType switch
                    {
                        "AR" => "CUSTOMER",
                        "AP" => "VENDOR",
                        _ => "الطرف المعني"
                    };
                    throw new AccountingException(
                        $"الحساب ({detail.AccountCode} - {account.AccountNameLocal}) هو حساب ضابط ({account.ControlAccountType}). يجب تحديد نوع الطرف (partyType) وكود الطرف (partyCode) — مثلاً partyType='{expectedParty}'.",
                        "GL_VOUCHER_CONTROL_ACCOUNT_PARTY_REQUIRED");
                }

                // Validate partyType matches control account type
                var validPartyType = account.ControlAccountType switch
                {
                    "AR" => detail.PartyType!.Equals("CUSTOMER", StringComparison.OrdinalIgnoreCase),
                    "AP" => detail.PartyType!.Equals("VENDOR", StringComparison.OrdinalIgnoreCase),
                    _ => true // Other control types accept any partyType
                };

                if (!validPartyType)
                {
                    var expected = account.ControlAccountType == "AR" ? "CUSTOMER" : "VENDOR";
                    throw new AccountingException(
                        $"الحساب ({detail.AccountCode}) من نوع ({account.ControlAccountType}) يتطلب partyType='{expected}' وليس '{detail.PartyType}'.",
                        "GL_VOUCHER_CONTROL_ACCOUNT_PARTY_TYPE_MISMATCH");
                }
            }

            totalDebit += detail.Debit * detail.ExchangeRate;
            totalCredit += detail.Credit * detail.ExchangeRate;
        }

        if (Math.Abs(totalDebit - totalCredit) > 0.001m)
        {
            throw new AccountingException($"القيد المحاسبي غير متوازن! إجمالي المدين ({totalDebit:N3}) لا يساوي إجمالي الدائن ({totalCredit:N3}). الفرق: {Math.Abs(totalDebit - totalCredit):N3}", "GL_UNBALANCED_VOUCHER");
        }

        var voucherDate = dto.VoucherDate == default ? DateTime.UtcNow : dto.VoucherDate;
        var year = voucherDate.Year;
        var month = voucherDate.Month;

        var period = await _periodRepository.GetPeriodByDateAsync(dto.FiscalYearId, voucherDate, cancellationToken);
        if (period != null)
        {
            if (period.Status == "HARD_CLOSE")
            {
                throw new AccountingException($"لا يمكن إنشاء السند بتاريخ ({voucherDate:yyyy-MM-dd}) لأن الفترة المالية ({period.PeriodNameLocal}) مقفلة نهائياً (HARD_CLOSE).", "GL_FISCAL_PERIOD_HARD_CLOSED");
            }
            if (period.Status == "SOFT_CLOSE")
            {
                throw new AccountingException($"الفترة المالية ({period.PeriodNameLocal}) مقفلة جزئياً (SOFT_CLOSE). يتطلب الترحيل فيها صلاحيات مشرف وتبرير معتمد.", "GL_FISCAL_PERIOD_SOFT_CLOSED");
            }
        }

        var nextNo = await _voucherRepository.GenerateNextSerialNoAsync(
            dto.BranchId,
            year,
            month,
            dto.VoucherType,
            voucherType.SerialResetPolicy,
            cancellationToken);

        var header = new GlVoucherHeader
        {
            BranchId = dto.BranchId,
            FiscalYearId = dto.FiscalYearId,
            VoucherYear = year,
            VoucherMonth = month,
            VoucherType = dto.VoucherType,
            VoucherNo = nextNo,
            VoucherDate = voucherDate,
            Description = dto.Description,
            TotalAmount = totalDebit,
            TotalLocalDebit = totalDebit,
            TotalLocalCredit = totalCredit,
            Status = 1, // Draft
            IsStandby = dto.IsStandby,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        int lineSer = 1;
        foreach (var d in dto.Details)
        {
            var localDebit = d.Debit * d.ExchangeRate;
            var localCredit = d.Credit * d.ExchangeRate;

            header.Details.Add(new GlVoucherDetail
            {
                LineSer = lineSer++,
                AccountCode = d.AccountCode,
                Debit = d.Debit,
                Credit = d.Credit,
                LocalDebit = localDebit,
                LocalCredit = localCredit,
                BaseDebit = localDebit,
                BaseCredit = localCredit,
                Description = d.Description ?? dto.Description,
                CurrencyId = d.CurrencyId,
                ExchangeRate = d.ExchangeRate,
                CostCenterCode = d.CostCenterCode,
                CostCenterMgrCode = d.CostCenterMgrCode,
                CostCenterMnrCode = d.CostCenterMnrCode,
                PartyType = d.PartyType?.ToUpperInvariant(),
                PartyCode = d.PartyCode,
                BranchId = d.BranchId ?? dto.BranchId
            });
        }

        await _voucherRepository.AddVoucherAsync(header, cancellationToken);
        await _voucherRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Voucher {VoucherNo} created successfully by user {User}", header.VoucherNo, username);

        var reloaded = await _voucherRepository.GetByIdAsync(header.Id, cancellationToken);
        return GlVoucherMapper.ToDto(reloaded ?? header, voucherType);
    }

    public async Task<GlVoucherHeaderDto> ReviewVoucherAsync(
        long id,
        string username,
        CancellationToken cancellationToken = default)
    {
        var voucher = await _voucherRepository.GetByIdAsync(id, cancellationToken);
        if (voucher == null)
        {
            throw new AccountingNotFoundException($"السند رقم ({id}) غير موجود.", "GL_VOUCHER_NOT_FOUND");
        }

        if (voucher.Status != 1)
        {
            throw new AccountingException("يمكن مراجعة السندات التي بحالة (مسودة) فقط.", "GL_VOUCHER_CANNOT_REVIEW");
        }

        voucher.Status = 2; // Reviewed
        voucher.IsReviewed = true;
        voucher.ReviewUser = username;
        voucher.ReviewDate = DateTime.UtcNow;
        voucher.UpdateUser = username;
        voucher.UpdateDate = DateTime.UtcNow;

        await _voucherRepository.SaveChangesAsync(cancellationToken);

        var voucherType = await _voucherRepository.GetVoucherTypeByCodeAsync(voucher.VoucherType, cancellationToken);
        return GlVoucherMapper.ToDto(voucher, voucherType);
    }

    public async Task<GlVoucherHeaderDto> PostVoucherAsync(
        long id,
        string username,
        CancellationToken cancellationToken = default)
    {
        var voucher = await _voucherRepository.GetByIdAsync(id, cancellationToken);
        if (voucher == null)
        {
            throw new AccountingNotFoundException($"السند رقم ({id}) غير موجود.", "GL_VOUCHER_NOT_FOUND");
        }

        if (voucher.Status == 3)
        {
            throw new AccountingException("السند مُمرحّل بالفعل.", "GL_VOUCHER_ALREADY_POSTED");
        }

        if (voucher.Status == 4)
        {
            throw new AccountingException("لا يمكن ترحيل سند معكوس.", "GL_VOUCHER_CANNOT_POST_REVERSED");
        }

        var postPeriod = await _periodRepository.GetPeriodByDateAsync(voucher.FiscalYearId, voucher.VoucherDate, cancellationToken);
        if (postPeriod != null)
        {
            if (postPeriod.Status == "HARD_CLOSE")
            {
                throw new AccountingException($"لا يمكن ترحيل السند رقم ({voucher.VoucherNo}) لأن الفترة المالية ({postPeriod.PeriodNameLocal}) مقفلة نهائياً (HARD_CLOSE).", "GL_FISCAL_PERIOD_HARD_CLOSED");
            }
            if (postPeriod.Status == "SOFT_CLOSE")
            {
                throw new AccountingException($"الفترة المالية ({postPeriod.PeriodNameLocal}) مقفلة جزئياً (SOFT_CLOSE). يتطلب الترحيل فيها صلاحيات مشرف وتبرير معتمد.", "GL_FISCAL_PERIOD_SOFT_CLOSED");
            }
        }

        voucher.Status = 3; // Posted
        voucher.PostUser = username;
        voucher.PostDate = DateTime.UtcNow;
        voucher.UpdateUser = username;
        voucher.UpdateDate = DateTime.UtcNow;

        if (postPeriod != null)
        {
            foreach (var detail in voucher.Details)
            {
                var lineBranchId = detail.BranchId ?? voucher.BranchId;
                await _balanceRepository.UpdateBalanceFromVoucherAsync(
                    voucher.FiscalYearId,
                    postPeriod.Id,
                    lineBranchId,
                    detail.AccountCode,
                    detail.CurrencyId,
                    detail.Debit,
                    detail.Credit,
                    detail.LocalDebit,
                    detail.LocalCredit,
                    isAddition: true,
                    username: username,
                    cancellationToken: cancellationToken);
            }
        }

        // Subledger Integration: Generate AR / AP entries
        foreach (var detail in voucher.Details)
        {
            if (string.IsNullOrWhiteSpace(detail.PartyCode)) continue;

            if (string.Equals(detail.PartyType, "CUSTOMER", StringComparison.OrdinalIgnoreCase))
            {
                var netAmount = detail.Debit - detail.Credit;
                var localAmount = detail.LocalDebit - detail.LocalCredit;
                var txType = netAmount >= 0 ? "INVOICE" : "RECEIPT";

                await _arSubledgerRepository.AddTransactionAsync(new ArSubledgerTransaction
                {
                    CustomerCode = detail.PartyCode,
                    JournalLineId = detail.Id,
                    VoucherId = voucher.Id,
                    TransactionType = txType,
                    TransactionDate = voucher.VoucherDate,
                    DueDate = voucher.VoucherDate.AddDays(30),
                    Amount = netAmount,
                    CurrencyId = detail.CurrencyId,
                    ExchangeRate = detail.ExchangeRate,
                    LocalAmount = localAmount,
                    OpenAmount = netAmount,
                    LocalOpenAmount = localAmount,
                    ReferenceNo = voucher.SourceRefId?.ToString() ?? voucher.VoucherNo.ToString(),
                    Description = detail.Description,
                    CreationUser = username,
                    CreationDate = DateTime.UtcNow
                }, cancellationToken);
            }
            else if (string.Equals(detail.PartyType, "VENDOR", StringComparison.OrdinalIgnoreCase))
            {
                var netAmount = detail.Credit - detail.Debit;
                var localAmount = detail.LocalCredit - detail.LocalDebit;
                var txType = netAmount >= 0 ? "BILL" : "PAYMENT";

                await _apSubledgerRepository.AddTransactionAsync(new ApSubledgerTransaction
                {
                    VendorCode = detail.PartyCode,
                    JournalLineId = detail.Id,
                    VoucherId = voucher.Id,
                    TransactionType = txType,
                    TransactionDate = voucher.VoucherDate,
                    DueDate = voucher.VoucherDate.AddDays(30),
                    Amount = netAmount,
                    CurrencyId = detail.CurrencyId,
                    ExchangeRate = detail.ExchangeRate,
                    LocalAmount = localAmount,
                    OpenAmount = netAmount,
                    LocalOpenAmount = localAmount,
                    ReferenceNo = voucher.SourceRefId?.ToString() ?? voucher.VoucherNo.ToString(),
                    Description = detail.Description,
                    CreationUser = username,
                    CreationDate = DateTime.UtcNow
                }, cancellationToken);
            }
        }

        await _voucherRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Voucher {VoucherNo} posted by {User}", voucher.VoucherNo, username);

        var voucherType = await _voucherRepository.GetVoucherTypeByCodeAsync(voucher.VoucherType, cancellationToken);
        return GlVoucherMapper.ToDto(voucher, voucherType);
    }

    public async Task<GlVoucherHeaderDto> ReverseVoucherAsync(
        long id,
        string username,
        string? reversalReason,
        CancellationToken cancellationToken = default)
    {
        var originalVoucher = await _voucherRepository.GetByIdAsync(id, cancellationToken);
        if (originalVoucher == null)
        {
            throw new AccountingNotFoundException($"السند رقم ({id}) غير موجود.", "GL_VOUCHER_NOT_FOUND");
        }

        if (originalVoucher.Status != 3)
        {
            throw new AccountingException("يمكن عكس السندات المرحّلة فقط.", "GL_VOUCHER_CANNOT_REVERSE_UNPOSTED");
        }

        if (originalVoucher.IsReversed)
        {
            throw new AccountingException("تم عكس هذا السند سابقاً.", "GL_VOUCHER_ALREADY_REVERSED");
        }

        originalVoucher.IsReversed = true;
        originalVoucher.ReverseUser = username;
        originalVoucher.ReverseDate = DateTime.UtcNow;
        originalVoucher.UpdateUser = username;
        originalVoucher.UpdateDate = DateTime.UtcNow;

        var voucherType = await _voucherRepository.GetVoucherTypeByCodeAsync(originalVoucher.VoucherType, cancellationToken);

        var now = DateTime.UtcNow;
        var nextNo = await _voucherRepository.GenerateNextSerialNoAsync(
            originalVoucher.BranchId,
            now.Year,
            now.Month,
            originalVoucher.VoucherType,
            voucherType?.SerialResetPolicy ?? "MONTHLY",
            cancellationToken);

        var reversalHeader = new GlVoucherHeader
        {
            BranchId = originalVoucher.BranchId,
            FiscalYearId = originalVoucher.FiscalYearId,
            VoucherYear = now.Year,
            VoucherMonth = now.Month,
            VoucherType = originalVoucher.VoucherType,
            VoucherNo = nextNo,
            VoucherDate = now,
            Description = $"قيد عكسي للقيد رقم ({originalVoucher.VoucherNo}). سبب العكس: {reversalReason ?? "لا يوجد"}",
            TotalAmount = originalVoucher.TotalAmount,
            TotalLocalDebit = originalVoucher.TotalLocalCredit,
            TotalLocalCredit = originalVoucher.TotalLocalDebit,
            Status = 3, // Immediately posted reversal
            IsAutoRecord = true,
            SourceSystemCode = "GL_REVERSAL",
            SourceRefId = originalVoucher.Id,
            PostUser = username,
            PostDate = now,
            CreationUser = username,
            CreationDate = now
        };

        int lineSer = 1;
        foreach (var detail in originalVoucher.Details)
        {
            reversalHeader.Details.Add(new GlVoucherDetail
            {
                LineSer = lineSer++,
                AccountCode = detail.AccountCode,
                Debit = detail.Credit, // SWAP DEBIT AND CREDIT
                Credit = detail.Debit,
                LocalDebit = detail.LocalCredit,
                LocalCredit = detail.LocalDebit,
                BaseDebit = detail.BaseCredit,
                BaseCredit = detail.BaseDebit,
                Description = $"عكس: {detail.Description}",
                CurrencyId = detail.CurrencyId,
                ExchangeRate = detail.ExchangeRate,
                CostCenterCode = detail.CostCenterCode,
                CostCenterMgrCode = detail.CostCenterMgrCode,
                CostCenterMnrCode = detail.CostCenterMnrCode,
                PartyType = detail.PartyType,
                PartyCode = detail.PartyCode,
                BranchId = detail.BranchId ?? originalVoucher.BranchId
            });
        }

        await _voucherRepository.AddVoucherAsync(reversalHeader, cancellationToken);
        await _voucherRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Voucher {OriginalNo} reversed by creating reversal voucher {ReversalNo}", originalVoucher.VoucherNo, reversalHeader.VoucherNo);

        var reloadedReversal = await _voucherRepository.GetByIdAsync(reversalHeader.Id, cancellationToken);
        return GlVoucherMapper.ToDto(reloadedReversal ?? reversalHeader, voucherType);
    }
}
