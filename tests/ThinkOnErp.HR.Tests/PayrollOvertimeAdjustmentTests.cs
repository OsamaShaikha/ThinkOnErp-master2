using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class PayrollOvertimeAdjustmentTests
{
    private static readonly DateTime AttendanceDate = new(2026, 11, 5);
    private static CreateOvertimePayrollAdjustmentDto Dto(long branchId = 7) => new(3, "E1", AttendanceDate, "2026-11", "OT_ALLOWANCE", branchId);

    [Fact]
    public async Task CreateOvertimeAdjustment_UsesAttendanceSalaryAndConfiguredComponent_AndLeavesPending()
    {
        var adjustments = new Mock<IPayrollAdjustmentRepository>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var employees = new Mock<IEmployeeRepository>();
        var periods = new Mock<IPayrollPeriodRepository>();
        var overtime = new Mock<IOvertimeCalculationService>();
        PayrollAdjustment? saved = null;
        attendance.Setup(r => r.GetAttendanceDayAsync("E1", AttendanceDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceDay { Id = 55, EmployeeCode = "E1", AttendanceDate = AttendanceDate, Status = "PRESENT", OvertimeHours = 2m, CheckInBranchId = 7 });
        attendance.Setup(r => r.GetCorrectionRequestsAsync("E1", "PENDING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AttendanceCorrectionRequest>());
        employees.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "E1", IsActive = true, EmploymentStatus = "ACTIVE", BranchId = 7 });
        periods.Setup(r => r.GetPeriodByCodeAsync(3, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollPeriod { CompanyId = 3, PeriodCode = "2026-11", StartDate = new(2026, 11, 1), EndDate = new(2026, 11, 30), Status = "OPEN" });
        employees.Setup(r => r.GetSalaryComponentByCodeAsync("OT_ALLOWANCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryComponent { ComponentCode = "OT_ALLOWANCE", ComponentType = "ALLOWANCE", IsActive = true });
        employees.Setup(r => r.GetActiveSalaryStructureAsync("E1", AttendanceDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryStructure { EmployeeCode = "E1", BasicSalary = 2400m, CurrencyCode = "JOD", IsActive = true });
        adjustments.Setup(r => r.GetAdjustmentBySourceAsync(3, "ATTENDANCE_OVERTIME", "55", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollAdjustment?)null);
        adjustments.Setup(r => r.HasCalculatedPayrollAsync("E1", "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        overtime.Setup(s => s.CalculateOvertimeEarningsAsync(3, 7, "JOD", 2400m, 2m, "REGULAR", AttendanceDate, It.IsAny<CancellationToken>())).ReturnsAsync(25m);
        adjustments.Setup(r => r.AddAdjustmentAsync(It.IsAny<PayrollAdjustment>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollAdjustment, CancellationToken>((value, _) => saved = value).Returns(Task.CompletedTask);
        adjustments.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await new PayrollAdjustmentService(adjustments.Object, attendanceRepository: attendance.Object,
            employeeRepository: employees.Object, payrollPeriodRepository: periods.Object, overtimeCalculationService: overtime.Object)
            .CreateOvertimeAdjustmentAsync(Dto(), "HR_USER");

        Assert.Same(saved, result);
        Assert.Equal("PENDING", result.Status);
        Assert.Equal("EARNING", result.AdjustmentType);
        Assert.Equal(25m, result.Amount);
        Assert.Equal("ATTENDANCE_OVERTIME", result.SourceType);
        Assert.Equal("55", result.SourceId);
        Assert.Equal(7, result.SourceBranchId);
        Assert.Equal(2m, result.SourceOvertimeHours);
        Assert.Equal(2400m, result.SourceBaseSalary);
        Assert.Equal("REGULAR", result.SourceDayType);
        Assert.Equal("HR_USER", result.CreationUser);
        adjustments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOvertimeAdjustment_ReplayReturnsExistingWithoutDuplicate()
    {
        var adjustments = new Mock<IPayrollAdjustmentRepository>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var employees = new Mock<IEmployeeRepository>();
        var periods = new Mock<IPayrollPeriodRepository>();
        var overtime = new Mock<IOvertimeCalculationService>();
        var existing = new PayrollAdjustment { Id = 99, CompanyId = 3, EmployeeCode = "E1", PayPeriod = "2026-11", ComponentCode = "OT_ALLOWANCE", SourceBranchId = 7, SourceType = "ATTENDANCE_OVERTIME", SourceId = "55", SourceAttendanceDate = AttendanceDate, SourceOvertimeHours = 1m, SourceBaseSalary = 2400m, SourceDayType = "REGULAR", SourceCurrencyCode = "JOD", Amount = 12.5m, Status = "APPROVED" };
        attendance.Setup(r => r.GetAttendanceDayAsync("E1", AttendanceDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceDay { Id = 55, EmployeeCode = "E1", AttendanceDate = AttendanceDate, Status = "PRESENT", OvertimeHours = 1m, CheckInBranchId = 7 });
        attendance.Setup(r => r.GetCorrectionRequestsAsync("E1", "PENDING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AttendanceCorrectionRequest>());
        employees.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "E1", IsActive = true, EmploymentStatus = "ACTIVE", BranchId = 7 });
        periods.Setup(r => r.GetPeriodByCodeAsync(3, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollPeriod { CompanyId = 3, PeriodCode = "2026-11", StartDate = new(2026, 11, 1), EndDate = new(2026, 11, 30), Status = "OPEN" });
        employees.Setup(r => r.GetSalaryComponentByCodeAsync("OT_ALLOWANCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryComponent { ComponentCode = "OT_ALLOWANCE", ComponentType = "ALLOWANCE", IsActive = true });
        employees.Setup(r => r.GetActiveSalaryStructureAsync("E1", AttendanceDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryStructure { EmployeeCode = "E1", BasicSalary = 2400m, CurrencyCode = "JOD", IsActive = true });
        overtime.Setup(s => s.CalculateOvertimeEarningsAsync(3, 7, "JOD", 2400m, 1m, "REGULAR", AttendanceDate, It.IsAny<CancellationToken>())).ReturnsAsync(12.5m);
        adjustments.Setup(r => r.GetAdjustmentBySourceAsync(3, "ATTENDANCE_OVERTIME", "55", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await new PayrollAdjustmentService(adjustments.Object, attendanceRepository: attendance.Object, employeeRepository: employees.Object,
            payrollPeriodRepository: periods.Object, overtimeCalculationService: overtime.Object)
            .CreateOvertimeAdjustmentAsync(Dto(), "HR_USER");

        Assert.Same(existing, result);
        adjustments.Verify(r => r.AddAdjustmentAsync(It.IsAny<PayrollAdjustment>(), It.IsAny<CancellationToken>()), Times.Never);
        adjustments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOvertimeAdjustment_RejectsBranchSpoofingBeforeWriting()
    {
        var adjustments = new Mock<IPayrollAdjustmentRepository>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var employees = new Mock<IEmployeeRepository>();
        var periods = new Mock<IPayrollPeriodRepository>();
        var overtime = new Mock<IOvertimeCalculationService>();
        attendance.Setup(r => r.GetAttendanceDayAsync("E1", AttendanceDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceDay { Id = 55, EmployeeCode = "E1", AttendanceDate = AttendanceDate, Status = "PRESENT", OvertimeHours = 1m, CheckInBranchId = 7 });
        employees.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "E1", IsActive = true, EmploymentStatus = "ACTIVE", BranchId = 7 });
        var service = new PayrollAdjustmentService(adjustments.Object, attendanceRepository: attendance.Object, employeeRepository: employees.Object,
            payrollPeriodRepository: periods.Object, overtimeCalculationService: overtime.Object);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateOvertimeAdjustmentAsync(Dto(8), "HR_USER"));

        Assert.Contains("branch recorded", error.Message);
        adjustments.Verify(r => r.AddAdjustmentAsync(It.IsAny<PayrollAdjustment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOvertimeAdjustment_RejectsMissingPunchWithoutCreatingPayableItem()
    {
        var adjustments = new Mock<IPayrollAdjustmentRepository>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var employees = new Mock<IEmployeeRepository>();
        var periods = new Mock<IPayrollPeriodRepository>();
        var overtime = new Mock<IOvertimeCalculationService>();
        attendance.Setup(r => r.GetAttendanceDayAsync("E1", AttendanceDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceDay { Id = 55, EmployeeCode = "E1", AttendanceDate = AttendanceDate, Status = "PRESENT", OvertimeHours = 1m, HasMissingPunch = true });
        employees.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "E1", IsActive = true, EmploymentStatus = "ACTIVE", BranchId = 7 });
        var service = new PayrollAdjustmentService(adjustments.Object, attendanceRepository: attendance.Object, employeeRepository: employees.Object,
            payrollPeriodRepository: periods.Object, overtimeCalculationService: overtime.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateOvertimeAdjustmentAsync(Dto(), "HR_USER"));

        adjustments.Verify(r => r.AddAdjustmentAsync(It.IsAny<PayrollAdjustment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOvertimeAdjustment_RejectsPendingAttendanceCorrection()
    {
        var adjustments = new Mock<IPayrollAdjustmentRepository>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var employees = new Mock<IEmployeeRepository>();
        var periods = new Mock<IPayrollPeriodRepository>();
        var overtime = new Mock<IOvertimeCalculationService>();
        attendance.Setup(r => r.GetAttendanceDayAsync("E1", AttendanceDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceDay { Id = 55, EmployeeCode = "E1", AttendanceDate = AttendanceDate, Status = "PRESENT", OvertimeHours = 1m, CheckInBranchId = 7 });
        attendance.Setup(r => r.GetCorrectionRequestsAsync("E1", "PENDING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new AttendanceCorrectionRequest { EmployeeCode = "E1", AttendanceDate = AttendanceDate } });
        employees.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "E1", IsActive = true, EmploymentStatus = "ACTIVE", BranchId = 7 });
        var service = new PayrollAdjustmentService(adjustments.Object, attendanceRepository: attendance.Object, employeeRepository: employees.Object,
            payrollPeriodRepository: periods.Object, overtimeCalculationService: overtime.Object);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateOvertimeAdjustmentAsync(Dto(), "HR_USER"));

        Assert.Contains("correction request is pending", error.Message);
        adjustments.Verify(r => r.AddAdjustmentAsync(It.IsAny<PayrollAdjustment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveOvertimeAdjustment_RevalidatesSourceAndPolicyAndSavesDecision()
    {
        var setup = CreateApprovalFixture(new AttendanceDay
        {
            Id = 55, EmployeeCode = "E1", AttendanceDate = AttendanceDate, Status = "PRESENT", OvertimeHours = 2m, CheckInBranchId = 7
        });

        var approved = await setup.Service.DecideAdjustmentAsync(70, true, "Reviewed against attendance", "APPROVER", 7);

        Assert.Equal("APPROVED", approved.Status);
        Assert.Equal("Reviewed against attendance", approved.DecisionReason);
        setup.Adjustments.Verify(r => r.UpdateAdjustmentAsync(It.Is<PayrollAdjustment>(a => a.Status == "APPROVED"), It.IsAny<CancellationToken>()), Times.Once);
        setup.Overtime.Verify(s => s.CalculateOvertimeEarningsAsync(3, 7, "JOD", 2400m, 2m, "REGULAR", AttendanceDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveOvertimeAdjustment_RejectsChangedAttendanceBeforeSaving()
    {
        var setup = CreateApprovalFixture(new AttendanceDay
        {
            Id = 55, EmployeeCode = "E1", AttendanceDate = AttendanceDate, Status = "PRESENT", OvertimeHours = 3m, CheckInBranchId = 7
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.DecideAdjustmentAsync(70, true, "Reviewed", "APPROVER", 7));

        Assert.Contains("source changed", error.Message);
        setup.Adjustments.Verify(r => r.UpdateAdjustmentAsync(It.IsAny<PayrollAdjustment>(), It.IsAny<CancellationToken>()), Times.Never);
        setup.Adjustments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveOvertimeAdjustment_RejectsMissingOrWrongBranchAndLegacyBypass()
    {
        var setup = CreateApprovalFixture(new AttendanceDay
        {
            Id = 55, EmployeeCode = "E1", AttendanceDate = AttendanceDate, Status = "PRESENT", OvertimeHours = 2m, CheckInBranchId = 7
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => setup.Service.DecideAdjustmentAsync(70, true, "Reviewed", "APPROVER"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => setup.Service.DecideAdjustmentAsync(70, true, "Reviewed", "APPROVER", 8));
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.ApproveAdjustmentAsync(70, "LEGACY_CALLER"));
        setup.Adjustments.Verify(r => r.UpdateAdjustmentAsync(It.IsAny<PayrollAdjustment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnpostOvertimeAdjustment_RequiresBranchAndReasonAndRecordsThem()
    {
        var setup = CreateApprovalFixture(new AttendanceDay
        {
            Id = 55, EmployeeCode = "E1", AttendanceDate = AttendanceDate, Status = "PRESENT", OvertimeHours = 2m, CheckInBranchId = 7
        });
        setup.Candidate.Status = "APPROVED";

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => setup.Service.UnpostAdjustmentAsync(70, "HR"));
        var unposted = await setup.Service.UnpostAdjustmentAsync(70, "HR", 7, "Reopened for attendance review");

        Assert.Equal("PENDING", unposted.Status);
        Assert.Equal("Reopened for attendance review", unposted.DecisionReason);
        Assert.Equal("HR", unposted.UpdateUser);
    }

    private static (PayrollAdjustmentService Service, Mock<IPayrollAdjustmentRepository> Adjustments, Mock<IOvertimeCalculationService> Overtime, PayrollAdjustment Candidate)
        CreateApprovalFixture(AttendanceDay currentAttendance)
    {
        var adjustments = new Mock<IPayrollAdjustmentRepository>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var employees = new Mock<IEmployeeRepository>();
        var periods = new Mock<IPayrollPeriodRepository>();
        var overtime = new Mock<IOvertimeCalculationService>();
        var candidate = new PayrollAdjustment
        {
            Id = 70, CompanyId = 3, EmployeeCode = "E1", PayPeriod = "2026-11", ComponentCode = "OT_ALLOWANCE",
            AdjustmentType = "EARNING", Amount = 25m, Status = "PENDING", SourceType = "ATTENDANCE_OVERTIME", SourceId = "55",
            SourceBranchId = 7, SourceAttendanceDate = AttendanceDate, SourceOvertimeHours = 2m, SourceBaseSalary = 2400m,
            SourceDayType = "REGULAR", SourceCurrencyCode = "JOD"
        };
        adjustments.Setup(r => r.GetAdjustmentByIdAsync(70, It.IsAny<CancellationToken>())).ReturnsAsync(candidate);
        adjustments.Setup(r => r.HasCalculatedPayrollAsync("E1", "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        adjustments.Setup(r => r.UpdateAdjustmentAsync(It.IsAny<PayrollAdjustment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        adjustments.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        attendance.Setup(r => r.GetAttendanceDayAsync("E1", AttendanceDate, It.IsAny<CancellationToken>())).ReturnsAsync(currentAttendance);
        attendance.Setup(r => r.GetCorrectionRequestsAsync("E1", "PENDING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AttendanceCorrectionRequest>());
        employees.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "E1", IsActive = true, EmploymentStatus = "ACTIVE", BranchId = 7 });
        employees.Setup(r => r.GetSalaryComponentByCodeAsync("OT_ALLOWANCE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryComponent { ComponentCode = "OT_ALLOWANCE", ComponentType = "ALLOWANCE", IsActive = true });
        employees.Setup(r => r.GetActiveSalaryStructureAsync("E1", AttendanceDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryStructure { EmployeeCode = "E1", BasicSalary = 2400m, IsActive = true });
        periods.Setup(r => r.GetPeriodByCodeAsync(3, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollPeriod { CompanyId = 3, PeriodCode = "2026-11", StartDate = new(2026, 11, 1), EndDate = new(2026, 11, 30), Status = "OPEN" });
        overtime.Setup(s => s.CalculateOvertimeEarningsAsync(3, 7, "JOD", 2400m, 2m, "REGULAR", AttendanceDate, It.IsAny<CancellationToken>())).ReturnsAsync(25m);
        return (new PayrollAdjustmentService(adjustments.Object, attendanceRepository: attendance.Object, employeeRepository: employees.Object,
            payrollPeriodRepository: periods.Object, overtimeCalculationService: overtime.Object), adjustments, overtime, candidate);
    }
}
