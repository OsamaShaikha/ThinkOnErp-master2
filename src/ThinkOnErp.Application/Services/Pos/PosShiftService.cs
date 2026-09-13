using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public class PosShiftService : IPosShiftService
{
    private readonly IPosShiftRepository _shiftRepository;
    private readonly IPosAuditService _auditService;

    public PosShiftService(
        IPosShiftRepository shiftRepository,
        IPosAuditService auditService)
    {
        _shiftRepository = shiftRepository;
        _auditService = auditService;
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

        var movement = new PosShiftCashMovement
        {
            ShiftId = dto.ShiftId,
            MovementType = dto.MovementType,
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

        RecalculateExpectedCash(shift);

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

        RecalculateExpectedCash(shift);

        shift.CountedCashAmount = dto.CountedCashAmount;
        shift.VarianceAmount = dto.CountedCashAmount - shift.ExpectedCashInDrawer;
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

        return await GetShiftByIdAsync(shift.Id, ct);
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

        return ApiResponse<PosShiftSummaryDto>.CreateSuccess(MapToSummaryDto(shift));
    }

    public async Task<ApiResponse<PosShiftSummaryDto>> GetShiftByIdAsync(long shiftId, CancellationToken ct = default)
    {
        var shift = await _shiftRepository.GetShiftByIdAsync(shiftId, ct);
        if (shift == null)
            return ApiResponse<PosShiftSummaryDto>.CreateFailure("Shift not found", null, 404);

        return ApiResponse<PosShiftSummaryDto>.CreateSuccess(MapToSummaryDto(shift));
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

        var dtos = items.Select(MapToSummaryDto).ToList();
        var paged = new PagedResultDto<PosShiftSummaryDto>(dtos, totalCount, pageIndex, pageSize);
        return ApiResponse<PagedResultDto<PosShiftSummaryDto>>.CreateSuccess(paged);
    }

    private static void RecalculateExpectedCash(PosShift shift)
    {
        shift.ExpectedCashInDrawer = shift.OpeningFloat
            + shift.TotalCashSales
            - shift.TotalCashRefunds
            + shift.TotalFloatIn
            - shift.TotalCashDrop
            - shift.TotalPayOut
            - shift.TotalTipPayout;
    }

    private static PosShiftSummaryDto MapToSummaryDto(PosShift s)
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
            OpenedAt = s.OpenedAt,
            BlindClosedAt = s.BlindClosedAt,
            ClosedAt = s.ClosedAt,
            OpeningFloat = s.OpeningFloat,
            TotalCashSales = s.TotalCashSales,
            TotalCardSales = s.TotalCardSales,
            TotalOtherSales = s.TotalOtherSales,
            TotalCashRefunds = s.TotalCashRefunds,
            TotalCardRefunds = s.TotalCardRefunds,
            TotalFloatIn = s.TotalFloatIn,
            TotalCashDrop = s.TotalCashDrop,
            TotalPayOut = s.TotalPayOut,
            TotalTipPayout = s.TotalTipPayout,
            ExpectedCashInDrawer = s.ExpectedCashInDrawer,
            CountedCashAmount = s.CountedCashAmount,
            VarianceAmount = s.VarianceAmount,
            IsAudited = s.IsAudited,
            AuditedBy = s.AuditedBy,
            AuditedAt = s.AuditedAt,
            AuditNotes = s.AuditNotes,
            CashMovements = s.CashMovements.Select(m => new PosShiftCashMovementItemDto
            {
                Id = m.Id,
                MovementType = m.MovementType,
                Amount = m.Amount,
                Reason = m.Reason,
                ApprovedBy = m.ApprovedBy,
                CreationDate = m.CreationDate,
                CreationUser = m.CreationUser
            }).ToList()
        };
    }
}
