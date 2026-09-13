using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Domain.Interfaces.Pos;

public interface IPosStaffAttendanceRepository
{
    Task<PosStaffAttendance?> GetActiveClockInAsync(long branchId, long userId, CancellationToken ct = default);
    Task<PosStaffAttendance?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<(IReadOnlyList<PosStaffAttendance> Items, long TotalCount)> GetAttendancePagedAsync(long branchId, long? userId, long? shiftId, DateTime? fromDate, DateTime? toDate, int pageIndex, int pageSize, CancellationToken ct = default);
    Task AddAttendanceAsync(PosStaffAttendance attendance, CancellationToken ct = default);
    Task UpdateAttendanceAsync(PosStaffAttendance attendance, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
