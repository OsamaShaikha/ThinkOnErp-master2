using System;
using System.Collections.Generic;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Common;

internal static class ShortLeaveCommon
{
    public static string NormalizeExcessAction(string? action, bool invalidOperation)
    {
        var normalized = action?.Trim();
        if (string.Equals(normalized, ShortLeaveCompensationTypes.DeductSalary, StringComparison.OrdinalIgnoreCase))
            return ShortLeaveCompensationTypes.DeductSalary;
        if (string.Equals(normalized, ShortLeaveCompensationTypes.DeductLeaveBalance, StringComparison.OrdinalIgnoreCase))
            return ShortLeaveCompensationTypes.DeductLeaveBalance;
        if (string.Equals(normalized, "REJECT", StringComparison.OrdinalIgnoreCase))
            return "REJECT";
        if (invalidOperation)
            throw new InvalidOperationException("The company short leave excess action is not supported.");
        throw new ArgumentException("Excess action must be DEDUCT_SALARY, DEDUCT_LEAVE_BALANCE, or REJECT.", nameof(action));
    }

    public static (int ExcusedLateMinutes, int ExcusedEarlyMinutes, int ExcusedMiddayMinutes) CalculateExcusedMinutes(IEnumerable<ShortLeaveRequest> approvedRequests)
    {
        int late = 0;
        int early = 0;
        int midday = 0;

        foreach (var req in approvedRequests)
        {
            if (req.Status != ShortLeaveStatus.Approved)
                continue;

            if (string.Equals(req.PermissionType, ShortLeaveTypes.LateArrival, StringComparison.OrdinalIgnoreCase))
                late += req.DurationMinutes;
            else if (string.Equals(req.PermissionType, ShortLeaveTypes.EarlyDeparture, StringComparison.OrdinalIgnoreCase))
                early += req.DurationMinutes;
            else if (string.Equals(req.PermissionType, ShortLeaveTypes.MiddayExit, StringComparison.OrdinalIgnoreCase))
                midday += req.DurationMinutes;
        }

        return (late, early, midday);
    }

    public static ShortLeaveDto MapToDto(ShortLeaveRequest r, string? employeeName) =>
        new(
            r.Id,
            r.CompanyId,
            r.BranchId,
            r.EmployeeCode,
            employeeName,
            r.PermissionType,
            r.PermissionDate,
            r.StartTime,
            r.EndTime,
            r.DurationMinutes,
            r.Reason,
            r.Status,
            r.CompensationType,
            r.DeductedSalaryAmount,
            r.ConvertedLeaveDays,
            r.ApprovedBy,
            r.ApprovedAtUtc,
            r.RejectedBy,
            r.RejectedAtUtc,
            r.RejectionReason,
            r.CancelledBy,
            r.CancelledAtUtc,
            r.CancellationReason,
            r.CreationDate
        );

    public static ShortLeavePolicyDto MapPolicyToDto(ShortLeavePolicy p) =>
        new(
            p.Id,
            p.CompanyId,
            p.MonthlyPaidMinutesQuota,
            p.MaxMinutesPerRequest,
            p.AllowMiddayExit,
            p.ExcessAction,
            p.LeaveTypeCodeForDeduction,
            p.StandardWorkdayHours,
            p.IsActive
        );
}
