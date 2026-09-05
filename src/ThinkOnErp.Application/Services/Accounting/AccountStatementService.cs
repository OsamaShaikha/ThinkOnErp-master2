using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Accounting.AccountStatement;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class AccountStatementService : IAccountStatementService
{
    private readonly IAccountStatementRepository _statementRepository;
    private readonly ILogger<AccountStatementService> _logger;

    public AccountStatementService(
        IAccountStatementRepository statementRepository,
        ILogger<AccountStatementService> logger)
    {
        _statementRepository = statementRepository;
        _logger = logger;
    }

    public async Task<AccountStatementReportDto> GetAccountStatementAsync(
        AccountStatementRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AccountCode))
        {
            throw new AccountingException("رقم الحساب مطلوب لإنشاء كشف الحساب.", "ACCOUNT_CODE_REQUIRED");
        }

        var account = await _statementRepository.GetAccountByCodeAsync(request.AccountCode.Trim(), cancellationToken);
        if (account == null)
        {
            throw new AccountingNotFoundException($"الحساب رقم ({request.AccountCode}) غير موجود.", "GL_ACCOUNT_NOT_FOUND");
        }

        // Determine target accounts (single account or parent hierarchy rollup)
        var targetCodes = new List<string> { account.AccountCode };
        if (request.IncludeChildAccounts && account.AccountType == "HEADER")
        {
            var childAccounts = await _statementRepository.GetChildAccountsAsync(account.AccountCode, cancellationToken);
            foreach (var child in childAccounts.Where(c => c.AccountType == "DETAIL"))
            {
                if (!targetCodes.Contains(child.AccountCode))
                    targetCodes.Add(child.AccountCode);
            }
        }

        // 1. Calculate Opening Balance prior to FromDate
        decimal openingDebit = 0m;
        decimal openingCredit = 0m;

        if (request.FromDate.HasValue)
        {
            var ob = await _statementRepository.GetOpeningBalanceAsync(
                targetCodes,
                request.FromDate.Value,
                request.BranchId,
                request.FiscalYearId,
                request.CostCenterCode,
                request.IncludeUnposted,
                cancellationToken);

            openingDebit = ob.Debit;
            openingCredit = ob.Credit;
        }

        // Normal balance direction
        bool isDebitNormal = account.NormalBalance.Equals("D", StringComparison.OrdinalIgnoreCase);
        decimal openingNetBalance = isDebitNormal
            ? (openingDebit - openingCredit)
            : (openingCredit - openingDebit);

        // 2. Fetch period transaction postings from optimized database view
        var periodDetails = await _statementRepository.GetStatementFromViewAsync(
            targetCodes,
            request.FromDate,
            request.ToDate,
            request.BranchId,
            request.FiscalYearId,
            request.CostCenterCode,
            request.IncludeUnposted,
            cancellationToken);

        // Fetch voucher types metadata for fast lookup
        var voucherTypes = (await _statementRepository.GetVoucherTypesAsync(cancellationToken))
            .ToDictionary(v => v.TypeCode);

        // 3. Process items and calculate running balance line by line
        decimal currentRunningBalance = openingNetBalance;
        decimal totalPeriodDebit = 0m;
        decimal totalPeriodCredit = 0m;

        var items = new List<AccountStatementItemDto>(periodDetails.Count);

        foreach (var detail in periodDetails)
        {
            totalPeriodDebit += detail.LocalDebit;
            totalPeriodCredit += detail.LocalCredit;

            decimal lineMovement = isDebitNormal
                ? (detail.LocalDebit - detail.LocalCredit)
                : (detail.LocalCredit - detail.LocalDebit);

            currentRunningBalance += lineMovement;

            voucherTypes.TryGetValue(detail.VoucherType, out var vType);

            var itemDto = new AccountStatementItemDto
            {
                VoucherId = detail.VoucherId,
                VoucherNo = detail.VoucherNo,
                VoucherDate = detail.VoucherDate,
                VoucherTypeCode = detail.VoucherType,
                VoucherTypeNameLocal = vType?.NameLocal ?? string.Empty,
                VoucherTypeNameEn = vType?.NameEn ?? string.Empty,
                VoucherPrefix = vType?.Prefix ?? string.Empty,
                LineSer = detail.LineSer,
                AccountCode = detail.AccountCode,
                AccountNameLocal = detail.AccountNameLocal ?? account.AccountNameLocal,
                AccountNameEn = detail.AccountNameEn ?? account.AccountNameEn,
                Debit = detail.Debit,
                Credit = detail.Credit,
                LocalDebit = detail.LocalDebit,
                LocalCredit = detail.LocalCredit,
                RunningBalance = currentRunningBalance,
                CurrencyId = detail.CurrencyId ?? 0,
                ExchangeRate = detail.ExchangeRate,
                CostCenterCode = detail.CostCenterCode,
                CostCenterNameLocal = detail.CostCenterNameLocal,
                CostCenterNameEn = detail.CostCenterNameLocal,
                Description = detail.Description,
                SourceSystemCode = string.Empty,
                SourceRefId = null,
                VoucherStatus = detail.VoucherStatus
            };

            items.Add(itemDto);
        }

        // 4. Calculate closing totals
        decimal periodNetMovement = isDebitNormal
            ? (totalPeriodDebit - totalPeriodCredit)
            : (totalPeriodCredit - totalPeriodDebit);

        decimal closingDebit = openingDebit + totalPeriodDebit;
        decimal closingCredit = openingCredit + totalPeriodCredit;
        decimal closingBalance = currentRunningBalance;

        return new AccountStatementReportDto
        {
            AccountCode = account.AccountCode,
            AccountNameLocal = account.AccountNameLocal,
            AccountNameEn = account.AccountNameEn,
            AccountType = account.AccountType,
            NormalBalance = account.NormalBalance,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            BranchId = request.BranchId,
            FiscalYearId = request.FiscalYearId,
            CostCenterCode = request.CostCenterCode,
            OpeningDebit = openingDebit,
            OpeningCredit = openingCredit,
            OpeningBalance = openingNetBalance,
            TotalPeriodDebit = totalPeriodDebit,
            TotalPeriodCredit = totalPeriodCredit,
            PeriodNetMovement = periodNetMovement,
            ClosingDebit = closingDebit,
            ClosingCredit = closingCredit,
            ClosingBalance = closingBalance,
            Items = items
        };
    }

    public async Task<IReadOnlyList<AccountStatementSummaryDto>> GetAccountsSummaryAsync(
        AccountStatementRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var allAccounts = await _statementRepository.GetAllAccountsAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.AccountCode))
        {
            var accCode = request.AccountCode.Trim();
            allAccounts = allAccounts.Where(a => a.AccountCode.StartsWith(accCode) || a.ParentAccountCode == accCode).ToList();
        }

        var accountsMap = allAccounts
            .Where(a => a.AccountType == "DETAIL")
            .ToDictionary(a => a.AccountCode);

        var aggregates = await _statementRepository.GetAccountsSummaryAggregatesAsync(
            request.FromDate,
            request.ToDate,
            request.BranchId,
            request.FiscalYearId,
            request.CostCenterCode,
            request.IncludeUnposted,
            cancellationToken);

        var result = new List<AccountStatementSummaryDto>(aggregates.Count);

        foreach (var agg in aggregates)
        {
            if (!accountsMap.TryGetValue(agg.AccountCode, out var acc))
                continue;

            bool isDebitNormal = acc.NormalBalance.Equals("D", StringComparison.OrdinalIgnoreCase);
            decimal openingBalance = isDebitNormal
                ? (agg.OpeningDebit - agg.OpeningCredit)
                : (agg.OpeningCredit - agg.OpeningDebit);

            decimal periodNetMovement = isDebitNormal
                ? (agg.PeriodDebit - agg.PeriodCredit)
                : (agg.PeriodCredit - agg.PeriodDebit);

            decimal closingDebit = agg.OpeningDebit + agg.PeriodDebit;
            decimal closingCredit = agg.OpeningCredit + agg.PeriodCredit;
            decimal closingBalance = isDebitNormal
                ? (closingDebit - closingCredit)
                : (closingCredit - closingDebit);

            result.Add(new AccountStatementSummaryDto
            {
                AccountCode = acc.AccountCode,
                AccountNameLocal = acc.AccountNameLocal,
                AccountNameEn = acc.AccountNameEn,
                AccountType = acc.AccountType,
                NormalBalance = acc.NormalBalance,
                OpeningDebit = agg.OpeningDebit,
                OpeningCredit = agg.OpeningCredit,
                OpeningBalance = openingBalance,
                PeriodDebit = agg.PeriodDebit,
                PeriodCredit = agg.PeriodCredit,
                PeriodNetMovement = periodNetMovement,
                ClosingDebit = closingDebit,
                ClosingCredit = closingCredit,
                ClosingBalance = closingBalance
            });
        }

        return result.OrderBy(r => r.AccountCode).ToList();
    }
}
