using ThinkOnErp.Domain.Entities.Hr;
namespace ThinkOnErp.Domain.Interfaces.Hr;
public interface IBranchAttendanceLocationRepository
{
    Task<BranchAttendanceLocation?> GetLocationAsync(long branchId, CancellationToken ct = default);
    Task<BranchAttendanceLocation> SaveLocationAsync(BranchAttendanceLocation value, string? expectedVersion, string user, CancellationToken ct = default);
}
