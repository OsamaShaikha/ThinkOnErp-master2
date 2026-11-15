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

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.CancelLeaveRequest;

public sealed record CancelLeaveRequestCommand(
    string EmployeeCode,
    long RequestId,
    CancelLeaveRequestDto Dto,
    string User) : IRequest<ApiResponse<LeaveRequestDto>>;

public sealed class CancelLeaveRequestCommandValidator : AbstractValidator<CancelLeaveRequestCommand>
{
    public CancelLeaveRequestCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty();
        RuleFor(x => x.RequestId).GreaterThan(0);
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class CancelLeaveRequestCommandHandler : IRequestHandler<CancelLeaveRequestCommand, ApiResponse<LeaveRequestDto>>
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IHrUnitOfWork? _unitOfWork;

    public CancelLeaveRequestCommandHandler(
        ILeaveRepository leaveRepo,
        IEmployeeRepository employeeRepo,
        IHrUnitOfWork? unitOfWork = null)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<LeaveRequestDto>> Handle(CancelLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        var reason = dto.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new ArgumentException("A cancellation reason between 1 and 500 characters is required.", nameof(dto.Reason));

        var snapshot = await _leaveRepo.GetLeaveRequestSnapshotAsync(request.RequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave request '{request.RequestId}' was not found.");
        if (!string.Equals(snapshot.EmployeeCode, request.EmployeeCode, StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException("Leave request was not found for this employee.");

        LeaveRequestDto result;
        if (_unitOfWork == null)
        {
            result = await _leaveRepo.ExecuteForLeaveEmployeeAsync(request.EmployeeCode,
                () => CancelCoreAsync(request.RequestId, reason, request.User, cancellationToken), cancellationToken);
        }
        else
        {
            result = await _unitOfWork.ExecuteForEmployeeDatesAsync(request.EmployeeCode, snapshot.StartDate, snapshot.EndDate,
                () => CancelCoreAsync(request.RequestId, reason, request.User, cancellationToken), cancellationToken);
        }

        return ApiResponse<LeaveRequestDto>.CreateSuccess(result, "Leave request cancelled and any posted balance usage reversed.");
    }

    private async Task<LeaveRequestDto> CancelCoreAsync(long requestId, string reason, string user, CancellationToken cancellationToken)
    {
        var req = await _leaveRepo.GetLeaveRequestByIdAsync(requestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave request '{requestId}' was not found.");
        if (req.Status is not ("PENDING" or "APPROVED"))
            throw new InvalidOperationException($"Only pending or approved leave requests can be cancelled; current status is '{req.Status}'.");

        if (req.Status == "APPROVED")
        {
            var leaveType = req.LeaveType
                ?? throw new InvalidOperationException("Approved leave request has no leave type; reconcile the request before cancellation.");
            var usageKey = LeaveMovementKey.Hash($"REQUEST_USE:{req.Id}");
            var usage = await _leaveRepo.GetMovementByKeyAsync(usageKey, cancellationToken);
            if (usage != null || leaveType.TracksBalance)
            {
                if (usage == null)
                    throw new InvalidOperationException("The original leave usage movement is missing; reconcile the ledger before cancellation.");
                var employee = await _employeeRepo.GetEmployeeByCodeAsync(req.EmployeeCode, cancellationToken)
                    ?? throw new InvalidOperationException("Employee not found.");
                var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
                var (policy, cycle) = await LeaveCommon.ResolveLeavePolicyCycleAsync(_leaveRepo, employee, req.LeaveTypeCode, req.StartDate.Date, settings, cancellationToken);
                if (req.EndDate.Date > cycle.End)
                    throw new InvalidOperationException("The approved leave crosses a leave-cycle boundary; reconcile it before cancellation.");
                await _leaveRepo.EnsureLeaveCycleOpenAsync(req.EmployeeCode, req.LeaveTypeCode, cycle.Start, cycle.End, cancellationToken);

                var balance = await _leaveRepo.GetLeaveBalanceByCycleAsync(req.EmployeeCode, req.LeaveTypeCode, cycle.Start, cancellationToken)
                    ?? throw new InvalidOperationException("Approved leave has no saved balance; reconcile the balance before cancellation.");
                if (usage.BalanceId != balance.Id || usage.Kind != "USAGE" || usage.UsedDelta != req.DaysRequested)
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
                    LeaveMovementKey.Hash($"REQUEST_CANCEL:{req.Id}"), $"LEAVE_REQUEST_CANCEL:{req.Id}", reason, user,
                    reversesId: usage.Id, cancellationToken: cancellationToken);
            }
        }

        var cancelledAt = DateTime.UtcNow;
        req.Status = "CANCELLED";
        req.CancelledBy = user;
        req.CancellationDate = cancelledAt;
        req.CancellationReason = reason;
        req.UpdateUser = user;
        req.UpdateDate = cancelledAt;
        await _leaveRepo.UpdateLeaveRequestAsync(req, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return LeaveCommon.MapLeaveRequest(req);
    }
}
