using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Accounting.OpeningBalances;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class OpeningBalanceService : IOpeningBalanceService
{
    // OB voucher type code as defined in GL_VOUCHER_TYPE seed data
    private const int ObVoucherTypeCode = 302;

    private readonly IGlOpeningBalanceRepository _obRepository;
    private readonly IGlVoucherRepository _voucherRepository;
    private readonly IGlAccountRepository _accountRepository;
    private readonly IFiscalYearRepository _fiscalYearRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<OpeningBalanceService> _logger;

    public OpeningBalanceService(
        IGlOpeningBalanceRepository obRepository,
        IGlVoucherRepository voucherRepository,
        IGlAccountRepository accountRepository,
        IFiscalYearRepository fiscalYearRepository,
        ICurrentTenantContext tenantContext,
        ILogger<OpeningBalanceService> logger)
    {
        _obRepository = obRepository;
        _voucherRepository = voucherRepository;
        _accountRepository = accountRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CREATE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<OpeningBalanceHeaderDto> CreateAsync(
        CreateOpeningBalanceDto dto,
        string username,
        CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.GetRequiredCompanyId();

        // 1. Validate fiscal year exists and is open
        var fiscalYear = await _fiscalYearRepository.GetByIdAsync(dto.FiscalYearId)
            ?? throw new AccountingNotFoundException(
                $"السنة المالية ({dto.FiscalYearId}) غير موجودة.",
                "OB_FISCAL_YEAR_NOT_FOUND");

        if (fiscalYear.IsClosed)
            throw new AccountingException(
                "لا يمكن إدخال أرصدة افتتاحية لسنة مالية مغلقة.",
                "OB_FISCAL_YEAR_CLOSED");

        // 2. Ensure uniqueness (one OB per branch + fiscal year)
        var exists = await _obRepository.ExistsForFiscalYearAsync(
            dto.BranchId, dto.FiscalYearId, cancellationToken);

        if (exists)
            throw new AccountingException(
                "يوجد قيد أرصدة افتتاحية مسبق لهذا الفرع والسنة المالية.",
                "OB_DUPLICATE");

        // 3. Build header
        var header = new GlOpeningBalanceHeader
        {
            BranchId = dto.BranchId,
            FiscalYearId = dto.FiscalYearId,
            AsOfDate = dto.AsOfDate == default ? fiscalYear.StartDate : dto.AsOfDate,
            Description = dto.Description ?? "أرصدة أول مدة",
            Status = 1,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        // 4. Add initial lines if provided
        if (dto.Lines.Count > 0)
        {
            await ValidateAndAttachLines(header, dto.Lines, companyId, cancellationToken);
            RecalculateTotals(header);
        }

        await _obRepository.AddAsync(header, cancellationToken);
        await _obRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Opening Balance created (ID={Id}) for Branch={BranchId} FiscalYear={FiscalYearId} by {User}",
            header.Id, header.BranchId, header.FiscalYearId, username);

        var reloaded = await _obRepository.GetByIdAsync(header.Id, cancellationToken);
        return MapToDto(reloaded ?? header);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // READ
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<OpeningBalanceHeaderDto?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var header = await _obRepository.GetByIdAsync(id, cancellationToken);
        return header == null ? null : MapToDto(header);
    }

    public async Task<OpeningBalanceHeaderDto?> GetByFiscalYearAsync(
        long branchId,
        long fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var header = await _obRepository.GetByBranchAndFiscalYearAsync(
            branchId, fiscalYearId, cancellationToken);
        return header == null ? null : MapToDto(header);
    }

    public async Task<IReadOnlyList<OpeningBalanceHeaderDto>> GetAllByBranchAsync(
        long branchId,
        CancellationToken cancellationToken = default)
    {
        var headers = await _obRepository.GetAllByBranchAsync(branchId, cancellationToken);
        return headers.Select(MapToDto).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LINE MANAGEMENT
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<OpeningBalanceHeaderDto> AddLineAsync(
        long headerId,
        OpeningBalanceLineDto line,
        string username,
        CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.GetRequiredCompanyId();
        var header = await GetDraftOrThrowAsync(headerId, cancellationToken);

        await ValidateAndAttachLines(header, new[] { line }, companyId, cancellationToken);

        RecalculateTotals(header);
        header.UpdateUser = username;
        header.UpdateDate = DateTime.UtcNow;

        await _obRepository.SaveChangesAsync(cancellationToken);

        var reloaded = await _obRepository.GetByIdAsync(headerId, cancellationToken);
        return MapToDto(reloaded ?? header);
    }

    public async Task<OpeningBalanceHeaderDto> UpdateLineAsync(
        long headerId,
        long lineId,
        OpeningBalanceLineDto lineDto,
        string username,
        CancellationToken cancellationToken = default)
    {
        var header = await GetDraftOrThrowAsync(headerId, cancellationToken);
        var companyId = _tenantContext.GetRequiredCompanyId();

        var existing = header.Details.FirstOrDefault(d => d.Id == lineId)
            ?? throw new AccountingNotFoundException(
                $"السطر ({lineId}) غير موجود في قيد الأرصدة الافتتاحية.",
                "OB_LINE_NOT_FOUND");

        ValidateLine(lineDto);

        var account = await _accountRepository.GetByCodeAsync(
            companyId, lineDto.AccountCode, cancellationToken)
            ?? throw new AccountingException(
                $"الحساب ({lineDto.AccountCode}) غير موجود أو غير نشط.",
                "OB_ACCOUNT_INVALID");

        if (!account.IsPostable)
            throw new AccountingException(
                $"الحساب ({lineDto.AccountCode}) ليس حساباً قابلاً للترحيل.",
                "OB_ACCOUNT_NOT_POSTABLE");

        existing.AccountCode = lineDto.AccountCode;
        existing.DebitAmount = lineDto.DebitAmount;
        existing.CreditAmount = lineDto.CreditAmount;
        existing.LocalDebit = lineDto.DebitAmount;
        existing.LocalCredit = lineDto.CreditAmount;
        existing.Description = lineDto.Description;

        RecalculateTotals(header);
        header.UpdateUser = username;
        header.UpdateDate = DateTime.UtcNow;

        await _obRepository.SaveChangesAsync(cancellationToken);

        var reloaded = await _obRepository.GetByIdAsync(headerId, cancellationToken);
        return MapToDto(reloaded ?? header);
    }

    public async Task<OpeningBalanceHeaderDto> DeleteLineAsync(
        long headerId,
        long lineId,
        string username,
        CancellationToken cancellationToken = default)
    {
        var header = await GetDraftOrThrowAsync(headerId, cancellationToken);

        var line = header.Details.FirstOrDefault(d => d.Id == lineId)
            ?? throw new AccountingNotFoundException(
                $"السطر ({lineId}) غير موجود.",
                "OB_LINE_NOT_FOUND");

        header.Details.Remove(line);
        RecalculateTotals(header);
        header.UpdateUser = username;
        header.UpdateDate = DateTime.UtcNow;

        await _obRepository.SaveChangesAsync(cancellationToken);

        var reloaded = await _obRepository.GetByIdAsync(headerId, cancellationToken);
        return MapToDto(reloaded ?? header);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CONFIRM — auto-generates OB voucher
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<OpeningBalanceHeaderDto> ConfirmAsync(
        long headerId,
        string username,
        CancellationToken cancellationToken = default)
    {
        var header = await GetDraftOrThrowAsync(headerId, cancellationToken);

        if (header.Details.Count == 0)
            throw new AccountingException(
                "لا يمكن تأكيد قيد أرصدة افتتاحية فارغ. أضف سطراً واحداً على الأقل.",
                "OB_NO_LINES");

        // Get OB voucher type (TypeCode = 302 / OPENING)
        var voucherType = await _voucherRepository.GetVoucherTypeByCodeAsync(
            ObVoucherTypeCode, cancellationToken)
            ?? throw new AccountingException(
                "نوع القيد (أرصدة افتتاحية) غير موجود في النظام. تأكد من تهيئة بيانات النظام.",
                "OB_VOUCHER_TYPE_MISSING");

        var now = DateTime.UtcNow;

        // Generate next serial number for the OB voucher
        var serialNo = await _voucherRepository.GenerateNextSerialNoAsync(
            header.BranchId,
            header.AsOfDate.Year,
            header.AsOfDate.Month,
            ObVoucherTypeCode,
            voucherType.SerialResetPolicy,
            cancellationToken);

        // Build the GL_VOUCHER_HEADER (posted directly — Status=3)
        var obVoucher = new GlVoucherHeader
        {
            BranchId = header.BranchId,
            FiscalYearId = header.FiscalYearId,
            VoucherYear = header.AsOfDate.Year,
            VoucherMonth = header.AsOfDate.Month,
            VoucherType = ObVoucherTypeCode,
            VoucherNo = serialNo,
            VoucherDate = header.AsOfDate,
            Description = header.Description ?? "أرصدة أول مدة",
            TotalAmount = header.TotalDebit,
            TotalLocalDebit = header.TotalDebit,
            TotalLocalCredit = header.TotalCredit,
            Status = 3,          // Posted directly
            IsAutoRecord = true,
            SourceSystemCode = "GL_OPENING_BALANCE",
            SourceRefId = header.Id,
            PostUser = username,
            PostDate = now,
            CreationUser = username,
            CreationDate = now
        };

        // Build GL_VOUCHER_DETAIL lines from OB detail lines
        int lineSer = 1;
        foreach (var detail in header.Details.OrderBy(d => d.LineSer))
        {
            obVoucher.Details.Add(new GlVoucherDetail
            {
                LineSer = lineSer++,
                AccountCode = detail.AccountCode,
                Debit = detail.DebitAmount,
                Credit = detail.CreditAmount,
                LocalDebit = detail.LocalDebit,
                LocalCredit = detail.LocalCredit,
                BaseDebit = detail.LocalDebit,
                BaseCredit = detail.LocalCredit,
                Description = detail.Description ?? header.Description,
                CurrencyId = detail.CurrencyId,
                ExchangeRate = detail.ExchangeRate
            });
        }

        // Save the voucher
        await _voucherRepository.AddVoucherAsync(obVoucher, cancellationToken);
        await _voucherRepository.SaveChangesAsync(cancellationToken);

        // Lock the OB header and link it to the generated voucher
        header.Status = 2;  // Confirmed
        header.ObVoucherId = obVoucher.Id;
        header.UpdateUser = username;
        header.UpdateDate = now;

        await _obRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Opening Balance (ID={ObId}) confirmed by {User}. Generated voucher ID={VoucherId}, No={VoucherNo}",
            header.Id, username, obVoucher.Id, obVoucher.VoucherNo);

        var reloaded = await _obRepository.GetByIdAsync(headerId, cancellationToken);
        return MapToDto(reloaded ?? header);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<GlOpeningBalanceHeader> GetDraftOrThrowAsync(
        long headerId,
        CancellationToken cancellationToken)
    {
        var header = await _obRepository.GetByIdAsync(headerId, cancellationToken)
            ?? throw new AccountingNotFoundException(
                $"قيد الأرصدة الافتتاحية ({headerId}) غير موجود.",
                "OB_NOT_FOUND");

        if (header.IsConfirmed)
            throw new AccountingException(
                "لا يمكن تعديل قيد أرصدة افتتاحية مؤكد.",
                "OB_ALREADY_CONFIRMED");

        return header;
    }

    private static void ValidateLine(OpeningBalanceLineDto line)
    {
        if (string.IsNullOrWhiteSpace(line.AccountCode))
            throw new AccountingException(
                "رقم الحساب مطلوب.",
                "OB_LINE_ACCOUNT_REQUIRED");

        if (line.DebitAmount < 0 || line.CreditAmount < 0)
            throw new AccountingException(
                "لا يمكن أن تكون قيم الأرصدة الافتتاحية سالبة.",
                "OB_LINE_NEGATIVE_AMOUNT");

        if (line.DebitAmount > 0 && line.CreditAmount > 0)
            throw new AccountingException(
                $"الحساب ({line.AccountCode}): لا يمكن أن يحتوي السطر على مدين ودائن في نفس الوقت.",
                "OB_LINE_BOTH_SIDES");

        if (line.DebitAmount == 0 && line.CreditAmount == 0)
            throw new AccountingException(
                $"الحساب ({line.AccountCode}): يجب أن يكون للسطر قيمة مدين أو دائن.",
                "OB_LINE_ZERO_AMOUNT");
    }

    private async Task ValidateAndAttachLines(
        GlOpeningBalanceHeader header,
        IEnumerable<OpeningBalanceLineDto> lines,
        long companyId,
        CancellationToken cancellationToken)
    {
        int nextSer = header.Details.Count > 0
            ? header.Details.Max(d => d.LineSer) + 1
            : 1;

        foreach (var line in lines)
        {
            ValidateLine(line);

            var account = await _accountRepository.GetByCodeAsync(
                companyId, line.AccountCode, cancellationToken)
                ?? throw new AccountingException(
                    $"الحساب ({line.AccountCode}) غير موجود أو غير نشط.",
                    "OB_ACCOUNT_INVALID");

            if (!account.IsPostable)
                throw new AccountingException(
                    $"الحساب ({line.AccountCode} - {account.AccountNameAr}) ليس حساباً قابلاً للترحيل (Header account).",
                    "OB_ACCOUNT_NOT_POSTABLE");

            header.Details.Add(new GlOpeningBalanceDetail
            {
                LineSer = nextSer++,
                AccountCode = line.AccountCode,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                LocalDebit = line.DebitAmount,
                LocalCredit = line.CreditAmount,
                CurrencyId = 1,
                ExchangeRate = 1.0m,
                Description = line.Description
            });
        }
    }

    private static void RecalculateTotals(GlOpeningBalanceHeader header)
    {
        header.TotalDebit = header.Details.Sum(d => d.DebitAmount);
        header.TotalCredit = header.Details.Sum(d => d.CreditAmount);
    }

    private static OpeningBalanceHeaderDto MapToDto(GlOpeningBalanceHeader header)
    {
        return new OpeningBalanceHeaderDto
        {
            Id = header.Id,
            BranchId = header.BranchId,
            FiscalYearId = header.FiscalYearId,
            AsOfDate = header.AsOfDate,
            Description = header.Description,
            Status = header.Status,
            TotalDebit = header.TotalDebit,
            TotalCredit = header.TotalCredit,
            ObVoucherId = header.ObVoucherId,
            CreationUser = header.CreationUser,
            CreationDate = header.CreationDate,
            UpdateUser = header.UpdateUser,
            UpdateDate = header.UpdateDate,
            Details = header.Details.Select(d => new OpeningBalanceDetailDto
            {
                Id = d.Id,
                HeaderId = d.HeaderId,
                LineSer = d.LineSer,
                AccountCode = d.AccountCode,
                AccountNameAr = d.Account?.AccountNameAr ?? string.Empty,
                AccountNameEn = d.Account?.AccountNameEn ?? string.Empty,
                DebitAmount = d.DebitAmount,
                CreditAmount = d.CreditAmount,
                LocalDebit = d.LocalDebit,
                LocalCredit = d.LocalCredit,
                Description = d.Description
            }).ToList()
        };
    }
}
