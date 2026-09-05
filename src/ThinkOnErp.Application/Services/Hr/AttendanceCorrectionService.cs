using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class AttendanceCorrectionService : IAttendanceCorrectionService
{
    private readonly IAttendanceCorrectionRepository _repository;
    private readonly IAttendanceCalculationEngine _calcEngine;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly ILogger<AttendanceCorrectionService> _logger;

    public AttendanceCorrectionService(
        IAttendanceCorrectionRepository repository,
        IAttendanceCalculationEngine calcEngine,
        IEmployeeRepository employeeRepo,
        ILogger<AttendanceCorrectionService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _calcEngine = calcEngine ?? throw new ArgumentNullException(nameof(calcEngine));
        _employeeRepo = employeeRepo ?? throw new ArgumentNullException(nameof(employeeRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<AttendanceCorrectionRequestDto>> GetRequestsAsync(string? employeeCode = null, DateTime? fromDate = null, DateTime? toDate = null, string? status = null)
    {
        var list = await _repository.GetRequestsAsync(employeeCode, fromDate, toDate, status);
        return list.Select(MapToDto).ToList();
    }

    public async Task<AttendanceCorrectionRequestDto?> GetRequestByIdAsync(long id)
    {
        var req = await _repository.GetByIdAsync(id);
        return req == null ? null : MapToDto(req);
    }

    public async Task<AttendanceCorrectionRequestDto> SubmitCorrectionAsync(CreateAttendanceCorrectionRequestDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var empCode = dto.EmployeeCode.Trim().ToUpperInvariant();

        var employee = await _employeeRepo.GetByCodeAsync(empCode);
        if (employee == null)
        {
            throw new HrNotFoundException($"الموظف ({empCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var existingDay = await _repository.GetAttendanceDayAsync(empCode, dto.AttendanceDate.Date);

        var request = new AttendanceCorrectionRequest
        {
            EmployeeCode = empCode,
            AttendanceDate = dto.AttendanceDate.Date,
            OldCheckIn = existingDay?.FirstCheckIn,
            OldCheckOut = existingDay?.LastCheckOut,
            RequestedCheckIn = dto.RequestedCheckIn,
            RequestedCheckOut = dto.RequestedCheckOut,
            Reason = dto.Reason.Trim(),
            Status = "PENDING",
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _repository.AddAsync(request);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Submitted attendance correction for {Emp} on {Date}", empCode, dto.AttendanceDate);
        return MapToDto(request);
    }

    public async Task<AttendanceCorrectionRequestDto> ApproveCorrectionAsync(long id, long companyId, string approvedBy)
    {
        var request = await _repository.GetByIdAsync(id);
        if (request == null)
        {
            throw new HrNotFoundException($"طلب تصحيح البصمة رقم ({id}) غير موجود.", "CORRECTION_NOT_FOUND");
        }

        if (request.Status != "PENDING")
        {
            throw new HrValidationException($"لا يمكن اعتماد طلب بحالة ({request.Status}).", "INVALID_STATUS");
        }

        request.Status = "APPROVED";
        request.ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? "SYSTEM" : approvedBy;
        request.ApprovalDate = DateTime.UtcNow;
        request.UpdateUser = request.ApprovedBy;
        request.UpdateDate = DateTime.UtcNow;

        _repository.Update(request);

        // Add corrected raw punches
        if (request.RequestedCheckIn.HasValue)
        {
            await _repository.AddRawPunchAsync(new RawAttendance
            {
                EmployeeCode = request.EmployeeCode,
                PunchTime = request.RequestedCheckIn.Value,
                PunchType = "IN",
                Source = "MANUAL_CORRECTION",
                IsProcessed = true,
                ProcessedDate = DateTime.UtcNow,
                CreationUser = request.ApprovedBy,
                CreationDate = DateTime.UtcNow
            });
        }

        if (request.RequestedCheckOut.HasValue)
        {
            await _repository.AddRawPunchAsync(new RawAttendance
            {
                EmployeeCode = request.EmployeeCode,
                PunchTime = request.RequestedCheckOut.Value,
                PunchType = "OUT",
                Source = "MANUAL_CORRECTION",
                IsProcessed = true,
                ProcessedDate = DateTime.UtcNow,
                CreationUser = request.ApprovedBy,
                CreationDate = DateTime.UtcNow
            });
        }

        await _repository.SaveChangesAsync();

        // Trigger automatic recalculation of attendance day
        await _calcEngine.CalculateDailyAttendanceAsync(request.EmployeeCode, request.AttendanceDate, companyId, request.ApprovedBy);

        _logger.LogInformation("Approved attendance correction #{Id} and recalculated attendance for {Emp}", id, request.EmployeeCode);
        return MapToDto(request);
    }

    public async Task<AttendanceCorrectionRequestDto> RejectCorrectionAsync(long id, RejectAttendanceCorrectionDto dto, string rejectedBy)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var request = await _repository.GetByIdAsync(id);
        if (request == null)
        {
            throw new HrNotFoundException($"طلب تصحيح البصمة رقم ({id}) غير موجود.", "CORRECTION_NOT_FOUND");
        }

        if (request.Status != "PENDING")
        {
            throw new HrValidationException($"لا يمكن رفض طلب بحالة ({request.Status}).", "INVALID_STATUS");
        }

        request.Status = "REJECTED";
        request.RejectionReason = dto.Reason.Trim();
        request.ApprovedBy = rejectedBy;
        request.ApprovalDate = DateTime.UtcNow;
        request.UpdateUser = rejectedBy;
        request.UpdateDate = DateTime.UtcNow;

        _repository.Update(request);
        await _repository.SaveChangesAsync();

        return MapToDto(request);
    }

    public async Task IngestRawPunchAsync(RawPunchDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var punch = new RawAttendance
        {
            EmployeeCode = dto.EmployeeCode.Trim().ToUpperInvariant(),
            PunchTime = dto.PunchTime,
            PunchType = dto.PunchType.ToUpperInvariant(),
            Source = dto.Source.ToUpperInvariant(),
            DeviceId = dto.DeviceId,
            ExternalReference = dto.ExternalReference,
            IsProcessed = false,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _repository.AddRawPunchAsync(punch);
        await _repository.SaveChangesAsync();
    }

    public async Task<List<AttendanceDayDto>> GetAttendanceDaysAsync(string employeeCode, DateTime fromDate, DateTime toDate)
    {
        var list = await _repository.GetAttendanceDaysAsync(employeeCode, fromDate, toDate);
        return list.Select(d => new AttendanceDayDto
        {
            Id = d.Id,
            EmployeeCode = d.EmployeeCode,
            EmployeeName = d.Employee?.NameAr,
            AttendanceDate = d.AttendanceDate,
            ShiftName = d.ShiftSchedule?.NameAr,
            FirstCheckIn = d.FirstCheckIn,
            LastCheckOut = d.LastCheckOut,
            ScheduledHours = d.ScheduledHours,
            ActualWorkedHours = d.ActualWorkedHours,
            LateArrivalMinutes = d.LateArrivalMinutes,
            EarlyLeaveMinutes = d.EarlyLeaveMinutes,
            OvertimeHours = d.OvertimeHours,
            HasMissingPunch = d.HasMissingPunch,
            Status = d.Status,
            Notes = d.Notes
        }).ToList();
    }

    private static AttendanceCorrectionRequestDto MapToDto(AttendanceCorrectionRequest r) => new()
    {
        Id = r.Id,
        EmployeeCode = r.EmployeeCode,
        EmployeeName = r.Employee?.NameAr,
        AttendanceDate = r.AttendanceDate,
        OldCheckIn = r.OldCheckIn,
        OldCheckOut = r.OldCheckOut,
        RequestedCheckIn = r.RequestedCheckIn,
        RequestedCheckOut = r.RequestedCheckOut,
        Reason = r.Reason,
        Status = r.Status,
        ApprovedBy = r.ApprovedBy,
        ApprovalDate = r.ApprovalDate,
        RejectionReason = r.RejectionReason,
        CreationDate = r.CreationDate
    };
}

