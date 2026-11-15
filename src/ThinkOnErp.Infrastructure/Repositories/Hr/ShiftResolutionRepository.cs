using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class ShiftResolutionRepository(OracleDbContext db) : IShiftResolutionRepository
{
    public async Task<IReadOnlyList<EmployeeShiftDay>> GetDailyShiftsAsync(string employeeCode, DateOnly workDate, CancellationToken cancellationToken = default)
    {
        var date = workDate.ToDateTime(TimeOnly.MinValue);
        return await db.Set<EmployeeShiftDay>().AsNoTracking().Where(s => s.EmployeeCode == employeeCode && s.WorkDate == date && s.IsActive).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeShiftAssignment>> GetAssignmentsAsync(string employeeCode, DateOnly workDate, CancellationToken cancellationToken = default)
    {
        var date = workDate.ToDateTime(TimeOnly.MinValue);
        return await db.Set<EmployeeShiftAssignment>().AsNoTracking().Where(s => s.EmployeeCode == employeeCode && s.IsActive &&
            s.EffectiveFrom.Date <= date && (!s.EffectiveTo.HasValue || s.EffectiveTo.Value.Date >= date)).ToListAsync(cancellationToken);
    }

    public Task<ShiftSchedule?> GetShiftAsync(string shiftCode, CancellationToken cancellationToken = default)
        => db.Set<ShiftSchedule>().AsNoTracking().SingleOrDefaultAsync(s => s.ShiftCode == shiftCode, cancellationToken);
}
