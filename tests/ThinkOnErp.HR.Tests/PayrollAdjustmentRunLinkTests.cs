using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class PayrollAdjustmentRunLinkTests
{
    [Fact]
    public async Task CalculatingPayrollPersistsRunThenLinksApprovedAdjustmentsToItsEmployeeLines()
    {
        var period = new PayrollPeriod { CompanyId = 21, PeriodCode = "2026-09", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30) };
        var run = new PayrollRun { Id = 81, PayPeriod = period.PeriodCode, Status = "CALCULATED" };
        run.Lines.Add(new PayrollRunLine { Id = 1901, EmployeeCode = "EMP-1" });
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(r => r.GetPeriodByCodeAsync(period.CompanyId, period.PeriodCode, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        periods.Setup(r => r.GetActiveEmployeesAsync(33, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { new Employee { EmployeeCode = "EMP-1" } });
        periods.Setup(r => r.AddPayrollRunAsync(run, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        periods.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        periods.Setup(r => r.LinkApprovedAdjustmentsToRunAsync(period.CompanyId, run, "REVIEWER", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var engine = new Mock<IPayrollCalculationEngine>();
        engine.Setup(e => e.CalculatePayrollForPeriodAsync(period, It.IsAny<IReadOnlyList<Employee>>(), 33, "REVIEWER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePreCalculationAsync(period.CompanyId, period.PeriodCode, 33, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new List<string>(), new List<string>()));
        var service = new PayrollService(periods.Object, engine.Object, validation.Object);

        await service.CreateAndCalculatePayrollRunAsync(new CreatePayrollRunDto(period.CompanyId, period.PeriodCode, 33), "REVIEWER");

        periods.Verify(r => r.AddPayrollRunAsync(run, It.IsAny<CancellationToken>()), Times.Once);
        periods.Verify(r => r.LinkApprovedAdjustmentsToRunAsync(period.CompanyId, run, "REVIEWER", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAndCalculatePayrollRun_WhenDtoNull_ThrowsArgumentNullException()
    {
        var service = new PayrollService(Mock.Of<IPayrollPeriodRepository>(), Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateAndCalculatePayrollRunAsync(null!, "USER"));
    }

    [Fact]
    public async Task CreateAndCalculatePayrollRun_WhenBranchIdNull_ThrowsArgumentException()
    {
        var service = new PayrollService(Mock.Of<IPayrollPeriodRepository>(), Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>());
        var dto = new CreatePayrollRunDto(1, "2026-11", null);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAndCalculatePayrollRunAsync(dto, "USER"));
        Assert.Contains("separate payroll run for each branch", ex.Message);
    }

    [Fact]
    public async Task CreateAndCalculatePayrollRun_WhenPeriodNotFound_ThrowsInvalidOperationException()
    {
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync((PayrollPeriod?)null);
        var service = new PayrollService(periods.Object, Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>());
        var dto = new CreatePayrollRunDto(1, "2026-11", 5);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAndCalculatePayrollRunAsync(dto, "USER"));
        Assert.Contains("Period '2026-11' not found", ex.Message);
    }

    [Fact]
    public async Task CreateAndCalculatePayrollRun_WhenPreCalculationValidationFails_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "2026-11" };
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePreCalculationAsync(1, "2026-11", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(false, new List<string> { "Missing salary structure for employee E1" }, new List<string>()));
        var service = new PayrollService(periods.Object, Mock.Of<IPayrollCalculationEngine>(), validation.Object);
        var dto = new CreatePayrollRunDto(1, "2026-11", 5);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAndCalculatePayrollRunAsync(dto, "USER"));
        Assert.Contains("Payroll validation failed", ex.Message);
        Assert.Contains("Missing salary structure for employee E1", ex.Message);
    }

    [Fact]
    public async Task CreateAndCalculatePayrollRun_WhenInputsChangeDuringCalculation_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "2026-11" };
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        periods.Setup(r => r.GetActiveEmployeesAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee>());
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePreCalculationAsync(1, "2026-11", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new List<string>(), new List<string>()));
        var engine = new Mock<IPayrollCalculationEngine>();
        engine.Setup(e => e.CalculatePayrollForPeriodAsync(period, It.IsAny<IReadOnlyList<Employee>>(), 5, "USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollRun { Status = "CALCULATED" });

        var snapshots = new Mock<IPayrollInputSnapshotService>();
        var captureCount = 0;
        snapshots.Setup(s => s.CaptureAsync(period, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => captureCount++ == 0 ? "FINGERPRINT_BEFORE" : "FINGERPRINT_AFTER");

        var service = new PayrollService(periods.Object, engine.Object, validation.Object, inputSnapshots: snapshots.Object);
        var dto = new CreatePayrollRunDto(1, "2026-11", 5);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAndCalculatePayrollRunAsync(dto, "USER"));
        Assert.Contains("Payroll inputs changed during calculation", ex.Message);
    }

    [Fact]
    public async Task CreateAndCalculatePayrollRun_WhenUnitOfWorkConfigured_ExecutesInsidePeriodLock()
    {
        var period = new PayrollPeriod { CompanyId = 10, PeriodCode = "2026-11" };
        var run = new PayrollRun { Id = 50, Status = "CALCULATED" };
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(r => r.GetPeriodByCodeAsync(10, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        periods.Setup(r => r.GetActiveEmployeesAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee>());
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePreCalculationAsync(10, "2026-11", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new List<string>(), new List<string>()));
        var engine = new Mock<IPayrollCalculationEngine>();
        engine.Setup(e => e.CalculatePayrollForPeriodAsync(period, It.IsAny<IReadOnlyList<Employee>>(), 2, "LOCK_USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var locked = false;
        var unitOfWork = new Mock<IHrUnitOfWork>();
        unitOfWork.Setup(u => u.ExecuteForPayrollPeriodAsync(10, "2026-11", It.IsAny<Func<Task<PayrollCalculationResultDto>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (long cid, string p, Func<Task<PayrollCalculationResultDto>> action, CancellationToken ct) =>
            {
                locked = true;
                return await action();
            });

        var service = new PayrollService(periods.Object, engine.Object, validation.Object, unitOfWork: unitOfWork.Object);
        var result = await service.CreateAndCalculatePayrollRunAsync(new CreatePayrollRunDto(10, "2026-11", 2), "LOCK_USER");

        Assert.True(locked);
        Assert.Equal("CALCULATED", result.Status);
        periods.Verify(r => r.AddPayrollRunAsync(run, It.IsAny<CancellationToken>()), Times.Once);
        periods.Verify(r => r.LinkApprovedAdjustmentsToRunAsync(10, run, "LOCK_USER", It.IsAny<CancellationToken>()), Times.Once);
        periods.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateAndCalculatePayrollRun_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var period = new PayrollPeriod { CompanyId = 5, PeriodCode = "2026-11" };
        var run = new PayrollRun { Id = 101, Status = "CALCULATED" };
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(r => r.GetPeriodByCodeAsync(5, "2026-11", token)).ReturnsAsync(period);
        periods.Setup(r => r.GetActiveEmployeesAsync(1, token)).ReturnsAsync(new List<Employee>());
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePreCalculationAsync(5, "2026-11", 1, token))
            .ReturnsAsync(new PayrollValidationResultDto(true, new List<string>(), new List<string>()));
        var engine = new Mock<IPayrollCalculationEngine>();
        engine.Setup(e => e.CalculatePayrollForPeriodAsync(period, It.IsAny<IReadOnlyList<Employee>>(), 1, "USER", token))
            .ReturnsAsync(run);

        var service = new PayrollService(periods.Object, engine.Object, validation.Object);
        var dto = new CreatePayrollRunDto(5, "2026-11", 1);
        await service.CreateAndCalculatePayrollRunAsync(dto, "USER", token);

        periods.Verify(r => r.GetPeriodByCodeAsync(5, "2026-11", token), Times.Once);
        periods.Verify(r => r.GetActiveEmployeesAsync(1, token), Times.Once);
        validation.Verify(v => v.ValidatePreCalculationAsync(5, "2026-11", 1, token), Times.Once);
        engine.Verify(e => e.CalculatePayrollForPeriodAsync(period, It.IsAny<IReadOnlyList<Employee>>(), 1, "USER", token), Times.Once);
        periods.Verify(r => r.AddPayrollRunAsync(run, token), Times.Once);
        periods.Verify(r => r.LinkApprovedAdjustmentsToRunAsync(5, run, "USER", token), Times.Once);
        periods.Verify(r => r.SaveChangesAsync(token), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateAndCalculatePayrollRun_WhenSnapshotServiceMatches_Succeeds()
    {
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "2026-11" };
        var run = new PayrollRun { Id = 20, Status = "CALCULATED" };
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        periods.Setup(r => r.GetActiveEmployeesAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee>());
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePreCalculationAsync(1, "2026-11", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new List<string>(), new List<string>()));
        var engine = new Mock<IPayrollCalculationEngine>();
        engine.Setup(e => e.CalculatePayrollForPeriodAsync(period, It.IsAny<IReadOnlyList<Employee>>(), 3, "USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var snapshots = new Mock<IPayrollInputSnapshotService>();
        snapshots.Setup(s => s.CaptureAsync(period, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync("IDENTICAL_FINGERPRINT");

        var service = new PayrollService(periods.Object, engine.Object, validation.Object, inputSnapshots: snapshots.Object);
        var result = await service.CreateAndCalculatePayrollRunAsync(new CreatePayrollRunDto(1, "2026-11", 3), "USER");

        Assert.Equal("CALCULATED", result.Status);
        snapshots.Verify(s => s.CaptureAsync(period, 3, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateAndCalculatePayrollRun_WhenValidationHasWarningsOnly_Succeeds()
    {
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "2026-11" };
        var run = new PayrollRun { Id = 30, Status = "CALCULATED" };
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        periods.Setup(r => r.GetActiveEmployeesAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee>());
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePreCalculationAsync(1, "2026-11", 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new List<string>(), new List<string> { "Warning: Tax policy near expiration" }));
        var engine = new Mock<IPayrollCalculationEngine>();
        engine.Setup(e => e.CalculatePayrollForPeriodAsync(period, It.IsAny<IReadOnlyList<Employee>>(), 4, "USER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var service = new PayrollService(periods.Object, engine.Object, validation.Object);
        var result = await service.CreateAndCalculatePayrollRunAsync(new CreatePayrollRunDto(1, "2026-11", 4), "USER");

        Assert.NotNull(result);
        Assert.Equal("CALCULATED", result.Status);
    }

    [Fact]
    public async Task GetPayrollRunById_WhenNotFound_ReturnsNull()
    {
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(r => r.GetPayrollRunByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);
        var service = new PayrollService(periods.Object, Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>());

        var result = await service.GetPayrollRunByIdAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPayrollRunById_WhenFound_ReturnsMappedDto()
    {
        var run = new PayrollRun { Id = 42, PayPeriod = "2026-11", Status = "CALCULATED", TotalGrossSalary = 5000m, TotalNetSalary = 4200m };
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(r => r.GetPayrollRunByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        var service = new PayrollService(periods.Object, Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>());

        var result = await service.GetPayrollRunByIdAsync(42);
        Assert.NotNull(result);
        Assert.Equal(42, result!.PayrollRunId);
        Assert.Equal("2026-11", result.PayPeriod);
        Assert.Equal("CALCULATED", result.Status);
        Assert.Equal(5000m, result.TotalGross);
        Assert.Equal(4200m, result.TotalNet);
    }

    [Fact]
    public async Task CancelPayrollRun_NullDto_ThrowsArgumentNullException()
    {
        var service = new PayrollService(Mock.Of<IPayrollPeriodRepository>(), Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CancelPayrollRunAsync(1, null!, "USER"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelPayrollRun_NullOrWhitespaceReason_ThrowsArgumentException(string? reason)
    {
        var service = new PayrollService(Mock.Of<IPayrollPeriodRepository>(), Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CancelPayrollRunAsync(1, new CancelPayrollRunDto(reason!), "USER"));
        Assert.Contains("cancellation reason of at most 1000 characters is required", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRun_ReasonTooLong_ThrowsArgumentException()
    {
        var longReason = new string('C', 1001);
        var service = new PayrollService(Mock.Of<IPayrollPeriodRepository>(), Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>());
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CancelPayrollRunAsync(1, new CancelPayrollRunDto(longReason), "USER"));
        Assert.Contains("cancellation reason of at most 1000 characters is required", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenRunNotFound_ThrowsKeyNotFoundException()
    {
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(r => r.GetPayrollRunSnapshotAsync(555, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var service = new PayrollService(periods.Object, Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>(), unitOfWork: unitOfWork.Object);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CancelPayrollRunAsync(555, new CancelPayrollRunDto("Mistake in calculation"), "USER"));
        Assert.Contains("555", ex.Message);
    }

    [Fact]
    public async Task ApprovePayrollRun_WhenRunNotFound_ThrowsKeyNotFoundException()
    {
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(r => r.GetPayrollRunSnapshotAsync(777, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var service = new PayrollService(periods.Object, Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>(), unitOfWork: unitOfWork.Object);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ApprovePayrollRunAsync(777, "APPROVER"));
        Assert.Contains("777", ex.Message);
    }

    [Fact]
    public async Task ApprovePayrollRun_WhenPeriodNotFound_ThrowsInvalidOperationException()
    {
        var run = new PayrollRun { Id = 888, PayPeriod = "2026-11", Status = "CALCULATED" };
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(r => r.GetPayrollRunSnapshotAsync(888, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        periods.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollPeriod?)null);
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var service = new PayrollService(periods.Object, Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>(), unitOfWork: unitOfWork.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApprovePayrollRunAsync(888, "APPROVER"));
        Assert.Contains("Payroll period was not found", ex.Message);
    }

    [Fact]
    public async Task GetPeriodsAsync_PropagatesCancellationTokenAndParameters()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var periods = new Mock<IPayrollPeriodRepository>();
        periods.Setup(r => r.GetPeriodsAsync(10L, 2026, token))
            .ReturnsAsync(new List<PayrollPeriod> { new() { Id = 1, PeriodCode = "2026-01" } });

        var service = new PayrollService(periods.Object, Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>());
        var list = await service.GetPeriodsAsync(10L, 2026, token);

        Assert.Single(list);
        periods.Verify(r => r.GetPeriodsAsync(10L, 2026, token), Times.Once);
    }
}
