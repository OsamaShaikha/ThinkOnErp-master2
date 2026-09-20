using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosStaffAttendanceService
{
    Task<ApiResponse<StaffAttendanceDto>> ClockInAsync(ClockInDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<StaffAttendanceDto>> ClockOutAsync(ClockOutDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<StaffAttendanceDto>>> GetAttendancePagedAsync(
        long branchId,
        long? userId = null,
        long? shiftId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
}

public class PosStaffAttendanceService : IPosStaffAttendanceService
{
    private readonly IPosStaffAttendanceRepository _repository;

    public PosStaffAttendanceService(IPosStaffAttendanceRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<StaffAttendanceDto>> ClockInAsync(ClockInDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<StaffAttendanceDto>.CreateFailure("Request body cannot be null", null, 400);

        if (dto.BranchId <= 0 || dto.UserId <= 0)
            return ApiResponse<StaffAttendanceDto>.CreateFailure("Valid BranchId and UserId are required", null, 400);

        var existing = await _repository.GetActiveClockInAsync(dto.BranchId, dto.UserId, ct);
        if (existing != null)
            return ApiResponse<StaffAttendanceDto>.CreateFailure("Staff member already has an active clock-in session", null, 400);

        var attendance = new PosStaffAttendance
        {
            BranchId = dto.BranchId,
            UserId = dto.UserId,
            ShiftId = dto.ShiftId,
            ClockInTime = DateTime.UtcNow,
            Notes = dto.Notes
        };

        await _repository.AddAttendanceAsync(attendance, ct);
        await _repository.SaveChangesAsync(ct);

        return ApiResponse<StaffAttendanceDto>.CreateSuccess(MapToDto(attendance), "Clocked in successfully");
    }

    public async Task<ApiResponse<StaffAttendanceDto>> ClockOutAsync(ClockOutDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null || dto.AttendanceId <= 0)
            return ApiResponse<StaffAttendanceDto>.CreateFailure("Valid AttendanceId is required", null, 400);

        var attendance = await _repository.GetByIdAsync(dto.AttendanceId, ct);
        if (attendance == null)
            return ApiResponse<StaffAttendanceDto>.CreateFailure("Attendance record not found", null, 404);

        if (attendance.ClockOutTime != null)
            return ApiResponse<StaffAttendanceDto>.CreateFailure("Staff member has already clocked out for this session", null, 400);

        attendance.ClockOutTime = DateTime.UtcNow;
        var duration = attendance.ClockOutTime.Value - attendance.ClockInTime;
        attendance.TotalHoursWorked = Math.Round((decimal)duration.TotalHours, 2);
        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            attendance.Notes = (attendance.Notes != null ? attendance.Notes + "; " : "") + dto.Notes;
        }

        await _repository.UpdateAttendanceAsync(attendance, ct);
        await _repository.SaveChangesAsync(ct);

        return ApiResponse<StaffAttendanceDto>.CreateSuccess(MapToDto(attendance), "Clocked out successfully");
    }

    public async Task<ApiResponse<PagedResultDto<StaffAttendanceDto>>> GetAttendancePagedAsync(
        long branchId,
        long? userId = null,
        long? shiftId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _repository.GetAttendancePagedAsync(
            branchId, userId, shiftId, fromDate, toDate, pageIndex, pageSize, ct);

        var dtos = items.Select(a => MapToDto(a)).ToList();
        var paged = new PagedResultDto<StaffAttendanceDto>(dtos, totalCount, pageIndex, pageSize);
        return ApiResponse<PagedResultDto<StaffAttendanceDto>>.CreateSuccess(paged);
    }

    private static StaffAttendanceDto MapToDto(PosStaffAttendance a)
    {
        return new StaffAttendanceDto
        {
            Id = a.Id,
            BranchId = a.BranchId,
            UserId = a.UserId,
            UserName = a.User?.FullNameLocal,
            ShiftId = a.ShiftId,
            ClockInTime = a.ClockInTime,
            ClockOutTime = a.ClockOutTime,
            TotalHoursWorked = a.TotalHoursWorked,
            Notes = a.Notes
        };
    }
}
