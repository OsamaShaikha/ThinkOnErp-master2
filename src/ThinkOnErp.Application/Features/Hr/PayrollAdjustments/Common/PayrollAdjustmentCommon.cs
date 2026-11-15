using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Common;

public static class PayrollAdjustmentCommon
{
    public static bool IsAttendanceOvertime(PayrollAdjustment adjustment)
        => string.Equals(adjustment.SourceType, "ATTENDANCE_OVERTIME", StringComparison.OrdinalIgnoreCase);

    public static void EnsureOvertimeBranchScope(PayrollAdjustment adjustment, long? branchId)
    {
        if (IsAttendanceOvertime(adjustment) && (!branchId.HasValue || branchId != adjustment.SourceBranchId))
            throw new UnauthorizedAccessException("A matching permitted source branch is required for this attendance overtime adjustment.");
    }

    public static async Task EnsureMutableAsync(
        PayrollAdjustment adjustment,
        IPayrollAdjustmentRepository adjustmentRepository,
        CancellationToken cancellationToken)
    {
        if (adjustment.PayrollRunLineId.HasValue || adjustment.Status == "PROCESSED")
            throw new InvalidOperationException("This adjustment has been consumed by payroll; reverse the dependent payroll first.");
        if (await adjustmentRepository.HasCalculatedPayrollAsync(adjustment.EmployeeCode, adjustment.PayPeriod, cancellationToken))
            throw new InvalidOperationException("Payroll is already calculated for this employee and period. Cancel the calculation before changing adjustments.");
    }

    public static async Task<T> ExecuteLockedAsync<T>(
        long id,
        IPayrollAdjustmentRepository adjustmentRepository,
        IHrUnitOfWork? unitOfWork,
        IAttendanceCorrectionRepository? attendanceRepository,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (unitOfWork == null) return await action();
        var adjustment = await adjustmentRepository.GetAdjustmentByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Payroll adjustment with ID {id} not found.");
        return await unitOfWork.ExecuteForPayrollPeriodAsync(adjustment.CompanyId, adjustment.PayPeriod, () =>
        {
            if (IsAttendanceOvertime(adjustment) && adjustment.SourceAttendanceDate.HasValue && attendanceRepository != null)
                return attendanceRepository.ExecuteForRawWorkDateAsync(adjustment.EmployeeCode, adjustment.SourceAttendanceDate.Value.Date, action, cancellationToken);
            return action();
        }, cancellationToken);
    }

    public static async Task EnsureAttendanceOvertimeSourceCurrentAsync(
        PayrollAdjustment adjustment,
        IAttendanceCorrectionRepository? attendanceRepository,
        IEmployeeRepository? employeeRepository,
        IPayrollPeriodRepository? payrollPeriodRepository,
        IOvertimeCalculationService? overtimeCalculationService,
        CancellationToken cancellationToken)
    {
        if (attendanceRepository == null || employeeRepository == null || payrollPeriodRepository == null || overtimeCalculationService == null
            || !long.TryParse(adjustment.SourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var attendanceId)
            || !adjustment.SourceAttendanceDate.HasValue || !adjustment.SourceBranchId.HasValue || !adjustment.SourceOvertimeHours.HasValue
            || !adjustment.SourceBaseSalary.HasValue || string.IsNullOrWhiteSpace(adjustment.SourceDayType)
            || string.IsNullOrWhiteSpace(adjustment.SourceCurrencyCode))
            throw new InvalidOperationException("Attendance overtime source snapshot is incomplete; it cannot be approved.");

        var date = adjustment.SourceAttendanceDate.Value.Date;
        var day = await attendanceRepository.GetAttendanceDayAsync(adjustment.EmployeeCode, date, cancellationToken);
        if (day == null || day.Id != attendanceId || day.AttendanceDate.Date != date || day.HasMissingPunch
            || day.OvertimeHours != adjustment.SourceOvertimeHours.Value)
            throw new InvalidOperationException("Attendance source changed after overtime submission. Remove and resubmit the adjustment after review.");
        var status = day.Status?.Trim().ToUpperInvariant();
        if (status is not ("PRESENT" or "REST_DAY" or "HOLIDAY"))
            throw new InvalidOperationException("Attendance status no longer qualifies for overtime approval.");
        var employee = await employeeRepository.GetEmployeeByCodeAsync(adjustment.EmployeeCode, cancellationToken)
            ?? throw new InvalidOperationException("Attendance overtime employee no longer exists.");
        if (!employee.IsActive || !string.Equals(employee.EmploymentStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Inactive employees cannot receive attendance overtime approval.");
        if (day.CheckInBranchId.HasValue && day.CheckOutBranchId.HasValue && day.CheckInBranchId.Value != day.CheckOutBranchId.Value)
            throw new InvalidOperationException("Attendance branch evidence now conflicts between check-in and check-out.");
        var currentBranch = day.CheckInBranchId ?? day.CheckOutBranchId ?? employee.BranchId;
        if (currentBranch != adjustment.SourceBranchId)
            throw new InvalidOperationException("Attendance source branch changed after overtime submission.");
        var pending = await attendanceRepository.GetCorrectionRequestsAsync(adjustment.EmployeeCode, "PENDING", cancellationToken);
        if (pending.Any(r => r.AttendanceDate.Date == date))
            throw new InvalidOperationException("A pending attendance correction must be resolved before overtime approval.");

        var period = await payrollPeriodRepository.GetPeriodByCodeAsync(adjustment.CompanyId, adjustment.PayPeriod, cancellationToken);
        if (period == null || period.CompanyId != adjustment.CompanyId || !string.Equals(period.Status?.Trim(), "OPEN", StringComparison.OrdinalIgnoreCase)
            || date < period.StartDate.Date || date > period.EndDate.Date)
            throw new InvalidOperationException("The source attendance date is no longer inside an open payroll period.");
        var component = await employeeRepository.GetSalaryComponentByCodeAsync(adjustment.ComponentCode, cancellationToken);
        if (component == null || !component.IsActive || component.ComponentType?.Trim().ToUpperInvariant() is not ("BASIC" or "ALLOWANCE"))
            throw new InvalidOperationException("The overtime earning component is no longer active.");
        var salary = await employeeRepository.GetActiveSalaryStructureAsync(adjustment.EmployeeCode, date, cancellationToken);
        if (salary == null || salary.BasicSalary != adjustment.SourceBaseSalary.Value
            || !string.Equals(salary.CurrencyCode?.Trim(), adjustment.SourceCurrencyCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The salary basis changed after overtime submission. Remove and resubmit the adjustment.");
        var currentDayType = status switch { "HOLIDAY" => "HOLIDAY", "REST_DAY" => "WEEKEND", _ => "REGULAR" };
        if (!string.Equals(currentDayType, adjustment.SourceDayType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The attendance day type changed after overtime submission.");
        var currentAmount = await overtimeCalculationService.CalculateOvertimeEarningsAsync(
            adjustment.CompanyId, currentBranch.Value, salary.CurrencyCode, salary.BasicSalary, day.OvertimeHours,
            currentDayType, date, cancellationToken);
        if (currentAmount != adjustment.Amount)
            throw new InvalidOperationException("The effective overtime policy changed after submission. Remove and resubmit under the current policy.");
    }

    public static async Task<PayrollAdjustment> DecideAdjustmentCoreAsync(
        long id,
        bool approve,
        string reason,
        string user,
        long? branchId,
        IPayrollAdjustmentRepository adjustmentRepository,
        IEmployeeRepository? employeeRepository,
        IAttendanceCorrectionRepository? attendanceRepository,
        IPayrollPeriodRepository? payrollPeriodRepository,
        IOvertimeCalculationService? overtimeCalculationService,
        CancellationToken cancellationToken)
    {
        var adjustment = await adjustmentRepository.GetAdjustmentByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Payroll adjustment with ID {id} not found.");
        await EnsureMutableAsync(adjustment, adjustmentRepository, cancellationToken);
        if (adjustment.Status != "PENDING")
            throw new InvalidOperationException("Only a pending adjustment can be approved or rejected.");
        EnsureOvertimeBranchScope(adjustment, branchId);
        if (approve)
        {
            var employeeUserName = employeeRepository == null ? null :
                await employeeRepository.GetLinkedUserNameByEmployeeCodeAsync(adjustment.EmployeeCode, cancellationToken);
            HrApprovalGuard.EnsureNotSelfApproved(user, adjustment.CreationUser, employeeUserName, "payroll adjustment");
        }
        if (approve && IsAttendanceOvertime(adjustment))
            await EnsureAttendanceOvertimeSourceCurrentAsync(adjustment, attendanceRepository, employeeRepository, payrollPeriodRepository, overtimeCalculationService, cancellationToken);

        adjustment.Status = approve ? "APPROVED" : "REJECTED";
        adjustment.DecisionReason = reason;
        adjustment.UpdateUser = user;
        adjustment.UpdateDate = DateTime.UtcNow;
        await adjustmentRepository.UpdateAdjustmentAsync(adjustment, cancellationToken);
        await adjustmentRepository.SaveChangesAsync(cancellationToken);
        return adjustment;
    }

    public static async Task<PayrollAdjustment> ApproveAdjustmentCoreAsync(
        long id,
        string user,
        IPayrollAdjustmentRepository adjustmentRepository,
        IEmployeeRepository? employeeRepository,
        CancellationToken cancellationToken)
    {
        var adj = await adjustmentRepository.GetAdjustmentByIdAsync(id, cancellationToken);
        if (adj == null) throw new KeyNotFoundException($"Payroll adjustment with ID {id} not found.");

        await EnsureMutableAsync(adj, adjustmentRepository, cancellationToken);
        if (adj.Status != "PENDING")
            throw new InvalidOperationException("Only a pending adjustment can be approved.");
        if (IsAttendanceOvertime(adj))
            throw new InvalidOperationException("Attendance overtime approval requires a reason and branch-scoped decision.");

        var employeeUserName = employeeRepository == null ? null :
            await employeeRepository.GetLinkedUserNameByEmployeeCodeAsync(adj.EmployeeCode, cancellationToken);
        HrApprovalGuard.EnsureNotSelfApproved(user, adj.CreationUser, employeeUserName, "payroll adjustment");

        adj.Status = "APPROVED";
        adj.UpdateUser = user;
        adj.UpdateDate = DateTime.UtcNow;

        await adjustmentRepository.UpdateAdjustmentAsync(adj, cancellationToken);
        await adjustmentRepository.SaveChangesAsync(cancellationToken);
        return adj;
    }

    public static async Task<PayrollAdjustment> RejectAdjustmentCoreAsync(
        long id,
        string user,
        IPayrollAdjustmentRepository adjustmentRepository,
        CancellationToken cancellationToken)
    {
        var adj = await adjustmentRepository.GetAdjustmentByIdAsync(id, cancellationToken);
        if (adj == null) throw new KeyNotFoundException($"Payroll adjustment with ID {id} not found.");

        await EnsureMutableAsync(adj, adjustmentRepository, cancellationToken);
        if (adj.Status != "PENDING")
            throw new InvalidOperationException("Only a pending adjustment can be rejected. Unpost an approved adjustment first.");
        if (IsAttendanceOvertime(adj))
            throw new InvalidOperationException("Attendance overtime rejection requires a reason and branch-scoped decision.");

        adj.Status = "REJECTED";
        adj.UpdateUser = user;
        adj.UpdateDate = DateTime.UtcNow;

        await adjustmentRepository.UpdateAdjustmentAsync(adj, cancellationToken);
        await adjustmentRepository.SaveChangesAsync(cancellationToken);
        return adj;
    }

    public static async Task<PayrollAdjustment> UnpostAdjustmentCoreAsync(
        long id,
        string user,
        long? branchId,
        string? reason,
        IPayrollAdjustmentRepository adjustmentRepository,
        CancellationToken cancellationToken)
    {
        var adj = await adjustmentRepository.GetAdjustmentByIdAsync(id, cancellationToken);
        if (adj == null) throw new KeyNotFoundException($"Payroll adjustment with ID {id} not found.");
        await EnsureMutableAsync(adj, adjustmentRepository, cancellationToken);
        if (adj.Status != "APPROVED")
            throw new InvalidOperationException("Only an approved adjustment can be unposted.");
        EnsureOvertimeBranchScope(adj, branchId);
        if (IsAttendanceOvertime(adj))
        {
            var normalizedReason = reason?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 1000)
                throw new ArgumentException("A reason between 1 and 1000 characters is required to unpost attendance overtime.", nameof(reason));
            adj.DecisionReason = normalizedReason;
        }
        adj.Status = "PENDING";
        adj.UpdateUser = user;
        adj.UpdateDate = DateTime.UtcNow;
        await adjustmentRepository.UpdateAdjustmentAsync(adj, cancellationToken);
        await adjustmentRepository.SaveChangesAsync(cancellationToken);
        return adj;
    }

    public static async Task DeleteAdjustmentCoreAsync(
        long id,
        IPayrollAdjustmentRepository adjustmentRepository,
        CancellationToken cancellationToken)
    {
        var adj = await adjustmentRepository.GetAdjustmentByIdAsync(id, cancellationToken);
        if (adj != null)
        {
            await EnsureMutableAsync(adj, adjustmentRepository, cancellationToken);
            if (adj.Status is not ("PENDING" or "REJECTED"))
                throw new InvalidOperationException("Only pending or rejected adjustments can be deleted. Unpost an approved adjustment first.");
            await adjustmentRepository.DeleteAdjustmentAsync(adj, cancellationToken);
            await adjustmentRepository.SaveChangesAsync(cancellationToken);
        }
    }

    public static PayrollAdjustmentDto ToDto(PayrollAdjustment a)
    {
        return new PayrollAdjustmentDto(
            a.Id,
            a.CompanyId,
            a.EmployeeCode,
            a.Employee?.NameLocal ?? a.EmployeeCode,
            a.PayPeriod,
            a.ComponentCode,
            a.Component?.NameLocal ?? a.ComponentCode,
            a.AdjustmentType,
            a.Amount,
            a.Description,
            a.Status,
            a.DecisionReason,
            a.SourceType,
            a.SourceId,
            a.SourceBranchId,
            a.SourceAttendanceDate,
            a.SourceOvertimeHours,
            a.SourceBaseSalary,
            a.SourceDayType,
            a.SourceCurrencyCode
        );
    }
}
