using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Accounting.Pdc;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class PdcService : IPdcService
{
    private readonly IPdcRepository _pdcRepository;
    private readonly IGlVoucherService _voucherService;
    private readonly ICustomerRepository _customerRepository;
    private readonly IVendorRepository _vendorRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<PdcService> _logger;

    public PdcService(
        IPdcRepository pdcRepository,
        IGlVoucherService voucherService,
        ICustomerRepository customerRepository,
        IVendorRepository vendorRepository,
        ICurrentTenantContext tenantContext,
        ILogger<PdcService> logger)
    {
        _pdcRepository = pdcRepository;
        _voucherService = voucherService;
        _customerRepository = customerRepository;
        _vendorRepository = vendorRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<PdcRegisterDto> CreatePdcAsync(CreatePdcDto dto, string username, CancellationToken cancellationToken = default)
    {
        if (dto.Amount <= 0)
        {
            throw new AccountingException("يجب أن يكون مبلغ الشيك أكبر من الصفر.", "INVALID_CHEQUE_AMOUNT");
        }

        if (string.IsNullOrWhiteSpace(dto.ChequeNumber))
        {
            throw new AccountingException("رقم الشيك إجباري.", "CHEQUE_NUMBER_REQUIRED");
        }

        var existing = await _pdcRepository.GetByChequeNumberAsync(dto.ChequeNumber, dto.ChequeType, cancellationToken);
        if (existing != null)
        {
            throw new AccountingException($"الشيك رقم ({dto.ChequeNumber}) مسجل مسبقاً في سجل الشيكات.", "DUPLICATE_CHEQUE_NUMBER");
        }

        string intermediateAcc = dto.IntermediateAccountCode ?? (dto.ChequeType == "RECEIVED" ? "111301" : "211201");
        decimal localAmt = Math.Round(dto.Amount * dto.ExchangeRate, 3);

        var pdc = new GlPdcRegister
        {
            BranchId = dto.BranchId,
            FiscalYearId = dto.FiscalYearId,
            ChequeType = dto.ChequeType.ToUpper(),
            ChequeNumber = dto.ChequeNumber,
            ChequeDate = dto.ChequeDate,
            DueDate = dto.DueDate,
            Amount = dto.Amount,
            LocalAmount = localAmt,
            CurrencyId = dto.CurrencyId,
            ExchangeRate = dto.ExchangeRate,
            DrawerBankName = dto.DrawerBankName,
            DrawerBankAccountNo = dto.DrawerBankAccountNo,
            BeneficiaryName = dto.BeneficiaryName,
            PartyType = dto.PartyType?.ToUpper(),
            PartyCode = dto.PartyCode,
            Status = "RECEIVED",
            IntermediateAccountCode = intermediateAcc,
            Notes = dto.Notes,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        await _pdcRepository.AddAsync(pdc, cancellationToken);
        await _pdcRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created PDC {ChequeNumber} of type {Type} by {User}", pdc.ChequeNumber, pdc.ChequeType, username);

        return await GetByIdAsync(pdc.Id, cancellationToken);
    }

    public async Task<PdcRegisterDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var pdc = await _pdcRepository.GetByIdAsync(id, cancellationToken);
        if (pdc == null)
        {
            throw new AccountingNotFoundException($"الشيك رقم ({id}) غير موجود بالسجل.", "PDC_NOT_FOUND");
        }

        string partyNameLocal = string.Empty;
        string partyNameEn = string.Empty;

        if (pdc.PartyType == "CUSTOMER" && !string.IsNullOrWhiteSpace(pdc.PartyCode))
        {
            var cust = await _customerRepository.GetByCodeAsync(pdc.PartyCode, cancellationToken);
            partyNameLocal = cust?.NameLocal ?? string.Empty;
            partyNameEn = cust?.NameEn ?? string.Empty;
        }
        else if (pdc.PartyType == "VENDOR" && !string.IsNullOrWhiteSpace(pdc.PartyCode))
        {
            var vend = await _vendorRepository.GetByCodeAsync(pdc.PartyCode, cancellationToken);
            partyNameLocal = vend?.NameLocal ?? string.Empty;
            partyNameEn = vend?.NameEn ?? string.Empty;
        }

        return MapToDto(pdc, partyNameLocal, partyNameEn);
    }

    public async Task<IReadOnlyList<PdcRegisterDto>> GetChequesAsync(PdcFilterDto filter, CancellationToken cancellationToken = default)
    {
        var cheques = await _pdcRepository.GetChequesAsync(
            filter.BranchId,
            filter.ChequeType,
            filter.Status,
            filter.PartyCode,
            filter.FromDueDate,
            filter.ToDueDate,
            cancellationToken);

        return cheques.Select(c => MapToDto(c, string.Empty, string.Empty)).ToList();
    }

    public async Task<PdcRegisterDto> DepositChequeAsync(long id, DepositPdcDto dto, string username, CancellationToken cancellationToken = default)
    {
        var pdc = await _pdcRepository.GetByIdAsync(id, cancellationToken);
        if (pdc == null)
        {
            throw new AccountingNotFoundException($"الشيك رقم ({id}) غير موجود بالسجل.", "PDC_NOT_FOUND");
        }

        if (pdc.Status != "RECEIVED")
        {
            throw new AccountingException($"لا يمكن إيداع شيك حالته ({pdc.Status}). يجب أن يكون الشيك بحالة مستلم (RECEIVED).", "INVALID_PDC_STATUS");
        }

        pdc.Status = "DEPOSITED";
        pdc.DepositBankAccountCode = dto.DepositBankAccountCode;
        pdc.DepositDate = dto.DepositDate;
        if (!string.IsNullOrWhiteSpace(dto.Notes)) pdc.Notes += " | " + dto.Notes;
        pdc.UpdateUser = username;
        pdc.UpdateDate = DateTime.UtcNow;

        await _pdcRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deposited PDC {ChequeNumber} in bank {Bank} by {User}", pdc.ChequeNumber, dto.DepositBankAccountCode, username);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<PdcRegisterDto> ClearChequeAsync(long id, ClearPdcDto dto, string username, CancellationToken cancellationToken = default)
    {
        var pdc = await _pdcRepository.GetByIdAsync(id, cancellationToken);
        if (pdc == null)
        {
            throw new AccountingNotFoundException($"الشيك رقم ({id}) غير موجود بالسجل.", "PDC_NOT_FOUND");
        }

        if (pdc.Status != "RECEIVED" && pdc.Status != "DEPOSITED" && pdc.Status != "ISSUED")
        {
            throw new AccountingException($"لا يمكن تحصيل شيك حالته ({pdc.Status}).", "INVALID_PDC_STATUS");
        }

        string bankAcc = dto.DepositBankAccountCode ?? pdc.DepositBankAccountCode ?? "111201";
        string intermediateAcc = pdc.IntermediateAccountCode ?? (pdc.ChequeType == "RECEIVED" ? "111301" : "211201");

        // Build Clearing GL Voucher
        CreateGlVoucherDto voucherDto;
        if (pdc.ChequeType == "RECEIVED")
        {
            // Debit: Current Bank, Credit: Cheques Under Collection
            voucherDto = new CreateGlVoucherDto
            {
                BranchId = pdc.BranchId,
                FiscalYearId = pdc.FiscalYearId,
                VoucherType = 1, // Journal Voucher (JV)
                VoucherDate = dto.ClearedDate,
                Description = $"تحصيل الشيك رقم {pdc.ChequeNumber} - بنك {pdc.DrawerBankName}",
                Details = new List<CreateGlVoucherDetailDto>
                {
                    new()
                    {
                        AccountCode = bankAcc,
                        Debit = pdc.Amount,
                        Credit = 0,
                        Description = $"إيداع تحصيل شيك رقم {pdc.ChequeNumber}",
                        CurrencyId = pdc.CurrencyId,
                        ExchangeRate = pdc.ExchangeRate,
                        BranchId = pdc.BranchId
                    },
                    new()
                    {
                        AccountCode = intermediateAcc,
                        Debit = 0,
                        Credit = pdc.Amount,
                        Description = $"إقفال شيك تحت التحصيل رقم {pdc.ChequeNumber}",
                        CurrencyId = pdc.CurrencyId,
                        ExchangeRate = pdc.ExchangeRate,
                        BranchId = pdc.BranchId
                    }
                }
            };
        }
        else
        {
            // Outward Cheque Cleared: Debit: Cheques Payable, Credit: Current Bank
            voucherDto = new CreateGlVoucherDto
            {
                BranchId = pdc.BranchId,
                FiscalYearId = pdc.FiscalYearId,
                VoucherType = 1, // Journal Voucher (JV)
                VoucherDate = dto.ClearedDate,
                Description = $"صرف الشيك الصادر رقم {pdc.ChequeNumber} - مستفيد {pdc.BeneficiaryName}",
                Details = new List<CreateGlVoucherDetailDto>
                {
                    new()
                    {
                        AccountCode = intermediateAcc,
                        Debit = pdc.Amount,
                        Credit = 0,
                        Description = $"إقفال شيكات صادرة رقم {pdc.ChequeNumber}",
                        CurrencyId = pdc.CurrencyId,
                        ExchangeRate = pdc.ExchangeRate,
                        BranchId = pdc.BranchId
                    },
                    new()
                    {
                        AccountCode = bankAcc,
                        Debit = 0,
                        Credit = pdc.Amount,
                        Description = $"خصم من البنك للشيك الصادر رقم {pdc.ChequeNumber}",
                        CurrencyId = pdc.CurrencyId,
                        ExchangeRate = pdc.ExchangeRate,
                        BranchId = pdc.BranchId
                    }
                }
            };
        }

        var voucher = await _voucherService.CreateVoucherAsync(voucherDto, username, cancellationToken);
        await _voucherService.PostVoucherAsync(voucher.Id, username, cancellationToken);

        pdc.Status = "CLEARED";
        pdc.DepositBankAccountCode = bankAcc;
        pdc.ClearedDate = dto.ClearedDate;
        pdc.ClearingVoucherId = voucher.Id;
        pdc.UpdateUser = username;
        pdc.UpdateDate = DateTime.UtcNow;

        await _pdcRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cleared PDC {ChequeNumber}, created GL voucher {VoucherId} by {User}", pdc.ChequeNumber, voucher.Id, username);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<PdcRegisterDto> BounceChequeAsync(long id, BouncePdcDto dto, string username, CancellationToken cancellationToken = default)
    {
        var pdc = await _pdcRepository.GetByIdAsync(id, cancellationToken);
        if (pdc == null)
        {
            throw new AccountingNotFoundException($"الشيك رقم ({id}) غير موجود بالسجل.", "PDC_NOT_FOUND");
        }

        if (pdc.Status != "RECEIVED" && pdc.Status != "DEPOSITED")
        {
            throw new AccountingException($"لا يمكن إثبات ارتداد شيك حالته ({pdc.Status}).", "INVALID_PDC_STATUS");
        }

        string partyAccountCode = "112101"; // AR Control
        if (pdc.PartyType == "CUSTOMER" && !string.IsNullOrWhiteSpace(pdc.PartyCode))
        {
            var cust = await _customerRepository.GetByCodeAsync(pdc.PartyCode, cancellationToken);
            if (cust != null) partyAccountCode = cust.ArControlAccountCode;
        }

        string intermediateAcc = pdc.IntermediateAccountCode ?? "111301";

        // Build Bounce Reversal GL Voucher
        var voucherDto = new CreateGlVoucherDto
        {
            BranchId = pdc.BranchId,
            FiscalYearId = pdc.FiscalYearId,
            VoucherType = 1, // JV
            VoucherDate = dto.BouncedDate,
            Description = $"ارتداد الشيك رقم {pdc.ChequeNumber} - سبب الارتداد: {dto.BounceReason}",
            Details = new List<CreateGlVoucherDetailDto>
            {
                // Debit: Customer Account (reopen debt)
                new()
                {
                    AccountCode = partyAccountCode,
                    Debit = pdc.Amount,
                    Credit = 0,
                    Description = $"إعادة فتح مديونية لارتداد الشيك رقم {pdc.ChequeNumber}",
                    CurrencyId = pdc.CurrencyId,
                    ExchangeRate = pdc.ExchangeRate,
                    PartyType = pdc.PartyType,
                    PartyCode = pdc.PartyCode,
                    BranchId = pdc.BranchId
                },
                // Credit: Intermediate Cheques Account
                new()
                {
                    AccountCode = intermediateAcc,
                    Debit = 0,
                    Credit = pdc.Amount,
                    Description = $"إلغاء شيك تحت التحصيل لارتداده - شيك رقم {pdc.ChequeNumber}",
                    CurrencyId = pdc.CurrencyId,
                    ExchangeRate = pdc.ExchangeRate,
                    BranchId = pdc.BranchId
                }
            }
        };

        if (dto.BankCharges > 0)
        {
            string chargesAcc = dto.BankChargesAccountCode ?? "510901";
            string bankAcc = pdc.DepositBankAccountCode ?? "111201";

            voucherDto.Details.Add(new CreateGlVoucherDetailDto
            {
                AccountCode = chargesAcc,
                Debit = dto.BankCharges,
                Credit = 0,
                Description = $"رسوم ارتداد شيك رقم {pdc.ChequeNumber}",
                CurrencyId = pdc.CurrencyId,
                ExchangeRate = pdc.ExchangeRate,
                BranchId = pdc.BranchId
            });

            voucherDto.Details.Add(new CreateGlVoucherDetailDto
            {
                AccountCode = bankAcc,
                Debit = 0,
                Credit = dto.BankCharges,
                Description = $"خصم رسوم ارتداد شيك رقم {pdc.ChequeNumber}",
                CurrencyId = pdc.CurrencyId,
                ExchangeRate = pdc.ExchangeRate,
                BranchId = pdc.BranchId
            });
        }

        var voucher = await _voucherService.CreateVoucherAsync(voucherDto, username, cancellationToken);
        await _voucherService.PostVoucherAsync(voucher.Id, username, cancellationToken);

        pdc.Status = "BOUNCED";
        pdc.BouncedDate = dto.BouncedDate;
        pdc.BounceReason = dto.BounceReason;
        pdc.BounceVoucherId = voucher.Id;
        pdc.UpdateUser = username;
        pdc.UpdateDate = DateTime.UtcNow;

        await _pdcRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Marked PDC {ChequeNumber} as BOUNCED, created reversal GL voucher {VoucherId} by {User}", pdc.ChequeNumber, voucher.Id, username);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<PdcRegisterDto> CancelChequeAsync(long id, string username, string? reason, CancellationToken cancellationToken = default)
    {
        var pdc = await _pdcRepository.GetByIdAsync(id, cancellationToken);
        if (pdc == null)
        {
            throw new AccountingNotFoundException($"الشيك رقم ({id}) غير موجود بالسجل.", "PDC_NOT_FOUND");
        }

        if (pdc.Status == "CLEARED")
        {
            throw new AccountingException("لا يمكن إلغاء شيك تم تحصيله بالفعل.", "CANNOT_CANCEL_CLEARED_CHEQUE");
        }

        pdc.Status = "CANCELLED";
        if (!string.IsNullOrWhiteSpace(reason)) pdc.Notes += " | سبب الإلغاء: " + reason;
        pdc.UpdateUser = username;
        pdc.UpdateDate = DateTime.UtcNow;

        await _pdcRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cancelled PDC {ChequeNumber} by {User}", pdc.ChequeNumber, username);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UpcomingMaturitySummaryDto> GetUpcomingMaturitiesAsync(long? branchId, int daysAhead, CancellationToken cancellationToken = default)
    {
        var list = await _pdcRepository.GetUpcomingMaturitiesAsync(branchId, daysAhead, cancellationToken);
        var dtos = list.Select(c => MapToDto(c, string.Empty, string.Empty)).ToList();

        var today = DateTime.UtcNow.Date;
        var in7 = today.AddDays(7);
        var in30 = today.AddDays(30);

        var due7 = dtos.Where(d => d.DueDate >= today && d.DueDate <= in7).ToList();
        var due30 = dtos.Where(d => d.DueDate >= today && d.DueDate <= in30).ToList();

        return new UpcomingMaturitySummaryDto
        {
            TotalChequesCount = dtos.Count,
            TotalAmount = dtos.Sum(d => d.Amount),
            DueIn7DaysCount = due7.Count,
            DueIn7DaysAmount = due7.Sum(d => d.Amount),
            DueIn30DaysCount = due30.Count,
            DueIn30DaysAmount = due30.Sum(d => d.Amount),
            Cheques = dtos
        };
    }

    private static PdcRegisterDto MapToDto(GlPdcRegister pdc, string partyNameLocal, string partyNameEn)
    {
        return new PdcRegisterDto
        {
            Id = pdc.Id,
            BranchId = pdc.BranchId,
            BranchNameLocal = pdc.Branch?.BranchNameLocal,
            BranchNameEn = pdc.Branch?.BranchNameEn,
            FiscalYearId = pdc.FiscalYearId,
            ChequeType = pdc.ChequeType,
            ChequeNumber = pdc.ChequeNumber,
            ChequeDate = pdc.ChequeDate,
            DueDate = pdc.DueDate,
            Amount = pdc.Amount,
            LocalAmount = pdc.LocalAmount,
            CurrencyId = pdc.CurrencyId,
            CurrencyName = pdc.Currency?.ShortNameEn,
            ExchangeRate = pdc.ExchangeRate,
            DrawerBankName = pdc.DrawerBankName,
            DrawerBankAccountNo = pdc.DrawerBankAccountNo,
            BeneficiaryName = pdc.BeneficiaryName,
            PartyType = pdc.PartyType,
            PartyCode = pdc.PartyCode,
            PartyNameLocal = partyNameLocal,
            PartyNameEn = partyNameEn,
            Status = pdc.Status,
            StatusNameLocal = pdc.Status switch
            {
                "RECEIVED" => "مستلم",
                "DEPOSITED" => "مودع بالبنك",
                "CLEARED" => "محصّل",
                "BOUNCED" => "مرتد",
                "CANCELLED" => "ملغى",
                "ISSUED" => "صادر",
                _ => pdc.Status
            },
            IntermediateAccountCode = pdc.IntermediateAccountCode,
            DepositBankAccountCode = pdc.DepositBankAccountCode,
            DepositDate = pdc.DepositDate,
            ClearedDate = pdc.ClearedDate,
            BouncedDate = pdc.BouncedDate,
            BounceReason = pdc.BounceReason,
            OriginatingVoucherId = pdc.OriginatingVoucherId,
            ClearingVoucherId = pdc.ClearingVoucherId,
            BounceVoucherId = pdc.BounceVoucherId,
            Notes = pdc.Notes
        };
    }
}
