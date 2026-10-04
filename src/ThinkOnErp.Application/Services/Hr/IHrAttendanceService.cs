using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IHrAttendanceService
{
    /// <summary>
    /// Validates employee location against branch geofence range and registers an attendance Check-In.
    /// </summary>
    Task<AttendancePunchResultDto> CheckInAsync(CheckInRequestDto dto, string user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates employee location against branch geofence range and registers an attendance Check-Out.
    /// </summary>
    Task<AttendancePunchResultDto> CheckOutAsync(CheckOutRequestDto dto, string user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets today's attendance summary and check-in/out status for the specified employee.
    /// </summary>
    Task<TodayAttendanceStatusDto?> GetTodayStatusAsync(string employeeCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns branches with distances and in-range indicators relative to the employee's current coordinates.
    /// </summary>
    Task<IReadOnlyList<NearbyBranchDto>> GetNearbyBranchesAsync(decimal latitude, decimal longitude, long? companyId = null, CancellationToken cancellationToken = default);
}
