using System;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IAttendanceCalculationEngine
{
    Task<AttendanceDay> CalculateDailyAttendanceAsync(string employeeCode, DateTime date, long companyId, string currentUser);
    Task<int> ProcessUnprocessedRawPunchesAsync(DateTime date, long companyId, string currentUser);
}
