using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IAttendanceCorrectionService
{
    Task<List<AttendanceCorrectionRequestDto>> GetRequestsAsync(string? employeeCode = null, DateTime? fromDate = null, DateTime? toDate = null, string? status = null);
    Task<AttendanceCorrectionRequestDto?> GetRequestByIdAsync(long id);
    Task<AttendanceCorrectionRequestDto> SubmitCorrectionAsync(CreateAttendanceCorrectionRequestDto dto, string currentUser);
    Task<AttendanceCorrectionRequestDto> ApproveCorrectionAsync(long id, long companyId, string approvedBy);
    Task<AttendanceCorrectionRequestDto> RejectCorrectionAsync(long id, RejectAttendanceCorrectionDto dto, string rejectedBy);
    Task IngestRawPunchAsync(RawPunchDto dto, string currentUser);
    Task<List<AttendanceDayDto>> GetAttendanceDaysAsync(string employeeCode, DateTime fromDate, DateTime toDate);
}
