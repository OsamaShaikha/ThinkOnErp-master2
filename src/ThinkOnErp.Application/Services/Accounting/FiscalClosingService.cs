using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Accounting.Closing;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class FiscalClosingService : IFiscalClosingService
{
    private readonly IGlFiscalPeriodRepository _periodRepository;
    private readonly IFiscalYearRepository _fiscalYearRepository;
    private readonly IGlAccountRepository _accountRepository;
    private readonly IGlVoucherRepository _voucherRepository;
    private readonly IGlAccountBalanceRepository _balanceRepository;
    private readonly IGlFiscalPeriodService _periodService;
    private readonly IGlVoucherService _voucherService;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<FiscalClosingService> _logger;

    public FiscalClosingService(
        IGlFiscalPeriodRepository periodRepository,
        IFiscalYearRepository fiscalYearRepository,
        IGlAccountRepository accountRepository,
        IGlVoucherRepository voucherRepository,
        IGlAccountBalanceRepository balanceRepository,
        IGlFiscalPeriodService periodService,
        IGlVoucherService voucherService,
        ICurrentTenantContext tenantContext,
        ILogger<FiscalClosingService> logger)
    {
        _periodRepository = periodRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _accountRepository = accountRepository;
        _voucherRepository = voucherRepository;
        _balanceRepository = balanceRepository;
        _periodService = periodService;
        _voucherService = voucherService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<PreClosingPeriodValidationDto> ValidatePeriodClosingAsync(long periodId, CancellationToken cancellationToken = default)
    {
        var period = await _periodRepository.GetByIdAsync(periodId, cancellationToken);
        if (period == null)
        {
            throw new AccountingNotFoundException($"الفترة المالية رقم ({periodId}) غير موجودة.", "PERIOD_NOT_FOUND");
        }

        var (vouchers, _) = await _voucherRepository.GetPagedVouchersAsync(
            branchId: null,
            year: null,
            month: null,
            typeCode: null,
            status: null,
            fromDate: period.StartDate,
            toDate: period.EndDate,
            searchKeyword: null,
            pageIndex: 1,
            pageSize: 10000,
            cancellationToken: cancellationToken);

        int drafts = vouchers.Count(v => v.Status == 1);
        int underReview = vouchers.Count(v => v.Status == 2);

        var messages = new List<string>();
        if (drafts > 0)
        {
            messages.Add($"يوجد {drafts} سند بحالة مسودة (Draft) في هذه الفترة.");
        }
        if (underReview > 0)
        {
            messages.Add($"يوجد {underReview} سند قيد المراجعة (Under Review) في هذه الفترة.");
        }

        return new PreClosingPeriodValidationDto
        {
            FiscalPeriodId = period.Id,
            PeriodNumber = period.PeriodNumber,
            PeriodNameAr = period.PeriodNameAr,
            UnpostedDraftVouchersCount = drafts,
            UnderReviewVouchersCount = underReview,
            IsReadyToClose = drafts == 0 && underReview == 0,
            ValidationMessages = messages
        };
    }

    public async Task<bool> ClosePeriodAsync(long periodId, ExecutePeriodCloseRequest request, string username, CancellationToken cancellationToken = default)
    {
        var validation = await ValidatePeriodClosingAsync(periodId, cancellationToken);
        if (!validation.IsReadyToClose)
        {
            throw new AccountingException($"لا يمكن إقفال الفترة المالية لوجود قيود غير مرحلة: {string.Join(" - ", validation.ValidationMessages)}", "PERIOD_HAS_UNPOSTED_VOUCHERS");
        }

        await _periodService.HardClosePeriodAsync(periodId, username, request.Reason, cancellationToken);
        return true;
    }

    public async Task<bool> ReopenPeriodAsync(long periodId, ExecutePeriodReopenRequest request, string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new AccountingException("يجب تحديد سبب إعادة فتح الفترة المالية رسمياً.", "REOPEN_REASON_REQUIRED");
        }

        await _periodService.ReopenPeriodAsync(periodId, username, request.Reason, cancellationToken);
        return true;
    }

    public async Task<PreClosingYearValidationDto> ValidateYearEndClosingAsync(long yearId, CancellationToken cancellationToken = default)
    {
        var year = await _fiscalYearRepository.GetByIdAsync(yearId);
        if (year == null)
        {
            throw new AccountingNotFoundException($"السنة المالية رقم ({yearId}) غير موجودة.", "YEAR_NOT_FOUND");
        }

        var companyId = _tenantContext.GetRequiredCompanyId();
        int yearNumber = year.StartDate.Year;

        var periods = await _periodRepository.GetByFiscalYearIdAsync(yearId, cancellationToken);
        var unclosedPeriods = periods.Count(p => p.Status != "HARD_CLOSED" && p.Status != "SOFT_CLOSED");

        var (vouchers, _) = await _voucherRepository.GetPagedVouchersAsync(
            branchId: null,
            year: yearNumber,
            month: null,
            typeCode: null,
            status: null,
            fromDate: null,
            toDate: null,
            searchKeyword: null,
            pageIndex: 1,
            pageSize: 10000,
            cancellationToken: cancellationToken);

        var unpostedVouchers = vouchers.Count(v => v.Status != 3);

        // Get Balances for Year
        var balances = await _balanceRepository.GetBalancesAsync(
            accountCode: null,
            branchId: null,
            fiscalYearId: yearId,
            fiscalPeriodId: null,
            cancellationToken: cancellationToken);

        var totalDebit = balances.Sum(b => b.ClosingDebit);
        var totalCredit = balances.Sum(b => b.ClosingCredit);
        var diff = totalDebit - totalCredit;

        var accounts = await _accountRepository.GetAllAsync(companyId, cancellationToken);
        var accountMap = accounts.ToDictionary(a => a.AccountCode);

        decimal totalRevenues = 0;
        decimal totalExpenses = 0;

        foreach (var bal in balances)
        {
            if (accountMap.TryGetValue(bal.AccountCode, out var acc))
            {
                var net = bal.ClosingCredit - bal.ClosingDebit;
                if (acc.AccountCode.StartsWith("4"))
                {
                    totalRevenues += net;
                }
                else if (acc.AccountCode.StartsWith("5"))
                {
                    totalExpenses += (bal.ClosingDebit - bal.ClosingCredit);
                }
            }
        }

        var projectedNetIncome = totalRevenues - totalExpenses;

        var messages = new List<string>();
        if (unclosedPeriods > 0)
        {
            messages.Add($"يوجد {unclosedPeriods} فترة مالية شهرية لم يتم إقفالها بعد.");
        }
        if (unpostedVouchers > 0)
        {
            messages.Add($"يوجد {unpostedVouchers} سند محاسبي غير مرحّل في هذه السنة.");
        }
        if (Math.Abs(diff) >= 0.001m)
        {
            messages.Add($"ميزان المراجعة غير متوازن بفارق قدره ({diff:N3}).");
        }

        return new PreClosingYearValidationDto
        {
            FiscalYearId = year.Id,
            YearNumber = yearNumber,
            UnclosedPeriodsCount = unclosedPeriods,
            TotalUnpostedVouchersCount = unpostedVouchers,
            TrialBalanceDebit = totalDebit,
            TrialBalanceCredit = totalCredit,
            TrialBalanceDifference = diff,
            ProjectedNetIncome = projectedNetIncome,
            IsReadyToClose = unclosedPeriods == 0 && unpostedVouchers == 0 && Math.Abs(diff) < 0.001m,
            ValidationMessages = messages
        };
    }

    public async Task<YearEndClosingResultDto> ExecuteYearEndClosingAsync(
        long yearId,
        ExecuteYearEndCloseRequest request,
        string username,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateYearEndClosingAsync(yearId, cancellationToken);
        if (!validation.IsReadyToClose)
        {
            throw new AccountingException($"لا يمكن تنفيذ الإقفال السنوي لوجود عوائق محاسبية: {string.Join(" - ", validation.ValidationMessages)}", "PRE_CLOSING_CHECK_FAILED");
        }

        var year = await _fiscalYearRepository.GetByIdAsync(yearId);
        if (year == null)
        {
            throw new AccountingNotFoundException($"السنة المالية رقم ({yearId}) غير موجودة.", "YEAR_NOT_FOUND");
        }

        var companyId = _tenantContext.GetRequiredCompanyId();
        int yearNumber = year.StartDate.Year;

        var retainedAcc = await _accountRepository.GetByCodeAsync(companyId, request.RetainedEarningsAccountCode, cancellationToken);
        if (retainedAcc == null)
        {
            throw new AccountingNotFoundException($"حساب الأرباح المبقاة/المحتجزة ({request.RetainedEarningsAccountCode}) غير موجود في الدليل.", "RETAINED_EARNINGS_ACCOUNT_NOT_FOUND");
        }

        var balances = await _balanceRepository.GetBalancesAsync(
            accountCode: null,
            branchId: null,
            fiscalYearId: yearId,
            fiscalPeriodId: null,
            cancellationToken: cancellationToken);

        var accounts = await _accountRepository.GetAllAsync(companyId, cancellationToken);
        var accountMap = accounts.ToDictionary(a => a.AccountCode);

        // Build Closing Voucher Lines
        var closingDetails = new List<CreateGlVoucherDetailDto>();
        decimal totalRevenues = 0;
        decimal totalExpenses = 0;

        foreach (var bal in balances.Where(b => b.AccountCode.StartsWith("4") || b.AccountCode.StartsWith("5")))
        {
            if (accountMap.TryGetValue(bal.AccountCode, out var acc) && acc.IsPostable)
            {
                var netBal = bal.ClosingDebit - bal.ClosingCredit;
                if (Math.Abs(netBal) < 0.001m) continue;

                if (acc.AccountCode.StartsWith("4")) // Revenue (Normal Credit) -> Debit to close
                {
                    var crAmount = bal.ClosingCredit - bal.ClosingDebit;
                    if (crAmount > 0)
                    {
                        closingDetails.Add(new CreateGlVoucherDetailDto
                        {
                            AccountCode = acc.AccountCode,
                            Debit = crAmount,
                            Credit = 0,
                            Description = $"إقفال سنوي لحساب الإيرادات {acc.AccountNameAr}"
                        });
                        totalRevenues += crAmount;
                    }
                }
                else if (acc.AccountCode.StartsWith("5")) // Expense (Normal Debit) -> Credit to close
                {
                    var drAmount = bal.ClosingDebit - bal.ClosingCredit;
                    if (drAmount > 0)
                    {
                        closingDetails.Add(new CreateGlVoucherDetailDto
                        {
                            AccountCode = acc.AccountCode,
                            Debit = 0,
                            Credit = drAmount,
                            Description = $"إقفال سنوي لحساب المصروفات {acc.AccountNameAr}"
                        });
                        totalExpenses += drAmount;
                    }
                }
            }
        }

        var netIncome = totalRevenues - totalExpenses;
        if (netIncome > 0) // Net Profit -> Credit Retained Earnings
        {
            closingDetails.Add(new CreateGlVoucherDetailDto
            {
                AccountCode = request.RetainedEarningsAccountCode,
                Debit = 0,
                Credit = netIncome,
                Description = $"ترحيل صافي أرباح السنة المالية {yearNumber} إلى الأرباح المبقاة"
            });
        }
        else if (netIncome < 0) // Net Loss -> Debit Retained Earnings
        {
            closingDetails.Add(new CreateGlVoucherDetailDto
            {
                AccountCode = request.RetainedEarningsAccountCode,
                Debit = Math.Abs(netIncome),
                Credit = 0,
                Description = $"ترحيل صافي خسائر السنة المالية {yearNumber} من الأرباح المبقاة"
            });
        }

        // Create & Post Closing Voucher
        var closingVoucherDto = new CreateGlVoucherDto
        {
            BranchId = year.BranchId,
            FiscalYearId = year.Id,
            VoucherType = 301, // Fiscal Year Closing Journal (CLS)
            VoucherDate = year.EndDate,
            Description = $"قيد الإقفال السنوي وتصفير الإيرادات والمصروفات للسنة المالية {yearNumber}",
            Details = closingDetails
        };

        var createdClosing = await _voucherService.CreateVoucherAsync(closingVoucherDto, username, cancellationToken);
        await _voucherService.ReviewVoucherAsync(createdClosing.Id, username, cancellationToken);
        var postedClosing = await _voucherService.PostVoucherAsync(createdClosing.Id, username, cancellationToken);

        long? openingVoucherId = null;
        string? openingVoucherNo = null;
        int rolledCount = 0;

        // Balance Sheet Roll-forward to Next Year
        if (request.GenerateOpeningVoucher && request.TargetNextFiscalYearId.HasValue)
        {
            var nextYear = await _fiscalYearRepository.GetByIdAsync(request.TargetNextFiscalYearId.Value);
            if (nextYear != null)
            {
                int nextYearNum = nextYear.StartDate.Year;
                var openingDetails = new List<CreateGlVoucherDetailDto>();
                foreach (var bal in balances.Where(b => b.AccountCode.StartsWith("1") || b.AccountCode.StartsWith("2") || b.AccountCode.StartsWith("3")))
                {
                    if (accountMap.TryGetValue(bal.AccountCode, out var acc) && acc.IsPostable)
                    {
                        var dr = bal.ClosingDebit;
                        var cr = bal.ClosingCredit;

                        // Add net income effect to retained earnings account
                        if (acc.AccountCode == request.RetainedEarningsAccountCode)
                        {
                            if (netIncome > 0) cr += netIncome;
                            else if (netIncome < 0) dr += Math.Abs(netIncome);
                        }

                        var net = dr - cr;
                        if (Math.Abs(net) < 0.001m) continue;

                        openingDetails.Add(new CreateGlVoucherDetailDto
                        {
                            AccountCode = acc.AccountCode,
                            Debit = net > 0 ? net : 0,
                            Credit = net < 0 ? Math.Abs(net) : 0,
                            Description = $"رصيد افتتاحي مدور من السنة المالية {yearNumber}"
                        });
                        rolledCount++;
                    }
                }

                if (openingDetails.Count > 0)
                {
                    var openingVoucherDto = new CreateGlVoucherDto
                    {
                        BranchId = nextYear.BranchId,
                        FiscalYearId = nextYear.Id,
                        VoucherType = 302, // Opening Balance Journal (OB)
                        VoucherDate = nextYear.StartDate,
                        Description = $"القيد الافتتاحي وتدوير أرصدة الميزانية العمومية للسنة المالية {nextYearNum}",
                        Details = openingDetails
                    };

                    var createdOpening = await _voucherService.CreateVoucherAsync(openingVoucherDto, username, cancellationToken);
                    await _voucherService.ReviewVoucherAsync(createdOpening.Id, username, cancellationToken);
                    var postedOpening = await _voucherService.PostVoucherAsync(createdOpening.Id, username, cancellationToken);
                    openingVoucherId = postedOpening.Id;
                    openingVoucherNo = postedOpening.VoucherNo.ToString();
                }
            }
        }

        // Lock Fiscal Year
        await _fiscalYearRepository.CloseAsync(year.Id, username);

        return new YearEndClosingResultDto
        {
            FiscalYearId = year.Id,
            YearNumber = yearNumber,
            ClosingVoucherId = postedClosing.Id,
            ClosingVoucherNo = postedClosing.VoucherNo.ToString(),
            TotalRevenues = totalRevenues,
            TotalExpenses = totalExpenses,
            NetIncome = netIncome,
            RetainedEarningsAccountCode = request.RetainedEarningsAccountCode,
            OpeningVoucherId = openingVoucherId,
            OpeningVoucherNo = openingVoucherNo,
            RolledAccountsCount = rolledCount,
            Message = $"تم إقفال السنة المالية {yearNumber} وتصفير الحسابات الاسمية وتوليد القيود المحاسبية بنجاح."
        };
    }
}
