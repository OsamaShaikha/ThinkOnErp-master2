using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Accounting.Subledger;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class ReceiptPaymentVoucherService : IReceiptPaymentVoucherService
{
    private readonly IGlVoucherService _voucherService;
    private readonly IGlVoucherRepository _voucherRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IVendorRepository _vendorRepository;
    private readonly IArSubledgerRepository _arRepository;
    private readonly IApSubledgerRepository _apRepository;
    private readonly ISubledgerService _subledgerService;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<ReceiptPaymentVoucherService> _logger;

    public ReceiptPaymentVoucherService(
        IGlVoucherService voucherService,
        IGlVoucherRepository voucherRepository,
        ICustomerRepository customerRepository,
        IVendorRepository vendorRepository,
        IArSubledgerRepository arRepository,
        IApSubledgerRepository apRepository,
        ISubledgerService subledgerService,
        ICurrentTenantContext tenantContext,
        ILogger<ReceiptPaymentVoucherService> logger)
    {
        _voucherService = voucherService;
        _voucherRepository = voucherRepository;
        _customerRepository = customerRepository;
        _vendorRepository = vendorRepository;
        _arRepository = arRepository;
        _apRepository = apRepository;
        _subledgerService = subledgerService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ReceiptVoucherDto> CreateReceiptVoucherAsync(CreateReceiptVoucherDto dto, string username, CancellationToken cancellationToken = default)
    {
        if (dto.TotalAmount <= 0)
        {
            throw new AccountingException("يجب أن يكون مبلغ سند القبض أكبر من الصفر.", "INVALID_RECEIPT_AMOUNT");
        }

        string creditAccountCode;
        string? partyType = null;
        string? partyCode = null;

        if (!string.IsNullOrWhiteSpace(dto.CustomerCode))
        {
            var customer = await _customerRepository.GetByCodeAsync(dto.CustomerCode, cancellationToken);
            if (customer == null)
            {
                throw new AccountingNotFoundException($"العميل برمز ({dto.CustomerCode}) غير موجود.", "CUSTOMER_NOT_FOUND");
            }
            creditAccountCode = customer.ArControlAccountCode;
            partyType = "CUSTOMER";
            partyCode = customer.CustomerCode;
        }
        else if (!string.IsNullOrWhiteSpace(dto.IncomeAccountCode))
        {
            creditAccountCode = dto.IncomeAccountCode;
        }
        else
        {
            throw new AccountingException("يجب تحديد العميل أو حساب الإيراد في سند القبض.", "RECEIPT_TARGET_REQUIRED");
        }

        var voucherDto = new CreateGlVoucherDto
        {
            BranchId = dto.BranchId,
            FiscalYearId = dto.FiscalYearId,
            VoucherType = 2, // RECEIPT
            VoucherDate = dto.VoucherDate,
            Description = dto.Description ?? $"سند قبض {(partyCode != null ? "- عميل " + partyCode : "")}",
            Details = new List<CreateGlVoucherDetailDto>
            {
                // Debit: Cash / Bank
                new()
                {
                    AccountCode = dto.CashOrBankAccountCode,
                    Debit = dto.TotalAmount,
                    Credit = 0,
                    Description = dto.Description ?? "قبض نقدي/بنكي",
                    CurrencyId = dto.CurrencyId,
                    ExchangeRate = dto.ExchangeRate,
                    BranchId = dto.BranchId
                },
                // Credit: Customer or Income
                new()
                {
                    AccountCode = creditAccountCode,
                    Debit = 0,
                    Credit = dto.TotalAmount,
                    Description = dto.Description,
                    CurrencyId = dto.CurrencyId,
                    ExchangeRate = dto.ExchangeRate,
                    PartyType = partyType,
                    PartyCode = partyCode,
                    CostCenterCode = dto.CostCenterCode,
                    BranchId = dto.BranchId
                }
            }
        };

        var created = await _voucherService.CreateVoucherAsync(voucherDto, username, cancellationToken);

        if (dto.AutoPost)
        {
            await _voucherService.PostVoucherAsync(created.Id, username, cancellationToken);

            // If invoices were requested for immediate settlement
            if (dto.InvoicesToSettle.Any() && !string.IsNullOrWhiteSpace(dto.CustomerCode))
            {
                var openPayments = await _arRepository.GetOpenPaymentsAsync(dto.CustomerCode, cancellationToken);
                var paymentTx = openPayments.FirstOrDefault(p => p.VoucherId == created.Id);
                if (paymentTx != null)
                {
                    await _subledgerService.ApplyArCashAsync(new ApplyCashDto
                    {
                        PaymentTransactionId = paymentTx.Id,
                        Invoices = dto.InvoicesToSettle.Select(i => new InvoiceApplicationItemDto
                        {
                            InvoiceTransactionId = i.InvoiceTransactionId,
                            AmountToApply = i.AmountToApply,
                            Notes = i.Notes ?? $"تسوية عند إصدار سند القبض رقم {created.VoucherNo}"
                        }).ToList()
                    }, username, cancellationToken);
                }
            }
        }

        return await GetReceiptVoucherByIdAsync(created.Id, cancellationToken);
    }

    public async Task<ReceiptVoucherDto> UpdateReceiptVoucherAsync(long voucherId, UpdateReceiptVoucherDto dto, string username, CancellationToken cancellationToken = default)
    {
        var voucher = await _voucherRepository.GetByIdAsync(voucherId, cancellationToken);
        if (voucher == null || voucher.VoucherType != 2)
        {
            throw new AccountingNotFoundException($"سند القبض رقم ({voucherId}) غير موجود.", "RECEIPT_NOT_FOUND");
        }

        if (voucher.Status == 3) // Posted
        {
            throw new AccountingException("لا يمكن تعديل سند قبض مُرحّل مباشرة. يجب إلغاء ترحيله أولاً أو عمل قيد عكسي.", "CANNOT_EDIT_POSTED_VOUCHER");
        }

        string creditAccountCode;
        string? partyType = null;
        string? partyCode = null;

        if (!string.IsNullOrWhiteSpace(dto.CustomerCode))
        {
            var customer = await _customerRepository.GetByCodeAsync(dto.CustomerCode, cancellationToken);
            if (customer == null)
            {
                throw new AccountingNotFoundException($"العميل برمز ({dto.CustomerCode}) غير موجود.", "CUSTOMER_NOT_FOUND");
            }
            creditAccountCode = customer.ArControlAccountCode;
            partyType = "CUSTOMER";
            partyCode = customer.CustomerCode;
        }
        else if (!string.IsNullOrWhiteSpace(dto.IncomeAccountCode))
        {
            creditAccountCode = dto.IncomeAccountCode;
        }
        else
        {
            throw new AccountingException("يجب تحديد العميل أو حساب الإيراد في سند القبض.", "RECEIPT_TARGET_REQUIRED");
        }

        voucher.VoucherDate = dto.VoucherDate;
        voucher.Description = dto.Description ?? $"سند قبض {(partyCode != null ? "- عميل " + partyCode : "")}";
        voucher.TotalAmount = dto.TotalAmount;
        voucher.TotalLocalDebit = dto.TotalAmount * dto.ExchangeRate;
        voucher.TotalLocalCredit = dto.TotalAmount * dto.ExchangeRate;
        voucher.UpdateUser = username;
        voucher.UpdateDate = DateTime.UtcNow;

        var debitLine = voucher.Details.FirstOrDefault(d => d.Debit > 0);
        if (debitLine != null)
        {
            debitLine.AccountCode = dto.CashOrBankAccountCode;
            debitLine.Debit = dto.TotalAmount;
            debitLine.LocalDebit = dto.TotalAmount * dto.ExchangeRate;
            debitLine.BaseDebit = dto.TotalAmount * dto.ExchangeRate;
            debitLine.Description = dto.Description ?? "قبض نقدي/بنكي";
            debitLine.CurrencyId = dto.CurrencyId;
            debitLine.ExchangeRate = dto.ExchangeRate;
        }

        var creditLine = voucher.Details.FirstOrDefault(d => d.Credit > 0);
        if (creditLine != null)
        {
            creditLine.AccountCode = creditAccountCode;
            creditLine.Credit = dto.TotalAmount;
            creditLine.LocalCredit = dto.TotalAmount * dto.ExchangeRate;
            creditLine.BaseCredit = dto.TotalAmount * dto.ExchangeRate;
            creditLine.Description = dto.Description;
            creditLine.CurrencyId = dto.CurrencyId;
            creditLine.ExchangeRate = dto.ExchangeRate;
            creditLine.PartyType = partyType;
            creditLine.PartyCode = partyCode;
            creditLine.CostCenterCode = dto.CostCenterCode;
        }

        await _voucherRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated receipt voucher {VoucherId} by {User}", voucherId, username);

        return await GetReceiptVoucherByIdAsync(voucherId, cancellationToken);
    }

    public async Task<bool> DeleteReceiptVoucherAsync(long voucherId, string username, CancellationToken cancellationToken = default)
    {
        var voucher = await _voucherRepository.GetByIdAsync(voucherId, cancellationToken);
        if (voucher == null || voucher.VoucherType != 2)
        {
            throw new AccountingNotFoundException($"سند القبض رقم ({voucherId}) غير موجود.", "RECEIPT_NOT_FOUND");
        }

        if (voucher.Status == 3) // Posted -> Reverse & unapply
        {
            var creditLine = voucher.Details.FirstOrDefault(d => d.Credit > 0);
            if (!string.IsNullOrWhiteSpace(creditLine?.PartyCode))
            {
                var openPayments = await _arRepository.GetTransactionsAsync(creditLine.PartyCode, null, null, onlyOpen: false, cancellationToken);
                var payTx = openPayments.FirstOrDefault(t => t.VoucherId == voucher.Id);
                if (payTx != null)
                {
                    var apps = await _arRepository.GetApplicationsByPaymentIdAsync(payTx.Id, cancellationToken);
                    foreach (var app in apps)
                    {
                        await _subledgerService.UnapplyArCashAsync(app.Id, username, cancellationToken);
                    }
                }
            }

            await _voucherService.ReverseVoucherAsync(voucherId, username, "إلغاء سند القبض", cancellationToken);
            _logger.LogInformation("Reversed posted receipt voucher {VoucherId} by {User}", voucherId, username);
            return true;
        }

        voucher.Status = 4; // Voided
        voucher.UpdateUser = username;
        voucher.UpdateDate = DateTime.UtcNow;
        await _voucherRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Voided draft receipt voucher {VoucherId} by {User}", voucherId, username);
        return true;
    }

    public async Task<ReceiptVoucherDto> GetReceiptVoucherByIdAsync(long voucherId, CancellationToken cancellationToken = default)
    {
        var voucher = await _voucherRepository.GetByIdAsync(voucherId, cancellationToken);
        if (voucher == null || voucher.VoucherType != 2)
        {
            throw new AccountingNotFoundException($"سند القبض رقم ({voucherId}) غير موجود.", "RECEIPT_NOT_FOUND");
        }

        var debitLine = voucher.Details.FirstOrDefault(d => d.Debit > 0);
        var creditLine = voucher.Details.FirstOrDefault(d => d.Credit > 0);

        Customer? customer = null;
        if (!string.IsNullOrWhiteSpace(creditLine?.PartyCode))
        {
            customer = await _customerRepository.GetByCodeAsync(creditLine.PartyCode, cancellationToken);
        }

        // Get settled invoices if posted
        var settled = new List<ReceiptSettledInvoiceDto>();
        if (voucher.Status == 3 && customer != null)
        {
            var openPayments = await _arRepository.GetTransactionsAsync(customer.CustomerCode, null, null, onlyOpen: false, cancellationToken);
            var payTx = openPayments.FirstOrDefault(t => t.VoucherId == voucher.Id);
            if (payTx != null)
            {
                var apps = await _arRepository.GetApplicationsByPaymentIdAsync(payTx.Id, cancellationToken);
                settled = apps.Select(a => new ReceiptSettledInvoiceDto
                {
                    ApplicationId = a.Id,
                    InvoiceTransactionId = a.InvoiceTransactionId,
                    InvoiceReferenceNo = a.InvoiceTransaction?.ReferenceNo,
                    AppliedAmount = a.AppliedAmount,
                    LocalAppliedAmount = a.LocalAppliedAmount,
                    AppliedDate = a.AppliedDate
                }).ToList();
            }
        }

        return new ReceiptVoucherDto
        {
            VoucherId = voucher.Id,
            VoucherNo = voucher.VoucherNo,
            BranchId = voucher.BranchId,
            BranchNameAr = voucher.Details.FirstOrDefault()?.Branch?.BranchNameAr,
            BranchNameEn = voucher.Details.FirstOrDefault()?.Branch?.BranchNameEn,
            VoucherDate = voucher.VoucherDate,
            CashOrBankAccountCode = debitLine?.AccountCode ?? string.Empty,
            CashOrBankAccountNameAr = debitLine?.Account?.AccountNameAr,
            CashOrBankAccountNameEn = debitLine?.Account?.AccountNameEn,
            TotalAmount = voucher.TotalAmount,
            LocalAmount = voucher.TotalLocalDebit,
            CurrencyId = debitLine?.CurrencyId ?? 1,
            ExchangeRate = debitLine?.ExchangeRate ?? 1.0m,
            Description = voucher.Description,
            CustomerCode = customer?.CustomerCode,
            CustomerNameAr = customer?.NameAr,
            CustomerNameEn = customer?.NameEn,
            IncomeAccountCode = customer == null ? creditLine?.AccountCode : null,
            IncomeAccountNameAr = customer == null ? creditLine?.Account?.AccountNameAr : null,
            IncomeAccountNameEn = customer == null ? creditLine?.Account?.AccountNameEn : null,
            Status = voucher.Status,
            StatusName = voucher.Status switch { 1 => "مسودة", 2 => "مُراجع", 3 => "مُرحّل", 4 => "معكوس", _ => "غير محدد" },
            CreationUser = voucher.CreationUser,
            CreationDate = voucher.CreationDate,
            SettledInvoices = settled
        };
    }

    public async Task<IReadOnlyList<ReceiptVoucherDto>> GetReceiptVouchersAsync(
        long? branchId,
        DateTime? fromDate,
        DateTime? toDate,
        string? customerCode,
        CancellationToken cancellationToken = default)
    {
        var result = await _voucherRepository.GetPagedVouchersAsync(
            branchId: branchId,
            year: null,
            month: null,
            typeCode: 2, // RECEIPT
            status: null,
            fromDate: fromDate,
            toDate: toDate,
            searchKeyword: null,
            pageIndex: 1,
            pageSize: 500,
            cancellationToken: cancellationToken);

        var list = new List<ReceiptVoucherDto>();
        foreach (var v in result.Items)
        {
            var debitLine = v.Details.FirstOrDefault(d => d.Debit > 0);
            var creditLine = v.Details.FirstOrDefault(d => d.Credit > 0);

            if (!string.IsNullOrWhiteSpace(customerCode) && creditLine?.PartyCode != customerCode)
            {
                continue;
            }

            list.Add(new ReceiptVoucherDto
            {
                VoucherId = v.Id,
                VoucherNo = v.VoucherNo,
                BranchId = v.BranchId,
                BranchNameAr = v.Details.FirstOrDefault()?.Branch?.BranchNameAr,
                BranchNameEn = v.Details.FirstOrDefault()?.Branch?.BranchNameEn,
                VoucherDate = v.VoucherDate,
                CashOrBankAccountCode = debitLine?.AccountCode ?? string.Empty,
                CashOrBankAccountNameAr = debitLine?.Account?.AccountNameAr,
                CashOrBankAccountNameEn = debitLine?.Account?.AccountNameEn,
                TotalAmount = v.TotalAmount,
                LocalAmount = v.TotalLocalDebit,
                CurrencyId = debitLine?.CurrencyId ?? 1,
                ExchangeRate = debitLine?.ExchangeRate ?? 1.0m,
                Description = v.Description,
                CustomerCode = creditLine?.PartyCode,
                IncomeAccountCode = creditLine?.PartyCode == null ? creditLine?.AccountCode : null,
                IncomeAccountNameAr = creditLine?.PartyCode == null ? creditLine?.Account?.AccountNameAr : null,
                Status = v.Status,
                StatusName = v.Status switch { 1 => "مسودة", 2 => "مُراجع", 3 => "مُرحّل", 4 => "معكوس", _ => "غير محدد" },
                CreationUser = v.CreationUser,
                CreationDate = v.CreationDate
            });
        }

        return list;
    }

    public async Task<PaymentVoucherDto> CreatePaymentVoucherAsync(CreatePaymentVoucherDto dto, string username, CancellationToken cancellationToken = default)
    {
        if (dto.TotalAmount <= 0)
        {
            throw new AccountingException("يجب أن يكون مبلغ سند الصرف أكبر من الصفر.", "INVALID_PAYMENT_AMOUNT");
        }

        string debitAccountCode;
        string? partyType = null;
        string? partyCode = null;

        if (!string.IsNullOrWhiteSpace(dto.VendorCode))
        {
            var vendor = await _vendorRepository.GetByCodeAsync(dto.VendorCode, cancellationToken);
            if (vendor == null)
            {
                throw new AccountingNotFoundException($"المورد برمز ({dto.VendorCode}) غير موجود.", "VENDOR_NOT_FOUND");
            }
            debitAccountCode = vendor.ApControlAccountCode;
            partyType = "VENDOR";
            partyCode = vendor.VendorCode;
        }
        else if (!string.IsNullOrWhiteSpace(dto.ExpenseAccountCode))
        {
            debitAccountCode = dto.ExpenseAccountCode;
        }
        else
        {
            throw new AccountingException("يجب تحديد المورد أو حساب المصروف في سند الصرف.", "PAYMENT_TARGET_REQUIRED");
        }

        var voucherDto = new CreateGlVoucherDto
        {
            BranchId = dto.BranchId,
            FiscalYearId = dto.FiscalYearId,
            VoucherType = 3, // PAYMENT
            VoucherDate = dto.VoucherDate,
            Description = dto.Description ?? $"سند صرف {(partyCode != null ? "- مورد " + partyCode : "")}",
            Details = new List<CreateGlVoucherDetailDto>
            {
                // Debit: Vendor or Expense
                new()
                {
                    AccountCode = debitAccountCode,
                    Debit = dto.TotalAmount,
                    Credit = 0,
                    Description = dto.Description,
                    CurrencyId = dto.CurrencyId,
                    ExchangeRate = dto.ExchangeRate,
                    PartyType = partyType,
                    PartyCode = partyCode,
                    CostCenterCode = dto.CostCenterCode,
                    BranchId = dto.BranchId
                },
                // Credit: Cash / Bank
                new()
                {
                    AccountCode = dto.CashOrBankAccountCode,
                    Debit = 0,
                    Credit = dto.TotalAmount,
                    Description = dto.Description ?? "صرف نقدي/بنكي",
                    CurrencyId = dto.CurrencyId,
                    ExchangeRate = dto.ExchangeRate,
                    BranchId = dto.BranchId
                }
            }
        };

        var created = await _voucherService.CreateVoucherAsync(voucherDto, username, cancellationToken);

        if (dto.AutoPost)
        {
            await _voucherService.PostVoucherAsync(created.Id, username, cancellationToken);

            // If bills were requested for immediate settlement
            if (dto.BillsToSettle.Any() && !string.IsNullOrWhiteSpace(dto.VendorCode))
            {
                var openPayments = await _apRepository.GetOpenPaymentsAsync(dto.VendorCode, cancellationToken);
                var paymentTx = openPayments.FirstOrDefault(p => p.VoucherId == created.Id);
                if (paymentTx != null)
                {
                    await _subledgerService.ApplyApCashAsync(new ApplyCashDto
                    {
                        PaymentTransactionId = paymentTx.Id,
                        Invoices = dto.BillsToSettle.Select(b => new InvoiceApplicationItemDto
                        {
                            InvoiceTransactionId = b.BillTransactionId,
                            AmountToApply = b.AmountToApply,
                            Notes = b.Notes ?? $"تسوية عند إصدار سند الصرف رقم {created.VoucherNo}"
                        }).ToList()
                    }, username, cancellationToken);
                }
            }
        }

        return await GetPaymentVoucherByIdAsync(created.Id, cancellationToken);
    }

    public async Task<PaymentVoucherDto> UpdatePaymentVoucherAsync(long voucherId, UpdatePaymentVoucherDto dto, string username, CancellationToken cancellationToken = default)
    {
        var voucher = await _voucherRepository.GetByIdAsync(voucherId, cancellationToken);
        if (voucher == null || voucher.VoucherType != 3)
        {
            throw new AccountingNotFoundException($"سند الصرف رقم ({voucherId}) غير موجود.", "PAYMENT_NOT_FOUND");
        }

        if (voucher.Status == 3) // Posted
        {
            throw new AccountingException("لا يمكن تعديل سند صرف مُرحّل مباشرة. يجب إلغاء ترحيله أولاً أو عمل قيد عكسي.", "CANNOT_EDIT_POSTED_VOUCHER");
        }

        string debitAccountCode;
        string? partyType = null;
        string? partyCode = null;

        if (!string.IsNullOrWhiteSpace(dto.VendorCode))
        {
            var vendor = await _vendorRepository.GetByCodeAsync(dto.VendorCode, cancellationToken);
            if (vendor == null)
            {
                throw new AccountingNotFoundException($"المورد برمز ({dto.VendorCode}) غير موجود.", "VENDOR_NOT_FOUND");
            }
            debitAccountCode = vendor.ApControlAccountCode;
            partyType = "VENDOR";
            partyCode = vendor.VendorCode;
        }
        else if (!string.IsNullOrWhiteSpace(dto.ExpenseAccountCode))
        {
            debitAccountCode = dto.ExpenseAccountCode;
        }
        else
        {
            throw new AccountingException("يجب تحديد المورد أو حساب المصروف في سند الصرف.", "PAYMENT_TARGET_REQUIRED");
        }

        voucher.VoucherDate = dto.VoucherDate;
        voucher.Description = dto.Description ?? $"سند صرف {(partyCode != null ? "- مورد " + partyCode : "")}";
        voucher.TotalAmount = dto.TotalAmount;
        voucher.TotalLocalDebit = dto.TotalAmount * dto.ExchangeRate;
        voucher.TotalLocalCredit = dto.TotalAmount * dto.ExchangeRate;
        voucher.UpdateUser = username;
        voucher.UpdateDate = DateTime.UtcNow;

        var debitLine = voucher.Details.FirstOrDefault(d => d.Debit > 0);
        if (debitLine != null)
        {
            debitLine.AccountCode = debitAccountCode;
            debitLine.Debit = dto.TotalAmount;
            debitLine.LocalDebit = dto.TotalAmount * dto.ExchangeRate;
            debitLine.BaseDebit = dto.TotalAmount * dto.ExchangeRate;
            debitLine.Description = dto.Description;
            debitLine.CurrencyId = dto.CurrencyId;
            debitLine.ExchangeRate = dto.ExchangeRate;
            debitLine.PartyType = partyType;
            debitLine.PartyCode = partyCode;
            debitLine.CostCenterCode = dto.CostCenterCode;
        }

        var creditLine = voucher.Details.FirstOrDefault(d => d.Credit > 0);
        if (creditLine != null)
        {
            creditLine.AccountCode = dto.CashOrBankAccountCode;
            creditLine.Credit = dto.TotalAmount;
            creditLine.LocalCredit = dto.TotalAmount * dto.ExchangeRate;
            creditLine.BaseCredit = dto.TotalAmount * dto.ExchangeRate;
            creditLine.Description = dto.Description ?? "صرف نقدي/بنكي";
            creditLine.CurrencyId = dto.CurrencyId;
            creditLine.ExchangeRate = dto.ExchangeRate;
        }

        await _voucherRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated payment voucher {VoucherId} by {User}", voucherId, username);

        return await GetPaymentVoucherByIdAsync(voucherId, cancellationToken);
    }

    public async Task<bool> DeletePaymentVoucherAsync(long voucherId, string username, CancellationToken cancellationToken = default)
    {
        var voucher = await _voucherRepository.GetByIdAsync(voucherId, cancellationToken);
        if (voucher == null || voucher.VoucherType != 3)
        {
            throw new AccountingNotFoundException($"سند الصرف رقم ({voucherId}) غير موجود.", "PAYMENT_NOT_FOUND");
        }

        if (voucher.Status == 3) // Posted -> Reverse & unapply
        {
            var debitLine = voucher.Details.FirstOrDefault(d => d.Debit > 0);
            if (!string.IsNullOrWhiteSpace(debitLine?.PartyCode))
            {
                var openPayments = await _apRepository.GetTransactionsAsync(debitLine.PartyCode, null, null, onlyOpen: false, cancellationToken);
                var payTx = openPayments.FirstOrDefault(t => t.VoucherId == voucher.Id);
                if (payTx != null)
                {
                    var apps = await _apRepository.GetApplicationsByPaymentIdAsync(payTx.Id, cancellationToken);
                    foreach (var app in apps)
                    {
                        await _subledgerService.UnapplyApCashAsync(app.Id, username, cancellationToken);
                    }
                }
            }

            await _voucherService.ReverseVoucherAsync(voucherId, username, "إلغاء سند الصرف", cancellationToken);
            _logger.LogInformation("Reversed posted payment voucher {VoucherId} by {User}", voucherId, username);
            return true;
        }

        voucher.Status = 4; // Voided
        voucher.UpdateUser = username;
        voucher.UpdateDate = DateTime.UtcNow;
        await _voucherRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Voided draft payment voucher {VoucherId} by {User}", voucherId, username);
        return true;
    }

    public async Task<PaymentVoucherDto> GetPaymentVoucherByIdAsync(long voucherId, CancellationToken cancellationToken = default)
    {
        var voucher = await _voucherRepository.GetByIdAsync(voucherId, cancellationToken);
        if (voucher == null || voucher.VoucherType != 3)
        {
            throw new AccountingNotFoundException($"سند الصرف رقم ({voucherId}) غير موجود.", "PAYMENT_NOT_FOUND");
        }

        var debitLine = voucher.Details.FirstOrDefault(d => d.Debit > 0);
        var creditLine = voucher.Details.FirstOrDefault(d => d.Credit > 0);

        Vendor? vendor = null;
        if (!string.IsNullOrWhiteSpace(debitLine?.PartyCode))
        {
            vendor = await _vendorRepository.GetByCodeAsync(debitLine.PartyCode, cancellationToken);
        }

        // Get settled bills if posted
        var settled = new List<PaymentSettledBillDto>();
        if (voucher.Status == 3 && vendor != null)
        {
            var openPayments = await _apRepository.GetTransactionsAsync(vendor.VendorCode, null, null, onlyOpen: false, cancellationToken);
            var payTx = openPayments.FirstOrDefault(t => t.VoucherId == voucher.Id);
            if (payTx != null)
            {
                var apps = await _apRepository.GetApplicationsByPaymentIdAsync(payTx.Id, cancellationToken);
                settled = apps.Select(a => new PaymentSettledBillDto
                {
                    ApplicationId = a.Id,
                    BillTransactionId = a.InvoiceTransactionId,
                    BillReferenceNo = a.InvoiceTransaction?.ReferenceNo,
                    AppliedAmount = a.AppliedAmount,
                    LocalAppliedAmount = a.LocalAppliedAmount,
                    AppliedDate = a.AppliedDate
                }).ToList();
            }
        }

        return new PaymentVoucherDto
        {
            VoucherId = voucher.Id,
            VoucherNo = voucher.VoucherNo,
            BranchId = voucher.BranchId,
            BranchNameAr = voucher.Details.FirstOrDefault()?.Branch?.BranchNameAr,
            BranchNameEn = voucher.Details.FirstOrDefault()?.Branch?.BranchNameEn,
            VoucherDate = voucher.VoucherDate,
            CashOrBankAccountCode = creditLine?.AccountCode ?? string.Empty,
            CashOrBankAccountNameAr = creditLine?.Account?.AccountNameAr,
            CashOrBankAccountNameEn = creditLine?.Account?.AccountNameEn,
            TotalAmount = voucher.TotalAmount,
            LocalAmount = voucher.TotalLocalDebit,
            CurrencyId = creditLine?.CurrencyId ?? 1,
            ExchangeRate = creditLine?.ExchangeRate ?? 1.0m,
            Description = voucher.Description,
            VendorCode = vendor?.VendorCode,
            VendorNameAr = vendor?.NameAr,
            VendorNameEn = vendor?.NameEn,
            ExpenseAccountCode = vendor == null ? debitLine?.AccountCode : null,
            ExpenseAccountNameAr = vendor == null ? debitLine?.Account?.AccountNameAr : null,
            ExpenseAccountNameEn = vendor == null ? debitLine?.Account?.AccountNameEn : null,
            CostCenterCode = debitLine?.CostCenterCode,
            CostCenterNameAr = debitLine?.CostCenter?.NameAr,
            Status = voucher.Status,
            StatusName = voucher.Status switch { 1 => "مسودة", 2 => "مُراجع", 3 => "مُرحّل", 4 => "معكوس", _ => "غير محدد" },
            CreationUser = voucher.CreationUser,
            CreationDate = voucher.CreationDate,
            SettledBills = settled
        };
    }

    public async Task<IReadOnlyList<PaymentVoucherDto>> GetPaymentVouchersAsync(
        long? branchId,
        DateTime? fromDate,
        DateTime? toDate,
        string? vendorCode,
        CancellationToken cancellationToken = default)
    {
        var result = await _voucherRepository.GetPagedVouchersAsync(
            branchId: branchId,
            year: null,
            month: null,
            typeCode: 3, // PAYMENT
            status: null,
            fromDate: fromDate,
            toDate: toDate,
            searchKeyword: null,
            pageIndex: 1,
            pageSize: 500,
            cancellationToken: cancellationToken);

        var list = new List<PaymentVoucherDto>();
        foreach (var v in result.Items)
        {
            var debitLine = v.Details.FirstOrDefault(d => d.Debit > 0);
            var creditLine = v.Details.FirstOrDefault(d => d.Credit > 0);

            if (!string.IsNullOrWhiteSpace(vendorCode) && debitLine?.PartyCode != vendorCode)
            {
                continue;
            }

            list.Add(new PaymentVoucherDto
            {
                VoucherId = v.Id,
                VoucherNo = v.VoucherNo,
                BranchId = v.BranchId,
                BranchNameAr = v.Details.FirstOrDefault()?.Branch?.BranchNameAr,
                BranchNameEn = v.Details.FirstOrDefault()?.Branch?.BranchNameEn,
                VoucherDate = v.VoucherDate,
                CashOrBankAccountCode = creditLine?.AccountCode ?? string.Empty,
                CashOrBankAccountNameAr = creditLine?.Account?.AccountNameAr,
                CashOrBankAccountNameEn = creditLine?.Account?.AccountNameEn,
                TotalAmount = v.TotalAmount,
                LocalAmount = v.TotalLocalDebit,
                CurrencyId = creditLine?.CurrencyId ?? 1,
                ExchangeRate = creditLine?.ExchangeRate ?? 1.0m,
                Description = v.Description,
                VendorCode = debitLine?.PartyCode,
                ExpenseAccountCode = debitLine?.PartyCode == null ? debitLine?.AccountCode : null,
                ExpenseAccountNameAr = debitLine?.PartyCode == null ? debitLine?.Account?.AccountNameAr : null,
                CostCenterCode = debitLine?.CostCenterCode,
                Status = v.Status,
                StatusName = v.Status switch { 1 => "مسودة", 2 => "مُراجع", 3 => "مُرحّل", 4 => "معكوس", _ => "غير محدد" },
                CreationUser = v.CreationUser,
                CreationDate = v.CreationDate
            });
        }

        return list;
    }
}
