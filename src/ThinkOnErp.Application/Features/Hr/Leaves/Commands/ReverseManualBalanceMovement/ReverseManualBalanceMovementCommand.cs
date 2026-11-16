using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.ReverseManualBalanceMovement;

public sealed record ReverseManualBalanceMovementCommand(
    string EmployeeCode,
    long MovementId,
    ReverseLeaveBalanceMovementDto Dto,
    string User) : IRequest<ApiResponse<LeaveBalanceDto>>;

public sealed class ReverseManualBalanceMovementCommandValidator : AbstractValidator<ReverseManualBalanceMovementCommand>
{
    public ReverseManualBalanceMovementCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty();
        RuleFor(x => x.MovementId).GreaterThan(0);
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class ReverseManualBalanceMovementCommandHandler : IRequestHandler<ReverseManualBalanceMovementCommand, ApiResponse<LeaveBalanceDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public ReverseManualBalanceMovementCommandHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeaveBalanceDto>> Handle(ReverseManualBalanceMovementCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        var normalizedEmployeeCode = request.EmployeeCode?.Trim() ?? string.Empty;
        var normalizedReason = dto.Reason?.Trim() ?? string.Empty;
        if (normalizedEmployeeCode.Length == 0) throw new ArgumentException("EmployeeCode is required.", nameof(request.EmployeeCode));
        if (request.MovementId <= 0) throw new ArgumentOutOfRangeException(nameof(request.MovementId));
        if (normalizedReason.Length is 0 or > 1000) throw new ArgumentException("A reversal reason from 1 to 1000 characters is required.", nameof(dto));

        var result = await _leaveRepo.ExecuteForLeaveEmployeeAsync(normalizedEmployeeCode,
            () => ReverseCoreAsync(normalizedEmployeeCode, request.MovementId, normalizedReason, request.User, cancellationToken), cancellationToken);

        return ApiResponse<LeaveBalanceDto>.CreateSuccess(result, "Manual leave-balance adjustment reversed.");
    }

    private async Task<LeaveBalanceDto> ReverseCoreAsync(string employeeCode, long movementId,
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
        var payload = LeaveMovementKey.Hash(JsonSerializer.Serialize(new { MovementId = original.Id, Reason = reason }));
        var replay = await _leaveRepo.GetMovementByKeyAsync(operationKey, cancellationToken);
        if (replay != null)
        {
            if (replay.PayloadHash != payload)
                throw new InvalidOperationException("This manual balance movement was already reversed with a different reason.");
            return await LeaveCommon.FormatManualMovementBalanceAsync(_leaveRepo, replay, cancellationToken);
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
        return await LeaveCommon.FormatManualMovementBalanceAsync(_leaveRepo, reversal, cancellationToken);
    }
}
