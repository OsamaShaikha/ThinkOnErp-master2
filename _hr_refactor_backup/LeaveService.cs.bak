using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface ILeaveService
{
    Task<LeaveMovementPageDto> GetMovementsAsync(string employeeCode, string? leaveTypeCode, DateTime? cycleStart, int page, int size, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<LeaveTypeDto?> GetLeaveTypeByCodeAsync(string leaveTypeCode, CancellationToken cancellationToken = default);
    Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveTypeDto> UpdateLeaveTypeAsync(string leaveTypeCode, UpdateLeaveTypeDto dto, string user, CancellationToken cancellationToken = default);
    Task<bool> DeleteLeaveTypeAsync(string leaveTypeCode, string user, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<LeaveRequestDto> Items, int TotalCount)> GetLeaveRequestsPagedAsync(
        string? employeeCode = null,
        string? leaveTypeCode = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<LeaveRequestDto> SubmitLeaveRequestAsync(SubmitLeaveRequestDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveRequestDto> SubmitLeaveRequestAsync(SubmitLeaveRequestDto dto, string user, long companyId, CancellationToken cancellationToken = default);
    Task<LeaveDaysCalculationDto> CalculateLeaveDaysAsync(string employeeCode, string leaveTypeCode, DateTime startDate, DateTime endDate, long companyId, CancellationToken cancellationToken = default);
    Task<LeaveRequestDto> ProcessLeaveRequestAsync(long requestId, ProcessLeaveRequestDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveRequestDto> CancelLeaveRequestAsync(string employeeCode, long requestId, CancelLeaveRequestDto dto, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveBalanceDto>> GetEmployeeBalancesAsync(string employeeCode, int? year = null, CancellationToken cancellationToken = default);
    Task<HrLeaveSettingsDto> GetHrLeaveSettingsAsync(CancellationToken cancellationToken = default);
    Task<HrLeaveSettingsDto> UpdateHrLeaveSettingsAsync(UpdateHrLeaveSettingsDto dto, string user, CancellationToken cancellationToken = default);

    Task<decimal> GetUnpaidLeaveDaysInPeriodAsync(string employeeCode, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default);

    Task<LeaveBalanceDto> GrantOrAdjustBalanceAsync(GrantLeaveBalanceDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveBalanceDto> ReverseManualBalanceMovementAsync(string employeeCode, long movementId, ReverseLeaveBalanceMovementDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveAccrualResultDto> RunDailyAccrualAsync(DailyLeaveAccrualDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveAccrualResultDto> RunMonthlyAccrualAsync(MonthlyLeaveAccrualDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveAccrualResultDto> RunYearlyAllocationAsync(YearlyLeaveAllocationDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveRolloverResultDto> RunYearEndRolloverAsync(YearEndRolloverDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveYearCloseDto> CreateLeaveYearCloseDraftAsync(YearEndRolloverDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveYearCloseDto> GetLeaveYearCloseAsync(long id, CancellationToken cancellationToken = default);
    Task<LeaveYearCloseDto> PostLeaveYearCloseAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<LeaveYearCloseDto> UnpostLeaveYearCloseAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<LeaveCashoutSettlementDto> CreateLeaveCashoutSettlementDraftAsync(CreateLeaveCashoutSettlementDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveCashoutSettlementDto> GetLeaveCashoutSettlementAsync(long id, CancellationToken cancellationToken = default);
    Task<LeaveCashoutSettlementDto> PostLeaveCashoutSettlementAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<LeaveCashoutSettlementDto> ReverseLeaveCashoutSettlementAsync(long id, ReverseLeaveCashoutSettlementDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveBalanceMigrationDto> CreateLeaveBalanceMigrationDraftAsync(CreateLeaveBalanceMigrationDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveBalanceMigrationDto> GetLeaveBalanceMigrationAsync(long id, CancellationToken cancellationToken = default);
    Task<LeaveBalanceMigrationDto> PostLeaveBalanceMigrationAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<LeaveBalanceMigrationDto> ReverseLeaveBalanceMigrationAsync(long id, string user, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeavePolicyDetailsDto>> GetLeavePoliciesAsync(CancellationToken cancellationToken = default);
    Task<LeavePolicyDetailsDto> CreateOrUpdateLeavePolicyAsync(CreateLeavePolicyDto dto, string user, CancellationToken cancellationToken = default);
}

public sealed class LeaveService : ILeaveService
{
    public async Task<LeaveMovementPageDto> GetMovementsAsync(string employeeCode, string? leaveTypeCode, DateTime? cycleStart, int page, int size, CancellationToken cancellationToken = default)
    {
        if (page < 1 || page > 1000000 || size < 1 || size > 200) throw new ArgumentException("Page must be 1..1000000 and size 1..200.");
        if (await _employeeRepo.GetEmployeeByCodeAsync(employeeCode, cancellationToken) == null)
            throw new KeyNotFoundException("Employee not found in the current HR scope.");
        var result = await _leaveRepo.GetMovementsAsync(employeeCode, leaveTypeCode, cycleStart, page, size, cancellationToken);
        return new LeaveMovementPageDto(result.Items.Select(m => new LeaveMovementDto(m.Id, m.BalanceId, m.EmployeeCode, m.LeaveTypeCode,
            m.CycleStartDate, m.SequenceNo, m.Kind, m.Source, m.Reason, m.Actor, DateTime.SpecifyKind(m.CreatedDate, DateTimeKind.Utc),
            m.AccruedDelta, m.UsedDelta, m.CarriedDelta, m.AccruedAfter, m.UsedAfter, m.CarriedAfter,
            m.AccruedAfter + m.CarriedAfter - m.UsedAfter, m.ExpiredDays, m.TransferredDays, m.ReversesMovementId)).ToList(), result.Total, page, size);
    }
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IHrUnitOfWork? _unitOfWork;
    private readonly IWorkCalendarService? _calendarService;
    private readonly IPolicyRepository? _policyRepository;

    public LeaveService(
        ILeaveRepository leaveRepo,
        IEmployeeRepository employeeRepo,
        IHrUnitOfWork? unitOfWork = null,
        IWorkCalendarService? calendarService = null,
        IPolicyRepository? policyRepository = null)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
        _unitOfWork = unitOfWork;
        _calendarService = calendarService;
        _policyRepository = policyRepository;
    }

    public async Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var types = await _leaveRepo.GetLeaveTypesAsync(activeOnly, cancellationToken);
        return types.Select(t => new LeaveTypeDto(
            t.LeaveTypeCode,
            t.NameLocal,
            t.NameEn,
            t.IsPaid,
            t.IsStatutory,
            t.MaxDaysPerYear,
            t.CarryForwardAllowed,
            t.CarryForwardCapDays,
            t.RequiresDocumentation,
            t.IsActive,
            t.TracksBalance,
            t.YearEndExcessAction
        )).ToList();
    }

    public async Task<LeaveTypeDto?> GetLeaveTypeByCodeAsync(string leaveTypeCode, CancellationToken cancellationToken = default)
    {
        var t = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken);
        if (t == null) return null;

        return new LeaveTypeDto(
            t.LeaveTypeCode,
            t.NameLocal,
            t.NameEn,
            t.IsPaid,
            t.IsStatutory,
            t.MaxDaysPerYear,
            t.CarryForwardAllowed,
            t.CarryForwardCapDays,
            t.RequiresDocumentation,
            t.IsActive,
            t.TracksBalance,
            t.YearEndExcessAction
        );
    }

    public async Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto dto, string user, CancellationToken cancellationToken = default)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (string.IsNullOrWhiteSpace(dto.LeaveTypeCode))
            throw new ArgumentException("Leave type code is required.", nameof(dto.LeaveTypeCode));

        var normalizedCode = dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var excessAction = NormalizeYearEndExcessAction(dto.YearEndExcessAction);
        var existing = await _leaveRepo.GetLeaveTypeByCodeAsync(normalizedCode, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Leave type with code '{dto.LeaveTypeCode}' already exists.");

        var entity = new LeaveType
        {
            LeaveTypeCode = normalizedCode,
            NameLocal = dto.NameLocal,
            NameEn = dto.NameEn,
            IsPaid = dto.IsPaid,
            TracksBalance = dto.TracksBalance,
            IsStatutory = dto.IsStatutory,
            MaxDaysPerYear = dto.MaxDaysPerYear,
            CarryForwardAllowed = dto.CarryForwardAllowed,
            CarryForwardCapDays = dto.CarryForwardCapDays,
            YearEndExcessAction = excessAction,
            RequiresDocumentation = dto.RequiresDocumentation,
            IsActive = dto.IsActive,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        await _leaveRepo.AddLeaveTypeAsync(entity, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return new LeaveTypeDto(
            entity.LeaveTypeCode,
            entity.NameLocal,
            entity.NameEn,
            entity.IsPaid,
            entity.IsStatutory,
            entity.MaxDaysPerYear,
            entity.CarryForwardAllowed,
            entity.CarryForwardCapDays,
            entity.RequiresDocumentation,
            entity.IsActive,
            entity.TracksBalance,
            entity.YearEndExcessAction
        );
    }

    public async Task<LeaveTypeDto> UpdateLeaveTypeAsync(string leaveTypeCode, UpdateLeaveTypeDto dto, string user, CancellationToken cancellationToken = default)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        var entity = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken);
        if (entity == null)
            throw new KeyNotFoundException($"Leave type with code '{leaveTypeCode}' not found.");

        entity.NameLocal = dto.NameLocal;
        entity.NameEn = dto.NameEn;
        entity.IsPaid = dto.IsPaid;
        if (dto.TracksBalance.HasValue)
            entity.TracksBalance = dto.TracksBalance.Value;
        entity.IsStatutory = dto.IsStatutory;
        entity.MaxDaysPerYear = dto.MaxDaysPerYear;
        entity.CarryForwardAllowed = dto.CarryForwardAllowed;
        entity.CarryForwardCapDays = dto.CarryForwardCapDays;
        if (dto.YearEndExcessAction != null)
            entity.YearEndExcessAction = NormalizeYearEndExcessAction(dto.YearEndExcessAction);
        entity.RequiresDocumentation = dto.RequiresDocumentation;
        entity.IsActive = dto.IsActive;
        entity.UpdateUser = user;
        entity.UpdateDate = DateTime.UtcNow;

        await _leaveRepo.UpdateLeaveTypeAsync(entity, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return new LeaveTypeDto(
            entity.LeaveTypeCode,
            entity.NameLocal,
            entity.NameEn,
            entity.IsPaid,
            entity.IsStatutory,
            entity.MaxDaysPerYear,
            entity.CarryForwardAllowed,
            entity.CarryForwardCapDays,
            entity.RequiresDocumentation,
            entity.IsActive,
            entity.TracksBalance,
            entity.YearEndExcessAction
        );
    }

    public async Task<bool> DeleteLeaveTypeAsync(string leaveTypeCode, string user, CancellationToken cancellationToken = default)
    {
        var entity = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken);
        if (entity == null) return false;

        entity.IsActive = false;
        entity.UpdateUser = user;
        entity.UpdateDate = DateTime.UtcNow;

        await _leaveRepo.UpdateLeaveTypeAsync(entity, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(IReadOnlyList<LeaveRequestDto> Items, int TotalCount)> GetLeaveRequestsPagedAsync(
        string? employeeCode = null,
        string? leaveTypeCode = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _leaveRepo.GetLeaveRequestsPagedAsync(
            employeeCode, leaveTypeCode, status, fromDate, toDate, pageIndex, pageSize, cancellationToken);

        var dtos = items.Select(r => MapLeaveRequest(r)).ToList();

        return (dtos, totalCount);
    }

    public Task<LeaveRequestDto> SubmitLeaveRequestAsync(SubmitLeaveRequestDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return _unitOfWork == null ? _leaveRepo.ExecuteForLeaveEmployeeAsync(dto.EmployeeCode,
                () => SubmitLeaveRequestCoreAsync(dto, user, null, cancellationToken), cancellationToken)
            : _unitOfWork.ExecuteForEmployeeDatesAsync(dto.EmployeeCode, dto.StartDate, dto.EndDate,
                () => SubmitLeaveRequestCoreAsync(dto, user, null, cancellationToken), cancellationToken);
    }

    public Task<LeaveRequestDto> SubmitLeaveRequestAsync(SubmitLeaveRequestDto dto, string user, long companyId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (companyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(companyId), "A valid tenant company is required to calculate leave days.");

        return _unitOfWork == null ? _leaveRepo.ExecuteForLeaveEmployeeAsync(dto.EmployeeCode,
                () => SubmitLeaveRequestCoreAsync(dto, user, companyId, cancellationToken), cancellationToken)
            : _unitOfWork.ExecuteForEmployeeDatesAsync(dto.EmployeeCode, dto.StartDate, dto.EndDate,
                () => SubmitLeaveRequestCoreAsync(dto, user, companyId, cancellationToken), cancellationToken);
    }

    private async Task<LeaveRequestDto> SubmitLeaveRequestCoreAsync(SubmitLeaveRequestDto dto, string user, long? companyId, CancellationToken cancellationToken)
    {
        if (dto.EndDate < dto.StartDate)
        {
            throw new InvalidOperationException("Leave end date cannot be earlier than start date.");
        }

        var emp = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken);
        if (emp == null)
        {
            throw new InvalidOperationException($"Employee '{dto.EmployeeCode}' not found.");
        }

        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(dto.LeaveTypeCode, cancellationToken);
        if (leaveType == null || !leaveType.IsActive)
        {
            throw new InvalidOperationException($"Leave type '{dto.LeaveTypeCode}' is invalid or inactive.");
        }

        var requestSettings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var (requestPolicy, requestCycle) = await ResolveLeavePolicyCycleAsync(emp, dto.LeaveTypeCode, dto.StartDate.Date, requestSettings, cancellationToken);
        if (leaveType.TracksBalance && requestPolicy == null && await HasLeavePolicyHistoryAsync(dto.LeaveTypeCode, cancellationToken))
            throw new InvalidOperationException("No leave policy is effective for this employee's cycle.");
        if (leaveType.TracksBalance && !IsApplicableToPolicy(emp, requestPolicy, requestSettings.EligibleCitizenshipCountryCode))
            throw new InvalidOperationException("The employee is not eligible under this leave policy.");
        if (dto.EndDate.Date > requestCycle.End)
            throw new InvalidOperationException("Split this leave request at the leave-year boundary before submission.");
        await _leaveRepo.EnsureLeaveCycleOpenAsync(dto.EmployeeCode, dto.LeaveTypeCode, requestCycle.Start, requestCycle.End, cancellationToken);

        var daysRequested = await CalculateRequestedWorkingDaysAsync(dto.StartDate.Date, dto.EndDate.Date, companyId, cancellationToken);
        if (daysRequested <= 0m)
            throw new InvalidOperationException("The requested date range contains no working days.");
        if (await _leaveRepo.HasConflictingLeaveAsync(dto.EmployeeCode, dto.StartDate.Date, dto.EndDate.Date,
                cancellationToken: cancellationToken))
            throw new InvalidOperationException("The employee already has a pending or approved leave or an approved short leave that overlaps these dates.");

        // Check the balance in the employee's configured leave cycle.
        if (leaveType.TracksBalance)
        {
            var balance = await _leaveRepo.GetLeaveBalanceByCycleAsync(dto.EmployeeCode, dto.LeaveTypeCode, requestCycle.Start, cancellationToken);
            var companyToday = GetLeaveCompanyToday(requestSettings);
            var asOfDate = companyToday < requestCycle.End ? companyToday : requestCycle.End;
            var accrued = balance?.AccruedDays ?? CalculateAccruedToDate(emp, leaveType, requestPolicy, asOfDate, requestSettings);
            var pending = await _leaveRepo.GetPendingLeaveDaysAsync(dto.EmployeeCode, dto.LeaveTypeCode, requestCycle.Start, requestCycle.End, cancellationToken: cancellationToken);
            decimal available = accrued + (balance?.CarriedForwardDays ?? 0m) - (balance?.UsedDays ?? 0m) - pending;

            if (daysRequested > available)
            {
                throw new InvalidOperationException($"Insufficient leave balance. Requested: {daysRequested} days, Available: {available} days.");
            }
        }

        var request = new LeaveRequest
        {
            EmployeeCode = dto.EmployeeCode,
            LeaveTypeCode = dto.LeaveTypeCode,
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate.Date,
            DaysRequested = daysRequested,
            Status = "PENDING",
            Reason = dto.Reason,
            AttachmentFileRef = dto.AttachmentFileRef,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        await _leaveRepo.AddLeaveRequestAsync(request, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return MapLeaveRequest(request, emp.NameLocal, leaveType.NameLocal);
    }

    private async Task<decimal> CalculateRequestedWorkingDaysAsync(DateTime start, DateTime end, long? companyId, CancellationToken cancellationToken)
    {
        if (_calendarService != null)
        {
            if (!companyId.HasValue)
                throw new InvalidOperationException("Tenant company context is required to calculate requested leave days.");
            return await _calendarService.CountWorkingDaysAsync(companyId.Value, start, end, cancellationToken);
        }

        var workingDays = 0;
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (day.DayOfWeek != DayOfWeek.Friday && day.DayOfWeek != DayOfWeek.Saturday)
                workingDays++;
        }

        return workingDays;
    }

    public async Task<LeaveDaysCalculationDto> CalculateLeaveDaysAsync(
        string employeeCode,
        string leaveTypeCode,
        DateTime startDate,
        DateTime endDate,
        long companyId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeCode))
            throw new ArgumentException("Employee code is required.", nameof(employeeCode));
        if (string.IsNullOrWhiteSpace(leaveTypeCode))
            throw new ArgumentException("Leave type code is required.", nameof(leaveTypeCode));
        var start = startDate.Date;
        var end = endDate.Date;
        if (end < start)
            throw new ArgumentException("End date cannot be earlier than start date.", nameof(endDate));

        var emp = await _employeeRepo.GetEmployeeByCodeAsync(employeeCode, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee '{employeeCode}' not found.");

        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave type '{leaveTypeCode}' not found.");
        if (!leaveType.IsActive)
            throw new InvalidOperationException($"Leave type '{leaveTypeCode}' is inactive.");

        var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var (policy, cycle) = await ResolveLeavePolicyCycleAsync(emp, leaveTypeCode, start, settings, cancellationToken);
        if (end > cycle.End)
            throw new InvalidOperationException("Split this leave calculation at the leave-year boundary before submission.");
        if (leaveType.TracksBalance)
        {
            if (policy == null && await HasLeavePolicyHistoryAsync(leaveTypeCode, cancellationToken))
                throw new InvalidOperationException("No leave policy is effective for this employee's cycle.");
            if (!IsApplicableToPolicy(emp, policy, settings.EligibleCitizenshipCountryCode))
                throw new InvalidOperationException("The employee is not eligible under this leave policy.");
        }

        var calendarDays = (end - start).Days + 1;

        int workingDays;
        if (_calendarService != null)
        {
            workingDays = await _calendarService.CountWorkingDaysAsync(companyId, start, end, cancellationToken);
        }
        else
        {
            workingDays = 0;
            for (var d = start; d <= end; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Friday && d.DayOfWeek != DayOfWeek.Saturday)
                    workingDays++;
            }
        }

        decimal available = 0m;
        DateTime? cycleStart = null;
        DateTime? cycleEnd = null;

        if (leaveType.TracksBalance)
        {
            cycleStart = cycle.Start;
            cycleEnd = cycle.End;

            var balance = await _leaveRepo.GetLeaveBalanceByCycleAsync(employeeCode, leaveTypeCode, cycle.Start, cancellationToken);
        var companyToday = GetLeaveCompanyToday(settings);
        var asOfDate = companyToday < cycle.End ? companyToday : cycle.End;
            var accrued = balance?.AccruedDays ?? CalculateAccruedToDate(emp, leaveType, policy, asOfDate, settings);
            var pending = await _leaveRepo.GetPendingLeaveDaysAsync(employeeCode, leaveTypeCode, cycle.Start, cycle.End, cancellationToken: cancellationToken);
            available = accrued + (balance?.CarriedForwardDays ?? 0m) - (balance?.UsedDays ?? 0m) - pending;
        }

        var hasSufficientBalance = !leaveType.TracksBalance || (available >= workingDays);

        return new LeaveDaysCalculationDto(
            employeeCode,
            leaveTypeCode,
            start,
            end,
            calendarDays,
            workingDays,
            available,
            hasSufficientBalance,
            cycleStart,
            cycleEnd);
    }

    public async Task<LeaveRequestDto> ProcessLeaveRequestAsync(long requestId, ProcessLeaveRequestDto dto, string user, CancellationToken cancellationToken = default)
    {
        var request = await _leaveRepo.GetLeaveRequestSnapshotAsync(requestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave request '{requestId}' was not found.");
        if (_unitOfWork == null)
            return await _leaveRepo.ExecuteForLeaveEmployeeAsync(request.EmployeeCode,
                () => ProcessLeaveRequestAsyncCore(requestId, dto, user, cancellationToken), cancellationToken);
        return await _unitOfWork.ExecuteForEmployeeDatesAsync(request.EmployeeCode, request.StartDate, request.EndDate,
            () => ProcessLeaveRequestAsyncCore(requestId, dto, user, cancellationToken), cancellationToken);
    }

    public async Task<LeaveRequestDto> CancelLeaveRequestAsync(string employeeCode, long requestId, CancelLeaveRequestDto dto,
        string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var reason = dto.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new ArgumentException("A cancellation reason between 1 and 500 characters is required.", nameof(dto.Reason));
        var snapshot = await _leaveRepo.GetLeaveRequestSnapshotAsync(requestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave request '{requestId}' was not found.");
        if (!string.Equals(snapshot.EmployeeCode, employeeCode, StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException("Leave request was not found for this employee.");

        if (_unitOfWork == null)
            return await _leaveRepo.ExecuteForLeaveEmployeeAsync(employeeCode,
                () => CancelLeaveRequestCoreAsync(requestId, reason, user, cancellationToken), cancellationToken);
        return await _unitOfWork.ExecuteForEmployeeDatesAsync(employeeCode, snapshot.StartDate, snapshot.EndDate,
            () => CancelLeaveRequestCoreAsync(requestId, reason, user, cancellationToken), cancellationToken);
    }

    private async Task<LeaveRequestDto> CancelLeaveRequestCoreAsync(long requestId, string reason, string user, CancellationToken cancellationToken)
    {
        var request = await _leaveRepo.GetLeaveRequestByIdAsync(requestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave request '{requestId}' was not found.");
        if (request.Status is not ("PENDING" or "APPROVED"))
            throw new InvalidOperationException($"Only pending or approved leave requests can be cancelled; current status is '{request.Status}'.");

        if (request.Status == "APPROVED")
        {
            var leaveType = request.LeaveType
                ?? throw new InvalidOperationException("Approved leave request has no leave type; reconcile the request before cancellation.");
            var usageKey = LeaveMovementKey.Hash($"REQUEST_USE:{request.Id}");
            var usage = await _leaveRepo.GetMovementByKeyAsync(usageKey, cancellationToken);
            if (usage != null || leaveType.TracksBalance)
            {
                if (usage == null)
                    throw new InvalidOperationException("The original leave usage movement is missing; reconcile the ledger before cancellation.");
                var employee = await _employeeRepo.GetEmployeeByCodeAsync(request.EmployeeCode, cancellationToken)
                    ?? throw new InvalidOperationException("Employee not found.");
                var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
                var (policy, cycle) = await ResolveLeavePolicyCycleAsync(employee, request.LeaveTypeCode, request.StartDate.Date, settings, cancellationToken);
                if (request.EndDate.Date > cycle.End)
                    throw new InvalidOperationException("The approved leave crosses a leave-cycle boundary; reconcile it before cancellation.");
                await _leaveRepo.EnsureLeaveCycleOpenAsync(request.EmployeeCode, request.LeaveTypeCode, cycle.Start, cycle.End, cancellationToken);

                var balance = await _leaveRepo.GetLeaveBalanceByCycleAsync(request.EmployeeCode, request.LeaveTypeCode, cycle.Start, cancellationToken)
                    ?? throw new InvalidOperationException("Approved leave has no saved balance; reconcile the balance before cancellation.");
                if (usage.BalanceId != balance.Id || usage.Kind != "USAGE" || usage.UsedDelta != request.DaysRequested)
                    throw new InvalidOperationException("The original leave usage movement does not match this approved request.");
                if (await _leaveRepo.HasMovementReversalAsync(usage.Id, cancellationToken))
                    throw new InvalidOperationException("The leave usage movement has already been reversed.");
                if (balance.UsedDays < usage.UsedDelta)
                    throw new InvalidOperationException("The balance no longer contains the request's usage; reconcile dependent movements before cancellation.");

                var before = LeaveBalanceState.From(balance);
                balance.UsedDays -= usage.UsedDelta;
                balance.UpdateUser = user;
                balance.UpdateDate = DateTime.UtcNow;
                await _leaveRepo.UpdateLeaveBalanceAsync(balance, cancellationToken);
                await _leaveRepo.RecordMovementAsync(balance, before, "REVERSAL",
                    LeaveMovementKey.Hash($"REQUEST_CANCEL:{request.Id}"), $"LEAVE_REQUEST_CANCEL:{request.Id}", reason, user,
                    reversesId: usage.Id, cancellationToken: cancellationToken);
            }
        }

        var cancelledAt = DateTime.UtcNow;
        request.Status = "CANCELLED";
        request.CancelledBy = user;
        request.CancellationDate = cancelledAt;
        request.CancellationReason = reason;
        request.UpdateUser = user;
        request.UpdateDate = cancelledAt;
        await _leaveRepo.UpdateLeaveRequestAsync(request, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return MapLeaveRequest(request);
    }

    private async Task<LeaveRequestDto> ProcessLeaveRequestAsyncCore(long requestId, ProcessLeaveRequestDto dto, string user, CancellationToken cancellationToken = default)
    {
        var request = await _leaveRepo.GetLeaveRequestByIdAsync(requestId, cancellationToken);
        if (request == null)
        {
            throw new InvalidOperationException($"Leave request with ID {requestId} not found.");
        }

        if (request.Status != "PENDING")
        {
            throw new InvalidOperationException($"Leave request {requestId} has already been processed with status '{request.Status}'.");
        }

        if (dto.Approved)
        {
            var employeeUserName = await _employeeRepo.GetLinkedUserNameByEmployeeCodeAsync(request.EmployeeCode, cancellationToken);
            HrApprovalGuard.EnsureNotSelfApproved(user, request.CreationUser, employeeUserName, "leave request");
            if (await _leaveRepo.HasConflictingLeaveAsync(request.EmployeeCode, request.StartDate.Date, request.EndDate.Date,
                    request.Id, cancellationToken))
                throw new InvalidOperationException("The employee already has another pending or approved leave or an approved short leave that overlaps these dates.");

            var closeEmployee = await _employeeRepo.GetEmployeeByCodeAsync(request.EmployeeCode, cancellationToken)
                ?? throw new InvalidOperationException("Employee not found.");
            var closeSettings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
            var (closePolicy, approvalCycle) = await ResolveLeavePolicyCycleAsync(closeEmployee, request.LeaveTypeCode, request.StartDate.Date, closeSettings, cancellationToken);
            if (request.LeaveType?.TracksBalance == true && closePolicy == null && await HasLeavePolicyHistoryAsync(request.LeaveTypeCode, cancellationToken))
                throw new InvalidOperationException("No leave policy is effective for this employee's cycle.");
            if (request.LeaveType?.TracksBalance == true && !IsApplicableToPolicy(closeEmployee, closePolicy, closeSettings.EligibleCitizenshipCountryCode))
                throw new InvalidOperationException("The employee is not eligible under this leave policy.");
            await _leaveRepo.EnsureLeaveCycleOpenAsync(request.EmployeeCode, request.LeaveTypeCode, approvalCycle.Start, approvalCycle.End, cancellationToken);
        }

        if (dto.Approved && request.LeaveType != null && request.LeaveType.TracksBalance)
        {
            var employee = await _employeeRepo.GetEmployeeByCodeAsync(request.EmployeeCode, cancellationToken)
                ?? throw new InvalidOperationException($"Employee '{request.EmployeeCode}' not found.");
            var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
            var (policy, cycle) = await ResolveLeavePolicyCycleAsync(employee, request.LeaveTypeCode, request.StartDate.Date, settings, cancellationToken);
            if (policy == null && await HasLeavePolicyHistoryAsync(request.LeaveTypeCode, cancellationToken))
                throw new InvalidOperationException("No leave policy is effective for this employee's cycle.");
            if (!IsApplicableToPolicy(employee, policy, settings.EligibleCitizenshipCountryCode))
                throw new InvalidOperationException("The employee is not eligible under this leave policy.");
            if (request.EndDate.Date > cycle.End)
                throw new InvalidOperationException("Split this leave request at the leave-year boundary before approval.");

            int year = cycle.Start.Year;
            var balance = await _leaveRepo.GetLeaveBalanceByCycleAsync(request.EmployeeCode, request.LeaveTypeCode, cycle.Start, cancellationToken);
            var beforeUsage = LeaveBalanceState.From(balance);
            var companyToday = GetLeaveCompanyToday(settings);
            var asOfDate = companyToday < cycle.End ? companyToday : cycle.End;
            var accruedToDate = CalculateAccruedToDate(employee, request.LeaveType, policy, asOfDate, settings);
            var accrued = balance?.AccruedDays ?? accruedToDate;
            var carried = balance?.CarriedForwardDays ?? 0m;
            var used = balance?.UsedDays ?? 0m;
            var pending = await _leaveRepo.GetPendingLeaveDaysAsync(request.EmployeeCode, request.LeaveTypeCode,
                cycle.Start, cycle.End, request.Id, cancellationToken);
            var available = accrued + carried - used - pending;
            if (request.DaysRequested <= 0 || request.DaysRequested > available)
                throw new InvalidOperationException("Insufficient leave balance at approval. Refresh the balance and review the request.");

            if (balance == null)
            {
                balance = new LeaveBalance
                {
                    EmployeeCode = request.EmployeeCode,
                    LeaveTypeCode = request.LeaveTypeCode,
                    YearNo = year,
                    CycleStartDate = cycle.Start,
                    CycleEndDate = cycle.End,
                    AccruedDays = accrued,
                    UsedDays = 0,
                    CarriedForwardDays = 0,
                    CreationUser = user,
                    CreationDate = DateTime.UtcNow
                };
                await _leaveRepo.AddLeaveBalanceAsync(balance, cancellationToken);
                if (balance.AccruedDays != 0)
                    await _leaveRepo.RecordMovementAsync(balance, beforeUsage, "ACCRUAL", LeaveMovementKey.Hash($"REQUEST_INIT:{request.Id}"),
                        $"LEAVE_REQUEST:{request.Id}", "Earned balance initialized at approval", user, cancellationToken: cancellationToken);
                beforeUsage = LeaveBalanceState.From(balance);
                balance.UsedDays = request.DaysRequested;
            }
            else
            {
                balance.UsedDays += request.DaysRequested;
                balance.UpdateUser = user;
                balance.UpdateDate = DateTime.UtcNow;
                await _leaveRepo.UpdateLeaveBalanceAsync(balance, cancellationToken);
            }
            await _leaveRepo.RecordMovementAsync(balance, beforeUsage, "USAGE", LeaveMovementKey.Hash($"REQUEST_USE:{request.Id}"),
                $"LEAVE_REQUEST:{request.Id}", string.IsNullOrWhiteSpace(request.Reason) ? "Approved leave request" : request.Reason, user, cancellationToken: cancellationToken);
        }

        request.Status = dto.Approved ? "APPROVED" : "REJECTED";
        request.ApprovedBy = user;
        request.ApprovalDate = DateTime.UtcNow;
        request.RejectionReason = dto.RejectionReason;
        request.UpdateUser = user;
        request.UpdateDate = DateTime.UtcNow;

        await _leaveRepo.UpdateLeaveRequestAsync(request, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return MapLeaveRequest(request);
    }

    private static LeaveRequestDto MapLeaveRequest(LeaveRequest request, string? employeeName = null, string? leaveTypeName = null)
    {
        var cancelled = string.Equals(request.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase);
        return new LeaveRequestDto(request.Id, request.EmployeeCode, employeeName ?? request.Employee?.NameLocal ?? request.EmployeeCode,
            request.LeaveTypeCode, leaveTypeName ?? request.LeaveType?.NameLocal ?? request.LeaveTypeCode, request.StartDate,
            request.EndDate, request.DaysRequested, request.Status, request.Reason, request.ApprovedBy, request.ApprovalDate,
            cancelled ? null : request.RejectionReason, request.CreationDate)
        {
            CancelledBy = cancelled ? request.CancelledBy ?? request.UpdateUser : null,
            CancellationDate = cancelled ? request.CancellationDate ?? request.UpdateDate : null,
            CancellationReason = cancelled ? request.CancellationReason ?? request.RejectionReason : null
        };
    }

    public async Task<IReadOnlyList<LeaveBalanceDto>> GetEmployeeBalancesAsync(string employeeCode, int? year = null, CancellationToken cancellationToken = default)
    {
        var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var companyToday = GetLeaveCompanyToday(settings);
        var allTypes = await _leaveRepo.GetLeaveTypesAsync(true, cancellationToken);
        var employee = await _employeeRepo.GetEmployeeByCodeAsync(employeeCode, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee '{employeeCode}' not found.");
        var result = new List<LeaveBalanceDto>();

        foreach (var type in allTypes.Where(t => t.TracksBalance))
        {
            var (policy, cycle) = year.HasValue
                ? await ResolveLeavePolicyCycleForLabelAsync(employee, type.LeaveTypeCode, year.Value, settings, cancellationToken)
                : await ResolveLeavePolicyCycleAsync(employee, type.LeaveTypeCode, companyToday, settings, cancellationToken);
            var noEffectivePolicy = policy == null && await HasLeavePolicyHistoryAsync(type.LeaveTypeCode, cancellationToken);
            var targetYear = cycle.Start.Year;
            var asOfDate = year.HasValue && companyToday > cycle.End ? cycle.End : companyToday;
            var bal = await _leaveRepo.GetLeaveBalanceByCycleAsync(employeeCode, type.LeaveTypeCode, cycle.Start, cancellationToken);
            // Existing balance rows remain authoritative (they include posted/manual transactions).
            // For employees with no row yet, expose the policy-earned amount instead of showing a
            // full annual quota before the employee has earned it.
            var cycleClosed = bal == null && await _leaveRepo.IsLeaveCycleClosedAsync(employeeCode, type.LeaveTypeCode,
                cycle.Start, cycle.End, cancellationToken);
            decimal accrued = bal?.AccruedDays ?? (companyToday < cycle.Start || cycleClosed || noEffectivePolicy ? 0m : CalculateAccruedToDate(employee, type, policy, asOfDate, settings));
            decimal carried = bal?.CarriedForwardDays ?? 0m;
            decimal used = bal?.UsedDays ?? 0m;
            decimal remaining = accrued + carried - used;
            var pending = await _leaveRepo.GetPendingLeaveDaysAsync(employeeCode, type.LeaveTypeCode, cycle.Start, cycle.End, cancellationToken: cancellationToken);

            var balanceDto = new LeaveBalanceDto(
                employeeCode,
                type.LeaveTypeCode,
                type.NameLocal,
                targetYear,
                accrued,
                used,
                carried,
                remaining
            )
            {
                CycleStartDate = bal?.CycleStartDate ?? cycle.Start,
                CycleEndDate = bal?.CycleEndDate ?? cycle.End,
                PendingDays = pending,
                AvailableDays = remaining - pending
            };
            result.Add(FormatBalance(balanceDto, settings));
        }

        return result;
    }

    private static LeaveBalanceDto FormatBalance(LeaveBalanceDto balance, HrLeaveSettings settings)
    {
        if (!string.Equals(settings.BalanceDisplayFormat, "DHM", StringComparison.OrdinalIgnoreCase) || settings.StandardWorkdayHours is not > 0m)
            return balance with { DisplayFormat = "DECIMAL" };

        return balance with
        {
            DisplayFormat = "DHM",
            AccruedDisplay = FormatDaysAsDhm(balance.AccruedDays, settings.StandardWorkdayHours.Value),
            UsedDisplay = FormatDaysAsDhm(balance.UsedDays, settings.StandardWorkdayHours.Value),
            CarriedForwardDisplay = FormatDaysAsDhm(balance.CarriedForwardDays, settings.StandardWorkdayHours.Value),
            RemainingDisplay = FormatDaysAsDhm(balance.RemainingDays, settings.StandardWorkdayHours.Value),
            PendingDisplay = FormatDaysAsDhm(balance.PendingDays, settings.StandardWorkdayHours.Value),
            AvailableDisplay = FormatDaysAsDhm(balance.AvailableDays, settings.StandardWorkdayHours.Value)
        };
    }

    private static string FormatDaysAsDhm(decimal days, decimal hoursPerWorkday)
    {
        var minutesPerWorkday = decimal.ToInt32(decimal.Round(hoursPerWorkday * 60m, 0, MidpointRounding.AwayFromZero));
        var totalMinutes = decimal.ToInt64(decimal.Round(days * minutesPerWorkday, 0, MidpointRounding.AwayFromZero));
        var sign = totalMinutes < 0 ? "-" : string.Empty;
        totalMinutes = Math.Abs(totalMinutes);
        var wholeDays = totalMinutes / minutesPerWorkday;
        var hours = totalMinutes % minutesPerWorkday / 60;
        var minutes = totalMinutes % 60;
        return $"{sign}{wholeDays}d {hours:00}h {minutes:00}m";
    }

    public async Task<HrLeaveSettingsDto> GetHrLeaveSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        return new HrLeaveSettingsDto(settings.LeaveYearStartMonth, settings.LeaveYearStartDay, settings.BalanceDisplayFormat,
            settings.StandardWorkdayHours, settings.AutomaticAccrualEnabled, settings.AccrualTimeZoneId, settings.EligibleCitizenshipCountryCode,
            settings.LeaveCashoutWageBasisCode, settings.LeaveCashoutBasisComponentCode, settings.LeaveCashoutDayDivisor, settings.LeaveCashoutPayComponentCode);
    }

    public async Task<HrLeaveSettingsDto> UpdateHrLeaveSettingsAsync(UpdateHrLeaveSettingsDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.LeaveYearStartMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(dto.LeaveYearStartMonth), "Leave year start month must be between 1 and 12.");
        if (dto.LeaveYearStartDay < 1 || dto.LeaveYearStartDay > DateTime.DaysInMonth(2000, dto.LeaveYearStartMonth))
            throw new ArgumentOutOfRangeException(nameof(dto.LeaveYearStartDay), "Leave year start day is invalid for the selected month.");
        var displayFormat = dto.BalanceDisplayFormat?.Trim().ToUpperInvariant();
        if (displayFormat is not ("DECIMAL" or "DHM"))
            throw new ArgumentException("Balance display format must be DECIMAL or DHM.", nameof(dto.BalanceDisplayFormat));
        if (displayFormat == "DHM" && (dto.StandardWorkdayHours is null or < 0.25m or > 24m))
            throw new ArgumentException("Standard workday hours must be between 0.25 and 24 for DHM display.", nameof(dto.StandardWorkdayHours));

        var current = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var cashoutBasis = dto.LeaveCashoutWageBasisCode == null
            ? current.LeaveCashoutWageBasisCode
            : string.IsNullOrWhiteSpace(dto.LeaveCashoutWageBasisCode) ? null : dto.LeaveCashoutWageBasisCode.Trim().ToUpperInvariant();
        var basisComponentCode = dto.LeaveCashoutBasisComponentCode == null
            ? current.LeaveCashoutBasisComponentCode
            : string.IsNullOrWhiteSpace(dto.LeaveCashoutBasisComponentCode) ? null : dto.LeaveCashoutBasisComponentCode.Trim().ToUpperInvariant();
        var cashoutDivisor = dto.LeaveCashoutDayDivisor ?? current.LeaveCashoutDayDivisor;
        var payComponentCode = dto.LeaveCashoutPayComponentCode == null
            ? current.LeaveCashoutPayComponentCode
            : string.IsNullOrWhiteSpace(dto.LeaveCashoutPayComponentCode) ? null : dto.LeaveCashoutPayComponentCode.Trim().ToUpperInvariant();
        if (cashoutBasis == null)
        {
            basisComponentCode = null;
            cashoutDivisor = null;
            payComponentCode = null;
        }
        else
        {
            if (cashoutBasis is not ("BASIC" or "GROSS_EARNINGS" or "SALARY_COMPONENT"))
                throw new ArgumentException("Leave cashout wage basis must be BASIC, GROSS_EARNINGS, or SALARY_COMPONENT.", nameof(dto.LeaveCashoutWageBasisCode));
            if (cashoutDivisor is null or <= 0m or > 999999.9999m)
                throw new ArgumentException("A positive company-configured leave cashout day divisor is required.", nameof(dto.LeaveCashoutDayDivisor));
            if (cashoutBasis == "SALARY_COMPONENT" && string.IsNullOrWhiteSpace(basisComponentCode))
                throw new ArgumentException("A salary component code is required for the SALARY_COMPONENT wage basis.", nameof(dto.LeaveCashoutBasisComponentCode));
            if (cashoutBasis != "SALARY_COMPONENT") basisComponentCode = null;
            if (string.IsNullOrWhiteSpace(payComponentCode))
                throw new ArgumentException("A payroll earning component code is required for leave cashout.", nameof(dto.LeaveCashoutPayComponentCode));

            var payComponent = await _employeeRepo.GetSalaryComponentByCodeAsync(payComponentCode, cancellationToken);
            var payComponentType = payComponent?.ComponentType?.Trim().ToUpperInvariant();
            if (payComponent == null || !payComponent.IsActive || payComponentType is "DEDUCTION" or "EMPLOYER_CONTRIB")
                throw new ArgumentException("The configured leave cashout payroll component must exist, be active, and represent an earning.", nameof(dto.LeaveCashoutPayComponentCode));
            if (cashoutBasis == "SALARY_COMPONENT")
            {
                var basisComponent = await _employeeRepo.GetSalaryComponentByCodeAsync(basisComponentCode!, cancellationToken);
                var basisComponentType = basisComponent?.ComponentType?.Trim().ToUpperInvariant();
                if (basisComponent == null || !basisComponent.IsActive || basisComponentType is "DEDUCTION" or "EMPLOYER_CONTRIB")
                    throw new ArgumentException("The configured wage basis component must exist, be active, and represent an earning.", nameof(dto.LeaveCashoutBasisComponentCode));
            }
        }
        var eligibleCitizenshipCountryCode = dto.EligibleCitizenshipCountryCode == null
            ? current.EligibleCitizenshipCountryCode
            : CountryCodeRules.NormalizeOptional(dto.EligibleCitizenshipCountryCode);
        if (string.IsNullOrEmpty(eligibleCitizenshipCountryCode)) eligibleCitizenshipCountryCode = null;
        var accrualEnabled = dto.AutomaticAccrualEnabled ?? current.AutomaticAccrualEnabled;
        var accrualTimeZoneId = dto.AccrualTimeZoneId == null
            ? current.AccrualTimeZoneId
            : string.IsNullOrWhiteSpace(dto.AccrualTimeZoneId) ? null : dto.AccrualTimeZoneId.Trim();
        if (accrualEnabled && string.IsNullOrWhiteSpace(accrualTimeZoneId))
            throw new ArgumentException("An IANA or Windows time-zone ID is required when automatic leave accrual is enabled.", nameof(dto.AccrualTimeZoneId));
        if (accrualTimeZoneId != null)
        {
            try { _ = TimeZoneInfo.FindSystemTimeZoneById(accrualTimeZoneId); }
            catch (TimeZoneNotFoundException ex) { throw new ArgumentException("The leave accrual time-zone ID is not recognized.", nameof(dto.AccrualTimeZoneId), ex); }
            catch (InvalidTimeZoneException ex) { throw new ArgumentException("The leave accrual time-zone definition is invalid.", nameof(dto.AccrualTimeZoneId), ex); }
        }
        var leaveYearChanged = current.LeaveYearStartMonth != dto.LeaveYearStartMonth || current.LeaveYearStartDay != dto.LeaveYearStartDay;
        if (leaveYearChanged && await _leaveRepo.HasLeaveBalancesAsync(cancellationToken))
            throw new InvalidOperationException("The leave-year start cannot be changed after balances exist. Migrate current opening balances first so accrued and used days are not assigned to the wrong cycle.");

        var settings = new HrLeaveSettings
        {
            Id = 1,
            LeaveYearStartMonth = dto.LeaveYearStartMonth,
            LeaveYearStartDay = dto.LeaveYearStartDay,
            BalanceDisplayFormat = displayFormat,
            StandardWorkdayHours = dto.StandardWorkdayHours,
            AutomaticAccrualEnabled = accrualEnabled,
            AccrualTimeZoneId = accrualTimeZoneId,
            EligibleCitizenshipCountryCode = eligibleCitizenshipCountryCode,
            LeaveCashoutWageBasisCode = cashoutBasis,
            LeaveCashoutBasisComponentCode = basisComponentCode,
            LeaveCashoutDayDivisor = cashoutDivisor,
            LeaveCashoutPayComponentCode = payComponentCode,
            UpdateUser = user,
            UpdateDate = DateTime.UtcNow
        };
        await _leaveRepo.SaveHrLeaveSettingsAsync(settings, current.LeaveYearStartMonth, current.LeaveYearStartDay, cancellationToken);
        return new HrLeaveSettingsDto(settings.LeaveYearStartMonth, settings.LeaveYearStartDay, settings.BalanceDisplayFormat,
            settings.StandardWorkdayHours, settings.AutomaticAccrualEnabled, settings.AccrualTimeZoneId, settings.EligibleCitizenshipCountryCode,
            settings.LeaveCashoutWageBasisCode, settings.LeaveCashoutBasisComponentCode, settings.LeaveCashoutDayDivisor, settings.LeaveCashoutPayComponentCode);
    }

    private static DateTime GetLeaveCompanyToday(HrLeaveSettings settings)
    {
        var utcNow = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(settings.AccrualTimeZoneId)) return utcNow.Date;

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.AccrualTimeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone).Date;
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new InvalidOperationException("The configured leave accrual time zone is not available.", ex);
        }
        catch (InvalidTimeZoneException ex)
        {
            throw new InvalidOperationException("The configured leave accrual time zone is invalid.", ex);
        }
    }

    private static decimal CalculateAccruedToDate(Employee employee, LeaveType leaveType, LeavePolicy? policy, DateTime asOfDate, HrLeaveSettings settings)
    {
        if (policy is { IsActive: false }) return 0m;
        if (!IsApplicableToPolicy(employee, policy, settings.EligibleCitizenshipCountryCode)) return 0m;
        var eligibleFrom = employee.HireDate.Date.AddMonths(policy?.MinServiceMonths ?? 0);
        if (eligibleFrom > asOfDate.Date)
            return 0m;

        var completedServiceYears = GetCompletedServiceYears(employee.HireDate, asOfDate);
        var annualQuota = GetAnnualEntitlement(policy, leaveType, completedServiceYears);
        var method = policy?.AccrualMethod?.ToUpperInvariant() ?? "YEARLY";

        if (method == "YEARLY")
        {
            if (policy?.ProrateFirstCycle != true)
                return annualQuota;

            var cycle = GetAccrualCycle(employee.HireDate.Date, asOfDate.Date, policy?.CycleBasis, settings);
            if (eligibleFrom <= cycle.Start)
                return annualQuota;
            var effectiveStart = eligibleFrom;
            var earnedDays = Math.Max(0, (asOfDate.Date - effectiveStart).Days + 1);
            return Math.Round(annualQuota * earnedDays / cycle.Days, 4, MidpointRounding.AwayFromZero);
        }

        if (method == "DAILY")
        {
            var cycle = GetAccrualCycle(employee.HireDate.Date, asOfDate.Date, policy?.CycleBasis, settings);
            var effectiveStart = eligibleFrom > cycle.Start ? eligibleFrom : cycle.Start;
            var earnedDays = Math.Max(0, (asOfDate.Date - effectiveStart).Days + 1);
            return Math.Round(annualQuota * earnedDays / cycle.Days, 4, MidpointRounding.AwayFromZero);
        }

        if (method == "MONTHLY")
        {
            var cycle = GetAccrualCycle(employee.HireDate.Date, asOfDate.Date, policy?.CycleBasis, settings);
            var monthStart = new DateTime(eligibleFrom.Year, eligibleFrom.Month, 1);
            var monthCursor = monthStart < cycle.Start ? new DateTime(cycle.Start.Year, cycle.Start.Month, 1) : monthStart;
            decimal earned = 0m;
            while (monthCursor <= asOfDate.Date)
            {
                var monthEnd = new DateTime(monthCursor.Year, monthCursor.Month, DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month));
                var effectiveStart = eligibleFrom > monthCursor ? eligibleFrom : monthCursor;
                if (cycle.Start > effectiveStart) effectiveStart = cycle.Start;
                var effectiveEnd = asOfDate.Date < monthEnd ? asOfDate.Date : monthEnd;
                if (effectiveEnd >= effectiveStart)
                    earned += (annualQuota / 12m) * ((effectiveEnd - effectiveStart).Days + 1) / DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month);
                monthCursor = monthCursor.AddMonths(1);
            }
            return Math.Round(earned, 4, MidpointRounding.AwayFromZero);
        }

        return 0m;
    }

    private static (DateTime Start, DateTime End, int Days) GetAccrualCycle(DateTime hireDate, DateTime asOfDate, string? cycleBasis, HrLeaveSettings settings)
    {
        if (string.Equals(cycleBasis, "EMPLOYMENT_ANNIVERSARY", StringComparison.OrdinalIgnoreCase))
        {
            var anniversary = new DateTime(asOfDate.Year, hireDate.Month, Math.Min(hireDate.Day, DateTime.DaysInMonth(asOfDate.Year, hireDate.Month)));
            var start = anniversary <= asOfDate ? anniversary : anniversary.AddYears(-1);
            var end = start.AddYears(1).AddDays(-1);
            return (start, end, (end - start).Days + 1);
        }

        // CALENDAR_YEAR is retained as a legacy policy value. The company-level leave year
        // setting is authoritative for all non-anniversary cycles.
        var companyCycle = GetAccrualCycleForLabel(hireDate, cycleBasis, asOfDate.Year, settings);
        if (asOfDate < companyCycle.Start)
            companyCycle = GetAccrualCycleForLabel(hireDate, cycleBasis, asOfDate.Year - 1, settings);
        return (companyCycle.Start, companyCycle.End, (companyCycle.End - companyCycle.Start).Days + 1);
    }

    private static (DateTime Start, DateTime End, int Days) GetAccrualCycleForLabel(DateTime hireDate, string? cycleBasis, int cycleYear, HrLeaveSettings settings)
    {
        DateTime start;
        if (string.Equals(cycleBasis, "EMPLOYMENT_ANNIVERSARY", StringComparison.OrdinalIgnoreCase))
            start = new DateTime(cycleYear, hireDate.Month, Math.Min(hireDate.Day, DateTime.DaysInMonth(cycleYear, hireDate.Month)));
        else
            start = new DateTime(cycleYear, settings.LeaveYearStartMonth, Math.Min(settings.LeaveYearStartDay, DateTime.DaysInMonth(cycleYear, settings.LeaveYearStartMonth)));
        var end = start.AddYears(1).AddDays(-1);
        return (start, end, (end - start).Days + 1);
    }

    private async Task<(LeavePolicy? Policy, (DateTime Start, DateTime End, int Days) Cycle)> ResolveLeavePolicyCycleAsync(
        Employee employee, string leaveTypeCode, DateTime asOfDate, HrLeaveSettings settings, CancellationToken cancellationToken)
    {
        var policy = await _leaveRepo.GetLeavePolicyByTypeCodeAsync(leaveTypeCode, cancellationToken, asOfDate.Date);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var cycle = GetAccrualCycle(employee.HireDate.Date, asOfDate.Date, policy?.CycleBasis, settings);
            var cyclePolicy = await _leaveRepo.GetLeavePolicyByTypeCodeAsync(leaveTypeCode, cancellationToken, cycle.Start);
            if (cyclePolicy?.Id == policy?.Id) return (cyclePolicy, cycle);
            policy = cyclePolicy;
        }
        throw new InvalidOperationException($"Could not resolve a stable leave policy cycle for '{leaveTypeCode}' and employee '{employee.EmployeeCode}'.");
    }

    private async Task<bool> HasLeavePolicyHistoryAsync(string leaveTypeCode, CancellationToken cancellationToken)
    {
        var versions = await _leaveRepo.GetLeavePolicyVersionsAsync(leaveTypeCode, cancellationToken);
        return versions?.Count > 0;
    }

    private async Task<(LeavePolicy? Policy, (DateTime Start, DateTime End, int Days) Cycle)> ResolveLeavePolicyCycleForLabelAsync(
        Employee employee, string leaveTypeCode, int cycleYear, HrLeaveSettings settings, CancellationToken cancellationToken)
    {
        var candidate = GetAccrualCycleForLabel(employee.HireDate.Date, null, cycleYear, settings);
        var policy = await _leaveRepo.GetLeavePolicyByTypeCodeAsync(leaveTypeCode, cancellationToken, candidate.Start);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var cycle = GetAccrualCycleForLabel(employee.HireDate.Date, policy?.CycleBasis, cycleYear, settings);
            var cyclePolicy = await _leaveRepo.GetLeavePolicyByTypeCodeAsync(leaveTypeCode, cancellationToken, cycle.Start);
            if (cyclePolicy?.Id == policy?.Id) return (cyclePolicy, cycle);
            policy = cyclePolicy;
        }
        throw new InvalidOperationException($"Could not resolve a stable leave policy cycle for '{leaveTypeCode}' and employee '{employee.EmployeeCode}'.");
    }

    private static decimal CalculateLegacyMonthlyEarned(decimal annualQuota, DateTime eligibleFrom, DateTime asOfDate, DateTime cycleStart)
    {
        var firstEligibleMonth = new DateTime(eligibleFrom.Year, eligibleFrom.Month, 1);
        var firstCycleMonth = new DateTime(cycleStart.Year, cycleStart.Month, 1);
        var monthCursor = firstEligibleMonth < firstCycleMonth ? firstCycleMonth : firstEligibleMonth;
        decimal earned = 0m;
        while (monthCursor <= asOfDate.Date)
        {
            var monthEnd = new DateTime(monthCursor.Year, monthCursor.Month, DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month));
            var effectiveStart = eligibleFrom > monthCursor ? eligibleFrom : monthCursor;
            if (cycleStart > effectiveStart) effectiveStart = cycleStart;
            var effectiveEnd = asOfDate.Date < monthEnd ? asOfDate.Date : monthEnd;
            if (effectiveEnd >= effectiveStart)
                earned += (annualQuota / 12m) * ((effectiveEnd - effectiveStart).Days + 1) / DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month);
            monthCursor = monthCursor.AddMonths(1);
        }
        return Math.Round(earned, 4, MidpointRounding.AwayFromZero);
    }

    public async Task<decimal> GetUnpaidLeaveDaysInPeriodAsync(string employeeCode, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default)
    {
        var unpaidRequests = await _leaveRepo.GetApprovedUnpaidLeavesInPeriodAsync(employeeCode, periodStart, periodEnd, cancellationToken);
        decimal totalUnpaidDays = 0m;

        foreach (var req in unpaidRequests)
        {
            var effectiveStart = req.StartDate > periodStart ? req.StartDate.Date : periodStart.Date;
            var effectiveEnd = req.EndDate < periodEnd ? req.EndDate.Date : periodEnd.Date;

            if (effectiveEnd >= effectiveStart)
            {
                var days = (effectiveEnd - effectiveStart).Days + 1;
                totalUnpaidDays += days;
            }
        }

        return totalUnpaidDays;
    }

    public Task<LeaveBalanceDto> GrantOrAdjustBalanceAsync(GrantLeaveBalanceDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return _leaveRepo.ExecuteForLeaveEmployeeAsync(dto.EmployeeCode,
            () => GrantOrAdjustBalanceCoreAsync(dto, user, cancellationToken), cancellationToken);
    }

    private async Task<LeaveBalanceDto> GrantOrAdjustBalanceCoreAsync(GrantLeaveBalanceDto dto, string user, CancellationToken cancellationToken)
    {
        var emp = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Employee '{dto.EmployeeCode}' not found.");

        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(dto.LeaveTypeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Leave type '{dto.LeaveTypeCode}' not found.");

        var leaveSettings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var hasPolicyHistory = await HasLeavePolicyHistoryAsync(dto.LeaveTypeCode, cancellationToken);
        var (policy, cycle) = await ResolveLeavePolicyCycleForLabelAsync(emp, dto.LeaveTypeCode, dto.YearNo, leaveSettings, cancellationToken);
        if (policy == null && hasPolicyHistory)
            throw new InvalidOperationException("No leave policy is effective for this employee's cycle.");
        if (string.IsNullOrWhiteSpace(dto.IdempotencyKey) || dto.IdempotencyKey.Length > 200)
            throw new ArgumentException("IdempotencyKey (1..200 characters) is required for manual balance changes.");
        if (decimal.Round(dto.Days, 4) != dto.Days)
            throw new ArgumentException("Manual balance days must have at most four decimal places.");
        if (dto.Days > 99_999_999.9999m || dto.Days < -99_999_999.9999m)
            throw new ArgumentOutOfRangeException(nameof(dto.Days), "Manual balance days exceed the NUMBER(12,4) storage limit.");
        if (dto.Days == 0m) throw new ArgumentException("Manual balance change must be non-zero.");
        var adjustmentType = dto.AdjustmentType?.Trim().ToUpperInvariant() ?? string.Empty;
        if (adjustmentType is not ("OPENING_BALANCE" or "MANUAL_GRANT" or "COMPENSATORY" or "CORRECTION"))
            throw new ArgumentException("AdjustmentType must be OPENING_BALANCE, MANUAL_GRANT, COMPENSATORY, or CORRECTION.");
        if (adjustmentType != "CORRECTION" && dto.Days < 0m)
            throw new ArgumentException("Only a CORRECTION may reduce the leave balance.");
        var reason = dto.Reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > 1000)
            throw new ArgumentException("A reason from 1 to 1000 characters is required for manual balance changes.");
        var operationKey = LeaveMovementKey.Hash("MANUAL:" + dto.IdempotencyKey.Trim());
        var payload = LeaveMovementKey.Hash(System.Text.Json.JsonSerializer.Serialize(new { dto.EmployeeCode, dto.LeaveTypeCode, dto.YearNo,
            Days = dto.Days.ToString("G29", System.Globalization.CultureInfo.InvariantCulture), Type = adjustmentType, Reason = reason }));
        var replay = await _leaveRepo.GetMovementByKeyAsync(operationKey, cancellationToken);
        if (replay != null)
        {
            if (replay.PayloadHash != payload) throw new InvalidOperationException("IdempotencyKey was already used for a different balance change.");
            var replayPending = await _leaveRepo.GetPendingLeaveDaysAsync(replay.EmployeeCode, replay.LeaveTypeCode,
                cycle.Start, cycle.End, cancellationToken: cancellationToken);
            var replayRemaining = replay.AccruedAfter + replay.CarriedAfter - replay.UsedAfter;
            return FormatBalance(new LeaveBalanceDto(replay.EmployeeCode, replay.LeaveTypeCode, leaveType.NameLocal, replay.CycleStartDate.Year,
                replay.AccruedAfter, replay.UsedAfter, replay.CarriedAfter, replayRemaining)
            { PendingDays = replayPending, AvailableDays = replayRemaining - replayPending }, leaveSettings);
        }
        await _leaveRepo.EnsureLeaveCycleOpenAsync(dto.EmployeeCode, dto.LeaveTypeCode, cycle.Start, cycle.End, cancellationToken);

        var balance = await _leaveRepo.GetLeaveBalanceByCycleAsync(dto.EmployeeCode, dto.LeaveTypeCode, cycle.Start, cancellationToken);
        var before = LeaveBalanceState.From(balance);
        var pendingDays = await _leaveRepo.GetPendingLeaveDaysAsync(dto.EmployeeCode, dto.LeaveTypeCode,
            cycle.Start, cycle.End, cancellationToken: cancellationToken);
        if (balance == null)
        {
            if (dto.Days < 0m)
                throw new InvalidOperationException("A negative manual correction cannot create a balance.");
            if (dto.Days < pendingDays)
                throw new InvalidOperationException("Manual balance change would leave pending requests without reserved days.");
            balance = new LeaveBalance
            {
                EmployeeCode = dto.EmployeeCode,
                LeaveTypeCode = dto.LeaveTypeCode,
                YearNo = dto.YearNo,
                CycleStartDate = cycle.Start,
                CycleEndDate = cycle.End,
                AccruedDays = dto.Days,
                UsedDays = 0,
                CarriedForwardDays = 0,
                CreationUser = user,
                CreationDate = DateTime.UtcNow
            };
            await _leaveRepo.AddLeaveBalanceAsync(balance, cancellationToken);
        }
        else
        {
            balance.CycleStartDate ??= cycle.Start;
            balance.CycleEndDate ??= cycle.End;
            var resultingAccrued = adjustmentType == "OPENING_BALANCE" ? dto.Days : balance.AccruedDays + dto.Days;
            if (resultingAccrued < 0m || resultingAccrued + balance.CarriedForwardDays - balance.UsedDays < 0m)
                throw new InvalidOperationException("Manual balance change exceeds the available balance; review usage and dependent leave requests.");
            if (resultingAccrued + balance.CarriedForwardDays - balance.UsedDays < pendingDays)
                throw new InvalidOperationException("Manual balance change would leave pending requests without reserved days.");
            balance.AccruedDays = resultingAccrued;
            balance.UpdateUser = user;
            balance.UpdateDate = DateTime.UtcNow;
            await _leaveRepo.UpdateLeaveBalanceAsync(balance, cancellationToken);
        }

        var movementKind = adjustmentType switch
        {
            "OPENING_BALANCE" => "OPENING_BALANCE",
            "CORRECTION" => "CORRECTION",
            "COMPENSATORY" => "COMPENSATORY",
            _ => "MANUAL_GRANT"
        };
        await _leaveRepo.RecordMovementAsync(balance, before, movementKind, operationKey, "MANUAL:" + dto.IdempotencyKey.Trim(),
            reason, user, payloadHash: payload, cancellationToken: cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        decimal remaining = (balance.AccruedDays + balance.CarriedForwardDays) - balance.UsedDays;
        var balanceDto = new LeaveBalanceDto(
            balance.EmployeeCode,
            balance.LeaveTypeCode,
            leaveType.NameLocal,
            balance.YearNo,
            balance.AccruedDays,
            balance.UsedDays,
            balance.CarriedForwardDays,
            remaining
        ) { PendingDays = pendingDays, AvailableDays = remaining - pendingDays };
        var displaySettings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        return FormatBalance(balanceDto, displaySettings);
    }

    public Task<LeaveBalanceDto> ReverseManualBalanceMovementAsync(string employeeCode, long movementId,
        ReverseLeaveBalanceMovementDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var normalizedEmployeeCode = employeeCode?.Trim() ?? string.Empty;
        var normalizedReason = dto.Reason?.Trim() ?? string.Empty;
        if (normalizedEmployeeCode.Length == 0) throw new ArgumentException("EmployeeCode is required.", nameof(employeeCode));
        if (movementId <= 0) throw new ArgumentOutOfRangeException(nameof(movementId));
        if (normalizedReason.Length is 0 or > 1000) throw new ArgumentException("A reversal reason from 1 to 1000 characters is required.", nameof(dto));
        return _leaveRepo.ExecuteForLeaveEmployeeAsync(normalizedEmployeeCode,
            () => ReverseManualBalanceMovementCoreAsync(normalizedEmployeeCode, movementId, normalizedReason, user, cancellationToken), cancellationToken);
    }

    private async Task<LeaveBalanceDto> ReverseManualBalanceMovementCoreAsync(string employeeCode, long movementId,
        string reason, string user, CancellationToken cancellationToken)
    {
        var original = await _leaveRepo.GetMovementByIdAsync(movementId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave movement '{movementId}' was not found.");
        if (!string.Equals(original.EmployeeCode, employeeCode, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Leave movement '{movementId}' was not found for employee '{employeeCode}'.");
        if (original.Kind is not ("OPENING_BALANCE" or "MANUAL_GRANT" or "COMPENSATORY" or "CORRECTION") ||
            !original.Source.StartsWith("MANUAL:", StringComparison.Ordinal))
            throw new InvalidOperationException("Only a manual opening, grant, or correction movement can be reversed through this operation.");

        var operationKey = LeaveMovementKey.Hash($"MANUAL_REVERSAL:{original.Id}");
        var payload = LeaveMovementKey.Hash(System.Text.Json.JsonSerializer.Serialize(new { MovementId = original.Id, Reason = reason }));
        var replay = await _leaveRepo.GetMovementByKeyAsync(operationKey, cancellationToken);
        if (replay != null)
        {
            if (replay.PayloadHash != payload)
                throw new InvalidOperationException("This manual balance movement was already reversed with a different reason.");
            return await FormatManualMovementBalanceAsync(replay, cancellationToken);
        }
        if (await _leaveRepo.HasMovementReversalAsync(original.Id, cancellationToken))
            throw new InvalidOperationException("This manual balance movement was already reversed.");
        if (await _leaveRepo.HasActiveMovementAfterAsync(original.BalanceId, original.SequenceNo, cancellationToken))
            throw new InvalidOperationException("Reverse later balance movements before reversing this manual adjustment.");

        var balance = await _leaveRepo.GetLeaveBalanceByCycleAsync(employeeCode, original.LeaveTypeCode,
            original.CycleStartDate, cancellationToken)
            ?? throw new InvalidOperationException("The original leave balance no longer exists.");
        var cycleStart = balance.CycleStartDate ?? new DateTime(balance.YearNo, 1, 1);
        var cycleEnd = balance.CycleEndDate ?? new DateTime(balance.YearNo, 12, 31);
        await _leaveRepo.EnsureLeaveCycleOpenAsync(employeeCode, original.LeaveTypeCode, cycleStart, cycleEnd, cancellationToken);
        var before = LeaveBalanceState.From(balance);
        if (before.Accrued != original.AccruedAfter || before.Used != original.UsedAfter || before.Carried != original.CarriedAfter)
            throw new InvalidOperationException("The balance changed after this manual adjustment; reverse dependent movements first.");

        balance.AccruedDays = before.Accrued - original.AccruedDelta;
        balance.UsedDays = before.Used - original.UsedDelta;
        balance.CarriedForwardDays = before.Carried - original.CarriedDelta;
        var availableAfter = balance.AccruedDays + balance.CarriedForwardDays - balance.UsedDays;
        if (balance.AccruedDays < 0m || balance.UsedDays < 0m || balance.CarriedForwardDays < 0m || availableAfter < 0m)
            throw new InvalidOperationException("Reversing this manual adjustment would create a negative leave balance.");
        var pendingDays = await _leaveRepo.GetPendingLeaveDaysAsync(employeeCode, original.LeaveTypeCode,
            cycleStart, cycleEnd, cancellationToken: cancellationToken);
        if (availableAfter < pendingDays)
            throw new InvalidOperationException("Reversing this manual adjustment would invalidate pending leave requests.");
        balance.UpdateUser = user;
        balance.UpdateDate = DateTime.UtcNow;
        await _leaveRepo.UpdateLeaveBalanceAsync(balance, cancellationToken);
        await _leaveRepo.RecordMovementAsync(balance, before, "REVERSAL", operationKey, $"MANUAL_REVERSAL:{original.Id}",
            reason, user, payloadHash: payload, reversesId: original.Id, cancellationToken: cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        var reversal = await _leaveRepo.GetMovementByKeyAsync(operationKey, cancellationToken)
            ?? throw new InvalidOperationException("The manual adjustment reversal was not recorded.");
        return await FormatManualMovementBalanceAsync(reversal, cancellationToken);
    }

    private async Task<LeaveBalanceDto> FormatManualMovementBalanceAsync(LeaveBalanceMovement movement, CancellationToken cancellationToken)
    {
        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(movement.LeaveTypeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Leave type '{movement.LeaveTypeCode}' no longer exists.");
        var sourceBalance = await _leaveRepo.GetLeaveBalanceByCycleAsync(movement.EmployeeCode, movement.LeaveTypeCode,
            movement.CycleStartDate, cancellationToken)
            ?? throw new InvalidOperationException("The original leave balance no longer exists.");
        var cycleEnd = sourceBalance.CycleEndDate ?? new DateTime(sourceBalance.YearNo, 12, 31);
        var pending = await _leaveRepo.GetPendingLeaveDaysAsync(movement.EmployeeCode, movement.LeaveTypeCode,
            movement.CycleStartDate, cycleEnd, cancellationToken: cancellationToken);
        var remaining = movement.AccruedAfter + movement.CarriedAfter - movement.UsedAfter;
        var balance = new LeaveBalanceDto(movement.EmployeeCode, movement.LeaveTypeCode, leaveType.NameLocal,
            movement.CycleStartDate.Year, movement.AccruedAfter, movement.UsedAfter, movement.CarriedAfter, remaining)
        { PendingDays = pending, AvailableDays = remaining - pending };
        return FormatBalance(balance, await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken));
    }

    public async Task<LeaveAccrualResultDto> RunMonthlyAccrualAsync(MonthlyLeaveAccrualDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.MonthNo < 1 || dto.MonthNo > 12)
            throw new ArgumentException("Month must be between 1 and 12.", nameof(dto.MonthNo));

        var leaveTypeCode = string.IsNullOrWhiteSpace(dto.LeaveTypeCode) ? "ANNUAL" : dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Leave type '{leaveTypeCode}' not found.");
        if (!leaveType.IsActive)
            throw new InvalidOperationException($"Leave type '{leaveTypeCode}' is inactive.");
        if (!leaveType.TracksBalance)
            throw new InvalidOperationException($"Leave type '{leaveTypeCode}' does not track a balance.");

        var leaveSettings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var hasPolicyHistory = await HasLeavePolicyHistoryAsync(leaveTypeCode, cancellationToken);

        var employees = new List<Employee>();
        if (!string.IsNullOrWhiteSpace(dto.EmployeeCode))
        {
            var emp = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken);
            if (emp != null && emp.IsActive && emp.EmploymentStatus == "ACTIVE")
                employees.Add(emp);
        }
        else
        {
            var all = await _employeeRepo.GetAllEmployeesForExportAsync(status: "ACTIVE", cancellationToken: cancellationToken);
            employees.AddRange(all);
        }

        var accrualDate = new DateTime(dto.YearNo, dto.MonthNo, DateTime.DaysInMonth(dto.YearNo, dto.MonthNo));
        var details = new List<string>();
        int updatedCount = 0;
        decimal totalAccrued = 0m;

        foreach (var emp in employees)
        {
            var (policy, cycle) = await ResolveLeavePolicyCycleAsync(emp, leaveTypeCode, accrualDate, leaveSettings, cancellationToken);
            if (policy is { IsActive: false }) continue;
            if (policy == null && hasPolicyHistory) continue;
            if (policy != null && !string.Equals(policy.AccrualMethod, "MONTHLY", StringComparison.OrdinalIgnoreCase))
                continue;
            var eligibleFrom = emp.HireDate.Date.AddMonths(policy?.MinServiceMonths ?? 0);
            if (eligibleFrom > accrualDate)
            {
                // Not yet eligible under the company's service rule.
                continue;
            }
            if (!IsApplicableToPolicy(emp, policy, leaveSettings.EligibleCitizenshipCountryCode)) continue;

            // Check tenure in years
            var completedServiceYears = GetCompletedServiceYears(emp.HireDate, accrualDate);
            var annualQuota = GetAnnualEntitlement(policy, leaveType, completedServiceYears);

            // Accrue up to the earned amount through the requested month end. Setting the
            // balance to this target (by adding only the difference) makes retries idempotent.
            var earnedToDate = CalculateAccruedToDate(emp, leaveType, policy, accrualDate, leaveSettings);
            // Legacy policies without a policy row used to accrue monthly; retain that behavior.
            if (policy == null)
                earnedToDate = CalculateLegacyMonthlyEarned(annualQuota, eligibleFrom, accrualDate, cycle.Start);

            var daysToAdd = await _leaveRepo.ApplyAccrualTargetAsync(new LeaveBalance
            {
                EmployeeCode = emp.EmployeeCode,
                LeaveTypeCode = leaveTypeCode,
                YearNo = cycle.Start.Year,
                CycleStartDate = cycle.Start,
                CycleEndDate = cycle.End,
                AccruedDays = earnedToDate,
                UsedDays = 0m,
                CarriedForwardDays = 0m
            }, leaveSettings.LeaveYearStartMonth, leaveSettings.LeaveYearStartDay, user, cancellationToken);

            if (daysToAdd > 0)
            {
                totalAccrued += daysToAdd;
                updatedCount++;
                details.Add($"{emp.EmployeeCode} ({emp.NameLocal}): +{daysToAdd:F2} days (Accrued: {earnedToDate:F2}/{annualQuota:F2})");
            }
        }

        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return new LeaveAccrualResultDto(
            employees.Count,
            updatedCount,
            totalAccrued,
            details
        );
    }

    public async Task<LeaveAccrualResultDto> RunDailyAccrualAsync(DailyLeaveAccrualDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.AsOfDate == default) throw new ArgumentException("An accrual date is required.", nameof(dto.AsOfDate));

        var asOfDate = dto.AsOfDate.Date;
        var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        if (asOfDate > GetLeaveCompanyToday(settings))
            throw new ArgumentException("Daily accrual cannot be run for a future company-local date.", nameof(dto.AsOfDate));

        var leaveTypeCode = string.IsNullOrWhiteSpace(dto.LeaveTypeCode) ? "ANNUAL" : dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Leave type '{leaveTypeCode}' not found.");
        if (!leaveType.IsActive)
            throw new InvalidOperationException($"Leave type '{leaveTypeCode}' is inactive.");
        if (!leaveType.TracksBalance)
            throw new InvalidOperationException($"Leave type '{leaveTypeCode}' does not track a balance.");

        var employees = new List<Employee>();
        if (!string.IsNullOrWhiteSpace(dto.EmployeeCode))
        {
            var employee = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken);
            if (employee != null && employee.IsActive && employee.EmploymentStatus == "ACTIVE") employees.Add(employee);
        }
        else
        {
            var all = await _employeeRepo.GetAllEmployeesForExportAsync(
                departmentCode: dto.DepartmentCode, status: "ACTIVE", cancellationToken: cancellationToken);
            employees.AddRange(all);
        }

        var details = new List<string>();
        var updatedCount = 0;
        decimal totalAccrued = 0m;
        foreach (var employee in employees)
        {
            var (policy, cycle) = await ResolveLeavePolicyCycleAsync(employee, leaveTypeCode, asOfDate, settings, cancellationToken);
            if (policy == null || !policy.IsActive || !string.Equals(policy.AccrualMethod, "DAILY", StringComparison.OrdinalIgnoreCase))
                continue;
            var eligibleFrom = employee.HireDate.Date.AddMonths(policy.MinServiceMonths);
            if (eligibleFrom > asOfDate) continue;
            if (!IsApplicableToPolicy(employee, policy, settings.EligibleCitizenshipCountryCode)) continue;

            var annualQuota = GetAnnualEntitlement(policy, leaveType, GetCompletedServiceYears(employee.HireDate, asOfDate));
            var targetDays = CalculateAccruedToDate(employee, leaveType, policy, asOfDate, settings);
            var added = await _leaveRepo.ApplyAccrualTargetAsync(new LeaveBalance
            {
                EmployeeCode = employee.EmployeeCode,
                LeaveTypeCode = leaveTypeCode,
                YearNo = cycle.Start.Year,
                CycleStartDate = cycle.Start,
                CycleEndDate = cycle.End,
                AccruedDays = targetDays,
                UsedDays = 0m,
                CarriedForwardDays = 0m
            }, settings.LeaveYearStartMonth, settings.LeaveYearStartDay, user, cancellationToken);

            if (added > 0m)
            {
                totalAccrued += added;
                updatedCount++;
                details.Add($"{employee.EmployeeCode} ({employee.NameLocal}): +{added:F4} days (target: {targetDays:F4}/{annualQuota:F4} through {asOfDate:yyyy-MM-dd})");
            }
        }

        await _leaveRepo.SaveChangesAsync(cancellationToken);
        return new LeaveAccrualResultDto(employees.Count, updatedCount, totalAccrued, details);
    }

    public async Task<LeaveAccrualResultDto> RunYearlyAllocationAsync(YearlyLeaveAllocationDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var leaveTypeCode = string.IsNullOrWhiteSpace(dto.LeaveTypeCode) ? "ANNUAL" : dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var leaveSettings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        if (dto.AsOfDate.HasValue && dto.AsOfDate.Value.Date > GetLeaveCompanyToday(leaveSettings))
            throw new ArgumentException("Yearly allocation cannot target a future company-local date.", nameof(dto.AsOfDate));

        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Leave type '{leaveTypeCode}' not found.");
        if (!leaveType.IsActive)
            throw new InvalidOperationException($"Leave type '{leaveTypeCode}' is inactive.");
        if (!leaveType.TracksBalance)
            throw new InvalidOperationException($"Leave type '{leaveTypeCode}' does not track a balance.");

        var hasPolicyHistory = await HasLeavePolicyHistoryAsync(leaveTypeCode, cancellationToken);

        var employees = new List<Employee>();
        if (!string.IsNullOrWhiteSpace(dto.EmployeeCode))
        {
            var emp = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken);
            if (emp != null && emp.IsActive && emp.EmploymentStatus == "ACTIVE")
                employees.Add(emp);
        }
        else
        {
            var all = await _employeeRepo.GetAllEmployeesForExportAsync(
                departmentCode: dto.DepartmentCode, status: "ACTIVE", cancellationToken: cancellationToken);
            employees.AddRange(all);
        }

        var details = new List<string>();
        int updatedCount = 0;
        decimal totalAllocated = 0m;

        foreach (var emp in employees)
        {
            var (policy, cycle) = dto.AsOfDate.HasValue
                ? await ResolveLeavePolicyCycleAsync(emp, leaveTypeCode, dto.AsOfDate.Value.Date, leaveSettings, cancellationToken)
                : await ResolveLeavePolicyCycleForLabelAsync(emp, leaveTypeCode, dto.YearNo, leaveSettings, cancellationToken);
            if (policy is { IsActive: false }) continue;
            if (policy == null && hasPolicyHistory) continue;
            if (policy != null && !string.Equals(policy.AccrualMethod, "YEARLY", StringComparison.OrdinalIgnoreCase))
                continue;
            var eligibleFrom = emp.HireDate.Date.AddMonths(policy?.MinServiceMonths ?? 0);
            if (eligibleFrom > cycle.End) continue;
            if (!IsApplicableToPolicy(emp, policy, leaveSettings.EligibleCitizenshipCountryCode)) continue;

            var completedServiceYears = GetCompletedServiceYears(emp.HireDate, cycle.End);
            var quota = GetAnnualEntitlement(policy, leaveType, completedServiceYears);

            if ((dto.ProrateForMidYearHires || policy?.ProrateFirstCycle == true) && eligibleFrom > cycle.Start)
            {
                var eligibleDays = Math.Max(0, (cycle.End - eligibleFrom).Days + 1);
                quota = Math.Round(quota * eligibleDays / ((cycle.End - cycle.Start).Days + 1), 4, MidpointRounding.AwayFromZero);
            }

            var added = await _leaveRepo.ApplyAccrualTargetAsync(new LeaveBalance
            {
                EmployeeCode = emp.EmployeeCode,
                LeaveTypeCode = leaveTypeCode,
                YearNo = cycle.Start.Year,
                CycleStartDate = cycle.Start,
                CycleEndDate = cycle.End,
                AccruedDays = quota,
                UsedDays = 0m,
                CarriedForwardDays = 0m
            }, leaveSettings.LeaveYearStartMonth, leaveSettings.LeaveYearStartDay, user, cancellationToken);

            totalAllocated += added;
            if (added > 0m) updatedCount++;
            details.Add($"{emp.EmployeeCode} ({emp.NameLocal}): Added {added:F2} days; cycle quota {quota:F2} for {cycle.Start:yyyy-MM-dd}..{cycle.End:yyyy-MM-dd}");
        }

        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return new LeaveAccrualResultDto(
            employees.Count,
            updatedCount,
            totalAllocated,
            details
        );
    }

    public async Task<LeaveRolloverResultDto> RunYearEndRolloverAsync(YearEndRolloverDto dto, string user, CancellationToken cancellationToken = default)
    {
        var close = await CreateLeaveYearCloseDraftAsync(dto, user, cancellationToken);
        if (!string.Equals(close.Status, "POSTED", StringComparison.OrdinalIgnoreCase))
            close = await PostLeaveYearCloseAsync(close.Id, user, cancellationToken);
        return new LeaveRolloverResultDto(close.Lines.Count, close.Lines.Count, close.TotalCarriedDays, close.TotalExpiredDays,
            close.Lines.Select(l => $"{l.EmployeeCode} [{l.LeaveTypeCode}]: Unused {l.UnusedDays:F4} => carried {l.CarriedDays:F4}, expired {l.ExpiredDays:F4}").ToList());
    }

    public async Task<LeaveYearCloseDto> CreateLeaveYearCloseDraftAsync(YearEndRolloverDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.FromYear < 1 || dto.FromYear > 9998 || dto.ToYear != dto.FromYear + 1)
            throw new ArgumentException("Leave-year close must move to the immediately following cycle year.");

        var leaveTypeCode = string.IsNullOrWhiteSpace(dto.LeaveTypeCode) ? "*" : dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var closeKey = $"{dto.FromYear}:{dto.ToYear}:{leaveTypeCode}";
        var existing = await _leaveRepo.GetLeaveYearCloseByKeyAsync(closeKey, cancellationToken);
        if (existing?.Status == "POSTED")
            return MapLeaveYearClose(existing);

        var balances = await _leaveRepo.GetLeaveBalancesByYearAsync(dto.FromYear, leaveTypeCode == "*" ? null : leaveTypeCode, cancellationToken);
        var close = new LeaveYearClose
        {
            Id = existing?.Id ?? 0,
            CloseKey = closeKey,
            FromYear = dto.FromYear,
            ToYear = dto.ToYear,
            LeaveTypeCode = leaveTypeCode,
            Status = "DRAFT",
            CreationUser = existing?.CreationUser ?? user,
            CreationDate = existing?.CreationDate ?? DateTime.UtcNow
        };

        foreach (var balance in balances.OrderBy(b => b.EmployeeCode, StringComparer.Ordinal).ThenBy(b => b.LeaveTypeCode, StringComparer.Ordinal).ThenBy(b => b.Id))
        {
            var leaveType = balance.LeaveType ?? await _leaveRepo.GetLeaveTypeByCodeAsync(balance.LeaveTypeCode, cancellationToken);
            if (leaveType == null || !leaveType.TracksBalance) continue;
            var unused = balance.AccruedDays + balance.CarriedForwardDays - balance.UsedDays;
            if (unused < 0m)
                throw new InvalidOperationException($"Negative leave balance for {balance.EmployeeCode}/{balance.LeaveTypeCode}; correct it before drafting the close.");

            var carried = leaveType.CarryForwardAllowed
                ? Math.Min(unused, leaveType.CarryForwardCapDays > 0m ? leaveType.CarryForwardCapDays : unused)
                : 0m;
            var excess = unused - carried;
            var action = NormalizeYearEndExcessAction(leaveType.YearEndExcessAction);
            if (action == "REQUIRE_USE" && excess > 0m)
                throw new InvalidOperationException($"{balance.EmployeeCode}/{balance.LeaveTypeCode} has {excess:F4} days that must be used before this leave-year can close.");
            var cashout = action == "CASHOUT" ? excess : 0m;
            var expired = action == "EXPIRE" ? excess : 0m;
            var sourceStart = balance.CycleStartDate ?? new DateTime(balance.YearNo, 1, 1);
            var sourceEnd = balance.CycleEndDate ?? new DateTime(balance.YearNo, 12, 31);
            if (await _leaveRepo.HasPendingLeaveRequestsAsync(balance.EmployeeCode, balance.LeaveTypeCode, sourceStart, sourceEnd, cancellationToken))
                throw new InvalidOperationException($"Resolve pending leave requests for {balance.EmployeeCode}/{balance.LeaveTypeCode} before drafting the close.");
            var targetStart = sourceEnd.AddDays(1);
            var targetEnd = targetStart.AddYears(1).AddDays(-1);
            var target = await _leaveRepo.GetLeaveBalanceByCycleAsync(balance.EmployeeCode, balance.LeaveTypeCode, targetStart, cancellationToken);

            close.Lines.Add(new LeaveYearCloseLine
            {
                EmployeeCode = balance.EmployeeCode,
                LeaveTypeCode = balance.LeaveTypeCode,
                SourceYear = balance.YearNo,
                TargetYear = targetStart.Year,
                SourceBalanceId = balance.Id,
                SourceCycleStartDate = sourceStart,
                SourceCycleEndDate = sourceEnd,
                TargetCycleStartDate = targetStart,
                TargetCycleEndDate = targetEnd,
                SourceAccruedBefore = balance.AccruedDays,
                SourceUsedDays = balance.UsedDays,
                SourceCarriedBefore = balance.CarriedForwardDays,
                TargetAccruedBefore = target?.AccruedDays ?? 0m,
                TargetCarriedBefore = target?.CarriedForwardDays ?? 0m,
                TargetUsedBefore = target?.UsedDays ?? 0m,
                UnusedDays = unused,
                CarriedDays = carried,
                ExpiredDays = expired,
                CashoutDays = cashout,
                TargetBalanceId = target?.Id,
                TracksBalanceSnapshot = leaveType.TracksBalance,
                CarryForwardAllowedSnapshot = leaveType.CarryForwardAllowed,
                CarryForwardCapDaysSnapshot = leaveType.CarryForwardCapDays,
                YearEndExcessActionSnapshot = action
            });
        }

        var savedClose = await _leaveRepo.SaveLeaveYearCloseDraftAsync(close, cancellationToken);
        return MapLeaveYearClose(savedClose);
    }

    public async Task<LeaveYearCloseDto> GetLeaveYearCloseAsync(long id, CancellationToken cancellationToken = default)
    {
        var close = await _leaveRepo.GetLeaveYearCloseAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave-year close '{id}' was not found.");
        return MapLeaveYearClose(close);
    }

    public async Task<LeaveYearCloseDto> PostLeaveYearCloseAsync(long id, string user, CancellationToken cancellationToken = default)
    {
        var close = await _leaveRepo.PostLeaveYearCloseAsync(id, user, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave-year close '{id}' was not found.");
        return MapLeaveYearClose(close);
    }

    public async Task<LeaveYearCloseDto> UnpostLeaveYearCloseAsync(long id, string user, CancellationToken cancellationToken = default)
    {
        var close = await _leaveRepo.UnpostLeaveYearCloseAsync(id, user, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave-year close '{id}' was not found.");
        return MapLeaveYearClose(close);
    }

    public async Task<LeaveCashoutSettlementDto> CreateLeaveCashoutSettlementDraftAsync(CreateLeaveCashoutSettlementDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.LeaveYearCloseId <= 0) throw new ArgumentOutOfRangeException(nameof(dto.LeaveYearCloseId));
        var key = dto.SettlementKey?.Trim();
        var payPeriodCode = dto.PayPeriod?.Trim().ToUpperInvariant();
        var currencyCode = dto.CurrencyCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200) throw new ArgumentException("Settlement key is required and must not exceed 200 characters.", nameof(dto.SettlementKey));
        if (string.IsNullOrWhiteSpace(payPeriodCode) || payPeriodCode.Length > 40) throw new ArgumentException("Pay period is required.", nameof(dto.PayPeriod));
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length > 20 || currencyCode.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("Currency code must contain only ASCII letters or digits.", nameof(dto.CurrencyCode));

        var payload = $"{dto.LeaveYearCloseId}|{payPeriodCode.ToUpperInvariant()}|{currencyCode}";
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var existing = await _leaveRepo.GetLeaveCashoutSettlementByKeyAsync(key, cancellationToken);
        if (existing != null)
        {
            if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
                throw new InvalidOperationException("The settlement key is already used for a different close, pay period, or currency.");
            return MapLeaveCashoutSettlement(existing);
        }

        var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var basis = settings.LeaveCashoutWageBasisCode?.Trim().ToUpperInvariant();
        var divisor = settings.LeaveCashoutDayDivisor;
        var payComponent = settings.LeaveCashoutPayComponentCode?.Trim();
        var basisComponent = settings.LeaveCashoutBasisComponentCode?.Trim();
        if (basis is not ("BASIC" or "GROSS_EARNINGS" or "SALARY_COMPONENT") || divisor is null or <= 0m || string.IsNullOrWhiteSpace(payComponent))
            throw new InvalidOperationException("Configure the company leave cashout wage basis, day divisor, and payroll component before drafting a settlement.");
        var leaveClose = await _leaveRepo.GetLeaveYearCloseAsync(dto.LeaveYearCloseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave-year close '{dto.LeaveYearCloseId}' was not found in the current HR scope.");
        if (leaveClose.Status != "DRAFT") throw new InvalidOperationException("Prepare and approve the cashout settlement before posting the leave-year close.");
        var period = await _leaveRepo.GetPayrollPeriodForLeaveCashoutAsync(payPeriodCode, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll period was not found in the current company scope.");
        if (period.Status != "OPEN") throw new InvalidOperationException("Cashout settlement drafts require an open payroll period.");
        var configuredCurrencyDecimals = _policyRepository == null
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : await _policyRepository.GetConfiguredPayrollCurrencyDecimalsAsync(period.CompanyId, cancellationToken);
        var branchRoundingRules = new Dictionary<long, int?>();

        var settlement = new LeaveCashoutSettlement
        {
            SettlementKey = key,
            PayloadHash = payloadHash,
            LeaveYearCloseId = leaveClose.Id,
            PayPeriod = period.PeriodCode,
            WageBasisCode = basis,
            BasisComponentCode = basis == "SALARY_COMPONENT" ? basisComponent : null,
            DayDivisor = divisor.Value,
            PayComponentCode = payComponent,
            Status = "DRAFT",
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        foreach (var closeLine in leaveClose.Lines.Where(l => l.CashoutDays > 0m).OrderBy(l => l.EmployeeCode, StringComparer.Ordinal).ThenBy(l => l.LeaveTypeCode, StringComparer.Ordinal))
        {
            var structure = await _employeeRepo.GetActiveSalaryStructureAsync(closeLine.EmployeeCode, period.StartDate, cancellationToken);
            if (structure == null) throw new InvalidOperationException($"No active salary structure applies to {closeLine.EmployeeCode} at payroll period start.");
            var lineCurrency = structure.CurrencyCode.Trim().ToUpperInvariant();
            if (!string.Equals(lineCurrency, currencyCode, StringComparison.Ordinal)) continue;
            var employee = await _employeeRepo.GetEmployeeByCodeAsync(closeLine.EmployeeCode, cancellationToken)
                ?? throw new InvalidOperationException($"Employee '{closeLine.EmployeeCode}' no longer exists in the current HR scope.");

            int? roundingRule = null;
            if (_policyRepository != null && employee.BranchId.HasValue)
            {
                if (!branchRoundingRules.TryGetValue(employee.BranchId.Value, out roundingRule))
                {
                    roundingRule = await _policyRepository.GetBranchRoundingRulesAsync(employee.BranchId.Value, cancellationToken) ?? 1;
                    if (roundingRule is < 1 or > 6)
                        throw new InvalidOperationException($"Branch {employee.BranchId.Value} has an invalid payroll rounding rule.");
                    branchRoundingRules.Add(employee.BranchId.Value, roundingRule);
                }
            }
            configuredCurrencyDecimals.TryGetValue(lineCurrency, out var configuredDecimals);
            var currencyDecimals = PayrollCurrencyRules.GetCurrencyDecimals(lineCurrency,
                configuredCurrencyDecimals.ContainsKey(lineCurrency) ? configuredDecimals : null);

            var basic = structure.BasicSalary;
            var earnings = structure.Lines.Where(l => l.IsActive && (l.Component?.ComponentType?.Trim().ToUpperInvariant() ?? "ALLOWANCE") is "ALLOWANCE" or "EARNING")
                .Select(l => new
                {
                    l.ComponentCode,
                    Amount = PayrollCurrencyRules.Round(l.Percent is > 0m
                        ? basic * (l.Percent.Value / 100m)
                        : l.Amount, currencyDecimals, roundingRule)
                }).ToList();
            decimal wageBasisAmount;
            IReadOnlyList<object> basisSnapshot;
            if (basis == "BASIC")
            {
                wageBasisAmount = basic;
                basisSnapshot = [new { componentCode = "BASIC", amount = basic }];
            }
            else if (basis == "GROSS_EARNINGS")
            {
                wageBasisAmount = basic + earnings.Sum(e => e.Amount);
                basisSnapshot = new object[] { new { componentCode = "BASIC", amount = basic } }
                    .Concat(earnings.Select(e => (object)new { componentCode = e.ComponentCode, amount = e.Amount })).ToList();
            }
            else
            {
                var selected = earnings.FirstOrDefault(e => string.Equals(e.ComponentCode, basisComponent, StringComparison.OrdinalIgnoreCase));
                if (selected == null) throw new InvalidOperationException($"Salary component '{basisComponent}' has no active earning line for {closeLine.EmployeeCode}.");
                wageBasisAmount = selected.Amount;
                basisSnapshot = [new { componentCode = selected.ComponentCode, amount = selected.Amount }];
            }

            var cashoutAmount = PayrollCurrencyRules.Round(closeLine.CashoutDays * wageBasisAmount / divisor.Value,
                currencyDecimals, roundingRule);
            settlement.Lines.Add(new LeaveCashoutSettlementLine
            {
                LeaveYearCloseLineId = closeLine.Id,
                EmployeeCode = closeLine.EmployeeCode,
                LeaveTypeCode = closeLine.LeaveTypeCode,
                SalaryStructureId = structure.Id,
                SalaryEffectiveFrom = structure.EffectiveFrom,
                CashoutDays = closeLine.CashoutDays,
                WageBasisAmount = wageBasisAmount,
                DayDivisor = divisor.Value,
                CashoutAmount = cashoutAmount,
                CurrencyCode = lineCurrency,
                BasisSnapshotJson = JsonSerializer.Serialize(new { structureId = structure.Id, effectiveFrom = structure.EffectiveFrom, currency = lineCurrency, basis, components = basisSnapshot })
            });
        }
        if (settlement.Lines.Count == 0) throw new InvalidOperationException("The posted leave close has no cashout days for the selected currency.");

        var saved = await _leaveRepo.SaveLeaveCashoutSettlementDraftAsync(settlement, cancellationToken);
        return MapLeaveCashoutSettlement(saved);
    }

    public async Task<LeaveCashoutSettlementDto> GetLeaveCashoutSettlementAsync(long id, CancellationToken cancellationToken = default)
    {
        var settlement = await _leaveRepo.GetLeaveCashoutSettlementAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave cashout settlement '{id}' was not found in the current HR scope.");
        return MapLeaveCashoutSettlement(settlement);
    }

    public async Task<LeaveCashoutSettlementDto> PostLeaveCashoutSettlementAsync(long id, string user, CancellationToken cancellationToken = default)
    {
        var settlement = await _leaveRepo.PostLeaveCashoutSettlementAsync(id, user, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave cashout settlement '{id}' was not found in the current HR scope.");
        return MapLeaveCashoutSettlement(settlement);
    }

    public async Task<LeaveCashoutSettlementDto> ReverseLeaveCashoutSettlementAsync(long id, ReverseLeaveCashoutSettlementDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var reason = dto.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw new ArgumentException("A reversal reason of 1 to 1000 characters is required.", nameof(dto.Reason));
        var settlement = await _leaveRepo.ReverseLeaveCashoutSettlementAsync(id, reason, user, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave cashout settlement '{id}' was not found in the current HR scope.");
        return MapLeaveCashoutSettlement(settlement);
    }

    private static LeaveCashoutSettlementDto MapLeaveCashoutSettlement(LeaveCashoutSettlement settlement)
        => new(settlement.Id, settlement.SettlementKey, settlement.LeaveYearCloseId, settlement.PayPeriod,
            settlement.Lines.Select(l => l.CurrencyCode).Distinct(StringComparer.Ordinal).SingleOrDefault() ?? string.Empty,
            settlement.WageBasisCode, settlement.BasisComponentCode, settlement.DayDivisor, settlement.PayComponentCode,
            settlement.Status, settlement.CreationUser, settlement.CreationDate,
            settlement.PostedBy, settlement.PostedDate, settlement.ReversedBy, settlement.ReversedDate, settlement.ReversalReason,
            settlement.Lines.Sum(l => l.CashoutDays), settlement.Lines.Sum(l => l.CashoutAmount),
            settlement.Lines.OrderBy(l => l.EmployeeCode, StringComparer.Ordinal).ThenBy(l => l.LeaveTypeCode, StringComparer.Ordinal)
                .Select(l => new LeaveCashoutSettlementLineDto(l.LeaveYearCloseLineId, l.EmployeeCode, l.LeaveTypeCode,
                    l.CashoutDays, l.SalaryStructureId, l.SalaryEffectiveFrom, l.WageBasisAmount, l.DayDivisor,
                    l.CashoutAmount, l.CurrencyCode, l.BasisSnapshotJson, l.PayrollAdjustmentId)).ToList());

    private static LeaveYearCloseDto MapLeaveYearClose(LeaveYearClose close)
        => new(close.Id, close.FromYear, close.ToYear, close.LeaveTypeCode, close.Status, close.CreationUser, close.CreationDate,
            close.PostedBy, close.PostedDate, close.UnpostedBy, close.UnpostedDate,
            close.Lines.Sum(l => l.UnusedDays), close.Lines.Sum(l => l.CarriedDays), close.Lines.Sum(l => l.ExpiredDays), close.Lines.Sum(l => l.CashoutDays),
            close.Lines.OrderBy(l => l.EmployeeCode, StringComparer.Ordinal).ThenBy(l => l.LeaveTypeCode, StringComparer.Ordinal).Select(l => new LeaveYearCloseLineDto(
                l.EmployeeCode, l.LeaveTypeCode, l.SourceYear, l.TargetYear,
                l.SourceCycleStartDate ?? new DateTime(l.SourceYear, 1, 1), l.SourceCycleEndDate ?? new DateTime(l.SourceYear, 12, 31),
                l.TargetCycleStartDate, l.TargetCycleEndDate, l.UnusedDays, l.CarriedDays, l.ExpiredDays, l.CashoutDays)).ToList());

    public async Task<LeaveBalanceMigrationDto> CreateLeaveBalanceMigrationDraftAsync(CreateLeaveBalanceMigrationDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var migrationKey = dto.MigrationKey?.Trim();
        if (string.IsNullOrWhiteSpace(migrationKey) || migrationKey.Length > 200)
            throw new ArgumentException("MigrationKey (1..200 characters) is required.", nameof(dto.MigrationKey));
        if (dto.EffectiveDate == default)
            throw new ArgumentException("An explicit effective date is required.", nameof(dto.EffectiveDate));
        if (dto.TargetYearStartMonth is < 1 or > 12 || dto.TargetYearStartDay < 1 ||
            dto.TargetYearStartDay > DateTime.DaysInMonth(2000, dto.TargetYearStartMonth))
            throw new ArgumentException("Target leave-year start month/day is invalid.");
        if (dto.Allocations == null || dto.Allocations.Count == 0 || dto.Allocations.Count > 10000)
            throw new ArgumentException("Between 1 and 10,000 explicit balance allocations are required.", nameof(dto.Allocations));
        if (dto.Allocations.Any(a => a is null))
            throw new ArgumentException("Allocation entries cannot be null.", nameof(dto.Allocations));

        var allocations = dto.Allocations.OrderBy(a => a.SourceBalanceId).ThenBy(a => a.TargetCycleStartDate).ToList();
        if (allocations.Any(a => a.SourceBalanceId <= 0 || a.AccruedDays != decimal.Round(a.AccruedDays, 4) ||
            a.UsedDays != decimal.Round(a.UsedDays, 4) || a.CarriedDays != decimal.Round(a.CarriedDays, 4)))
            throw new ArgumentException("Allocation source IDs must be positive and days may have at most four decimal places.", nameof(dto.Allocations));

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            MigrationKey = migrationKey,
            EffectiveDate = dto.EffectiveDate.Date,
            dto.TargetYearStartMonth,
            dto.TargetYearStartDay,
            Allocations = allocations.Select(a => new { a.SourceBalanceId, Start = a.TargetCycleStartDate.Date, End = a.TargetCycleEndDate.Date,
                Accrued = a.AccruedDays.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
                Used = a.UsedDays.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
                Carried = a.CarriedDays.ToString("G29", System.Globalization.CultureInfo.InvariantCulture) })
        });
        var payloadHash = LeaveMovementKey.Hash(payload);
        var existingMigration = await _leaveRepo.GetLeaveBalanceMigrationByKeyAsync(migrationKey, cancellationToken);
        if (existingMigration != null)
        {
            if (existingMigration.PayloadHash != payloadHash)
                throw new InvalidOperationException("MigrationKey was already used for a different reviewed allocation plan.");
            return MapLeaveBalanceMigration(existingMigration);
        }

        var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        if (settings.LeaveYearStartMonth == dto.TargetYearStartMonth && settings.LeaveYearStartDay == dto.TargetYearStartDay)
            throw new InvalidOperationException("The target leave-year start matches the current setting; no cycle migration is needed.");

        var balances = await _leaveRepo.GetAllLeaveBalancesForMigrationAsync(cancellationToken);
        if (balances.Count == 0)
            throw new InvalidOperationException("There are no existing leave balances to migrate.");
        var byId = balances.ToDictionary(b => b.Id);
        var groupedAllocations = allocations.GroupBy(a => a.SourceBalanceId).ToDictionary(g => g.Key, g => g.ToList());
        if (groupedAllocations.Keys.Any(id => !byId.ContainsKey(id)))
            throw new ArgumentException("An allocation refers to a balance outside the current tenant.", nameof(dto.Allocations));
        var missingSourceIds = byId.Keys.Where(id => !groupedAllocations.ContainsKey(id)).ToArray();
        if (missingSourceIds.Length > 0)
            throw new InvalidOperationException($"The reviewed plan must allocate every existing balance; {missingSourceIds.Length} balance row(s) are missing.");

        var targetKey = new Func<string, string, DateTime, string>((employee, leaveType, start) =>
            $"{employee}\u001f{leaveType}\u001f{start:yyyyMMdd}");
        var targets = new Dictionary<string, (LeaveBalance? Balance, DateTime Start, DateTime End)>();
        var migration = new LeaveBalanceMigration
        {
            MigrationKey = migrationKey,
            EffectiveDate = dto.EffectiveDate.Date,
            SourceYearStartMonth = settings.LeaveYearStartMonth,
            SourceYearStartDay = settings.LeaveYearStartDay,
            TargetYearStartMonth = dto.TargetYearStartMonth,
            TargetYearStartDay = dto.TargetYearStartDay,
            PayloadHash = payloadHash,
            Status = "DRAFT",
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        foreach (var (sourceId, sourceAllocations) in groupedAllocations.OrderBy(x => x.Key))
        {
            var source = byId[sourceId];
            var sourceStart = source.CycleStartDate?.Date ?? new DateTime(source.YearNo, 1, 1);
            var sourceEnd = source.CycleEndDate?.Date ?? new DateTime(source.YearNo, 12, 31);
            if (sourceEnd < sourceStart) throw new InvalidOperationException($"Source balance {source.Id} has an invalid cycle.");
            if (sourceAllocations.GroupBy(a => a.TargetCycleStartDate.Date).Any(g => g.Count() > 1))
                throw new ArgumentException($"Balance {source.Id} has duplicate allocations for one target cycle.", nameof(dto.Allocations));
            if (sourceAllocations.Sum(a => a.AccruedDays) != source.AccruedDays ||
                sourceAllocations.Sum(a => a.UsedDays) != source.UsedDays ||
                sourceAllocations.Sum(a => a.CarriedDays) != source.CarriedForwardDays)
                throw new InvalidOperationException($"Allocations for balance {source.Id} must preserve accrued, used and carried days exactly.");

            foreach (var allocation in sourceAllocations)
            {
                var targetStart = allocation.TargetCycleStartDate.Date;
                var targetEnd = allocation.TargetCycleEndDate.Date;
                if (targetStart.Year is < 1 or > 9998 || targetEnd < targetStart ||
                    targetStart.Month != dto.TargetYearStartMonth ||
                    targetStart.Day != Math.Min(dto.TargetYearStartDay, DateTime.DaysInMonth(targetStart.Year, dto.TargetYearStartMonth)))
                    throw new ArgumentException($"Target cycle for balance {source.Id} does not begin on the configured target leave-year boundary.", nameof(dto.Allocations));
                var nextStartYear = targetStart.Year + 1;
                var nextStart = new DateTime(nextStartYear, dto.TargetYearStartMonth,
                    Math.Min(dto.TargetYearStartDay, DateTime.DaysInMonth(nextStartYear, dto.TargetYearStartMonth)));
                if (targetEnd != nextStart.AddDays(-1))
                    throw new ArgumentException($"Target cycle for balance {source.Id} must end the day before its next configured annual boundary.", nameof(dto.Allocations));

                var key = targetKey(source.EmployeeCode, source.LeaveTypeCode, targetStart);
                if (!targets.ContainsKey(key))
                {
                    var target = await _leaveRepo.GetLeaveBalanceByCycleAsync(source.EmployeeCode, source.LeaveTypeCode, targetStart, cancellationToken);
                    if (target != null && (target.CycleEndDate?.Date ?? targetEnd) != targetEnd)
                        throw new InvalidOperationException($"Existing target balance {target.Id} has a different cycle end.");
                    targets.Add(key, (target, targetStart, targetEnd));
                }
                migration.Lines.Add(new LeaveBalanceMigrationLine
                {
                    SourceBalanceId = source.Id,
                    EmployeeCode = source.EmployeeCode,
                    LeaveTypeCode = source.LeaveTypeCode,
                    SourceCycleStartDate = sourceStart,
                    SourceCycleEndDate = sourceEnd,
                    SourceYearNo = source.YearNo,
                    SourceCycleStartWasNull = !source.CycleStartDate.HasValue,
                    SourceCycleEndWasNull = !source.CycleEndDate.HasValue,
                    SourceAccruedDays = source.AccruedDays,
                    SourceUsedDays = source.UsedDays,
                    SourceCarriedDays = source.CarriedForwardDays,
                    TargetCycleStartDate = targetStart,
                    TargetCycleEndDate = targetEnd,
                    AllocatedAccruedDays = allocation.AccruedDays,
                    AllocatedUsedDays = allocation.UsedDays,
                    AllocatedCarriedDays = allocation.CarriedDays,
                    TargetBalanceId = targets[key].Balance?.Id,
                    TargetAccruedBefore = targets[key].Balance?.AccruedDays ?? 0m,
                    TargetUsedBefore = targets[key].Balance?.UsedDays ?? 0m,
                    TargetCarriedBefore = targets[key].Balance?.CarriedForwardDays ?? 0m
                });
            }
        }

        var saved = await _leaveRepo.SaveLeaveBalanceMigrationDraftAsync(migration, cancellationToken);
        return MapLeaveBalanceMigration(saved);
    }

    public async Task<LeaveBalanceMigrationDto> GetLeaveBalanceMigrationAsync(long id, CancellationToken cancellationToken = default)
    {
        var migration = await _leaveRepo.GetLeaveBalanceMigrationAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave-balance migration '{id}' was not found.");
        return MapLeaveBalanceMigration(migration);
    }

    public async Task<LeaveBalanceMigrationDto> PostLeaveBalanceMigrationAsync(long id, string user, CancellationToken cancellationToken = default)
    {
        var migration = await _leaveRepo.ApplyLeaveBalanceMigrationAsync(id, user, reverse: false, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave-balance migration '{id}' was not found.");
        return MapLeaveBalanceMigration(migration);
    }

    public async Task<LeaveBalanceMigrationDto> ReverseLeaveBalanceMigrationAsync(long id, string user, CancellationToken cancellationToken = default)
    {
        var migration = await _leaveRepo.ApplyLeaveBalanceMigrationAsync(id, user, reverse: true, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave-balance migration '{id}' was not found.");
        return MapLeaveBalanceMigration(migration);
    }

    private static LeaveBalanceMigrationDto MapLeaveBalanceMigration(LeaveBalanceMigration migration)
        => new(migration.Id, migration.MigrationKey, migration.EffectiveDate, migration.SourceYearStartMonth, migration.SourceYearStartDay,
            migration.TargetYearStartMonth, migration.TargetYearStartDay, migration.Status, migration.CreationUser, migration.CreationDate,
            migration.PostedBy, migration.PostedDate, migration.ReversedBy, migration.ReversedDate,
            migration.Lines.OrderBy(l => l.EmployeeCode, StringComparer.Ordinal).ThenBy(l => l.LeaveTypeCode, StringComparer.Ordinal)
                .ThenBy(l => l.SourceCycleStartDate).ThenBy(l => l.TargetCycleStartDate)
                .Select(l => new LeaveBalanceMigrationLineDto(l.SourceBalanceId, l.TargetBalanceId, l.EmployeeCode, l.LeaveTypeCode,
                    l.SourceCycleStartDate, l.SourceCycleEndDate, l.SourceAccruedDays, l.SourceUsedDays, l.SourceCarriedDays,
                    l.TargetCycleStartDate, l.TargetCycleEndDate, l.AllocatedAccruedDays, l.AllocatedUsedDays, l.AllocatedCarriedDays)).ToList());

    public async Task<IReadOnlyList<LeavePolicyDetailsDto>> GetLeavePoliciesAsync(CancellationToken cancellationToken = default)
    {
        var policies = new List<LeavePolicy>();
        foreach (var type in await _leaveRepo.GetLeaveTypesAsync(activeOnly: false, cancellationToken))
            policies.AddRange(await _leaveRepo.GetLeavePolicyVersionsAsync(type.LeaveTypeCode, cancellationToken));
        return policies.Select(p => new LeavePolicyDetailsDto(
            p.Id,
            p.PolicyName,
            p.LeaveTypeCode,
            p.AccrualMethod,
            p.AccrualRate,
            p.ApplicableTo,
            p.MinServiceMonths,
            p.Tier1YearsThreshold,
            p.Tier1Days,
            p.Tier2Days,
            p.IsActive,
            p.AnnualEntitlementDays,
            p.CycleBasis,
            p.ProrateFirstCycle,
            p.EffectiveFrom,
            p.EffectiveTo
        )).ToList();
    }

    public async Task<LeavePolicyDetailsDto> CreateOrUpdateLeavePolicyAsync(CreateLeavePolicyDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.LeaveTypeCode))
            throw new ArgumentException("Leave type code is required.", nameof(dto.LeaveTypeCode));
        if (dto.MinServiceMonths < 0)
            throw new ArgumentOutOfRangeException(nameof(dto.MinServiceMonths), "Minimum service months cannot be negative.");
        if (dto.AnnualEntitlementDays is <= 0m)
            throw new ArgumentOutOfRangeException(nameof(dto.AnnualEntitlementDays), "Annual entitlement must be greater than zero when provided.");
        if (dto.Tier1YearsThreshold < 0 || dto.Tier1Days < 0m || dto.Tier2Days < 0m || dto.AccrualRate < 0m)
            throw new ArgumentOutOfRangeException(nameof(dto), "Service thresholds, quotas, and accrual rate cannot be negative.");
        if (string.IsNullOrWhiteSpace(dto.ApplicableTo))
            throw new ArgumentException("Policy applicability is required.", nameof(dto.ApplicableTo));
        var applicableTo = dto.ApplicableTo.Trim().ToUpperInvariant();
        if (applicableTo is not ("ALL" or "GENDER_F" or "GENDER_M" or "CITIZENS"))
            throw new ArgumentException("ApplicableTo must be ALL, GENDER_F, GENDER_M, or CITIZENS.", nameof(dto.ApplicableTo));

        if (string.IsNullOrWhiteSpace(dto.AccrualMethod))
            throw new ArgumentException("Accrual method is required.", nameof(dto.AccrualMethod));
        if (string.IsNullOrWhiteSpace(dto.CycleBasis))
            throw new ArgumentException("Cycle basis is required.", nameof(dto.CycleBasis));
        var accrualMethod = dto.AccrualMethod.Trim().ToUpperInvariant();
        var prorateFirstCycle = dto.ProrateFirstCycle || accrualMethod == "PRO_RATA";
        if (accrualMethod == "PRO_RATA")
            accrualMethod = "MONTHLY";
        if (accrualMethod is not ("YEARLY" or "MONTHLY" or "DAILY" or "PRO_RATA"))
            throw new ArgumentException("Accrual method must be YEARLY, MONTHLY, or DAILY.", nameof(dto.AccrualMethod));

        var cycleBasis = dto.CycleBasis.Trim().ToUpperInvariant();
        if (cycleBasis is not ("CALENDAR_YEAR" or "COMPANY_LEAVE_YEAR" or "EMPLOYMENT_ANNIVERSARY"))
            throw new ArgumentException("Cycle basis must be CALENDAR_YEAR, COMPANY_LEAVE_YEAR, or EMPLOYMENT_ANNIVERSARY.", nameof(dto.CycleBasis));

        var normalizedLeaveTypeCode = dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(normalizedLeaveTypeCode, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave type '{normalizedLeaveTypeCode}' not found.");
        if (!leaveType.IsActive && dto.IsActive)
            throw new InvalidOperationException("An active leave policy cannot be assigned to an inactive leave type.");

        var policySettings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var companyToday = GetLeaveCompanyToday(policySettings);
        var effectiveFrom = dto.EffectiveFrom?.Date ?? companyToday;
        if (effectiveFrom < companyToday)
            throw new ArgumentException("A policy version cannot be backdated; use a prospective effective date.", nameof(dto.EffectiveFrom));

        return await _leaveRepo.ExecuteForLeavePolicyAsync(normalizedLeaveTypeCode, async () =>
        {
            var versions = await _leaveRepo.GetLeavePolicyVersionsAsync(normalizedLeaveTypeCode, cancellationToken);
            var latest = versions.OrderByDescending(p => p.EffectiveFrom).ThenByDescending(p => p.Id).FirstOrDefault();
            var today = companyToday;
            var defaultStart = latest == null ? today :
                latest.EffectiveTo.HasValue && latest.EffectiveTo.Value.Date.AddDays(1) > today.AddDays(1)
                    ? latest.EffectiveTo.Value.Date.AddDays(1)
                    : today.AddDays(1);
            var start = dto.EffectiveFrom?.Date ?? defaultStart;
            if (latest != null && start <= latest.EffectiveFrom.Date)
                throw new InvalidOperationException("A new policy version must start after the latest existing version.");
            if (latest != null && start <= today)
                throw new InvalidOperationException("A replacement policy version must take effect after today to protect earned balance.");
            if (latest?.EffectiveTo is DateTime latestEnd && latestEnd.Date >= start)
                throw new InvalidOperationException("The requested policy version overlaps the latest policy version.");
            if (versions.Where(version => latest == null || version.Id != latest.Id).Any(version => version.EffectiveFrom.Date <= start &&
                    (!version.EffectiveTo.HasValue || version.EffectiveTo.Value.Date >= start)))
                throw new InvalidOperationException("The requested policy version overlaps an existing version.");

            var policy = new LeavePolicy
            {
                PolicyName = dto.PolicyName,
                LeaveTypeCode = normalizedLeaveTypeCode,
                EffectiveFrom = start,
                AccrualMethod = accrualMethod,
                AccrualRate = dto.AccrualRate,
                AnnualEntitlementDays = dto.AnnualEntitlementDays,
                CycleBasis = cycleBasis,
                ProrateFirstCycle = prorateFirstCycle,
                ApplicableTo = applicableTo,
                MinServiceMonths = dto.MinServiceMonths,
                Tier1YearsThreshold = dto.Tier1YearsThreshold,
                Tier1Days = dto.Tier1Days,
                Tier2Days = dto.Tier2Days,
                IsActive = dto.IsActive,
                CreationUser = user,
                CreationDate = DateTime.UtcNow
            };

            if (latest != null)
            {
                if (!latest.EffectiveTo.HasValue)
                {
                    latest.EffectiveTo = start.AddDays(-1);
                    latest.UpdateUser = user;
                    latest.UpdateDate = DateTime.UtcNow;
                    await _leaveRepo.UpdateLeavePolicyAsync(latest, cancellationToken);
                }
            }
            await _leaveRepo.AddLeavePolicyAsync(policy, cancellationToken);
            await _leaveRepo.SaveChangesAsync(cancellationToken);
            return new LeavePolicyDetailsDto(policy.Id, policy.PolicyName, policy.LeaveTypeCode, policy.AccrualMethod,
                policy.AccrualRate, policy.ApplicableTo, policy.MinServiceMonths, policy.Tier1YearsThreshold,
                policy.Tier1Days, policy.Tier2Days, policy.IsActive, policy.AnnualEntitlementDays,
                policy.CycleBasis, policy.ProrateFirstCycle, policy.EffectiveFrom, policy.EffectiveTo);
        }, cancellationToken);
    }

    private static int GetCompletedServiceYears(DateTime hireDate, DateTime asOfDate)
    {
        var hire = hireDate.Date;
        var asOf = asOfDate.Date;
        if (asOf < hire) return 0;
        var years = asOf.Year - hire.Year;
        var anniversaryDay = Math.Min(hire.Day, DateTime.DaysInMonth(asOf.Year, hire.Month));
        var anniversary = new DateTime(asOf.Year, hire.Month, anniversaryDay);
        return Math.Max(0, asOf < anniversary ? years - 1 : years);
    }

    private static bool IsApplicableToPolicy(Employee employee, LeavePolicy? policy, string? eligibleCitizenshipCountryCode)
    {
        if (policy == null) return true;
        if (!policy.IsActive) return false;
        return policy.ApplicableTo.Trim().ToUpperInvariant() switch
        {
            "ALL" => true,
            "GENDER_F" => string.Equals(employee.Gender?.Trim(), "F", StringComparison.OrdinalIgnoreCase),
            "GENDER_M" => string.Equals(employee.Gender?.Trim(), "M", StringComparison.OrdinalIgnoreCase),
            "CITIZENS" => !string.IsNullOrWhiteSpace(eligibleCitizenshipCountryCode) &&
                !string.IsNullOrWhiteSpace(employee.CitizenshipCountryCode) &&
                string.Equals(employee.CitizenshipCountryCode.Trim(), eligibleCitizenshipCountryCode.Trim(), StringComparison.OrdinalIgnoreCase),
            var value => throw new InvalidOperationException($"Unknown leave policy applicability '{value}'.")
        };
    }

    private static decimal GetAnnualEntitlement(LeavePolicy? policy, LeaveType leaveType, int completedServiceYears)
    {
        if (policy?.AnnualEntitlementDays is > 0m)
            return policy.AnnualEntitlementDays.Value;

        var thresholdYears = policy?.Tier1YearsThreshold ?? 5;
        return completedServiceYears >= thresholdYears
            ? policy?.Tier2Days ?? 21m
            : policy?.Tier1Days ?? leaveType.MaxDaysPerYear;
    }

    private static string NormalizeYearEndExcessAction(string? action)
    {
        var normalized = string.IsNullOrWhiteSpace(action) ? "EXPIRE" : action.Trim().ToUpperInvariant();
        if (normalized is not ("EXPIRE" or "REQUIRE_USE" or "CASHOUT"))
            throw new ArgumentException("Year-end excess action must be EXPIRE, REQUIRE_USE, or CASHOUT.", nameof(action));
        return normalized;
    }
}

