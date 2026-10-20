using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public class PosShiftService : IPosShiftService
{
    private readonly IPosShiftRepository _shiftRepository;
    private readonly IPosAuditService _auditService;
    private readonly IReceiptPaymentVoucherService? _receiptVoucherService;
    private readonly IGlFiscalPeriodRepository? _fiscalPeriodRepository;
    private readonly IPosZReportService? _zReportService;
    private readonly ISysSettingRepository? _settingRepo;
    private readonly ILogger<PosShiftService>? _logger;
    private readonly IPaymentMethodRepository? _paymentMethodRepository;
    private readonly IPosBranchSettingRepository? _branchSettingRepository;

    public PosShiftService(
        IPosShiftRepository shiftRepository,
        IPosAuditService auditService,
        IReceiptPaymentVoucherService? receiptVoucherService = null,
        IGlFiscalPeriodRepository? fiscalPeriodRepository = null,
        IPosZReportService? zReportService = null,
        ISysSettingRepository? settingRepo = null,
        ILogger<PosShiftService>? logger = null,
        IPaymentMethodRepository? paymentMethodRepository = null,
        IPosBranchSettingRepository? branchSettingRepository = null)
    {
        _shiftRepository = shiftRepository;
        _auditService = auditService;
        _receiptVoucherService = receiptVoucherService;
        _fiscalPeriodRepository = fiscalPeriodRepository;
        _zReportService = zReportService;
        _settingRepo = settingRepo;
        _logger = logger;
        _paymentMethodRepository = paymentMethodRepository;
        _branchSettingRepository = branchSettingRepository;
    }

    public async Task<ApiResponse<PosShiftSummaryDto>> OpenShiftAsync(OpenShiftDto dto, string username, CancellationToken ct = default)
    {
        // 1. Verify Till exists and is active
        var till = await _shiftRepository.GetTillByIdAsync(dto.TillId, dto.BranchId, ct);
        if (till == null)
            return ApiResponse<PosShiftSummaryDto>.CreateFailure("Till not found or does not belong to specified branch", null, 404);

        if (!till.IsActive)
            return ApiResponse<PosShiftSummaryDto>.CreateFailure("Till is deactivated", null, 400);

        // 2. Check if Till already has an open or suspended shift
        var existingTillShift = await _shiftRepository.GetActiveShiftByTillAsync(dto.TillId, ct);
        if (existingTillShift != null)
            return ApiResponse<PosShiftSummaryDto>.CreateFailure($"Till already has an active shift: {existingTillShift.ShiftNumber}", null, 400);

        // 3. Check if Cashier user already has an active shift anywhere
        var existingUserShift = await _shiftRepository.GetActiveShiftByCashierAsync(dto.CashierUserId, ct);
        if (existingUserShift != null)
            return ApiResponse<PosShiftSummaryDto>.CreateFailure($"Cashier already has an active shift on Till ID {existingUserShift.TillId}", null, 400);

        // 4. Generate shift number
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var countToday = await _shiftRepository.GetShiftCountForDayAsync(dto.BranchId, ct);
        var shiftNumber = $"SH-{dto.BranchId}-{dto.TillId}-{today}-{countToday + 1:D3}";

        var shift = new PosShift
        {
            BranchId = dto.BranchId,
            TillId = dto.TillId,
            CashierUserId = dto.CashierUserId,
            ShiftNumber = shiftNumber,
            Status = PosShiftStatus.Open,
            OpenedAt = DateTime.UtcNow,
            OpeningFloat = dto.OpeningFloat,
            ExpectedCashInDrawer = dto.OpeningFloat,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        await _shiftRepository.AddShiftAsync(shift, ct);
        await _shiftRepository.SaveChangesAsync(ct);

        return await GetShiftByIdAsync(shift.Id, ct);
    }

    public async Task<ApiResponse<PosShiftCashMovementItemDto>> RecordCashMovementAsync(CashMovementDto dto, string username, CancellationToken ct = default)
    {
        var shift = await _shiftRepository.GetShiftByIdAsync(dto.ShiftId, ct);
        if (shift == null)
            return ApiResponse<PosShiftCashMovementItemDto>.CreateFailure("Shift not found", null, 404);

        if (shift.Status != PosShiftStatus.Open)
            return ApiResponse<PosShiftCashMovementItemDto>.CreateFailure("Cannot add cash movements to a closed or suspended shift", null, 400);

        if (dto.Amount <= 0)
            return ApiResponse<PosShiftCashMovementItemDto>.CreateFailure("Movement amount must be greater than zero", null, 400);

        var paymentMethod = Enum.IsDefined(typeof(PosPaymentMethod), dto.PaymentMethod) && dto.PaymentMethod != 0
            ? (PosPaymentMethod)dto.PaymentMethod
            : PosPaymentMethod.Cash;

        var movement = new PosShiftCashMovement
        {
            ShiftId = dto.ShiftId,
            MovementType = dto.MovementType,
            PaymentMethod = paymentMethod,
            Amount = dto.Amount,
            Reason = dto.Reason,
            ApprovedBy = dto.ApprovedBy,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        // Update shift aggregates
        switch (dto.MovementType)
        {
            case PosCashMovementType.FloatIn:
                shift.TotalFloatIn += dto.Amount;
                break;
            case PosCashMovementType.CashDrop:
                shift.TotalCashDrop += dto.Amount;
                break;
            case PosCashMovementType.PayOut:
                shift.TotalPayOut += dto.Amount;
                break;
            case PosCashMovementType.TipPayout:
                shift.TotalTipPayout += dto.Amount;
                break;
        }

        RecalculateExpectedTotals(shift);

        await _shiftRepository.AddCashMovementAsync(movement, ct);
        await _shiftRepository.UpdateShiftAsync(shift, ct);
        await _shiftRepository.SaveChangesAsync(ct);

        await _auditService.LogCashMovementAsync(
            shift.BranchId,
            shift.Id,
            shift.TillId,
            movement.MovementType,
            movement.Amount,
            movement.Reason,
            movement.ApprovedBy,
            username,
            ct);

        var resultDto = new PosShiftCashMovementItemDto
        {
            Id = movement.Id,
            MovementType = movement.MovementType,
            PaymentMethod = (int)movement.PaymentMethod,
            PaymentMethodName = GetPaymentMethodName(movement.PaymentMethod),
            Amount = movement.Amount,
            Reason = movement.Reason,
            ApprovedBy = movement.ApprovedBy,
            CreationDate = movement.CreationDate,
            CreationUser = movement.CreationUser
        };

        return ApiResponse<PosShiftCashMovementItemDto>.CreateSuccess(resultDto, "Cash movement recorded successfully");
    }

    public async Task<ApiResponse<PosShiftSummaryDto>> BlindCloseShiftAsync(BlindCloseShiftDto dto, string username, CancellationToken ct = default)
    {
        var shift = await _shiftRepository.GetShiftByIdAsync(dto.ShiftId, ct);
        if (shift == null)
            return ApiResponse<PosShiftSummaryDto>.CreateFailure("Shift not found", null, 404);

        if (shift.Status != PosShiftStatus.Open && shift.Status != PosShiftStatus.Suspended)
            return ApiResponse<PosShiftSummaryDto>.CreateFailure("Shift is already closed", null, 400);

        var hasParkedOrders = await _shiftRepository.HasParkedOrdersAsync(shift.Id, ct);
        if (hasParkedOrders)
            return ApiResponse<PosShiftSummaryDto>.CreateFailure("Cannot close shift while there are parked or pending orders", null, 400);

        RecalculateExpectedTotals(shift);

        shift.CountedCashAmount = dto.CountedCashAmount;
        shift.VarianceAmount = dto.CountedCashAmount - shift.ExpectedCashInDrawer;

        // Card declaration from terminal slip
        if (dto.CountedCardAmount.HasValue)
        {
            shift.CountedCardAmount = dto.CountedCardAmount.Value;
            shift.CardVarianceAmount = dto.CountedCardAmount.Value - shift.ExpectedCardAmount;
        }
        else if (dto.TenderDeclarations != null && dto.TenderDeclarations.Any(d => d.PaymentMethod == (int)PosPaymentMethod.Card))
        {
            var cardDec = dto.TenderDeclarations.Where(d => d.PaymentMethod == (int)PosPaymentMethod.Card).Sum(d => d.CountedAmount);
            shift.CountedCardAmount = cardDec;
            shift.CardVarianceAmount = cardDec - shift.ExpectedCardAmount;
        }
        else
        {
            // If cashier did not declare cards, default counted to expected with variance 0
            shift.CountedCardAmount = shift.ExpectedCardAmount;
            shift.CardVarianceAmount = 0m;
        }

        shift.BlindClosedAt = DateTime.UtcNow;
        shift.Status = PosShiftStatus.BlindClosed;
        shift.UpdateUser = username;
        shift.UpdateDate = DateTime.UtcNow;

        await _shiftRepository.UpdateShiftAsync(shift, ct);
        await _shiftRepository.SaveChangesAsync(ct);

        await _auditService.LogBlindCloseShiftAsync(
            shift.BranchId,
            shift.Id,
            shift.TillId,
            shift.ExpectedCashInDrawer,
            dto.CountedCashAmount,
            shift.VarianceAmount,
            username,
            ct);

        // Auto-generate fiscal Z-Report immediately upon shift closing
        if (_zReportService != null)
        {
            await _zReportService.GenerateZReportAsync(new GenerateZReportDto
            {
                BranchId = shift.BranchId,
                TillId = shift.TillId,
                ShiftId = shift.Id
            }, username, ct);
        }

        return await GetShiftByIdAsync(shift.Id, ct);
    }

    public async Task<ApiResponse<PosShiftSummaryDto>> AuditShiftAsync(AuditShiftDto dto, string username, CancellationToken ct = default)
    {
        var shift = await _shiftRepository.GetShiftByIdAsync(dto.ShiftId, ct);
        if (shift == null)
            return ApiResponse<PosShiftSummaryDto>.CreateFailure("Shift not found", null, 404);

        if (shift.Status != PosShiftStatus.BlindClosed)
            return ApiResponse<PosShiftSummaryDto>.CreateFailure("Only blind-closed shifts can be audited and finalized", null, 400);

        shift.IsAudited = true;
        shift.AuditedBy = dto.AuditedBy;
        shift.AuditedAt = DateTime.UtcNow;
        shift.AuditNotes = dto.AuditNotes;
        shift.Status = PosShiftStatus.AuditedAndClosed;
        shift.ClosedAt = DateTime.UtcNow;
        shift.UpdateUser = username;
        shift.UpdateDate = DateTime.UtcNow;

        await _shiftRepository.UpdateShiftAsync(shift, ct);
        await _shiftRepository.SaveChangesAsync(ct);

        await _auditService.LogShiftAuditAsync(
            shift.BranchId,
            shift.Id,
            dto.AuditedBy,
            dto.AuditNotes,
            shift.VarianceAmount,
            username,
            ct);

        // Ensure Z-Report exists
        if (_zReportService != null)
        {
            var existingZ = await _zReportService.GetZReportByShiftIdAsync(shift.Id, ct);
            if (!existingZ.Success)
            {
                await _zReportService.GenerateZReportAsync(new GenerateZReportDto
                {
                    BranchId = shift.BranchId,
                    TillId = shift.TillId,
                    ShiftId = shift.Id
                }, username, ct);
            }
        }

        return await GetShiftByIdAsync(shift.Id, ct);
    }

    private async Task<int> ResolveClosingModeAsync(CancellationToken ct)
    {
        if (_settingRepo == null) return SysCodeKeys.PosShiftClosingModes.EnforceBlindClose;
        var setting = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosShiftClosingMode);
        if (setting != null && int.TryParse(setting.SettingValue, out var mode))
        {
            return mode;
        }
        return SysCodeKeys.PosShiftClosingModes.EnforceBlindClose;
    }

    public async Task<ApiResponse<PosShiftSummaryDto>> GetActiveShiftAsync(long branchId, long? tillId, long? cashierUserId, CancellationToken ct = default)
    {
        PosShift? shift = null;
        if (tillId.HasValue)
            shift = await _shiftRepository.GetActiveShiftByTillAsync(tillId.Value, ct);
        else if (cashierUserId.HasValue)
            shift = await _shiftRepository.GetActiveShiftByCashierAsync(cashierUserId.Value, ct);

        if (shift == null)
            return ApiResponse<PosShiftSummaryDto>.CreateFailure("No active shift found", null, 404);

        var closingMode = await ResolveClosingModeAsync(ct);
        var maskExpected = closingMode == SysCodeKeys.PosShiftClosingModes.EnforceBlindClose && shift.Status == PosShiftStatus.Open;
        return ApiResponse<PosShiftSummaryDto>.CreateSuccess(MapToSummaryDto(shift, closingMode, maskExpected));
    }

    public async Task<ApiResponse<PosShiftSummaryDto>> GetShiftByIdAsync(long shiftId, CancellationToken ct = default)
    {
        var shift = await _shiftRepository.GetShiftByIdAsync(shiftId, ct);
        if (shift == null)
            return ApiResponse<PosShiftSummaryDto>.CreateFailure("Shift not found", null, 404);

        var closingMode = await ResolveClosingModeAsync(ct);
        var maskExpected = closingMode == SysCodeKeys.PosShiftClosingModes.EnforceBlindClose && shift.Status == PosShiftStatus.Open;
        var dto = MapToSummaryDto(shift, closingMode, maskExpected);

        // If shift is closed, attach the Z-Report
        if ((shift.Status == PosShiftStatus.BlindClosed || shift.Status == PosShiftStatus.AuditedAndClosed) && _zReportService != null)
        {
            var zResult = await _zReportService.GetZReportByShiftIdAsync(shiftId, ct);
            if (zResult.Success)
            {
                dto.ZReport = zResult.Data;
            }
        }

        return ApiResponse<PosShiftSummaryDto>.CreateSuccess(dto);
    }

    public async Task<ApiResponse<PagedResultDto<PosShiftSummaryDto>>> GetShiftsPagedAsync(
        long branchId,
        long? tillId = null,
        long? cashierUserId = null,
        PosShiftStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _shiftRepository.GetShiftsPagedAsync(
            branchId, tillId, cashierUserId, status, fromDate, toDate, pageIndex, pageSize, ct);

        var closingMode = await ResolveClosingModeAsync(ct);
        var dtos = items.Select(s => MapToSummaryDto(s, closingMode, false)).ToList();
        var paged = new PagedResultDto<PosShiftSummaryDto>(dtos, totalCount, pageIndex, pageSize);
        return ApiResponse<PagedResultDto<PosShiftSummaryDto>>.CreateSuccess(paged);
    }

    private static void RecalculateExpectedTotals(PosShift shift)
    {
        shift.ExpectedCashInDrawer = shift.OpeningFloat
            + shift.TotalCashSales
            - shift.TotalCashRefunds
            + shift.TotalFloatIn
            - shift.TotalCashDrop
            - shift.TotalPayOut
            - shift.TotalTipPayout;

        shift.ExpectedCardAmount = shift.TotalCardSales - shift.TotalCardRefunds;
    }

    private static PosShiftSummaryDto MapToSummaryDto(PosShift s, int closingMode = 1, bool maskExpected = false)
    {
        return new PosShiftSummaryDto
        {
            Id = s.Id,
            BranchId = s.BranchId,
            TillId = s.TillId,
            TillCode = s.Till?.TillCode ?? string.Empty,
            TillName = s.Till?.TillName ?? string.Empty,
            CashierUserId = s.CashierUserId,
            CashierUserName = s.CashierUser?.UserName ?? string.Empty,
            ShiftNumber = s.ShiftNumber,
            Status = s.Status,
            ClosingMode = closingMode,
            OpenedAt = s.OpenedAt,
            BlindClosedAt = s.BlindClosedAt,
            ClosedAt = s.ClosedAt,
            OpeningFloat = s.OpeningFloat,
            TotalCashSales = maskExpected ? 0m : s.TotalCashSales,
            TotalCardSales = maskExpected ? 0m : s.TotalCardSales,
            TotalCustomerAccountSales = maskExpected ? 0m : s.TotalCustomerAccountSales,
            TotalHospitalitySales = maskExpected ? 0m : s.TotalHospitalitySales,
            TotalOtherSales = maskExpected ? 0m : s.TotalOtherSales,
            TotalCashRefunds = maskExpected ? 0m : s.TotalCashRefunds,
            TotalCardRefunds = maskExpected ? 0m : s.TotalCardRefunds,
            TotalFloatIn = maskExpected ? 0m : s.TotalFloatIn,
            TotalCashDrop = maskExpected ? 0m : s.TotalCashDrop,
            TotalPayOut = maskExpected ? 0m : s.TotalPayOut,
            TotalTipPayout = maskExpected ? 0m : s.TotalTipPayout,
            ExpectedCashInDrawer = maskExpected ? 0m : s.ExpectedCashInDrawer,
            CountedCashAmount = s.CountedCashAmount,
            VarianceAmount = s.VarianceAmount,
            ExpectedCardAmount = maskExpected ? 0m : s.ExpectedCardAmount,
            CountedCardAmount = s.CountedCardAmount,
            CardVarianceAmount = s.CardVarianceAmount,
            IsAudited = s.IsAudited,
            AuditedBy = s.AuditedBy,
            AuditedAt = s.AuditedAt,
            AuditNotes = s.AuditNotes,
            CashMovements = s.CashMovements.Select(m => new PosShiftCashMovementItemDto
            {
                Id = m.Id,
                MovementType = m.MovementType,
                PaymentMethod = (int)m.PaymentMethod,
                PaymentMethodName = GetPaymentMethodName(m.PaymentMethod),
                VoucherType = (int)m.VoucherType,
                VoucherTypeName = m.VoucherType == PosVoucherType.Receipt ? "سند قبض" : "سند صرف",
                VoucherNumber = m.VoucherNumber,
                GlAccountCode = m.GlAccountCode,
                BeneficiaryName = m.BeneficiaryName,
                TaxInvoiceNumber = m.TaxInvoiceNumber,
                GlVoucherId = m.GlVoucherId,
                Amount = m.Amount,
                Reason = m.Reason,
                ApprovedBy = m.ApprovedBy,
                CreationDate = m.CreationDate,
                CreationUser = m.CreationUser
            }).ToList(),
            TenderReconciliations = BuildTenderReconciliations(s)
        };
    }

    public async Task<ApiResponse<PosVoucherSummaryDto>> CreateVoucherAsync(CreatePosVoucherDto dto, string username, CancellationToken ct = default)
    {
        if (dto.Amount <= 0)
            return ApiResponse<PosVoucherSummaryDto>.CreateFailure("Voucher amount must be greater than zero", null, 400);

        var shift = await _shiftRepository.GetShiftByIdAsync(dto.ShiftId, ct);
        if (shift == null)
            return ApiResponse<PosVoucherSummaryDto>.CreateFailure("Shift not found", null, 404);

        if (shift.Status != PosShiftStatus.Open)
            return ApiResponse<PosVoucherSummaryDto>.CreateFailure("Cannot issue vouchers for a closed or suspended shift", null, 400);

        var voucherType = Enum.IsDefined(typeof(PosVoucherType), dto.VoucherType)
            ? (PosVoucherType)dto.VoucherType
            : PosVoucherType.Payment;

        var paymentMethod = Enum.IsDefined(typeof(PosPaymentMethod), dto.PaymentMethod)
            ? (PosPaymentMethod)dto.PaymentMethod
            : PosPaymentMethod.Cash;

        PosCashMovementType movementType = voucherType == PosVoucherType.Receipt
            ? PosCashMovementType.FloatIn
            : PosCashMovementType.PayOut;

        var prefix = voucherType == PosVoucherType.Receipt ? "RV" : "PV";
        var voucherSeq = shift.CashMovements.Count + 1;
        var voucherNumber = $"{prefix}-SH{shift.Id}-{voucherSeq:D4}";

        if (paymentMethod == PosPaymentMethod.Cash)
        {
            if (voucherType == PosVoucherType.Receipt)
                shift.TotalFloatIn += dto.Amount;
            else
                shift.TotalPayOut += dto.Amount;
        }
        else if (paymentMethod == PosPaymentMethod.Card)
        {
            if (voucherType == PosVoucherType.Receipt)
                shift.TotalCardSales += dto.Amount;
            else
                shift.TotalCardRefunds += dto.Amount;
        }
        else
        {
            shift.TotalOtherSales += dto.Amount;
        }

        RecalculateExpectedTotals(shift);

        // Resolve Target GL Account (Expense or Receipt)
        string? targetGlAccount = dto.GlAccountCode;

        // 1. Check branch-specific setting first
        if (string.IsNullOrWhiteSpace(targetGlAccount) && _branchSettingRepository != null && shift.BranchId > 0)
        {
            var branchSetting = await _branchSettingRepository.GetByBranchIdAsync(shift.BranchId, ct);
            if (branchSetting != null)
            {
                var branchTarget = voucherType == PosVoucherType.Receipt
                    ? branchSetting.DefaultReceiptAccount
                    : branchSetting.DefaultExpenseAccount;

                if (!string.IsNullOrWhiteSpace(branchTarget))
                {
                    targetGlAccount = branchTarget.Trim();
                }
            }
        }

        // 2. Check global SYS_SETTINGS
        if (string.IsNullOrWhiteSpace(targetGlAccount) && _settingRepo != null)
        {
            int settingKey = voucherType == PosVoucherType.Receipt
                ? SysSettingKeys.PosDefaultReceiptAccount
                : SysSettingKeys.PosDefaultExpenseAccount;

            var setting = await _settingRepo.GetByCodeAsync(settingKey);
            if (setting != null && !string.IsNullOrWhiteSpace(setting.SettingValue))
            {
                targetGlAccount = setting.SettingValue.Trim();
            }
        }

        // Standard Chart of Accounts Fallbacks if not configured in settings
        if (string.IsNullOrWhiteSpace(targetGlAccount))
        {
            targetGlAccount = voucherType == PosVoucherType.Receipt ? "421101" : "525101";
        }

        var movement = new PosShiftCashMovement
        {
            ShiftId = dto.ShiftId,
            MovementType = movementType,
            PaymentMethod = paymentMethod,
            VoucherType = voucherType,
            VoucherNumber = voucherNumber,
            GlAccountCode = targetGlAccount,
            BeneficiaryName = dto.BeneficiaryName,
            TaxInvoiceNumber = dto.TaxInvoiceNumber,
            Amount = dto.Amount,
            Reason = dto.Reason,
            ApprovedBy = dto.ApprovedBy,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        if (_receiptVoucherService != null && (!string.IsNullOrWhiteSpace(targetGlAccount) || !string.IsNullOrWhiteSpace(dto.CustomerCode) || !string.IsNullOrWhiteSpace(dto.VendorCode)))
        {
            try
            {
                var period = _fiscalPeriodRepository != null
                    ? await _fiscalPeriodRepository.GetPeriodByDateOnlyAsync(DateTime.UtcNow, ct)
                    : null;
                var fiscalYearId = period?.FiscalYearId ?? 1;

                string pMethodStr = paymentMethod switch
                {
                    PosPaymentMethod.Card => "CARD",
                    PosPaymentMethod.BankTransfer => "BANK",
                    PosPaymentMethod.Cheque => "CHEQUE",
                    _ => "CASH"
                };

                string cashOrBankAcc = paymentMethod switch
                {
                    PosPaymentMethod.Card => "111201",
                    PosPaymentMethod.BankTransfer => "111201",
                    PosPaymentMethod.Cheque => "111301",
                    _ => "111102"
                };

                if (paymentMethod == PosPaymentMethod.Cash)
                {
                    // 1. Check branch-specific cash drawer account first
                    if (_branchSettingRepository != null && shift.BranchId > 0)
                    {
                        var branchSetting = await _branchSettingRepository.GetByBranchIdAsync(shift.BranchId, ct);
                        if (branchSetting != null && !string.IsNullOrWhiteSpace(branchSetting.DefaultCashAccount))
                        {
                            cashOrBankAcc = branchSetting.DefaultCashAccount.Trim();
                        }
                    }

                    // 2. Check global SYS_SETTINGS
                    if (cashOrBankAcc == "111102" && _settingRepo != null)
                    {
                        var cashSetting = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosDefaultCashAccount);
                        if (cashSetting != null && !string.IsNullOrWhiteSpace(cashSetting.SettingValue))
                        {
                            cashOrBankAcc = cashSetting.SettingValue.Trim();
                        }
                    }
                }

                // Resolve exact GL account dynamically from the enterprise PaymentMethod definition
                if (_paymentMethodRepository != null && dto.PaymentMethod > 0)
                {
                    try
                    {
                        var pm = await _paymentMethodRepository.GetByIdAsync(dto.PaymentMethod, ct);
                        if (pm != null)
                        {
                            if (!string.IsNullOrWhiteSpace(pm.GlAccountCode))
                            {
                                cashOrBankAcc = pm.GlAccountCode.Trim();
                            }

                            var typeUpper = pm.MethodType?.Trim().ToUpperInvariant() ?? string.Empty;
                            pMethodStr = typeUpper switch
                            {
                                "CARD" => "CARD",
                                "BANK" => "BANK",
                                "CHEQUE" => "CHEQUE",
                                _ => "CASH"
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to resolve GL account for PaymentMethod ID {PaymentMethodId}", dto.PaymentMethod);
                    }
                }

                if (voucherType == PosVoucherType.Receipt)
                {
                    var receiptDto = new CreateReceiptVoucherDto
                    {
                        BranchId = shift.BranchId,
                        FiscalYearId = fiscalYearId,
                        VoucherDate = DateTime.UtcNow,
                        Description = $"سند قبض POS ({voucherNumber}) - {dto.Reason}",
                        PaymentMethod = pMethodStr,
                        CashOrBankAccountCode = cashOrBankAcc,
                        TotalAmount = dto.Amount,
                        CustomerCode = dto.CustomerCode,
                        IncomeAccountCode = string.IsNullOrWhiteSpace(dto.CustomerCode) ? targetGlAccount : null,
                        ChequeNumber = dto.ChequeNumber,
                        TransferReference = dto.TransferReference,
                        AutoPost = true
                    };
                    var createdReceipt = await _receiptVoucherService.CreateReceiptVoucherAsync(receiptDto, username, ct);
                    movement.GlVoucherId = createdReceipt.VoucherId;
                }
                else
                {
                    var payDto = new CreatePaymentVoucherDto
                    {
                        BranchId = shift.BranchId,
                        FiscalYearId = fiscalYearId,
                        VoucherDate = DateTime.UtcNow,
                        Description = $"سند صرف POS ({voucherNumber}) - {dto.Reason}",
                        PaymentMethod = pMethodStr,
                        CashOrBankAccountCode = cashOrBankAcc,
                        TotalAmount = dto.Amount,
                        VendorCode = dto.VendorCode,
                        ExpenseAccountCode = string.IsNullOrWhiteSpace(dto.VendorCode) ? targetGlAccount : null,
                        BeneficiaryName = dto.BeneficiaryName,
                        ChequeNumber = dto.ChequeNumber,
                        TransferReference = dto.TransferReference,
                        AutoPost = true
                    };
                    var createdPayment = await _receiptVoucherService.CreatePaymentVoucherAsync(payDto, username, ct);
                    movement.GlVoucherId = createdPayment.VoucherId;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to create GL voucher for POS voucher {VoucherNumber}. Shift cash movement will be saved without linked GL voucher.", voucherNumber);
            }
        }

        await _shiftRepository.AddCashMovementAsync(movement, ct);
        await _shiftRepository.UpdateShiftAsync(shift, ct);
        await _shiftRepository.SaveChangesAsync(ct);

        var summary = new PosVoucherSummaryDto
        {
            Id = movement.Id,
            VoucherNumber = movement.VoucherNumber,
            ShiftId = movement.ShiftId,
            VoucherType = (int)movement.VoucherType,
            VoucherTypeName = movement.VoucherType == PosVoucherType.Receipt ? "سند قبض" : "سند صرف",
            PaymentMethod = (int)movement.PaymentMethod,
            PaymentMethodName = GetPaymentMethodName(movement.PaymentMethod),
            Amount = movement.Amount,
            GlAccountCode = movement.GlAccountCode,
            BeneficiaryName = movement.BeneficiaryName,
            TaxInvoiceNumber = movement.TaxInvoiceNumber,
            Reason = movement.Reason,
            ApprovedBy = movement.ApprovedBy,
            GlVoucherId = movement.GlVoucherId,
            CreationDate = movement.CreationDate,
            CreationUser = movement.CreationUser
        };

        return ApiResponse<PosVoucherSummaryDto>.CreateSuccess(summary, "Voucher created successfully");
    }

    public async Task<ApiResponse<IReadOnlyList<PosVoucherSummaryDto>>> GetShiftVouchersAsync(long shiftId, int? voucherType = null, int? paymentMethod = null, CancellationToken ct = default)
    {
        var shift = await _shiftRepository.GetShiftByIdAsync(shiftId, ct);
        if (shift == null)
            return ApiResponse<IReadOnlyList<PosVoucherSummaryDto>>.CreateFailure("Shift not found", null, 404);

        var query = shift.CashMovements.AsEnumerable();
        if (voucherType.HasValue)
            query = query.Where(m => (int)m.VoucherType == voucherType.Value);
        if (paymentMethod.HasValue)
            query = query.Where(m => (int)m.PaymentMethod == paymentMethod.Value);

        var list = query.OrderByDescending(m => m.CreationDate).Select(m => new PosVoucherSummaryDto
        {
            Id = m.Id,
            VoucherNumber = m.VoucherNumber ?? $"V-{m.Id}",
            ShiftId = m.ShiftId,
            VoucherType = (int)m.VoucherType,
            VoucherTypeName = m.VoucherType == PosVoucherType.Receipt ? "سند قبض" : "سند صرف",
            PaymentMethod = (int)m.PaymentMethod,
            PaymentMethodName = GetPaymentMethodName(m.PaymentMethod),
            Amount = m.Amount,
            GlAccountCode = m.GlAccountCode,
            BeneficiaryName = m.BeneficiaryName,
            TaxInvoiceNumber = m.TaxInvoiceNumber,
            Reason = m.Reason,
            ApprovedBy = m.ApprovedBy,
            GlVoucherId = m.GlVoucherId,
            CreationDate = m.CreationDate,
            CreationUser = m.CreationUser
        }).ToList();

        return ApiResponse<IReadOnlyList<PosVoucherSummaryDto>>.CreateSuccess(list);
    }

    public async Task<ApiResponse<PosVoucherSummaryDto>> CreateReceiptVoucherAsync(CreatePosReceiptVoucherDto dto, string username, CancellationToken ct = default)
    {
        var voucherDto = new CreatePosVoucherDto
        {
            ShiftId = dto.ShiftId,
            VoucherType = (int)PosVoucherType.Receipt,
            PaymentMethod = dto.PaymentMethod,
            Amount = dto.Amount,
            GlAccountCode = dto.GlAccountCode, // If specified, uses it; otherwise null and relies automatically on configured default receipt account
            CustomerCode = dto.CustomerCode,
            BeneficiaryName = !string.IsNullOrWhiteSpace(dto.PayerName) ? dto.PayerName : dto.BeneficiaryName,
            TaxInvoiceNumber = dto.ReferenceNumber,
            Reason = dto.Reason,
            ApprovedBy = dto.ApprovedBy,
            ChequeNumber = dto.ChequeNumber,
            TransferReference = dto.TransferReference
        };
        return await CreateVoucherAsync(voucherDto, username, ct);
    }

    public async Task<ApiResponse<PosVoucherSummaryDto>> CreatePaymentVoucherAsync(CreatePosPaymentVoucherDto dto, string username, CancellationToken ct = default)
    {
        var voucherDto = new CreatePosVoucherDto
        {
            ShiftId = dto.ShiftId,
            VoucherType = (int)PosVoucherType.Payment,
            PaymentMethod = dto.PaymentMethod,
            Amount = dto.Amount,
            GlAccountCode = dto.GlAccountCode, // If specified, uses it; otherwise null and relies automatically on configured default expense account
            VendorCode = dto.VendorCode,
            BeneficiaryName = dto.BeneficiaryName,
            TaxInvoiceNumber = dto.TaxInvoiceNumber,
            Reason = dto.Reason,
            ApprovedBy = dto.ApprovedBy,
            ChequeNumber = dto.ChequeNumber,
            TransferReference = dto.TransferReference
        };
        return await CreateVoucherAsync(voucherDto, username, ct);
    }

    public async Task<ApiResponse<IReadOnlyList<PosVoucherSummaryDto>>> GetShiftReceiptsAsync(long shiftId, int? paymentMethod = null, CancellationToken ct = default)
    {
        return await GetShiftVouchersAsync(shiftId, (int)PosVoucherType.Receipt, paymentMethod, ct);
    }

    public async Task<ApiResponse<IReadOnlyList<PosVoucherSummaryDto>>> GetShiftPaymentsAsync(long shiftId, int? paymentMethod = null, CancellationToken ct = default)
    {
        return await GetShiftVouchersAsync(shiftId, (int)PosVoucherType.Payment, paymentMethod, ct);
    }

    public Task<ApiResponse<IReadOnlyList<PosExpenseAccountLookupDto>>> GetReceiptAccountsLookupAsync(CancellationToken ct = default)
    {
        var accounts = new List<PosExpenseAccountLookupDto>
        {
            new() { AccountCode = "421101", AccountNameAr = "إيرادات أخرى متنوعة", AccountNameEn = "Miscellaneous Revenue", Category = "Revenues" },
            new() { AccountCode = "112101", AccountNameAr = "سداد ذمم عملاء", AccountNameEn = "Customer Account Settlement", Category = "Receivables" },
            new() { AccountCode = "212101", AccountNameAr = "تأمينات مستلمة من العملاء", AccountNameEn = "Customer Deposits Received", Category = "Liabilities" },
            new() { AccountCode = "422101", AccountNameAr = "إيرادات خدمات وتوصيل", AccountNameEn = "Delivery & Service Fees", Category = "Revenues" },
            new() { AccountCode = "111101", AccountNameAr = "عهدة نقدية واردة للصندوق", AccountNameEn = "Float / Cash In", Category = "Assets" }
        };

        return Task.FromResult(ApiResponse<IReadOnlyList<PosExpenseAccountLookupDto>>.CreateSuccess(accounts));
    }

    public Task<ApiResponse<IReadOnlyList<PosExpenseAccountLookupDto>>> GetExpenseAccountsLookupAsync(CancellationToken ct = default)
    {
        var accounts = new List<PosExpenseAccountLookupDto>
        {
            new() { AccountCode = "523101", AccountNameAr = "مصاريف صيانة ونظافة", AccountNameEn = "Maintenance & Cleaning", Category = "Expenses" },
            new() { AccountCode = "524101", AccountNameAr = "مهمات ومستلزمات تشغيلية", AccountNameEn = "Operating Supplies", Category = "Expenses" },
            new() { AccountCode = "632102", AccountNameAr = "مصاريف ضيافة وبوفيه", AccountNameEn = "Hospitality & Buffet", Category = "Expenses" },
            new() { AccountCode = "525101", AccountNameAr = "مشتريات نثرية ومطبخ", AccountNameEn = "Petty Cash Purchases", Category = "Expenses" },
            new() { AccountCode = "526101", AccountNameAr = "مصاريف وقود ومواصلات", AccountNameEn = "Fuel & Transportation", Category = "Expenses" },
            new() { AccountCode = "211101", AccountNameAr = "دفعات موردين", AccountNameEn = "Vendor Payments", Category = "Payables" },
            new() { AccountCode = "212101", AccountNameAr = "تأمينات مستردة للعملاء", AccountNameEn = "Customer Deposits", Category = "Liabilities" },
            new() { AccountCode = "421101", AccountNameAr = "إيرادات أخرى متنوعة", AccountNameEn = "Miscellaneous Revenue", Category = "Revenues" }
        };

        return Task.FromResult(ApiResponse<IReadOnlyList<PosExpenseAccountLookupDto>>.CreateSuccess(accounts));
    }

    public async Task<ApiResponse<PosDefaultAccountsDto>> GetDefaultAccountsAsync(long? branchId = null, CancellationToken ct = default)
    {
        var result = new PosDefaultAccountsDto
        {
            BranchId = branchId,
            DefaultCashAccount = "111102",
            DefaultCashAccountNameAr = "صندوق نقطة البيع (كاشير)",
            DefaultExpenseAccount = "525101",
            DefaultExpenseAccountNameAr = "مشتريات ومصروفات نثرية",
            DefaultReceiptAccount = "421101",
            DefaultReceiptAccountNameAr = "إيرادات أخرى متنوعة"
        };

        // 1. First load global settings as baseline
        if (_settingRepo != null)
        {
            var cashSetting = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosDefaultCashAccount);
            if (cashSetting != null && !string.IsNullOrWhiteSpace(cashSetting.SettingValue))
            {
                result.DefaultCashAccount = cashSetting.SettingValue.Trim();
            }

            var expSetting = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosDefaultExpenseAccount);
            if (expSetting != null && !string.IsNullOrWhiteSpace(expSetting.SettingValue))
            {
                result.DefaultExpenseAccount = expSetting.SettingValue.Trim();
            }

            var recSetting = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosDefaultReceiptAccount);
            if (recSetting != null && !string.IsNullOrWhiteSpace(recSetting.SettingValue))
            {
                result.DefaultReceiptAccount = recSetting.SettingValue.Trim();
            }
        }

        // 2. If branchId is specified, overlay branch-specific settings
        if (branchId.HasValue && branchId.Value > 0 && _branchSettingRepository != null)
        {
            var branchSetting = await _branchSettingRepository.GetByBranchIdAsync(branchId.Value, ct);
            if (branchSetting != null)
            {
                result.BranchId = branchSetting.BranchId;
                result.BranchName = branchSetting.Branch?.BranchNameLocal;
                if (!string.IsNullOrWhiteSpace(branchSetting.DefaultCashAccount))
                    result.DefaultCashAccount = branchSetting.DefaultCashAccount.Trim();
                if (!string.IsNullOrWhiteSpace(branchSetting.DefaultExpenseAccount))
                    result.DefaultExpenseAccount = branchSetting.DefaultExpenseAccount.Trim();
                if (!string.IsNullOrWhiteSpace(branchSetting.DefaultReceiptAccount))
                    result.DefaultReceiptAccount = branchSetting.DefaultReceiptAccount.Trim();
                result.DefaultWarehouseId = branchSetting.DefaultWarehouseId;
            }
        }

        result.DefaultCashAccountNameAr = ResolveAccountNameAr(result.DefaultCashAccount);
        result.DefaultExpenseAccountNameAr = ResolveAccountNameAr(result.DefaultExpenseAccount);
        result.DefaultReceiptAccountNameAr = ResolveAccountNameAr(result.DefaultReceiptAccount);

        var msg = branchId.HasValue && branchId.Value > 0
            ? $"تم جلب الحسابات الافتراضية لنقاط البيع للفرع {branchId.Value} بنجاح."
            : "تم جلب الحسابات الافتراضية لنقاط البيع بنجاح.";

        return ApiResponse<PosDefaultAccountsDto>.CreateSuccess(result, msg);
    }

    public async Task<ApiResponse<IReadOnlyList<PosDefaultAccountsDto>>> GetAllBranchDefaultAccountsAsync(CancellationToken ct = default)
    {
        if (_branchSettingRepository == null)
            return ApiResponse<IReadOnlyList<PosDefaultAccountsDto>>.CreateSuccess(Array.Empty<PosDefaultAccountsDto>());

        var branchSettings = await _branchSettingRepository.GetAllAsync(ct);
        var list = new List<PosDefaultAccountsDto>();

        foreach (var s in branchSettings)
        {
            list.Add(new PosDefaultAccountsDto
            {
                BranchId = s.BranchId,
                BranchName = s.Branch?.BranchNameLocal,
                DefaultCashAccount = s.DefaultCashAccount ?? "111102",
                DefaultCashAccountNameAr = ResolveAccountNameAr(s.DefaultCashAccount ?? "111102"),
                DefaultExpenseAccount = s.DefaultExpenseAccount ?? "525101",
                DefaultExpenseAccountNameAr = ResolveAccountNameAr(s.DefaultExpenseAccount ?? "525101"),
                DefaultReceiptAccount = s.DefaultReceiptAccount ?? "421101",
                DefaultReceiptAccountNameAr = ResolveAccountNameAr(s.DefaultReceiptAccount ?? "421101"),
                DefaultWarehouseId = s.DefaultWarehouseId
            });
        }

        return ApiResponse<IReadOnlyList<PosDefaultAccountsDto>>.CreateSuccess(list, "تم جلب إعدادات الحسابات الافتراضية لجميع الفروع بنجاح.");
    }

    public async Task<ApiResponse<PosDefaultAccountsDto>> UpdateDefaultAccountsAsync(UpdatePosDefaultAccountsDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<PosDefaultAccountsDto>.CreateFailure("Request body cannot be null", null, 400);

        // Branch-specific update
        if (dto.BranchId.HasValue && dto.BranchId.Value > 0)
        {
            if (_branchSettingRepository == null)
                return ApiResponse<PosDefaultAccountsDto>.CreateFailure("Branch settings repository unavailable", null, 500);

            var branchSetting = await _branchSettingRepository.GetByBranchIdAsync(dto.BranchId.Value, ct);
            if (branchSetting == null)
            {
                branchSetting = new PosBranchSetting
                {
                    BranchId = dto.BranchId.Value,
                    DefaultCashAccount = dto.DefaultCashAccount?.Trim(),
                    DefaultExpenseAccount = dto.DefaultExpenseAccount?.Trim(),
                    DefaultReceiptAccount = dto.DefaultReceiptAccount?.Trim(),
                    DefaultWarehouseId = dto.DefaultWarehouseId,
                    IsActive = true,
                    CreationUser = username,
                    CreationDate = DateTime.UtcNow
                };
                await _branchSettingRepository.AddAsync(branchSetting, ct);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(dto.DefaultCashAccount))
                    branchSetting.DefaultCashAccount = dto.DefaultCashAccount.Trim();
                if (!string.IsNullOrWhiteSpace(dto.DefaultExpenseAccount))
                    branchSetting.DefaultExpenseAccount = dto.DefaultExpenseAccount.Trim();
                if (!string.IsNullOrWhiteSpace(dto.DefaultReceiptAccount))
                    branchSetting.DefaultReceiptAccount = dto.DefaultReceiptAccount.Trim();
                if (dto.DefaultWarehouseId.HasValue)
                    branchSetting.DefaultWarehouseId = dto.DefaultWarehouseId;

                branchSetting.UpdateUser = username;
                branchSetting.UpdateDate = DateTime.UtcNow;
                await _branchSettingRepository.UpdateAsync(branchSetting, ct);
            }

            await _branchSettingRepository.SaveChangesAsync(ct);
            return await GetDefaultAccountsAsync(dto.BranchId.Value, ct);
        }

        // Global settings update
        if (_settingRepo == null)
            return ApiResponse<PosDefaultAccountsDto>.CreateFailure("Settings repository unavailable", null, 500);

        if (!string.IsNullOrWhiteSpace(dto.DefaultCashAccount))
        {
            var cash = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosDefaultCashAccount);
            if (cash == null)
            {
                await _settingRepo.AddAsync(new SysSetting
                {
                    SettingCode = SysSettingKeys.PosDefaultCashAccount,
                    SettingDesc = "POS Default Cash/Till Account",
                    SettingValue = dto.DefaultCashAccount.Trim()
                });
            }
            else
            {
                cash.SettingValue = dto.DefaultCashAccount.Trim();
                await _settingRepo.UpdateAsync(cash);
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.DefaultExpenseAccount))
        {
            var exp = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosDefaultExpenseAccount);
            if (exp == null)
            {
                await _settingRepo.AddAsync(new SysSetting
                {
                    SettingCode = SysSettingKeys.PosDefaultExpenseAccount,
                    SettingDesc = "POS Default Expense Account (Pay-Outs)",
                    SettingValue = dto.DefaultExpenseAccount.Trim()
                });
            }
            else
            {
                exp.SettingValue = dto.DefaultExpenseAccount.Trim();
                await _settingRepo.UpdateAsync(exp);
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.DefaultReceiptAccount))
        {
            var rec = await _settingRepo.GetByCodeAsync(SysSettingKeys.PosDefaultReceiptAccount);
            if (rec == null)
            {
                await _settingRepo.AddAsync(new SysSetting
                {
                    SettingCode = SysSettingKeys.PosDefaultReceiptAccount,
                    SettingDesc = "POS Default Receipt Account (Float-In)",
                    SettingValue = dto.DefaultReceiptAccount.Trim()
                });
            }
            else
            {
                rec.SettingValue = dto.DefaultReceiptAccount.Trim();
                await _settingRepo.UpdateAsync(rec);
            }
        }

        return await GetDefaultAccountsAsync(null, ct);
    }

    private static string ResolveAccountNameAr(string accountCode) => accountCode switch
    {
        "111101" => "الصندوق الرئيسي",
        "111102" => "صندوق نقطة البيع (كاشير)",
        "111201" => "البنك / نقاط البيع الإلكترونية",
        "111301" => "شيكات تحت التحصيل",
        "421101" => "إيرادات أخرى متنوعة",
        "112101" => "سداد ذمم عملاء",
        "525101" => "مشتريات ومصروفات نثرية",
        "632102" => "مصاريف ضيافة وبوفيه",
        _ => accountCode
    };

    private static string GetPaymentMethodName(PosPaymentMethod method) => method switch
    {
        PosPaymentMethod.Cash => "نقدي (Cash)",
        PosPaymentMethod.Card => "شبكة / مدى (Cards)",
        PosPaymentMethod.BankTransfer => "تحويل بنكي (Bank Transfer)",
        PosPaymentMethod.Cheque => "شيك (Cheque)",
        PosPaymentMethod.CustomerAccount => "حساب عميل (Customer Account)",
        PosPaymentMethod.Hospitality => "ضيافة (Hospitality)",
        _ => method.ToString()
    };

    private static List<PosTenderReconciliationDto> BuildTenderReconciliations(PosShift s)
    {
        var list = new List<PosTenderReconciliationDto>();

        // 1. Cash (Declared by Cashier)
        var cashExpected = s.ExpectedCashInDrawer;
        var cashCounted = s.CountedCashAmount ?? cashExpected;
        var cashVariance = s.VarianceAmount ?? (cashCounted - cashExpected);
        list.Add(new PosTenderReconciliationDto
        {
            PaymentMethod = (int)PosPaymentMethod.Cash,
            PaymentMethodName = "نقدي (Cash)",
            ExpectedAmount = cashExpected,
            CountedAmount = cashCounted,
            VarianceAmount = cashVariance,
            IsAutoDeclared = false,
            Notes = cashVariance == 0 ? "متطابق" : (cashVariance > 0 ? "فائض نقدي" : "عجز نقدي")
        });

        // 2. Card / Mada (Declared from Terminal Slip)
        var cardExpected = s.ExpectedCardAmount;
        var cardCounted = s.CountedCardAmount ?? cardExpected;
        var cardVariance = s.CardVarianceAmount ?? (cardCounted - cardExpected);
        list.Add(new PosTenderReconciliationDto
        {
            PaymentMethod = (int)PosPaymentMethod.Card,
            PaymentMethodName = "شبكة / مدى (Cards)",
            ExpectedAmount = cardExpected,
            CountedAmount = cardCounted,
            VarianceAmount = cardVariance,
            IsAutoDeclared = false,
            Notes = cardVariance == 0 ? "متطابق مع إيصال الشبكة" : (cardVariance > 0 ? "فائض بالشبكة" : "عجز بالشبكة")
        });

        // 3. Customer Account (Auto-declared from Registered Orders - NOT manually entered)
        if (s.TotalCustomerAccountSales > 0)
        {
            list.Add(new PosTenderReconciliationDto
            {
                PaymentMethod = (int)PosPaymentMethod.CustomerAccount,
                PaymentMethodName = "ذمم عملاء (Customer Account)",
                ExpectedAmount = s.TotalCustomerAccountSales,
                CountedAmount = s.TotalCustomerAccountSales,
                VarianceAmount = 0m,
                IsAutoDeclared = true,
                Notes = "مطابقة آلية من فواتير الذمم المسجلة"
            });
        }

        // 4. Hospitality (Auto-declared from Registered Orders - NOT manually entered)
        if (s.TotalHospitalitySales > 0)
        {
            list.Add(new PosTenderReconciliationDto
            {
                PaymentMethod = (int)PosPaymentMethod.Hospitality,
                PaymentMethodName = "ضيافة (Hospitality)",
                ExpectedAmount = s.TotalHospitalitySales,
                CountedAmount = s.TotalHospitalitySales,
                VarianceAmount = 0m,
                IsAutoDeclared = true,
                Notes = "مطابقة آلية من طلبات الضيافة المسجلة"
            });
        }

        // 5. Other Payments (if any)
        if (s.TotalOtherSales > 0)
        {
            list.Add(new PosTenderReconciliationDto
            {
                PaymentMethod = 0,
                PaymentMethodName = "أخرى (Other)",
                ExpectedAmount = s.TotalOtherSales,
                CountedAmount = s.TotalOtherSales,
                VarianceAmount = 0m,
                IsAutoDeclared = true,
                Notes = "مطابقة آلية من المبيعات الأخرى"
            });
        }

        return list;
    }
}
