using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class PayrollInputWriterLockTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MobileEvidenceSurvivesRejectedFinancialProcessing(bool checkIn)
    {
        var attendance = new Mock<IAttendanceCorrectionRepository>(); var employees = new Mock<IEmployeeRepository>();
        var branches = new Mock<IBranchRepository>(); var geo = new Mock<IGeoLocationService>(); var processing = new Mock<IRawAttendanceProcessingService>();
        employees.Setup(e => e.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(new Employee { EmployeeCode = "E1", BranchId = 1 });
        branches.Setup(b => b.GetByIdAsync(1)).ReturnsAsync(new ThinkOnErp.Domain.Entities.SysBranch { Id = 1, CompanyId = 1, IsActive = true });
        geo.Setup(g => g.CheckProximity(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<ThinkOnErp.Domain.Entities.SysBranch>(), It.IsAny<decimal?>()))
            .Returns(new GeofenceCheckResult { IsWithinRange = true, Status = "INSIDE" });
        RawAttendance? captured = null;
        attendance.Setup(r => r.AcceptMobileEvidenceAsync(It.IsAny<RawAttendance>(), It.IsAny<Func<long, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RawAttendance raw, Func<long, string> receipt, CancellationToken ct) => { raw.Id = 321; raw.EventResponseJson = receipt(321); captured = raw; return raw; });
        processing.Setup(p => p.ProcessAsync(321, "MOBILE", It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Payroll is approved."));
        var service = new HrAttendanceService(attendance.Object, employees.Object, branches.Object, Mock.Of<IPolicyRepository>(), geo.Object,
            Mock.Of<ILogger<HrAttendanceService>>(), processing: processing.Object);
        var result = checkIn ? await service.CheckInAsync(new("E1", 0m, 0m, EventId: Guid.NewGuid().ToString()), "MOBILE")
            : await service.CheckOutAsync(new("E1", 0m, 0m, EventId: Guid.NewGuid().ToString()), "MOBILE");
        Assert.True(result.Success); Assert.True(result.EvidenceOnly); Assert.Equal(321, result.RawEventId);
        Assert.NotNull(captured); Assert.False(captured!.IsProcessed); Assert.NotNull(captured.EventResponseJson);
        processing.Verify(p => p.ProcessAsync(321, "MOBILE", It.IsAny<CancellationToken>()), Times.Once);
        attendance.Verify(r => r.RecordRawAttemptAsync(321, "MOBILE", "Payroll is approved.", It.IsAny<CancellationToken>()), Times.Once);
        attendance.Verify(r => r.AddAttendanceDayAsync(It.IsAny<AttendanceDay>(), It.IsAny<CancellationToken>()), Times.Never);
        attendance.Verify(r => r.UpdateAttendanceDayAsync(It.IsAny<AttendanceDay>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SalaryAssignmentProtectsLaterPeriodsAndDoesNotTruncateOldSalaryWhenRejected()
    {
        var employees = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var start = new DateTime(2026, 11, 1);
        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync("E1", start, DateTime.MaxValue.Date,
                It.IsAny<Func<Task<SalaryStructureDetailsDto>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Payroll is approved."));
        var service = new EmployeeService(employees.Object, unitOfWork.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignSalaryStructureAsync("E1",
            new AssignSalaryStructureDto(start, start.AddDays(5), 500m, "JOD", "BANK_TRANSFER", null), "HR"));
        unitOfWork.Verify(u => u.ExecuteForEmployeeDatesAsync("E1", start, DateTime.MaxValue.Date,
            It.IsAny<Func<Task<SalaryStructureDetailsDto>>>(), It.IsAny<CancellationToken>()), Times.Once);
        employees.Verify(r => r.GetSalaryStructuresAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        employees.Verify(r => r.AddSalaryStructureAsync(It.IsAny<SalaryStructure>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(-0.01)]
    public async Task SalaryAssignment_WhenNegativeBasicSalary_ThrowsArgumentExceptionBeforeCallingUnitOfWork(decimal negativeSalary)
    {
        var employees = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var service = new EmployeeService(employees.Object, unitOfWork.Object);

        var dto = new AssignSalaryStructureDto(new DateTime(2026, 11, 1), null, negativeSalary, "JOD", "BANK_TRANSFER", null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.AssignSalaryStructureAsync("E1", dto, "HR"));
        unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SalaryAssignment_WhenEffectiveToPrecedesEffectiveFrom_ThrowsArgumentExceptionBeforeCallingUnitOfWork()
    {
        var employees = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var service = new EmployeeService(employees.Object, unitOfWork.Object);

        var start = new DateTime(2026, 11, 10);
        var invertedEnd = new DateTime(2026, 11, 5);
        var dto = new AssignSalaryStructureDto(start, invertedEnd, 1000m, "JOD", "BANK_TRANSFER", null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.AssignSalaryStructureAsync("E1", dto, "HR"));
        unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SalaryAssignment_WhenDtoIsNull_ThrowsArgumentNullException()
    {
        var service = new EmployeeService(Mock.Of<IEmployeeRepository>(), Mock.Of<IHrUnitOfWork>());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.AssignSalaryStructureAsync("E1", null!, "HR"));
    }

    [Fact]
    public async Task SalaryAssignment_FutureStructureExists_ThrowsInvalidOperationException()
    {
        var employees = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var emp = new Employee { EmployeeCode = "E1", BranchId = 1 };
        employees.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(emp);

        var existingFuture = new SalaryStructure
        {
            Id = 2,
            EmployeeCode = "E1",
            EffectiveFrom = new DateTime(2026, 12, 1),
            BasicSalary = 1000m,
            IsActive = true
        };
        employees.Setup(r => r.GetSalaryStructuresAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existingFuture });

        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync("E1", It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Func<Task<SalaryStructureDetailsDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, DateTime _, DateTime _, Func<Task<SalaryStructureDetailsDto>> action, CancellationToken _) => action());

        var service = new EmployeeService(employees.Object, unitOfWork.Object);
        var dto = new AssignSalaryStructureDto(new DateTime(2026, 11, 1), null, 800m, "JOD", "BANK_TRANSFER", null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignSalaryStructureAsync("E1", dto, "HR"));

        Assert.Contains("starts on or after this date", ex.Message);
    }

    [Fact]
    public async Task SalaryAssignment_PrecedingStructure_TruncatedToDayBeforeNewEffectiveFrom()
    {
        var employees = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var emp = new Employee { EmployeeCode = "E1", BranchId = 1 };
        employees.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(emp);

        var oldStructure = new SalaryStructure
        {
            Id = 1,
            EmployeeCode = "E1",
            EffectiveFrom = new DateTime(2024, 1, 1),
            EffectiveTo = null,
            BasicSalary = 500m,
            CurrencyCode = "JOD",
            PaymentMethod = "BANK_TRANSFER",
            IsActive = true
        };
        employees.Setup(r => r.GetSalaryStructuresAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { oldStructure });

        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync("E1", It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<Func<Task<SalaryStructureDetailsDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, DateTime _, DateTime _, Func<Task<SalaryStructureDetailsDto>> action, CancellationToken _) => action());

        var service = new EmployeeService(employees.Object, unitOfWork.Object);
        var newStart = new DateTime(2026, 11, 1);
        var dto = new AssignSalaryStructureDto(newStart, null, 750m, "JOD", "BANK_TRANSFER", null);

        var result = await service.AssignSalaryStructureAsync("E1", dto, "HR_USER");

        Assert.Equal(750m, result.BasicSalary);
        Assert.Equal(newStart, result.EffectiveFrom);
        Assert.Equal(new DateTime(2026, 10, 31), oldStructure.EffectiveTo); // Truncated to day before
        employees.Verify(r => r.AddSalaryStructureAsync(It.IsAny<SalaryStructure>(), It.IsAny<CancellationToken>()), Times.Once);
        employees.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
