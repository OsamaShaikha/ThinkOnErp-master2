using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.CreateOvertimeAdjustment;

public sealed record CreateOvertimeAdjustmentCommand(
    CreateOvertimePayrollAdjustmentDto Dto,
    string Username
) : IRequest<ApiResponse<PayrollAdjustment>>;

public sealed class CreateOvertimeAdjustmentCommandValidator : AbstractValidator<CreateOvertimeAdjustmentCommand>
{
    public CreateOvertimeAdjustmentCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.CompanyId).GreaterThan(0);
        RuleFor(x => x.Dto.BranchId).GreaterThan(0);
        RuleFor(x => x.Dto.EmployeeCode).NotEmpty();
        RuleFor(x => x.Dto.PayPeriod).NotEmpty();
        RuleFor(x => x.Dto.ComponentCode).NotEmpty();
    }
}

public sealed class CreateOvertimeAdjustmentCommandHandler : IRequestHandler<CreateOvertimeAdjustmentCommand, ApiResponse<PayrollAdjustment>>
{
    private readonly IPayrollAdjustmentRepository _adjustmentRepository;
    private readonly IAttendanceCorrectionRepository? _attendanceRepository;
    private readonly IEmployeeRepository? _employeeRepository;
    private readonly IPayrollPeriodRepository? _payrollPeriodRepository;
    private readonly IOvertimeCalculationService? _overtimeCalculationService;
    private readonly IHrUnitOfWork? _unitOfWork;

    public CreateOvertimeAdjustmentCommandHandler(
        IPayrollAdjustmentRepository adjustmentRepository,
        IAttendanceCorrectionRepository? attendanceRepository = null,
        IEmployeeRepository? employeeRepository = null,
        IPayrollPeriodRepository? payrollPeriodRepository = null,
        IOvertimeCalculationService? overtimeCalculationService = null,
        IHrUnitOfWork? unitOfWork = null)
    {
        _adjustmentRepository = adjustmentRepository;
        _attendanceRepository = attendanceRepository;
        _employeeRepository = employeeRepository;
        _payrollPeriodRepository = payrollPeriodRepository;
        _overtimeCalculationService = overtimeCalculationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<PayrollAdjustment>> Handle(CreateOvertimeAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (dto.CompanyId <= 0 || dto.BranchId <= 0 || string.IsNullOrWhiteSpace(dto.EmployeeCode)
            || string.IsNullOrWhiteSpace(dto.PayPeriod) || string.IsNullOrWhiteSpace(dto.ComponentCode))
            throw new ArgumentException("Company, branch, employee, pay period and earning component are required.");
        if (_attendanceRepository == null || _employeeRepository == null || _payrollPeriodRepository == null || _overtimeCalculationService == null)
            throw new InvalidOperationException("Attendance-linked overtime adjustment dependencies are not configured.");

        var result = _unitOfWork == null
            ? await CreateOvertimeAdjustmentCoreAsync(dto, request.Username, cancellationToken)
            : await _unitOfWork.ExecuteForPayrollPeriodAsync(dto.CompanyId, dto.PayPeriod,
                () => _attendanceRepository.ExecuteForRawWorkDateAsync(dto.EmployeeCode.Trim(), dto.AttendanceDate.Date,
                    () => CreateOvertimeAdjustmentCoreAsync(dto, request.Username, cancellationToken), cancellationToken), cancellationToken);

        return ApiResponse<PayrollAdjustment>.CreateSuccess(result, "Overtime adjustment submitted for review.");
    }

    private async Task<PayrollAdjustment> CreateOvertimeAdjustmentCoreAsync(CreateOvertimePayrollAdjustmentDto dto, string user, CancellationToken cancellationToken)
    {
        var attendanceDate = dto.AttendanceDate.Date;
        var employeeCode = dto.EmployeeCode.Trim();
        var payPeriodCode = dto.PayPeriod.Trim();
        var componentCode = dto.ComponentCode.Trim();
        var attendance = await _attendanceRepository!.GetAttendanceDayAsync(employeeCode, attendanceDate, cancellationToken)
            ?? throw new KeyNotFoundException("Attendance day was not found for the requested employee and date.");
        var attendanceStatus = attendance.Status?.Trim().ToUpperInvariant();
        if (!string.Equals(attendance.EmployeeCode, employeeCode, StringComparison.OrdinalIgnoreCase)
            || attendance.AttendanceDate.Date != attendanceDate)
            throw new InvalidOperationException("Attendance source does not match the requested employee and date.");
        if (attendance.Id <= 0 || attendance.HasMissingPunch || attendance.OvertimeHours <= 0m || attendanceStatus is not ("PRESENT" or "REST_DAY" or "HOLIDAY"))
            throw new InvalidOperationException("Only completed attendance with positive overtime and no missing punch can be submitted for review.");

        var employee = await _employeeRepository!.GetEmployeeByCodeAsync(employeeCode, cancellationToken)
            ?? throw new KeyNotFoundException("Employee was not found.");
        if (!employee.IsActive || !string.Equals(employee.EmploymentStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Overtime cannot be submitted for an inactive employee.");
        if (attendance.CheckInBranchId.HasValue && attendance.CheckOutBranchId.HasValue
            && attendance.CheckInBranchId.Value != attendance.CheckOutBranchId.Value)
            throw new InvalidOperationException("Attendance branch evidence conflicts between check-in and check-out.");
        var sourceBranchId = attendance.CheckInBranchId ?? attendance.CheckOutBranchId ?? employee.BranchId;
        if (!sourceBranchId.HasValue || sourceBranchId.Value != dto.BranchId)
            throw new InvalidOperationException("Requested branch is not the branch recorded for this attendance source.");
        var pendingCorrections = await _attendanceRepository.GetCorrectionRequestsAsync(employeeCode, "PENDING", cancellationToken);
        if (pendingCorrections.Any(r => r.AttendanceDate.Date == attendanceDate))
            throw new InvalidOperationException("Attendance overtime cannot be submitted while a correction request is pending for the source date.");

        var sourceType = "ATTENDANCE_OVERTIME";
        var sourceId = attendance.Id.ToString(CultureInfo.InvariantCulture);
        var existing = await _adjustmentRepository.GetAdjustmentBySourceAsync(dto.CompanyId, sourceType, sourceId, cancellationToken);

        var period = await _payrollPeriodRepository!.GetPeriodByCodeAsync(dto.CompanyId, payPeriodCode, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll period was not found for this company.");
        if (period.CompanyId != dto.CompanyId || !string.Equals(period.Status?.Trim(), "OPEN", StringComparison.OrdinalIgnoreCase)
            || attendanceDate < period.StartDate.Date || attendanceDate > period.EndDate.Date)
            throw new InvalidOperationException("Attendance date must be inside an open payroll period.");
        if (await _adjustmentRepository.HasCalculatedPayrollAsync(employeeCode, payPeriodCode, cancellationToken))
            throw new InvalidOperationException("Payroll is already calculated for this employee and period. Cancel the calculation before submitting overtime.");

        var component = await _employeeRepository.GetSalaryComponentByCodeAsync(componentCode, cancellationToken)
            ?? throw new KeyNotFoundException("Configured salary component was not found.");
        var componentType = component.ComponentType?.Trim().ToUpperInvariant();
        if (!component.IsActive || componentType is not ("BASIC" or "ALLOWANCE"))
            throw new InvalidOperationException("Overtime must use an active earning salary component.");

        var salary = await _employeeRepository.GetActiveSalaryStructureAsync(employeeCode, attendanceDate, cancellationToken)
            ?? throw new InvalidOperationException("No active salary structure exists for the attendance date.");
        if (salary.BasicSalary <= 0m)
            throw new InvalidOperationException("Active salary structure has no positive basic salary for overtime calculation.");
        var dayType = attendanceStatus switch
        {
            "HOLIDAY" => "HOLIDAY",
            "REST_DAY" => "WEEKEND",
            _ => "REGULAR"
        };
        var amount = await _overtimeCalculationService!.CalculateOvertimeEarningsAsync(
            dto.CompanyId, sourceBranchId.Value, salary.CurrencyCode, salary.BasicSalary, attendance.OvertimeHours,
            dayType, attendanceDate, cancellationToken);
        if (amount <= 0m)
            throw new InvalidOperationException("Attendance overtime does not meet the effective company policy minimum.");

        if (existing != null)
        {
            var sameSourceSnapshot = string.Equals(existing.SourceType, sourceType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.SourceId, sourceId, StringComparison.Ordinal)
                && string.Equals(existing.EmployeeCode, employeeCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.PayPeriod, payPeriodCode, StringComparison.Ordinal)
                && string.Equals(existing.ComponentCode, component.ComponentCode, StringComparison.OrdinalIgnoreCase)
                && existing.SourceBranchId == sourceBranchId
                && existing.SourceAttendanceDate?.Date == attendanceDate
                && existing.SourceOvertimeHours == attendance.OvertimeHours
                && existing.SourceBaseSalary == salary.BasicSalary
                && string.Equals(existing.SourceCurrencyCode?.Trim(), salary.CurrencyCode?.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.SourceDayType, dayType, StringComparison.OrdinalIgnoreCase)
                && existing.Amount == amount;
            if (!sameSourceSnapshot)
                throw new InvalidOperationException("Attendance overtime already has a payroll adjustment with a different attendance, salary, currency, policy, or payroll snapshot.");
            return existing;
        }

        var adjustment = new PayrollAdjustment
        {
            CompanyId = dto.CompanyId,
            EmployeeCode = employeeCode,
            PayPeriod = payPeriodCode,
            ComponentCode = component.ComponentCode,
            AdjustmentType = "EARNING",
            Amount = amount,
            Description = $"Attendance overtime {attendance.OvertimeHours:0.####} hours on {attendanceDate:yyyy-MM-dd}",
            Status = "PENDING",
            SourceType = sourceType,
            SourceId = sourceId,
            SourceBranchId = sourceBranchId,
            SourceAttendanceDate = attendanceDate,
            SourceOvertimeHours = attendance.OvertimeHours,
            SourceBaseSalary = salary.BasicSalary,
            SourceDayType = dayType,
            SourceCurrencyCode = salary.CurrencyCode?.Trim(),
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };
        await _adjustmentRepository.AddAdjustmentAsync(adjustment, cancellationToken);
        await _adjustmentRepository.SaveChangesAsync(cancellationToken);
        return adjustment;
    }
}
