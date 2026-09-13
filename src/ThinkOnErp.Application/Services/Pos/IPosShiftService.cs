using System;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosShiftService
{
    Task<ApiResponse<PosShiftSummaryDto>> OpenShiftAsync(OpenShiftDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PosShiftCashMovementItemDto>> RecordCashMovementAsync(CashMovementDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PosShiftSummaryDto>> BlindCloseShiftAsync(BlindCloseShiftDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PosShiftSummaryDto>> AuditShiftAsync(AuditShiftDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PosShiftSummaryDto>> GetActiveShiftAsync(long branchId, long? tillId, long? cashierUserId, CancellationToken ct = default);
    Task<ApiResponse<PosShiftSummaryDto>> GetShiftByIdAsync(long shiftId, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<PosShiftSummaryDto>>> GetShiftsPagedAsync(
        long branchId,
        long? tillId = null,
        long? cashierUserId = null,
        PosShiftStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
}
