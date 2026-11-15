using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class LeavePayrollLockTests
{
    [Fact]
    public async Task CorrectionDoesNotModifyAttendanceWhenPayrollDateLockRejectsTheChange()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var date = new DateTime(2026, 11, 2);
        repository.Setup(r => r.GetCorrectionRequestSnapshotAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceCorrectionRequest { Id = 1, EmployeeCode = "E1", AttendanceDate = date });
        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync("E1", date, date,
                It.IsAny<Func<Task<AttendanceCorrectionRequest>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Payroll is approved."));
        var service = new AttendanceCorrectionService(repository.Object, Mock.Of<IAttendanceCalculationEngine>(), unitOfWork.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessCorrectionRequestAsync(1, true, "HR"));
        repository.Verify(r => r.GetCorrectionRequestByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessingReadsRequestAgainInsideItsPayrollDateLock()
    {
        var repository = new Mock<ILeaveRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var start = new DateTime(2026, 11, 1);
        var end = new DateTime(2026, 11, 3);
        var locked = false;
        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveRequest { Id = 1, EmployeeCode = "E1", StartDate = start, EndDate = end, Status = "PENDING" });
        repository.Setup(r => r.GetLeaveRequestByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new LeaveRequest { Id = 1, Status = locked ? "APPROVED" : "PENDING" });
        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync("E1", start, end, It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (string employee, DateTime from, DateTime to, Func<Task<LeaveRequestDto>> action, CancellationToken token) =>
            {
                locked = true;
                return await action();
            });
        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>(), unitOfWork.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessLeaveRequestAsync(1, new ProcessLeaveRequestDto(true, null), "HR"));
        Assert.True(locked);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelLeaveRequest_PayrollDateLockRejects_DoesNotCancelOrSave()
    {
        var repository = new Mock<ILeaveRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var start = new DateTime(2026, 11, 1);
        var end = new DateTime(2026, 11, 3);

        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveRequest { Id = 1, EmployeeCode = "E1", StartDate = start, EndDate = end, Status = "PENDING" });

        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync("E1", start, end,
                It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Approved payroll exists for this period."));

        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>(), unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelLeaveRequestAsync("E1", 1, new CancelLeaveRequestDto("Cancel reason"), "HR"));

        repository.Verify(r => r.GetLeaveRequestByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateLeaveRequest_PayrollDateLockRejects_DoesNotCreateOrSave()
    {
        var repository = new Mock<ILeaveRepository>();
        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var start = new DateTime(2026, 11, 5);
        var end = new DateTime(2026, 11, 7);

        employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { EmployeeCode = "E1", IsActive = true, EmploymentStatus = "ACTIVE", HireDate = new DateTime(2020, 1, 1) });
        repository.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeaveType { LeaveTypeCode = "ANNUAL", IsActive = true, TracksBalance = true });

        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync("E1", start, end,
                It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Payroll period is closed."));

        var service = new LeaveService(repository.Object, employeeRepo.Object, unitOfWork.Object);

        var dto = new SubmitLeaveRequestDto("E1", "ANNUAL", start, end, 3m, null, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitLeaveRequestAsync(dto, "HR"));

        repository.Verify(r => r.AddLeaveRequestAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AttendanceCorrection_MissingSnapshot_ThrowsKeyNotFoundBeforePayrollDateLock()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();

        repository.Setup(r => r.GetCorrectionRequestSnapshotAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceCorrectionRequest?)null);

        var service = new AttendanceCorrectionService(repository.Object, Mock.Of<IAttendanceCalculationEngine>(), unitOfWork.Object);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ProcessCorrectionRequestAsync(999, true, "HR"));

        Assert.Contains("999", ex.Message);
        unitOfWork.Verify(u => u.ExecuteForEmployeeDatesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<Func<Task<AttendanceCorrectionRequest>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelLeaveRequest_MissingSnapshot_ThrowsKeyNotFoundBeforePayrollDateLock()
    {
        var repository = new Mock<ILeaveRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();

        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(888, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveRequest?)null);

        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>(), unitOfWork.Object);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CancelLeaveRequestAsync("E1", 888, new CancelLeaveRequestDto("Reason"), "HR"));

        Assert.Contains("888", ex.Message);
        unitOfWork.Verify(u => u.ExecuteForEmployeeDatesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelLeaveRequest_NullDto_ThrowsArgumentNullException()
    {
        var service = new LeaveService(Mock.Of<ILeaveRepository>(), Mock.Of<IEmployeeRepository>());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.CancelLeaveRequestAsync("E1", 1, null!, "HR"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelLeaveRequest_NullOrWhitespaceReason_ThrowsArgumentException(string? reason)
    {
        var service = new LeaveService(Mock.Of<ILeaveRepository>(), Mock.Of<IEmployeeRepository>());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CancelLeaveRequestAsync("E1", 1, new CancelLeaveRequestDto(reason!), "HR"));
        Assert.Contains("cancellation reason between 1 and 500 characters", ex.Message);
    }

    [Fact]
    public async Task CancelLeaveRequest_ReasonExceeds500Chars_ThrowsArgumentException()
    {
        var longReason = new string('X', 501);
        var service = new LeaveService(Mock.Of<ILeaveRepository>(), Mock.Of<IEmployeeRepository>());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CancelLeaveRequestAsync("E1", 1, new CancelLeaveRequestDto(longReason), "HR"));
        Assert.Contains("cancellation reason between 1 and 500 characters", ex.Message);
    }

    [Fact]
    public async Task CancelLeaveRequest_EmployeeMismatch_ThrowsKeyNotFoundException()
    {
        var repository = new Mock<ILeaveRepository>();
        var request = new LeaveRequest { Id = 10, EmployeeCode = "E1", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 2) };
        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var service = new LeaveService(repository.Object, Mock.Of<IEmployeeRepository>());
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CancelLeaveRequestAsync("E2_DIFFERENT", 10, new CancelLeaveRequestDto("Valid Reason"), "HR"));
        Assert.Contains("not found for this employee", ex.Message);
    }

    [Fact]
    public async Task AttendanceCorrection_ProcessRequestAsync_NullDto_ThrowsArgumentNullException()
    {
        var service = new AttendanceCorrectionService(Mock.Of<IAttendanceCorrectionRepository>(), Mock.Of<IAttendanceCalculationEngine>());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.ProcessRequestAsync(1, null!, "HR"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AttendanceCorrection_RejectWithoutReason_ThrowsArgumentException(string? reason)
    {
        var service = new AttendanceCorrectionService(Mock.Of<IAttendanceCorrectionRepository>(), Mock.Of<IAttendanceCalculationEngine>());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ProcessCorrectionRequestAsync(1, false, "HR", reason));
        Assert.Contains("rejection reason up to 1000 characters is required", ex.Message);
    }

    [Fact]
    public async Task AttendanceCorrection_RejectWithReasonTooLong_ThrowsArgumentException()
    {
        var longReason = new string('A', 1001);
        var service = new AttendanceCorrectionService(Mock.Of<IAttendanceCorrectionRepository>(), Mock.Of<IAttendanceCalculationEngine>());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ProcessCorrectionRequestAsync(1, false, "HR", longReason));
        Assert.Contains("rejection reason up to 1000 characters is required", ex.Message);
    }

    [Fact]
    public async Task AttendanceCorrection_RejectDirectlyCallsRepositoryWithoutPayrollLock()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var rejectedRequest = new AttendanceCorrectionRequest
        {
            Id = 5,
            EmployeeCode = "E1",
            Status = "REJECTED",
            RejectionReason = "Not justified",
            AttendanceDate = new DateTime(2026, 11, 2)
        };

        repository.Setup(r => r.RejectCorrectionAsync(5, "Not justified", "HR_MGR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rejectedRequest);

        var service = new AttendanceCorrectionService(repository.Object, Mock.Of<IAttendanceCalculationEngine>(), unitOfWork.Object);
        var result = await service.ProcessCorrectionRequestAsync(5, false, "HR_MGR", "Not justified");

        Assert.Equal("REJECTED", result.Status);
        Assert.Equal("Not justified", result.RejectionReason);
        repository.Verify(r => r.RejectCorrectionAsync(5, "Not justified", "HR_MGR", It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.ExecuteForEmployeeDatesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<Func<Task<AttendanceCorrectionRequest>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AttendanceCorrection_SubmitRequestAsync_NullDto_ThrowsArgumentNullException()
    {
        var service = new AttendanceCorrectionService(Mock.Of<IAttendanceCorrectionRepository>(), Mock.Of<IAttendanceCalculationEngine>());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.SubmitRequestAsync(null!, "USER"));
    }
}
