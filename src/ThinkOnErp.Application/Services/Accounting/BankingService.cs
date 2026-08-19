using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Accounting.Banking;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class BankingService : IBankingService
{
    private readonly IBankingRepository _bankingRepository;
    private readonly IGlAccountRepository _accountRepository;
    private readonly IGlVoucherRepository _voucherRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<BankingService> _logger;

    public BankingService(
        IBankingRepository bankingRepository,
        IGlAccountRepository accountRepository,
        IGlVoucherRepository voucherRepository,
        ICurrentTenantContext tenantContext,
        ILogger<BankingService> logger)
    {
        _bankingRepository = bankingRepository;
        _accountRepository = accountRepository;
        _voucherRepository = voucherRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BankAccountDto>> GetBankAccountsAsync(long? branchId, bool? activeOnly, CancellationToken cancellationToken = default)
    {
        var list = await _bankingRepository.GetBankAccountsAsync(branchId, activeOnly, cancellationToken);
        return list.Select(MapToBankAccountDto).ToList();
    }

    public async Task<BankAccountDto> GetBankAccountByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var acc = await _bankingRepository.GetBankAccountByIdAsync(id, cancellationToken);
        if (acc == null)
        {
            throw new AccountingNotFoundException($"الحساب البنكي رقم ({id}) غير موجود.", "BANK_ACCOUNT_NOT_FOUND");
        }
        return MapToBankAccountDto(acc);
    }

    public async Task<BankAccountDto> CreateBankAccountAsync(CreateBankAccountDto dto, string username, CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.GetRequiredCompanyId();
        var glAcc = await _accountRepository.GetByCodeAsync(companyId, dto.GlAccountCode, cancellationToken);
        if (glAcc == null)
        {
            throw new AccountingNotFoundException($"حساب الأستاذ العام ({dto.GlAccountCode}) غير موجود في الدليل.", "GL_ACCOUNT_NOT_FOUND");
        }

        var existing = await _bankingRepository.GetBankAccountByNumberAsync(dto.AccountNumber, cancellationToken);
        if (existing != null)
        {
            throw new AccountingException($"رقم الحساب البنكي ({dto.AccountNumber}) مسجل مسبقاً.", "DUPLICATE_BANK_ACCOUNT");
        }

        var account = new BankAccount
        {
            BranchId = dto.BranchId,
            AccountNumber = dto.AccountNumber,
            AccountNameAr = dto.AccountNameAr,
            AccountNameEn = dto.AccountNameEn,
            BankName = dto.BankName,
            BankBranchName = dto.BankBranchName,
            Iban = dto.Iban,
            SwiftCode = dto.SwiftCode,
            CurrencyId = dto.CurrencyId,
            GlAccountCode = dto.GlAccountCode,
            OverdraftLimit = dto.OverdraftLimit,
            OpeningBalance = dto.OpeningBalance,
            CurrentBalance = dto.OpeningBalance,
            IsActive = dto.IsActive,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        await _bankingRepository.AddBankAccountAsync(account, cancellationToken);
        await _bankingRepository.SaveChangesAsync(cancellationToken);

        return await GetBankAccountByIdAsync(account.Id, cancellationToken);
    }

    public async Task<BankAccountDto> UpdateBankAccountAsync(long id, UpdateBankAccountDto dto, string username, CancellationToken cancellationToken = default)
    {
        var account = await _bankingRepository.GetBankAccountByIdAsync(id, cancellationToken);
        if (account == null)
        {
            throw new AccountingNotFoundException($"الحساب البنكي رقم ({id}) غير موجود.", "BANK_ACCOUNT_NOT_FOUND");
        }

        var companyId = _tenantContext.GetRequiredCompanyId();
        var glAcc = await _accountRepository.GetByCodeAsync(companyId, dto.GlAccountCode, cancellationToken);
        if (glAcc == null)
        {
            throw new AccountingNotFoundException($"حساب الأستاذ العام ({dto.GlAccountCode}) غير موجود في الدليل.", "GL_ACCOUNT_NOT_FOUND");
        }

        account.AccountNameAr = dto.AccountNameAr;
        account.AccountNameEn = dto.AccountNameEn;
        account.BankName = dto.BankName;
        account.BankBranchName = dto.BankBranchName;
        account.Iban = dto.Iban;
        account.SwiftCode = dto.SwiftCode;
        account.GlAccountCode = dto.GlAccountCode;
        account.OverdraftLimit = dto.OverdraftLimit;
        account.IsActive = dto.IsActive;
        account.UpdateUser = username;
        account.UpdateDate = DateTime.UtcNow;

        await _bankingRepository.SaveChangesAsync(cancellationToken);
        return await GetBankAccountByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<CashRegisterDto>> GetCashRegistersAsync(long? branchId, bool? activeOnly, CancellationToken cancellationToken = default)
    {
        var list = await _bankingRepository.GetCashRegistersAsync(branchId, activeOnly, cancellationToken);
        return list.Select(MapToCashRegisterDto).ToList();
    }

    public async Task<CashRegisterDto> GetCashRegisterByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var reg = await _bankingRepository.GetCashRegisterByIdAsync(id, cancellationToken);
        if (reg == null)
        {
            throw new AccountingNotFoundException($"الصندوق/الخزينة رقم ({id}) غير موجودة.", "CASH_REGISTER_NOT_FOUND");
        }
        return MapToCashRegisterDto(reg);
    }

    public async Task<CashRegisterDto> CreateCashRegisterAsync(CreateCashRegisterDto dto, string username, CancellationToken cancellationToken = default)
    {
        var companyId = _tenantContext.GetRequiredCompanyId();
        var glAcc = await _accountRepository.GetByCodeAsync(companyId, dto.GlAccountCode, cancellationToken);
        if (glAcc == null)
        {
            throw new AccountingNotFoundException($"حساب الأستاذ العام ({dto.GlAccountCode}) غير موجود في الدليل.", "GL_ACCOUNT_NOT_FOUND");
        }

        var existing = await _bankingRepository.GetCashRegisterByCodeAsync(dto.Code, cancellationToken);
        if (existing != null)
        {
            throw new AccountingException($"رمز الخزينة ({dto.Code}) مسجل مسبقاً.", "DUPLICATE_CASH_REGISTER_CODE");
        }

        var reg = new CashRegister
        {
            BranchId = dto.BranchId,
            Code = dto.Code,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            RegisterType = dto.RegisterType,
            CustodianName = dto.CustodianName,
            GlAccountCode = dto.GlAccountCode,
            CurrencyId = dto.CurrencyId,
            MinLimit = dto.MinLimit,
            MaxLimit = dto.MaxLimit,
            OpeningBalance = dto.OpeningBalance,
            CurrentBalance = dto.OpeningBalance,
            IsActive = dto.IsActive,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        await _bankingRepository.AddCashRegisterAsync(reg, cancellationToken);
        await _bankingRepository.SaveChangesAsync(cancellationToken);

        return await GetCashRegisterByIdAsync(reg.Id, cancellationToken);
    }

    public async Task<CashRegisterDto> UpdateCashRegisterAsync(long id, UpdateCashRegisterDto dto, string username, CancellationToken cancellationToken = default)
    {
        var reg = await _bankingRepository.GetCashRegisterByIdAsync(id, cancellationToken);
        if (reg == null)
        {
            throw new AccountingNotFoundException($"الصندوق/الخزينة رقم ({id}) غير موجودة.", "CASH_REGISTER_NOT_FOUND");
        }

        var companyId = _tenantContext.GetRequiredCompanyId();
        var glAcc = await _accountRepository.GetByCodeAsync(companyId, dto.GlAccountCode, cancellationToken);
        if (glAcc == null)
        {
            throw new AccountingNotFoundException($"حساب الأستاذ العام ({dto.GlAccountCode}) غير موجود في الدليل.", "GL_ACCOUNT_NOT_FOUND");
        }

        reg.NameAr = dto.NameAr;
        reg.NameEn = dto.NameEn;
        reg.RegisterType = dto.RegisterType;
        reg.CustodianName = dto.CustodianName;
        reg.GlAccountCode = dto.GlAccountCode;
        reg.MinLimit = dto.MinLimit;
        reg.MaxLimit = dto.MaxLimit;
        reg.IsActive = dto.IsActive;
        reg.UpdateUser = username;
        reg.UpdateDate = DateTime.UtcNow;

        await _bankingRepository.SaveChangesAsync(cancellationToken);
        return await GetCashRegisterByIdAsync(id, cancellationToken);
    }

    public async Task<BankReconciliationDto> CreateReconciliationAsync(CreateBankReconciliationDto dto, string username, CancellationToken cancellationToken = default)
    {
        var bankAcc = await _bankingRepository.GetBankAccountByIdAsync(dto.BankAccountId, cancellationToken);
        if (bankAcc == null)
        {
            throw new AccountingNotFoundException($"الحساب البنكي رقم ({dto.BankAccountId}) غير موجود.", "BANK_ACCOUNT_NOT_FOUND");
        }

        // Calculate book ending balance as of StatementDate
        var (vouchers, _) = await _voucherRepository.GetPagedVouchersAsync(
            branchId: bankAcc.BranchId,
            year: null,
            month: null,
            typeCode: null,
            status: 3, // Posted
            fromDate: null,
            toDate: dto.StatementDate,
            searchKeyword: null,
            pageIndex: 1,
            pageSize: 10000,
            cancellationToken: cancellationToken);

        var bankLines = vouchers.SelectMany(v => v.Details).Where(d => d.AccountCode == bankAcc.GlAccountCode).ToList();
        var bookBalance = bankAcc.OpeningBalance + bankLines.Sum(d => d.LocalDebit - d.LocalCredit);

        var reconciliation = new BankReconciliation
        {
            BankAccountId = dto.BankAccountId,
            FiscalYearId = dto.FiscalYearId,
            FiscalPeriodId = dto.FiscalPeriodId,
            StatementDate = dto.StatementDate,
            StatementEndingBalance = dto.StatementEndingBalance,
            BookEndingBalance = bookBalance,
            TotalReconciledAmount = 0,
            UnreconciledDifference = dto.StatementEndingBalance - bookBalance,
            Status = "DRAFT",
            Notes = dto.Notes,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        await _bankingRepository.AddReconciliationAsync(reconciliation, cancellationToken);
        await _bankingRepository.SaveChangesAsync(cancellationToken);

        if (dto.StatementLines != null && dto.StatementLines.Count > 0)
        {
            var lines = dto.StatementLines.Select(l => new BankStatementLine
            {
                ReconciliationId = reconciliation.Id,
                TransactionDate = l.TransactionDate,
                ValueDate = l.ValueDate,
                ReferenceNo = l.ReferenceNo,
                ChequeNo = l.ChequeNo,
                Description = l.Description,
                Debit = l.Debit,
                Credit = l.Credit,
                Balance = l.Balance,
                IsReconciled = false
            }).ToList();

            await _bankingRepository.AddStatementLinesAsync(lines, cancellationToken);
            await _bankingRepository.SaveChangesAsync(cancellationToken);
        }

        return await GetReconciliationByIdAsync(reconciliation.Id, cancellationToken);
    }

    public async Task<BankReconciliationDto> GetReconciliationByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var rec = await _bankingRepository.GetReconciliationByIdAsync(id, cancellationToken);
        if (rec == null)
        {
            throw new AccountingNotFoundException($"تسوية البنك رقم ({id}) غير موجودة.", "RECONCILIATION_NOT_FOUND");
        }

        return new BankReconciliationDto
        {
            Id = rec.Id,
            BankAccountId = rec.BankAccountId,
            BankAccountNameAr = rec.BankAccount?.AccountNameAr,
            BankName = rec.BankAccount?.BankName,
            FiscalYearId = rec.FiscalYearId,
            FiscalPeriodId = rec.FiscalPeriodId,
            StatementDate = rec.StatementDate,
            StatementEndingBalance = rec.StatementEndingBalance,
            BookEndingBalance = rec.BookEndingBalance,
            TotalReconciledAmount = rec.TotalReconciledAmount,
            UnreconciledDifference = rec.UnreconciledDifference,
            Status = rec.Status,
            Notes = rec.Notes,
            Lines = rec.StatementLines.Select(l => new BankStatementLineDto
            {
                Id = l.Id,
                ReconciliationId = l.ReconciliationId,
                TransactionDate = l.TransactionDate,
                ValueDate = l.ValueDate,
                ReferenceNo = l.ReferenceNo,
                ChequeNo = l.ChequeNo,
                Description = l.Description,
                Debit = l.Debit,
                Credit = l.Credit,
                Balance = l.Balance,
                IsReconciled = l.IsReconciled,
                ReconciledDate = l.ReconciledDate,
                MatchedVoucherDetailId = l.MatchedVoucherDetailId
            }).ToList()
        };
    }

    public async Task<IReadOnlyList<BankReconciliationDto>> GetReconciliationsAsync(long? bankAccountId, long? fiscalYearId, CancellationToken cancellationToken = default)
    {
        var list = await _bankingRepository.GetReconciliationsAsync(bankAccountId, fiscalYearId, cancellationToken);
        return list.Select(rec => new BankReconciliationDto
        {
            Id = rec.Id,
            BankAccountId = rec.BankAccountId,
            BankAccountNameAr = rec.BankAccount?.AccountNameAr,
            BankName = rec.BankAccount?.BankName,
            FiscalYearId = rec.FiscalYearId,
            FiscalPeriodId = rec.FiscalPeriodId,
            StatementDate = rec.StatementDate,
            StatementEndingBalance = rec.StatementEndingBalance,
            BookEndingBalance = rec.BookEndingBalance,
            TotalReconciledAmount = rec.TotalReconciledAmount,
            UnreconciledDifference = rec.UnreconciledDifference,
            Status = rec.Status,
            Notes = rec.Notes
        }).ToList();
    }

    public async Task<AutoReconcileResultDto> AutoReconcileAsync(long reconciliationId, string username, CancellationToken cancellationToken = default)
    {
        var rec = await _bankingRepository.GetReconciliationByIdAsync(reconciliationId, cancellationToken);
        if (rec == null)
        {
            throw new AccountingNotFoundException($"تسوية البنك رقم ({reconciliationId}) غير موجودة.", "RECONCILIATION_NOT_FOUND");
        }

        var (vouchers, _) = await _voucherRepository.GetPagedVouchersAsync(
            branchId: rec.BankAccount?.BranchId,
            year: null,
            month: null,
            typeCode: null,
            status: 3, // Posted
            fromDate: null,
            toDate: rec.StatementDate,
            searchKeyword: null,
            pageIndex: 1,
            pageSize: 10000,
            cancellationToken: cancellationToken);

        var glLines = vouchers
            .SelectMany(v => v.Details.Select(d => new { Detail = d, Date = v.VoucherDate }))
            .Where(x => x.Detail.AccountCode == rec.BankAccount?.GlAccountCode)
            .ToList();

        int matchedCount = 0;
        decimal matchedTotal = 0;

        foreach (var stmtLine in rec.StatementLines.Where(l => !l.IsReconciled))
        {
            // Try matching with GL lines: 
            // In Bank Statement: Debit = Withdrawal (GL Credit), Credit = Deposit (GL Debit)
            var matchedGl = glLines.FirstOrDefault(gl =>
                ((stmtLine.Credit > 0 && Math.Abs(gl.Detail.LocalDebit - stmtLine.Credit) < 0.001m) ||
                 (stmtLine.Debit > 0 && Math.Abs(gl.Detail.LocalCredit - stmtLine.Debit) < 0.001m))
                && gl.Date.Date == stmtLine.TransactionDate.Date);

            if (matchedGl != null)
            {
                stmtLine.IsReconciled = true;
                stmtLine.ReconciledDate = DateTime.UtcNow;
                stmtLine.MatchedVoucherDetailId = matchedGl.Detail.Id;
                matchedCount++;
                matchedTotal += (stmtLine.Credit > 0 ? stmtLine.Credit : stmtLine.Debit);
                glLines.Remove(matchedGl); // Avoid duplicate matching
            }
        }

        rec.TotalReconciledAmount += matchedTotal;
        rec.UnreconciledDifference = rec.StatementEndingBalance - rec.BookEndingBalance;
        rec.Status = rec.StatementLines.All(l => l.IsReconciled) ? "COMPLETED" : "IN_PROGRESS";
        rec.UpdateUser = username;
        rec.UpdateDate = DateTime.UtcNow;

        await _bankingRepository.SaveChangesAsync(cancellationToken);

        return new AutoReconcileResultDto
        {
            ReconciliationId = rec.Id,
            TotalLines = rec.StatementLines.Count,
            MatchedLinesCount = matchedCount,
            MatchedAmount = matchedTotal,
            UnmatchedLinesCount = rec.StatementLines.Count(l => !l.IsReconciled),
            UnreconciledDifference = rec.UnreconciledDifference
        };
    }

    public async Task<BankReconciliationDto> ManualMatchAsync(long reconciliationId, ManualMatchLineDto matchDto, string username, CancellationToken cancellationToken = default)
    {
        var rec = await _bankingRepository.GetReconciliationByIdAsync(reconciliationId, cancellationToken);
        if (rec == null)
        {
            throw new AccountingNotFoundException($"تسوية البنك رقم ({reconciliationId}) غير موجودة.", "RECONCILIATION_NOT_FOUND");
        }

        var line = rec.StatementLines.FirstOrDefault(l => l.Id == matchDto.StatementLineId);
        if (line == null)
        {
            throw new AccountingNotFoundException($"سطر كشف الحساب رقم ({matchDto.StatementLineId}) غير موجود في هذه التسوية.", "LINE_NOT_FOUND");
        }

        line.IsReconciled = true;
        line.ReconciledDate = DateTime.UtcNow;
        line.MatchedVoucherDetailId = matchDto.VoucherDetailId;

        rec.TotalReconciledAmount += (line.Credit > 0 ? line.Credit : line.Debit);
        rec.UpdateUser = username;
        rec.UpdateDate = DateTime.UtcNow;

        await _bankingRepository.SaveChangesAsync(cancellationToken);
        return await GetReconciliationByIdAsync(reconciliationId, cancellationToken);
    }

    public async Task<BankReconciliationStatementDto> GetReconciliationStatementReportAsync(long reconciliationId, CancellationToken cancellationToken = default)
    {
        var rec = await _bankingRepository.GetReconciliationByIdAsync(reconciliationId, cancellationToken);
        if (rec == null)
        {
            throw new AccountingNotFoundException($"تسوية البنك رقم ({reconciliationId}) غير موجودة.", "RECONCILIATION_NOT_FOUND");
        }

        var unpresented = rec.StatementLines.Where(l => !l.IsReconciled && l.Debit > 0).Sum(l => l.Debit);
        var inTransit = rec.StatementLines.Where(l => !l.IsReconciled && l.Credit > 0).Sum(l => l.Credit);

        var adjusted = rec.BookEndingBalance + inTransit - unpresented;
        var diff = rec.StatementEndingBalance - adjusted;

        return new BankReconciliationStatementDto
        {
            BankAccountNameAr = rec.BankAccount?.AccountNameAr ?? string.Empty,
            BankAccountNumber = rec.BankAccount?.AccountNumber ?? string.Empty,
            AsOfDate = rec.StatementDate,
            BookBalance = rec.BookEndingBalance,
            UnpresentedChequesAmount = unpresented,
            DepositsInTransitAmount = inTransit,
            BankChargesNotRecorded = 0,
            AdjustedBookBalance = adjusted,
            BankStatementBalance = rec.StatementEndingBalance,
            Difference = diff
        };
    }

    private static BankAccountDto MapToBankAccountDto(BankAccount b)
    {
        return new BankAccountDto
        {
            Id = b.Id,
            BranchId = b.BranchId,
            BranchNameAr = b.Branch?.BranchNameAr,
            AccountNumber = b.AccountNumber,
            AccountNameAr = b.AccountNameAr,
            AccountNameEn = b.AccountNameEn,
            BankName = b.BankName,
            BankBranchName = b.BankBranchName,
            Iban = b.Iban,
            SwiftCode = b.SwiftCode,
            CurrencyId = b.CurrencyId,
            CurrencyName = b.Currency?.ShortNameEn,
            GlAccountCode = b.GlAccountCode,
            GlAccountNameAr = b.GlAccount?.AccountNameAr,
            OverdraftLimit = b.OverdraftLimit,
            OpeningBalance = b.OpeningBalance,
            CurrentBalance = b.CurrentBalance,
            IsActive = b.IsActive
        };
    }

    private static CashRegisterDto MapToCashRegisterDto(CashRegister c)
    {
        return new CashRegisterDto
        {
            Id = c.Id,
            BranchId = c.BranchId,
            BranchNameAr = c.Branch?.BranchNameAr,
            Code = c.Code,
            NameAr = c.NameAr,
            NameEn = c.NameEn,
            RegisterType = c.RegisterType,
            CustodianName = c.CustodianName,
            GlAccountCode = c.GlAccountCode,
            GlAccountNameAr = c.GlAccount?.AccountNameAr,
            CurrencyId = c.CurrencyId,
            CurrencyName = c.Currency?.ShortNameEn,
            MinLimit = c.MinLimit,
            MaxLimit = c.MaxLimit,
            OpeningBalance = c.OpeningBalance,
            CurrentBalance = c.CurrentBalance,
            IsActive = c.IsActive
        };
    }
}
