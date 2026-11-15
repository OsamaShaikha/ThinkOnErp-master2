using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.ProcessLeaveRequest;

public sealed record ProcessLeaveRequestCommand(
    long RequestId,
    ProcessLeaveRequestDto Dto,
    string User) : IRequest<ApiResponse<LeaveRequestDto>>;

public sealed class ProcessLeaveRequestCommandValidator : AbstractValidator<ProcessLeaveRequestCommand>
{
    public ProcessLeaveRequestCommandValidator()
    {
        RuleFor(x => x.RequestId).GreaterThan(0);
        RuleFor(x => x.Dto).NotNull();
    }
}

public sealed class ProcessLeaveRequestCommandHandler : IRequestHandler<ProcessLeaveRequestCommand, ApiResponse<LeaveRequestDto>>
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IHrUnitOfWork? _unitOfWork;

    public ProcessLeaveRequestCommandHandler(
        ILeaveRepository leaveRepo,
        IEmployeeRepository employeeRepo,
        IHrUnitOfWork? unitOfWork = null)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<LeaveRequestDto>> Handle(ProcessLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var snapshot = await _leaveRepo.GetLeaveRequestSnapshotAsync(request.RequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave request '{request.RequestId}' was not found.");

        LeaveRequestDto result;
        if (_unitOfWork == null)
        {
            result = await _leaveRepo.ExecuteForLeaveEmployeeAsync(snapshot.EmployeeCode,
                () => ProcessCoreAsync(request.RequestId, request.Dto, request.User, cancellationToken), cancellationToken);
        }
        else
        {
            result = await _unitOfWork.ExecuteForEmployeeDatesAsync(snapshot.EmployeeCode, snapshot.StartDate, snapshot.EndDate,
                () => ProcessCoreAsync(request.RequestId, request.Dto, request.User, cancellationToken), cancellationToken);
        }

        return ApiResponse<LeaveRequestDto>.CreateSuccess(result, "Leave request processed successfully.");
    }

    private async Task<LeaveRequestDto> ProcessCoreAsync(long requestId, ProcessLeaveRequestDto dto, string user, CancellationToken cancellationToken)
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
            var (closePolicy, approvalCycle) = await LeaveCommon.ResolveLeavePolicyCycleAsync(_leaveRepo, closeEmployee, request.LeaveTypeCode, request.StartDate.Date, closeSettings, cancellationToken);
            if (request.LeaveType?.TracksBalance == true && closePolicy == null && await LeaveCommon.HasLeavePolicyHistoryAsync(_leaveRepo, request.LeaveTypeCode, cancellationToken))
                throw new InvalidOperationException("No leave policy is effective for this employee's cycle.");
            if (request.LeaveType?.TracksBalance == true && !LeaveCommon.IsApplicableToPolicy(closeEmployee, closePolicy, closeSettings.EligibleCitizenshipCountryCode))
                throw new InvalidOperationException("The employee is not eligible under this leave policy.");
            await _leaveRepo.EnsureLeaveCycleOpenAsync(request.EmployeeCode, request.LeaveTypeCode, approvalCycle.Start, approvalCycle.End, cancellationToken);
        }

        if (dto.Approved && request.LeaveType != null && request.LeaveType.TracksBalance)
        {
            var employee = await _employeeRepo.GetEmployeeByCodeAsync(request.EmployeeCode, cancellationToken)
                ?? throw new InvalidOperationException($"Employee '{request.EmployeeCode}' not found.");
            var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
            var (policy, cycle) = await LeaveCommon.ResolveLeavePolicyCycleAsync(_leaveRepo, employee, request.LeaveTypeCode, request.StartDate.Date, settings, cancellationToken);
            if (policy == null && await LeaveCommon.HasLeavePolicyHistoryAsync(_leaveRepo, request.LeaveTypeCode, cancellationToken))
                throw new InvalidOperationException("No leave policy is effective for this employee's cycle.");
            if (!LeaveCommon.IsApplicableToPolicy(employee, policy, settings.EligibleCitizenshipCountryCode))
                throw new InvalidOperationException("The employee is not eligible under this leave policy.");
            if (request.EndDate.Date > cycle.End)
                throw new InvalidOperationException("Split this leave request at the leave-year boundary before approval.");

            int year = cycle.Start.Year;
            var balance = await _leaveRepo.GetLeaveBalanceByCycleAsync(request.EmployeeCode, request.LeaveTypeCode, cycle.Start, cancellationToken);
            var beforeUsage = LeaveBalanceState.From(balance);
            var companyToday = LeaveCommon.GetLeaveCompanyToday(settings);
            var asOfDate = companyToday < cycle.End ? companyToday : cycle.End;
            var accruedToDate = LeaveCommon.CalculateAccruedToDate(employee, request.LeaveType, policy, asOfDate, settings);
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

        return LeaveCommon.MapLeaveRequest(request);
    }
}
