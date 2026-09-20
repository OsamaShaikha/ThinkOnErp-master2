using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface ILeaveService
{
    Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
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
    Task<LeaveRequestDto> ProcessLeaveRequestAsync(long requestId, ProcessLeaveRequestDto dto, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveBalanceDto>> GetEmployeeBalancesAsync(string employeeCode, int? year = null, CancellationToken cancellationToken = default);

    Task<decimal> GetUnpaidLeaveDaysInPeriodAsync(string employeeCode, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default);
}

public sealed class LeaveService : ILeaveService
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;

    public LeaveService(ILeaveRepository leaveRepo, IEmployeeRepository employeeRepo)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
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
            t.IsActive
        )).ToList();
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

        var dtos = items.Select(r => new LeaveRequestDto(
            r.Id,
            r.EmployeeCode,
            r.Employee?.NameLocal ?? r.EmployeeCode,
            r.LeaveTypeCode,
            r.LeaveType?.NameLocal ?? r.LeaveTypeCode,
            r.StartDate,
            r.EndDate,
            r.DaysRequested,
            r.Status,
            r.Reason,
            r.ApprovedBy,
            r.ApprovalDate,
            r.RejectionReason,
            r.CreationDate
        )).ToList();

        return (dtos, totalCount);
    }

    public async Task<LeaveRequestDto> SubmitLeaveRequestAsync(SubmitLeaveRequestDto dto, string user, CancellationToken cancellationToken = default)
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

        int year = dto.StartDate.Year;

        // If paid leave, check available balance
        if (leaveType.IsPaid)
        {
            var balance = await _leaveRepo.GetLeaveBalanceAsync(dto.EmployeeCode, dto.LeaveTypeCode, year, cancellationToken);
            decimal available = (balance?.AccruedDays ?? leaveType.MaxDaysPerYear) + (balance?.CarriedForwardDays ?? 0m) - (balance?.UsedDays ?? 0m);

            if (dto.DaysRequested > available)
            {
                throw new InvalidOperationException($"Insufficient leave balance. Requested: {dto.DaysRequested} days, Available: {available} days.");
            }
        }

        var request = new LeaveRequest
        {
            EmployeeCode = dto.EmployeeCode,
            LeaveTypeCode = dto.LeaveTypeCode,
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate.Date,
            DaysRequested = dto.DaysRequested,
            Status = "PENDING",
            Reason = dto.Reason,
            AttachmentFileRef = dto.AttachmentFileRef,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        await _leaveRepo.AddLeaveRequestAsync(request, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return new LeaveRequestDto(
            request.Id,
            request.EmployeeCode,
            emp.NameLocal,
            request.LeaveTypeCode,
            leaveType.NameLocal,
            request.StartDate,
            request.EndDate,
            request.DaysRequested,
            request.Status,
            request.Reason,
            null,
            null,
            null,
            request.CreationDate
        );
    }

    public async Task<LeaveRequestDto> ProcessLeaveRequestAsync(long requestId, ProcessLeaveRequestDto dto, string user, CancellationToken cancellationToken = default)
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

        request.Status = dto.Approved ? "APPROVED" : "REJECTED";
        request.ApprovedBy = user;
        request.ApprovalDate = DateTime.UtcNow;
        request.RejectionReason = dto.RejectionReason;
        request.UpdateUser = user;
        request.UpdateDate = DateTime.UtcNow;

        if (dto.Approved && request.LeaveType != null && request.LeaveType.IsPaid)
        {
            int year = request.StartDate.Year;
            var balance = await _leaveRepo.GetLeaveBalanceAsync(request.EmployeeCode, request.LeaveTypeCode, year, cancellationToken);
            if (balance == null)
            {
                balance = new LeaveBalance
                {
                    EmployeeCode = request.EmployeeCode,
                    LeaveTypeCode = request.LeaveTypeCode,
                    YearNo = year,
                    AccruedDays = request.LeaveType.MaxDaysPerYear,
                    UsedDays = request.DaysRequested,
                    CarriedForwardDays = 0,
                    CreationUser = user,
                    CreationDate = DateTime.UtcNow
                };
                await _leaveRepo.AddLeaveBalanceAsync(balance, cancellationToken);
            }
            else
            {
                balance.UsedDays += request.DaysRequested;
                balance.UpdateUser = user;
                balance.UpdateDate = DateTime.UtcNow;
                await _leaveRepo.UpdateLeaveBalanceAsync(balance, cancellationToken);
            }
        }

        await _leaveRepo.UpdateLeaveRequestAsync(request, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return new LeaveRequestDto(
            request.Id,
            request.EmployeeCode,
            request.Employee?.NameLocal ?? request.EmployeeCode,
            request.LeaveTypeCode,
            request.LeaveType?.NameLocal ?? request.LeaveTypeCode,
            request.StartDate,
            request.EndDate,
            request.DaysRequested,
            request.Status,
            request.Reason,
            request.ApprovedBy,
            request.ApprovalDate,
            request.RejectionReason,
            request.CreationDate
        );
    }

    public async Task<IReadOnlyList<LeaveBalanceDto>> GetEmployeeBalancesAsync(string employeeCode, int? year = null, CancellationToken cancellationToken = default)
    {
        int targetYear = year ?? DateTime.UtcNow.Year;
        var balances = await _leaveRepo.GetEmployeeBalancesAsync(employeeCode, targetYear, cancellationToken);
        var allTypes = await _leaveRepo.GetLeaveTypesAsync(true, cancellationToken);

        var result = new List<LeaveBalanceDto>();

        foreach (var type in allTypes.Where(t => t.IsPaid))
        {
            var bal = balances.FirstOrDefault(b => b.LeaveTypeCode == type.LeaveTypeCode);
            decimal accrued = bal?.AccruedDays ?? type.MaxDaysPerYear;
            decimal carried = bal?.CarriedForwardDays ?? 0m;
            decimal used = bal?.UsedDays ?? 0m;
            decimal remaining = accrued + carried - used;

            result.Add(new LeaveBalanceDto(
                employeeCode,
                type.LeaveTypeCode,
                type.NameLocal,
                targetYear,
                accrued,
                used,
                carried,
                remaining
            ));
        }

        return result;
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
}
