using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Interfaces.Pos;

public interface IPosShiftRepository
{
    Task<PosTill?> GetTillByIdAsync(long tillId, long branchId, CancellationToken ct = default);
    Task<PosShift?> GetShiftByIdAsync(long shiftId, CancellationToken ct = default);
    Task<PosShift?> GetActiveShiftByTillAsync(long tillId, CancellationToken ct = default);
    Task<PosShift?> GetActiveShiftByCashierAsync(long cashierUserId, CancellationToken ct = default);
    Task<(IReadOnlyList<PosShift> Items, long TotalCount)> GetShiftsPagedAsync(
        long branchId,
        long? tillId = null,
        long? cashierUserId = null,
        PosShiftStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task<int> GetShiftCountForDayAsync(long branchId, CancellationToken ct = default);
    Task<bool> HasParkedOrdersAsync(long shiftId, CancellationToken ct = default);
    Task AddShiftAsync(PosShift shift, CancellationToken ct = default);
    Task UpdateShiftAsync(PosShift shift, CancellationToken ct = default);
    Task AddCashMovementAsync(PosShiftCashMovement movement, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
