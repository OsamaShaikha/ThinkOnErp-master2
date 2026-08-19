using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Accounting.Subledger;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class SubledgerService : ISubledgerService
{
    private readonly IArSubledgerRepository _arRepository;
    private readonly IApSubledgerRepository _apRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IVendorRepository _vendorRepository;
    private readonly IGlAccountBalanceRepository _balanceRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<SubledgerService> _logger;

    public SubledgerService(
        IArSubledgerRepository arRepository,
        IApSubledgerRepository apRepository,
        ICustomerRepository customerRepository,
        IVendorRepository vendorRepository,
        IGlAccountBalanceRepository balanceRepository,
        ICurrentTenantContext tenantContext,
        ILogger<SubledgerService> logger)
    {
        _arRepository = arRepository;
        _apRepository = apRepository;
        _customerRepository = customerRepository;
        _vendorRepository = vendorRepository;
        _balanceRepository = balanceRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SubledgerTransactionDto>> GetArTransactionsAsync(
        string? customerCode,
        DateTime? fromDate,
        DateTime? toDate,
        bool onlyOpen = false,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _arRepository.GetTransactionsAsync(customerCode, fromDate, toDate, onlyOpen, cancellationToken);
        return transactions.Select(t => new SubledgerTransactionDto
        {
            Id = t.Id,
            PartyCode = t.CustomerCode,
            PartyNameAr = t.Customer?.NameAr ?? string.Empty,
            PartyNameEn = t.Customer?.NameEn ?? string.Empty,
            JournalLineId = t.JournalLineId,
            VoucherId = t.VoucherId,
            VoucherNo = t.Voucher?.VoucherNo.ToString(),
            TransactionType = t.TransactionType,
            TransactionDate = t.TransactionDate,
            DueDate = t.DueDate,
            Amount = t.Amount,
            CurrencyId = t.CurrencyId,
            CurrencyName = t.Currency?.ShortNameEn,
            ExchangeRate = t.ExchangeRate,
            LocalAmount = t.LocalAmount,
            OpenAmount = t.OpenAmount,
            LocalOpenAmount = t.LocalOpenAmount,
            ReferenceNo = t.ReferenceNo,
            Description = t.Description
        }).ToList();
    }

    public async Task<IReadOnlyList<SubledgerTransactionDto>> GetApTransactionsAsync(
        string? vendorCode,
        DateTime? fromDate,
        DateTime? toDate,
        bool onlyOpen = false,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _apRepository.GetTransactionsAsync(vendorCode, fromDate, toDate, onlyOpen, cancellationToken);
        return transactions.Select(t => new SubledgerTransactionDto
        {
            Id = t.Id,
            PartyCode = t.VendorCode,
            PartyNameAr = t.Vendor?.NameAr ?? string.Empty,
            PartyNameEn = t.Vendor?.NameEn ?? string.Empty,
            JournalLineId = t.JournalLineId,
            VoucherId = t.VoucherId,
            VoucherNo = t.Voucher?.VoucherNo.ToString(),
            TransactionType = t.TransactionType,
            TransactionDate = t.TransactionDate,
            DueDate = t.DueDate,
            Amount = t.Amount,
            CurrencyId = t.CurrencyId,
            CurrencyName = t.Currency?.ShortNameEn,
            ExchangeRate = t.ExchangeRate,
            LocalAmount = t.LocalAmount,
            OpenAmount = t.OpenAmount,
            LocalOpenAmount = t.LocalOpenAmount,
            ReferenceNo = t.ReferenceNo,
            Description = t.Description
        }).ToList();
    }

    public async Task<IReadOnlyList<OpenInvoiceDto>> GetArOpenInvoicesAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        var invoices = await _arRepository.GetOpenInvoicesAsync(customerCode, cancellationToken);
        return invoices.Select(i => new OpenInvoiceDto
        {
            TransactionId = i.Id,
            PartyCode = i.CustomerCode,
            TransactionType = i.TransactionType,
            TransactionDate = i.TransactionDate,
            DueDate = i.DueDate,
            OriginalAmount = i.Amount,
            OpenAmount = i.OpenAmount,
            LocalOpenAmount = i.LocalOpenAmount,
            ReferenceNo = i.ReferenceNo,
            Description = i.Description,
            ExchangeRate = i.ExchangeRate,
            CurrencyId = i.CurrencyId,
            CurrencyName = i.Currency?.ShortNameEn
        }).ToList();
    }

    public async Task<IReadOnlyList<OpenInvoiceDto>> GetApOpenBillsAsync(string vendorCode, CancellationToken cancellationToken = default)
    {
        var bills = await _apRepository.GetOpenBillsAsync(vendorCode, cancellationToken);
        return bills.Select(b => new OpenInvoiceDto
        {
            TransactionId = b.Id,
            PartyCode = b.VendorCode,
            TransactionType = b.TransactionType,
            TransactionDate = b.TransactionDate,
            DueDate = b.DueDate,
            OriginalAmount = b.Amount,
            OpenAmount = b.OpenAmount,
            LocalOpenAmount = b.LocalOpenAmount,
            ReferenceNo = b.ReferenceNo,
            Description = b.Description,
            ExchangeRate = b.ExchangeRate,
            CurrencyId = b.CurrencyId,
            CurrencyName = b.Currency?.ShortNameEn
        }).ToList();
    }

    public async Task<IReadOnlyList<OpenInvoiceDto>> GetArOpenPaymentsAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        var payments = await _arRepository.GetOpenPaymentsAsync(customerCode, cancellationToken);
        return payments.Select(p => new OpenInvoiceDto
        {
            TransactionId = p.Id,
            PartyCode = p.CustomerCode,
            TransactionType = p.TransactionType,
            TransactionDate = p.TransactionDate,
            DueDate = p.DueDate,
            OriginalAmount = p.Amount,
            OpenAmount = p.OpenAmount,
            LocalOpenAmount = p.LocalOpenAmount,
            ReferenceNo = p.ReferenceNo,
            Description = p.Description,
            ExchangeRate = p.ExchangeRate,
            CurrencyId = p.CurrencyId,
            CurrencyName = p.Currency?.ShortNameEn
        }).ToList();
    }

    public async Task<IReadOnlyList<OpenInvoiceDto>> GetApOpenPaymentsAsync(string vendorCode, CancellationToken cancellationToken = default)
    {
        var payments = await _apRepository.GetOpenPaymentsAsync(vendorCode, cancellationToken);
        return payments.Select(p => new OpenInvoiceDto
        {
            TransactionId = p.Id,
            PartyCode = p.VendorCode,
            TransactionType = p.TransactionType,
            TransactionDate = p.TransactionDate,
            DueDate = p.DueDate,
            OriginalAmount = p.Amount,
            OpenAmount = p.OpenAmount,
            LocalOpenAmount = p.LocalOpenAmount,
            ReferenceNo = p.ReferenceNo,
            Description = p.Description,
            ExchangeRate = p.ExchangeRate,
            CurrencyId = p.CurrencyId,
            CurrencyName = p.Currency?.ShortNameEn
        }).ToList();
    }

    public async Task<List<CashApplicationResultDto>> ApplyArCashAsync(ApplyCashDto dto, string username, CancellationToken cancellationToken = default)
    {
        var payment = await _arRepository.GetByIdAsync(dto.PaymentTransactionId, cancellationToken);
        if (payment == null)
        {
            throw new AccountingNotFoundException($"معاملة السداد رقم ({dto.PaymentTransactionId}) غير موجودة.", "AR_PAYMENT_NOT_FOUND");
        }

        var results = new List<CashApplicationResultDto>();

        foreach (var item in dto.Invoices)
        {
            var invoice = await _arRepository.GetByIdAsync(item.InvoiceTransactionId, cancellationToken);
            if (invoice == null)
            {
                throw new AccountingNotFoundException($"الفاتورة رقم ({item.InvoiceTransactionId}) غير موجودة.", "AR_INVOICE_NOT_FOUND");
            }

            if (item.AmountToApply <= 0) continue;

            if (item.AmountToApply > Math.Abs(invoice.OpenAmount))
            {
                throw new AccountingException($"المبلغ المطلوب مطابقته ({item.AmountToApply:N3}) أكبر من المبلغ المفتوح للفاتورة ({invoice.OpenAmount:N3}).", "INVALID_APPLIED_AMOUNT");
            }

            var localApplied = Math.Round(item.AmountToApply * invoice.ExchangeRate, 3);

            var application = new ArCashApplication
            {
                PaymentTransactionId = payment.Id,
                InvoiceTransactionId = invoice.Id,
                AppliedAmount = item.AmountToApply,
                LocalAppliedAmount = localApplied,
                AppliedDate = DateTime.UtcNow,
                Notes = item.Notes,
                CreationUser = username,
                CreationDate = DateTime.UtcNow
            };

            await _arRepository.AddCashApplicationAsync(application, cancellationToken);

            // Update open amounts
            invoice.OpenAmount -= item.AmountToApply;
            invoice.LocalOpenAmount -= localApplied;

            payment.OpenAmount = Math.Abs(payment.OpenAmount) >= item.AmountToApply ? payment.OpenAmount + item.AmountToApply : 0;
            payment.LocalOpenAmount = Math.Abs(payment.LocalOpenAmount) >= localApplied ? payment.LocalOpenAmount + localApplied : 0;

            results.Add(new CashApplicationResultDto
            {
                PaymentTransactionId = payment.Id,
                InvoiceTransactionId = invoice.Id,
                AppliedAmount = item.AmountToApply,
                LocalAppliedAmount = localApplied,
                AppliedDate = application.AppliedDate,
                Notes = application.Notes
            });
        }

        await _arRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Applied cash for AR payment {PaymentId} by {User}", payment.Id, username);
        return results;
    }

    public async Task<List<CashApplicationResultDto>> ApplyApCashAsync(ApplyCashDto dto, string username, CancellationToken cancellationToken = default)
    {
        var payment = await _apRepository.GetByIdAsync(dto.PaymentTransactionId, cancellationToken);
        if (payment == null)
        {
            throw new AccountingNotFoundException($"معاملة السداد رقم ({dto.PaymentTransactionId}) غير موجودة.", "AP_PAYMENT_NOT_FOUND");
        }

        var results = new List<CashApplicationResultDto>();

        foreach (var item in dto.Invoices)
        {
            var bill = await _apRepository.GetByIdAsync(item.InvoiceTransactionId, cancellationToken);
            if (bill == null)
            {
                throw new AccountingNotFoundException($"فاتورة الشراء رقم ({item.InvoiceTransactionId}) غير موجودة.", "AP_BILL_NOT_FOUND");
            }

            if (item.AmountToApply <= 0) continue;

            if (item.AmountToApply > Math.Abs(bill.OpenAmount))
            {
                throw new AccountingException($"المبلغ المطلوب مطابقته ({item.AmountToApply:N3}) أكبر من المبلغ المفتوح للفاتورة ({bill.OpenAmount:N3}).", "INVALID_APPLIED_AMOUNT");
            }

            var localApplied = Math.Round(item.AmountToApply * bill.ExchangeRate, 3);

            var application = new ApCashApplication
            {
                PaymentTransactionId = payment.Id,
                InvoiceTransactionId = bill.Id,
                AppliedAmount = item.AmountToApply,
                LocalAppliedAmount = localApplied,
                AppliedDate = DateTime.UtcNow,
                Notes = item.Notes,
                CreationUser = username,
                CreationDate = DateTime.UtcNow
            };

            await _apRepository.AddCashApplicationAsync(application, cancellationToken);

            // Update open amounts
            bill.OpenAmount -= item.AmountToApply;
            bill.LocalOpenAmount -= localApplied;

            payment.OpenAmount = Math.Abs(payment.OpenAmount) >= item.AmountToApply ? payment.OpenAmount + item.AmountToApply : 0;
            payment.LocalOpenAmount = Math.Abs(payment.LocalOpenAmount) >= localApplied ? payment.LocalOpenAmount + localApplied : 0;

            results.Add(new CashApplicationResultDto
            {
                PaymentTransactionId = payment.Id,
                InvoiceTransactionId = bill.Id,
                AppliedAmount = item.AmountToApply,
                LocalAppliedAmount = localApplied,
                AppliedDate = application.AppliedDate,
                Notes = application.Notes
            });
        }

        await _apRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Applied cash for AP payment {PaymentId} by {User}", payment.Id, username);
        return results;
    }

    public async Task<List<CashApplicationResultDto>> AutoApplyArCashAsync(AutoApplyCashDto dto, string username, CancellationToken cancellationToken = default)
    {
        var payment = await _arRepository.GetByIdAsync(dto.PaymentTransactionId, cancellationToken);
        if (payment == null)
        {
            throw new AccountingNotFoundException($"معاملة السداد رقم ({dto.PaymentTransactionId}) غير موجودة.", "AR_PAYMENT_NOT_FOUND");
        }

        var availablePayment = Math.Abs(payment.OpenAmount);
        if (availablePayment <= 0)
        {
            throw new AccountingException("معاملة السداد مستهلكة بالكامل.", "PAYMENT_ALREADY_FULLY_APPLIED");
        }

        var openInvoices = await _arRepository.GetOpenInvoicesAsync(payment.CustomerCode, cancellationToken);
        if (!openInvoices.Any())
        {
            throw new AccountingException($"لا توجد فواتير مفتوحة غير مسددة للعميل ({payment.CustomerCode}).", "NO_OPEN_INVOICES");
        }

        var manualDto = new ApplyCashDto
        {
            PaymentTransactionId = payment.Id,
            Invoices = new List<InvoiceApplicationItemDto>()
        };

        var remaining = availablePayment;
        foreach (var inv in openInvoices)
        {
            if (remaining <= 0) break;
            var alloc = Math.Min(remaining, inv.OpenAmount);
            manualDto.Invoices.Add(new InvoiceApplicationItemDto
            {
                InvoiceTransactionId = inv.Id,
                AmountToApply = alloc,
                Notes = dto.Notes ?? "Auto FIFO Matching"
            });
            remaining -= alloc;
        }

        return await ApplyArCashAsync(manualDto, username, cancellationToken);
    }

    public async Task<List<CashApplicationResultDto>> AutoApplyApCashAsync(AutoApplyCashDto dto, string username, CancellationToken cancellationToken = default)
    {
        var payment = await _apRepository.GetByIdAsync(dto.PaymentTransactionId, cancellationToken);
        if (payment == null)
        {
            throw new AccountingNotFoundException($"معاملة السداد رقم ({dto.PaymentTransactionId}) غير موجودة.", "AP_PAYMENT_NOT_FOUND");
        }

        var availablePayment = Math.Abs(payment.OpenAmount);
        if (availablePayment <= 0)
        {
            throw new AccountingException("معاملة السداد مستهلكة بالكامل.", "PAYMENT_ALREADY_FULLY_APPLIED");
        }

        var openBills = await _apRepository.GetOpenBillsAsync(payment.VendorCode, cancellationToken);
        if (!openBills.Any())
        {
            throw new AccountingException($"لا توجد فواتير مشتريات مفتوحة غير مسددة للمورد ({payment.VendorCode}).", "NO_OPEN_BILLS");
        }

        var manualDto = new ApplyCashDto
        {
            PaymentTransactionId = payment.Id,
            Invoices = new List<InvoiceApplicationItemDto>()
        };

        var remaining = availablePayment;
        foreach (var bill in openBills)
        {
            if (remaining <= 0) break;
            var alloc = Math.Min(remaining, bill.OpenAmount);
            manualDto.Invoices.Add(new InvoiceApplicationItemDto
            {
                InvoiceTransactionId = bill.Id,
                AmountToApply = alloc,
                Notes = dto.Notes ?? "Auto FIFO Matching"
            });
            remaining -= alloc;
        }

        return await ApplyApCashAsync(manualDto, username, cancellationToken);
    }

    public async Task<bool> UnapplyArCashAsync(long applicationId, string username, CancellationToken cancellationToken = default)
    {
        var app = await _arRepository.GetApplicationByIdAsync(applicationId, cancellationToken);
        if (app == null)
        {
            throw new AccountingNotFoundException($"حركة التسوية رقم ({applicationId}) غير موجودة.", "CASH_APPLICATION_NOT_FOUND");
        }

        // Restore open amounts
        app.InvoiceTransaction.OpenAmount += app.AppliedAmount;
        app.InvoiceTransaction.LocalOpenAmount += app.LocalAppliedAmount;

        var paymentLocal = Math.Round(app.AppliedAmount * app.PaymentTransaction.ExchangeRate, 3);
        app.PaymentTransaction.OpenAmount -= app.AppliedAmount;
        app.PaymentTransaction.LocalOpenAmount -= paymentLocal;

        _arRepository.RemoveCashApplication(app);
        await _arRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Unapplied AR cash application {AppId} by {User}", applicationId, username);
        return true;
    }

    public async Task<bool> UnapplyApCashAsync(long applicationId, string username, CancellationToken cancellationToken = default)
    {
        var app = await _apRepository.GetApplicationByIdAsync(applicationId, cancellationToken);
        if (app == null)
        {
            throw new AccountingNotFoundException($"حركة التسوية رقم ({applicationId}) غير موجودة.", "CASH_APPLICATION_NOT_FOUND");
        }

        // Restore open amounts
        app.InvoiceTransaction.OpenAmount += app.AppliedAmount;
        app.InvoiceTransaction.LocalOpenAmount += app.LocalAppliedAmount;

        var paymentLocal = Math.Round(app.AppliedAmount * app.PaymentTransaction.ExchangeRate, 3);
        app.PaymentTransaction.OpenAmount -= app.AppliedAmount;
        app.PaymentTransaction.LocalOpenAmount -= paymentLocal;

        _apRepository.RemoveCashApplication(app);
        await _apRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Unapplied AP cash application {AppId} by {User}", applicationId, username);
        return true;
    }

    public async Task<IReadOnlyList<CashApplicationDetailDto>> GetArApplicationsByInvoiceAsync(long invoiceId, CancellationToken cancellationToken = default)
    {
        var apps = await _arRepository.GetApplicationsByInvoiceIdAsync(invoiceId, cancellationToken);
        return apps.Select(a => new CashApplicationDetailDto
        {
            Id = a.Id,
            PaymentTransactionId = a.PaymentTransactionId,
            PaymentVoucherNo = a.PaymentTransaction?.Voucher?.VoucherNo.ToString(),
            PaymentDate = a.PaymentTransaction?.TransactionDate ?? a.AppliedDate,
            InvoiceTransactionId = a.InvoiceTransactionId,
            InvoiceReferenceNo = a.InvoiceTransaction?.ReferenceNo,
            InvoiceDate = a.InvoiceTransaction?.TransactionDate ?? a.AppliedDate,
            AppliedAmount = a.AppliedAmount,
            LocalAppliedAmount = a.LocalAppliedAmount,
            AppliedDate = a.AppliedDate,
            Notes = a.Notes,
            CreationUser = a.CreationUser
        }).ToList();
    }

    public async Task<IReadOnlyList<CashApplicationDetailDto>> GetArApplicationsByPaymentAsync(long paymentId, CancellationToken cancellationToken = default)
    {
        var apps = await _arRepository.GetApplicationsByPaymentIdAsync(paymentId, cancellationToken);
        return apps.Select(a => new CashApplicationDetailDto
        {
            Id = a.Id,
            PaymentTransactionId = a.PaymentTransactionId,
            PaymentVoucherNo = a.PaymentTransaction?.Voucher?.VoucherNo.ToString(),
            PaymentDate = a.PaymentTransaction?.TransactionDate ?? a.AppliedDate,
            InvoiceTransactionId = a.InvoiceTransactionId,
            InvoiceReferenceNo = a.InvoiceTransaction?.ReferenceNo,
            InvoiceDate = a.InvoiceTransaction?.TransactionDate ?? a.AppliedDate,
            AppliedAmount = a.AppliedAmount,
            LocalAppliedAmount = a.LocalAppliedAmount,
            AppliedDate = a.AppliedDate,
            Notes = a.Notes,
            CreationUser = a.CreationUser
        }).ToList();
    }

    public async Task<IReadOnlyList<CashApplicationDetailDto>> GetApApplicationsByBillAsync(long billId, CancellationToken cancellationToken = default)
    {
        var apps = await _apRepository.GetApplicationsByBillIdAsync(billId, cancellationToken);
        return apps.Select(a => new CashApplicationDetailDto
        {
            Id = a.Id,
            PaymentTransactionId = a.PaymentTransactionId,
            PaymentVoucherNo = a.PaymentTransaction?.Voucher?.VoucherNo.ToString(),
            PaymentDate = a.PaymentTransaction?.TransactionDate ?? a.AppliedDate,
            InvoiceTransactionId = a.InvoiceTransactionId,
            InvoiceReferenceNo = a.InvoiceTransaction?.ReferenceNo,
            InvoiceDate = a.InvoiceTransaction?.TransactionDate ?? a.AppliedDate,
            AppliedAmount = a.AppliedAmount,
            LocalAppliedAmount = a.LocalAppliedAmount,
            AppliedDate = a.AppliedDate,
            Notes = a.Notes,
            CreationUser = a.CreationUser
        }).ToList();
    }

    public async Task<IReadOnlyList<CashApplicationDetailDto>> GetApApplicationsByPaymentAsync(long paymentId, CancellationToken cancellationToken = default)
    {
        var apps = await _apRepository.GetApplicationsByPaymentIdAsync(paymentId, cancellationToken);
        return apps.Select(a => new CashApplicationDetailDto
        {
            Id = a.Id,
            PaymentTransactionId = a.PaymentTransactionId,
            PaymentVoucherNo = a.PaymentTransaction?.Voucher?.VoucherNo.ToString(),
            PaymentDate = a.PaymentTransaction?.TransactionDate ?? a.AppliedDate,
            InvoiceTransactionId = a.InvoiceTransactionId,
            InvoiceReferenceNo = a.InvoiceTransaction?.ReferenceNo,
            InvoiceDate = a.InvoiceTransaction?.TransactionDate ?? a.AppliedDate,
            AppliedAmount = a.AppliedAmount,
            LocalAppliedAmount = a.LocalAppliedAmount,
            AppliedDate = a.AppliedDate,
            Notes = a.Notes,
            CreationUser = a.CreationUser
        }).ToList();
    }

    public async Task<StatementOfAccountDto> GetCustomerStatementAsync(string customerCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByCodeAsync(customerCode, cancellationToken);
        if (customer == null)
        {
            throw new AccountingNotFoundException($"العميل برمز ({customerCode}) غير موجود.", "CUSTOMER_NOT_FOUND");
        }

        var allTransactions = await _arRepository.GetTransactionsAsync(customerCode, null, toDate, onlyOpen: false, cancellationToken);

        var openingTx = allTransactions.Where(t => t.TransactionDate < fromDate);
        var openingBalance = openingTx.Sum(t => t.LocalAmount);

        var periodTx = allTransactions.Where(t => t.TransactionDate >= fromDate && t.TransactionDate <= toDate).OrderBy(t => t.TransactionDate).ToList();

        var running = openingBalance;
        var rows = new List<StatementRowDto>();

        foreach (var t in periodTx)
        {
            var debit = t.LocalAmount > 0 ? t.LocalAmount : 0;
            var credit = t.LocalAmount < 0 ? -t.LocalAmount : 0;
            running += t.LocalAmount;

            rows.Add(new StatementRowDto
            {
                Date = t.TransactionDate,
                TransactionType = t.TransactionType,
                ReferenceNo = t.ReferenceNo,
                Description = t.Description,
                Debit = debit,
                Credit = credit,
                RunningBalance = running
            });
        }

        return new StatementOfAccountDto
        {
            PartyCode = customer.CustomerCode,
            PartyNameAr = customer.NameAr,
            PartyNameEn = customer.NameEn,
            PartyType = "CUSTOMER",
            OpeningBalance = openingBalance,
            TotalDebit = rows.Sum(r => r.Debit),
            TotalCredit = rows.Sum(r => r.Credit),
            ClosingBalance = running,
            Rows = rows
        };
    }

    public async Task<StatementOfAccountDto> GetVendorStatementAsync(string vendorCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var vendor = await _vendorRepository.GetByCodeAsync(vendorCode, cancellationToken);
        if (vendor == null)
        {
            throw new AccountingNotFoundException($"المورد برمز ({vendorCode}) غير موجود.", "VENDOR_NOT_FOUND");
        }

        var allTransactions = await _apRepository.GetTransactionsAsync(vendorCode, null, toDate, onlyOpen: false, cancellationToken);

        var openingTx = allTransactions.Where(t => t.TransactionDate < fromDate);
        var openingBalance = openingTx.Sum(t => t.LocalAmount);

        var periodTx = allTransactions.Where(t => t.TransactionDate >= fromDate && t.TransactionDate <= toDate).OrderBy(t => t.TransactionDate).ToList();

        var running = openingBalance;
        var rows = new List<StatementRowDto>();

        foreach (var t in periodTx)
        {
            var credit = t.LocalAmount > 0 ? t.LocalAmount : 0;
            var debit = t.LocalAmount < 0 ? -t.LocalAmount : 0;
            running += t.LocalAmount;

            rows.Add(new StatementRowDto
            {
                Date = t.TransactionDate,
                TransactionType = t.TransactionType,
                ReferenceNo = t.ReferenceNo,
                Description = t.Description,
                Debit = debit,
                Credit = credit,
                RunningBalance = running
            });
        }

        return new StatementOfAccountDto
        {
            PartyCode = vendor.VendorCode,
            PartyNameAr = vendor.NameAr,
            PartyNameEn = vendor.NameEn,
            PartyType = "VENDOR",
            OpeningBalance = openingBalance,
            TotalDebit = rows.Sum(r => r.Debit),
            TotalCredit = rows.Sum(r => r.Credit),
            ClosingBalance = running,
            Rows = rows
        };
    }

    public async Task<AgingReportDto> GetArAgingReportAsync(DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var openTransactions = await _arRepository.GetTransactionsAsync(null, null, asOfDate, onlyOpen: true, cancellationToken);
        var customers = await _customerRepository.GetAllAsync(cancellationToken: cancellationToken);
        var custDict = customers.ToDictionary(c => c.CustomerCode);

        var grouped = openTransactions.Where(t => t.LocalOpenAmount > 0).GroupBy(t => t.CustomerCode);

        var buckets = new List<AgingBucketDto>();

        foreach (var g in grouped)
        {
            custDict.TryGetValue(g.Key, out var cust);
            var b = new AgingBucketDto
            {
                PartyCode = g.Key,
                PartyNameAr = cust?.NameAr ?? string.Empty,
                PartyNameEn = cust?.NameEn ?? string.Empty
            };

            foreach (var t in g)
            {
                var dueDate = t.DueDate ?? t.TransactionDate;
                var days = (asOfDate.Date - dueDate.Date).Days;

                if (days <= 30) b.CurrentAmount += t.LocalOpenAmount;
                else if (days <= 60) b.Days31To60 += t.LocalOpenAmount;
                else if (days <= 90) b.Days61To90 += t.LocalOpenAmount;
                else if (days <= 120) b.Days91To120 += t.LocalOpenAmount;
                else b.Over120Days += t.LocalOpenAmount;
            }

            b.TotalOutstanding = b.CurrentAmount + b.Days31To60 + b.Days61To90 + b.Days91To120 + b.Over120Days;
            buckets.Add(b);
        }

        return new AgingReportDto
        {
            SubledgerType = "AR",
            AsOfDate = asOfDate,
            TotalCurrent = buckets.Sum(b => b.CurrentAmount),
            Total31To60 = buckets.Sum(b => b.Days31To60),
            Total61To90 = buckets.Sum(b => b.Days61To90),
            Total91To120 = buckets.Sum(b => b.Days91To120),
            TotalOver120 = buckets.Sum(b => b.Over120Days),
            GrandTotal = buckets.Sum(b => b.TotalOutstanding),
            Buckets = buckets
        };
    }

    public async Task<AgingReportDto> GetApAgingReportAsync(DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var openTransactions = await _apRepository.GetTransactionsAsync(null, null, asOfDate, onlyOpen: true, cancellationToken);
        var vendors = await _vendorRepository.GetAllAsync(cancellationToken: cancellationToken);
        var vendDict = vendors.ToDictionary(v => v.VendorCode);

        var grouped = openTransactions.Where(t => t.LocalOpenAmount > 0).GroupBy(t => t.VendorCode);

        var buckets = new List<AgingBucketDto>();

        foreach (var g in grouped)
        {
            vendDict.TryGetValue(g.Key, out var vend);
            var b = new AgingBucketDto
            {
                PartyCode = g.Key,
                PartyNameAr = vend?.NameAr ?? string.Empty,
                PartyNameEn = vend?.NameEn ?? string.Empty
            };

            foreach (var t in g)
            {
                var dueDate = t.DueDate ?? t.TransactionDate;
                var days = (asOfDate.Date - dueDate.Date).Days;

                if (days <= 30) b.CurrentAmount += t.LocalOpenAmount;
                else if (days <= 60) b.Days31To60 += t.LocalOpenAmount;
                else if (days <= 90) b.Days61To90 += t.LocalOpenAmount;
                else if (days <= 120) b.Days91To120 += t.LocalOpenAmount;
                else b.Over120Days += t.LocalOpenAmount;
            }

            b.TotalOutstanding = b.CurrentAmount + b.Days31To60 + b.Days61To90 + b.Days91To120 + b.Over120Days;
            buckets.Add(b);
        }

        return new AgingReportDto
        {
            SubledgerType = "AP",
            AsOfDate = asOfDate,
            TotalCurrent = buckets.Sum(b => b.CurrentAmount),
            Total31To60 = buckets.Sum(b => b.Days31To60),
            Total61To90 = buckets.Sum(b => b.Days61To90),
            Total91To120 = buckets.Sum(b => b.Days91To120),
            TotalOver120 = buckets.Sum(b => b.Over120Days),
            GrandTotal = buckets.Sum(b => b.TotalOutstanding),
            Buckets = buckets
        };
    }

    public async Task<SubledgerReconciliationDto> ReconcileArAsync(long fiscalYearId, CancellationToken cancellationToken = default)
    {
        var glBalances = await _balanceRepository.GetBalancesAsync("112101", null, fiscalYearId, null, cancellationToken);
        var glTotal = glBalances.Sum(b => b.LocalClosingDebit - b.LocalClosingCredit);

        var subTx = await _arRepository.GetTransactionsAsync(null, null, null, onlyOpen: false, cancellationToken);
        var subTotal = subTx.Sum(t => t.LocalAmount);

        return new SubledgerReconciliationDto
        {
            SubledgerType = "AR",
            ControlAccountCode = "112101",
            GeneralLedgerBalance = glTotal,
            SubledgerTotalBalance = subTotal,
            Variance = glTotal - subTotal
        };
    }

    public async Task<SubledgerReconciliationDto> ReconcileApAsync(long fiscalYearId, CancellationToken cancellationToken = default)
    {
        var glBalances = await _balanceRepository.GetBalancesAsync("211101", null, fiscalYearId, null, cancellationToken);
        var glTotal = glBalances.Sum(b => b.LocalClosingCredit - b.LocalClosingDebit);

        var subTx = await _apRepository.GetTransactionsAsync(null, null, null, onlyOpen: false, cancellationToken);
        var subTotal = subTx.Sum(t => t.LocalAmount);

        return new SubledgerReconciliationDto
        {
            SubledgerType = "AP",
            ControlAccountCode = "211101",
            GeneralLedgerBalance = glTotal,
            SubledgerTotalBalance = subTotal,
            Variance = glTotal - subTotal
        };
    }
}
