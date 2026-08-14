using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Mappings.Accounting;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class GlVoucherService : IGlVoucherService
{
    private readonly IGlVoucherRepository _voucherRepository;
    private readonly IGlAccountRepository _accountRepository;
    private readonly IGlCostCenterRepository _costCenterRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<GlVoucherService> _logger;

    public GlVoucherService(
        IGlVoucherRepository voucherRepository,
        IGlAccountRepository accountRepository,
        IGlCostCenterRepository costCenterRepository,
        ICurrentTenantContext tenantContext,
        ILogger<GlVoucherService> logger)
    {
        _voucherRepository = voucherRepository;
        _accountRepository = accountRepository;
        _costCenterRepository = costCenterRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GlVoucherTypeDto>> GetVoucherTypesAsync(CancellationToken cancellationToken = default)
    {
        var types = await _voucherRepository.GetVoucherTypesAsync(cancellationToken);
        return types.Select(GlVoucherMapper.ToDto).ToList();
    }

    public async Task<GlVoucherTypeDto?> GetVoucherTypeByCodeAsync(int typeCode, CancellationToken cancellationToken = default)
    {
        var type = await _voucherRepository.GetVoucherTypeByCodeAsync(typeCode, cancellationToken);
        return type == null ? null : GlVoucherMapper.ToDto(type);
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
            throw new AccountingException($"نوع القيد ({voucherType.NameAr}) آلي ومخصص لعمليات النظام فقط.", "GL_VOUCHER_MANUAL_DISALLOWED");
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
                throw new AccountingException($"لا يمكن الترحيل على الحساب الرئيسي ({detail.AccountCode} - {account.AccountNameAr}). يجب الترحيل على حساب فرعي قابل للترحيل.", "GL_VOUCHER_ACCOUNT_HEADER_NOT_POSTABLE");
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
                    throw new AccountingException($"لا يمكن الترحيل على مركز التكلفة الرئيسي ({detail.CostCenterCode} - {costCenter.NameAr}). يجب الترحيل على مركز تكلفة فرعي قابل للترحيل.", "GL_VOUCHER_COST_CENTER_NOT_POSTABLE");
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
                CostCenterMnrCode = d.CostCenterMnrCode
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

        voucher.Status = 3; // Posted
        voucher.PostUser = username;
        voucher.PostDate = DateTime.UtcNow;
        voucher.UpdateUser = username;
        voucher.UpdateDate = DateTime.UtcNow;

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
                CostCenterMnrCode = detail.CostCenterMnrCode
            });
        }

        await _voucherRepository.AddVoucherAsync(reversalHeader, cancellationToken);
        await _voucherRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Voucher {OriginalNo} reversed by creating reversal voucher {ReversalNo}", originalVoucher.VoucherNo, reversalHeader.VoucherNo);

        var reloadedReversal = await _voucherRepository.GetByIdAsync(reversalHeader.Id, cancellationToken);
        return GlVoucherMapper.ToDto(reloadedReversal ?? reversalHeader, voucherType);
    }
}
