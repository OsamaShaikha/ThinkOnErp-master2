using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class PayrollCancellationTests
{
    private static PayrollService Service(Mock<IPayrollPeriodRepository> repository)
        => new(repository.Object, Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>());

    [Fact]
    public async Task CancellationRetainsCalculatedAmountsAndSnapshotAndRetryDoesNotSaveAgain()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var snapshot = new PayrollCalculationSnapshot { ExplanationJson = "original calculation" };
        var run = new PayrollRun { Id = 1, Status = "CALCULATED", TotalNetSalary = 500m };
        run.Lines.Add(new PayrollRunLine { NetPay = 500m, Snapshot = snapshot, Status = "CALCULATED" });
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(new PayrollPeriod());
        var service = Service(repository);
        var result = await service.CancelPayrollRunAsync(1, new CancelPayrollRunDto("Incorrect attendance"), "HR");
        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal("HR", result.CancelledBy);
        Assert.Equal("Incorrect attendance", result.CancellationReason);
        Assert.Equal(500m, result.TotalNet);
        Assert.Same(snapshot, run.Lines[0].Snapshot);
        Assert.Equal(500m, run.Lines[0].NetPay);
        await service.CancelPayrollRunAsync(1, new CancelPayrollRunDto("Retry"), "OTHER");
        Assert.Equal("HR", run.CancelledBy);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("APPROVED")]
    [InlineData("POSTED_TO_GL")]
    [InlineData("PAID")]
    public async Task FinancialPayrollCannotBeCancelledByCalculationWorkflow(string status)
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun { Id = 1, Status = status };
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository).CancelPayrollRunAsync(1, new CancelPayrollRunDto("Correction"), "HR"));
        Assert.Equal(status, run.Status);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LinkedSettlementPreventsCancellingEvenIfRunStatusSaysCalculated()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun { Id = 1, Status = "CALCULATED" };
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(new PayrollPeriod());
        repository.Setup(r => r.HasPayrollFinancialLinksAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository).CancelPayrollRunAsync(1, new CancelPayrollRunDto("Correction"), "HR"));
        Assert.Equal("CALCULATED", run.Status);
    }

    [Fact]
    public async Task ActiveCalculatedRunBlocksDuplicateCalculation()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        repository.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(new PayrollPeriod());
        repository.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 1, It.IsAny<CancellationToken>())).ReturnsAsync(new PayrollRun { Id = 12, Status = "CALCULATED" });
        repository.Setup(r => r.GetActiveEmployeesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Employee>());
        var result = await new PayrollValidationService(repository.Object, Mock.Of<IPolicyRepository>()).ValidatePreCalculationAsync(1, "2026-11", 1);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ID: 12"));
    }

    [Fact]
    public async Task CancelledRunCannotProduceBankFile()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new PayrollRun { Status = "CANCELLED" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new BankPayrollExportService(repository.Object).ExportPayrollToBankFormatAsync(1, "CSV"));
    }

    [Fact]
    public async Task CancelPayrollRun_WhenDtoNull_ThrowsArgumentNullException()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var service = Service(repository);
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CancelPayrollRunAsync(1, null!, "USER"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelPayrollRun_WhenReasonNullOrWhitespace_ThrowsArgumentException(string? reason)
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var service = Service(repository);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CancelPayrollRunAsync(1, new CancelPayrollRunDto(reason!), "USER"));
        Assert.Contains("cancellation reason", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenReasonExceeds1000Chars_ThrowsArgumentException()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var service = Service(repository);
        var longReason = new string('A', 1001);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CancelPayrollRunAsync(1, new CancelPayrollRunDto(longReason), "USER"));
        Assert.Contains("at most 1000 characters", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenRunNotFound_ThrowsKeyNotFoundException()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        repository.Setup(r => r.GetPayrollRunByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);
        var service = Service(repository);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CancelPayrollRunAsync(999, new CancelPayrollRunDto("Valid reason"), "USER"));
    }

    [Fact]
    public async Task CancelPayrollRun_WhenPeriodNotFound_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun { Id = 1, Status = "CALCULATED" };
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollPeriod?)null);
        var service = Service(repository);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelPayrollRunAsync(1, new CancelPayrollRunDto("Valid reason"), "USER"));
        Assert.Contains("Payroll period was not found", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenPeriodIsClosed_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun { Id = 1, Status = "CALCULATED" };
        var period = new PayrollPeriod { PeriodCode = "2026-11", Status = "CLOSED" };
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        var service = Service(repository);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelPayrollRunAsync(1, new CancelPayrollRunDto("Valid reason"), "USER"));
        Assert.Contains("Reopen the closed payroll period", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenRunHasJournalVoucherId_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun { Id = 1, Status = "CALCULATED", JournalVoucherId = 555 };
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        var service = Service(repository);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelPayrollRunAsync(1, new CancelPayrollRunDto("Valid reason"), "USER"));
        Assert.Contains("accounting effects can be cancelled", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenRunHasApprovalDate_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun { Id = 1, Status = "CALCULATED", ApprovalDate = DateTime.UtcNow };
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        var service = Service(repository);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelPayrollRunAsync(1, new CancelPayrollRunDto("Valid reason"), "USER"));
        Assert.Contains("accounting effects can be cancelled", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenLineHasProcessedStatus_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun { Id = 1, Status = "CALCULATED" };
        run.Lines.Add(new PayrollRunLine { Id = 10, Status = "APPROVED" });
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        var service = Service(repository);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelPayrollRunAsync(1, new CancelPayrollRunDto("Valid reason"), "USER"));
        Assert.Contains("One or more payroll lines are already processed", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenDraftStatus_CancelsRunAndAllLinesSuccessfully()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun { Id = 2, Status = "DRAFT", TotalNetSalary = 1200m };
        run.Lines.Add(new PayrollRunLine { Id = 101, Status = "DRAFT", NetPay = 600m });
        run.Lines.Add(new PayrollRunLine { Id = 102, Status = "DRAFT", NetPay = 600m });
        var period = new PayrollPeriod { PeriodCode = "2026-11", Status = "OPEN" };
        repository.Setup(r => r.GetPayrollRunByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        repository.Setup(r => r.HasPayrollFinancialLinksAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var service = Service(repository);
        var result = await service.CancelPayrollRunAsync(2, new CancelPayrollRunDto("Draft mistake"), "CANCEL_ACTOR");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal("CANCEL_ACTOR", result.CancelledBy);
        Assert.Equal("Draft mistake", result.CancellationReason);
        Assert.Equal("CANCEL_ACTOR", run.UpdateUser);
        Assert.NotNull(run.CancellationDate);
        Assert.All(run.Lines, line => Assert.Equal("CANCELLED", line.Status));
        repository.Verify(r => r.UpdatePayrollRunAsync(run, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenUnitOfWorkConfigured_ExecutesInsidePeriodLock()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var run = new PayrollRun { Id = 3, Status = "CALCULATED", PayPeriod = "2026-11" };
        var period = new PayrollPeriod { CompanyId = 5, PeriodCode = "2026-11", Status = "OPEN" };
        repository.Setup(r => r.GetPayrollRunSnapshotAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPayrollRunByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        repository.Setup(r => r.HasPayrollFinancialLinksAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var locked = false;
        unitOfWork.Setup(u => u.ExecuteForPayrollPeriodAsync(5, "2026-11", It.IsAny<Func<Task<PayrollCalculationResultDto>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (long cid, string p, Func<Task<PayrollCalculationResultDto>> action, CancellationToken ct) =>
            {
                locked = true;
                return await action();
            });

        var service = new PayrollService(repository.Object, Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>(), unitOfWork.Object);
        var result = await service.CancelPayrollRunAsync(3, new CancelPayrollRunDto("Locked cancellation"), "LOCK_USER");

        Assert.True(locked);
        Assert.Equal("CANCELLED", result.Status);
    }

    [Fact]
    public async Task CancelPayrollRun_TrimsCancellationReasonAndSetsAuditFields()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun { Id = 4, Status = "CALCULATED" };
        var period = new PayrollPeriod { PeriodCode = "2026-11", Status = "OPEN" };
        repository.Setup(r => r.GetPayrollRunByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        repository.Setup(r => r.HasPayrollFinancialLinksAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var service = Service(repository);
        var result = await service.CancelPayrollRunAsync(4, new CancelPayrollRunDto("   Trimmed reason here   "), "TRIM_ACTOR");

        Assert.Equal("Trimmed reason here", result.CancellationReason);
        Assert.Equal("Trimmed reason here", run.CancellationReason);
        Assert.Equal("TRIM_ACTOR", run.CancelledBy);
        Assert.Equal("TRIM_ACTOR", run.UpdateUser);
        Assert.NotNull(run.CancellationDate);
        Assert.Equal(run.CancellationDate, run.UpdateDate);
    }

    [Fact]
    public async Task CancelPayrollRun_PreservesAllCalculatedLineAmounts()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var line = new PayrollRunLine
        {
            Id = 55,
            EmployeeCode = "EMP_PRESERVE",
            BasicSalary = 800m,
            TotalEarnings = 950m,
            GrossSalary = 950m,
            SscEmployeeContrib = 71.25m,
            SscEmployerContrib = 135m,
            IncomeTaxWithheld = 20m,
            OtherDeductions = 10m,
            NetPay = 848.75m,
            Status = "CALCULATED"
        };
        var run = new PayrollRun
        {
            Id = 5,
            Status = "CALCULATED",
            TotalGrossSalary = 950m,
            TotalEmployeeSsc = 71.25m,
            TotalEmployerSsc = 135m,
            TotalIncomeTax = 20m,
            TotalOtherDeductions = 10m,
            TotalNetSalary = 848.75m
        };
        run.Lines.Add(line);
        var period = new PayrollPeriod { PeriodCode = "2026-11", Status = "OPEN" };
        repository.Setup(r => r.GetPayrollRunByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        repository.Setup(r => r.HasPayrollFinancialLinksAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var service = Service(repository);
        var result = await service.CancelPayrollRunAsync(5, new CancelPayrollRunDto("Preserve amounts check"), "AUDITOR");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal("CANCELLED", line.Status);
        Assert.Equal(800m, line.BasicSalary);
        Assert.Equal(950m, line.TotalEarnings);
        Assert.Equal(950m, line.GrossSalary);
        Assert.Equal(71.25m, line.SscEmployeeContrib);
        Assert.Equal(135m, line.SscEmployerContrib);
        Assert.Equal(20m, line.IncomeTaxWithheld);
        Assert.Equal(10m, line.OtherDeductions);
        Assert.Equal(848.75m, line.NetPay);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenHasFinancialLinks_ThrowsSpecificInvalidOperationException()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun { Id = 6, Status = "CALCULATED" };
        var period = new PayrollPeriod { PeriodCode = "2026-11", Status = "OPEN" };
        repository.Setup(r => r.GetPayrollRunByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        repository.Setup(r => r.HasPayrollFinancialLinksAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var service = Service(repository);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelPayrollRunAsync(6, new CancelPayrollRunDto("Dependency check"), "USER"));
        Assert.Contains("linked loan or adjustment settlements", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRun_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun { Id = 7, Status = "CALCULATED" };
        var period = new PayrollPeriod { PeriodCode = "2026-11", Status = "OPEN" };

        repository.Setup(r => r.GetPayrollRunByIdAsync(7, ct)).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, ct)).ReturnsAsync(period);
        repository.Setup(r => r.HasPayrollFinancialLinksAsync(7, ct)).ReturnsAsync(false);

        var service = Service(repository);
        var result = await service.CancelPayrollRunAsync(7, new CancelPayrollRunDto("Cancel with token"), "USER", ct);

        Assert.Equal("CANCELLED", result.Status);
        repository.Verify(r => r.GetPayrollRunByIdAsync(7, ct), Times.Once);
        repository.Verify(r => r.GetPeriodForRunAsync(run, ct), Times.Once);
        repository.Verify(r => r.HasPayrollFinancialLinksAsync(7, ct), Times.Once);
        repository.Verify(r => r.UpdatePayrollRunAsync(run, ct), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(ct), Times.Once);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenMultipleLinesExist_SetsAllLinesToCancelled()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun { Id = 10, Status = "CALCULATED" };
        run.Lines.Add(new PayrollRunLine { Id = 1, EmployeeCode = "E1", Status = "CALCULATED" });
        run.Lines.Add(new PayrollRunLine { Id = 2, EmployeeCode = "E2", Status = "CALCULATED" });
        run.Lines.Add(new PayrollRunLine { Id = 3, EmployeeCode = "E3", Status = "DRAFT" });

        var period = new PayrollPeriod { PeriodCode = "2026-11", Status = "OPEN" };
        repository.Setup(r => r.GetPayrollRunByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        repository.Setup(r => r.HasPayrollFinancialLinksAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var service = Service(repository);
        var result = await service.CancelPayrollRunAsync(10, new CancelPayrollRunDto("Cancel all lines"), "SUPER_USER");

        Assert.Equal("CANCELLED", result.Status);
        Assert.All(run.Lines, line => Assert.Equal("CANCELLED", line.Status));
    }

    [Fact]
    public async Task CancelPayrollRun_WhenReasonExactly1000Chars_Succeeds()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var exact1000 = new string('C', 1000);
        var run = new PayrollRun { Id = 11, Status = "CALCULATED" };
        var period = new PayrollPeriod { PeriodCode = "2026-11", Status = "OPEN" };

        repository.Setup(r => r.GetPayrollRunByIdAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        repository.Setup(r => r.HasPayrollFinancialLinksAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var service = Service(repository);
        var result = await service.CancelPayrollRunAsync(11, new CancelPayrollRunDto(exact1000), "TEST_USER");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal(exact1000, result.CancellationReason);
        Assert.Equal(exact1000, run.CancellationReason);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenAlreadyCancelled_ReturnsExistingResultIdempotentlyWithoutSavingAgain()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun
        {
            Id = 13,
            Status = "CANCELLED",
            CancelledBy = "INITIAL_USER",
            CancellationReason = "Initial cancellation",
            CancellationDate = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc),
            TotalNetSalary = 999m
        };

        repository.Setup(r => r.GetPayrollRunByIdAsync(13, It.IsAny<CancellationToken>())).ReturnsAsync(run);

        var service = Service(repository);
        var result = await service.CancelPayrollRunAsync(13, new CancelPayrollRunDto("Duplicate attempt"), "SECONDARY_USER");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal("INITIAL_USER", result.CancelledBy);
        Assert.Equal("Initial cancellation", result.CancellationReason);
        Assert.Equal(run.CancellationDate, result.CancellationDate);
        repository.Verify(r => r.UpdatePayrollRunAsync(It.IsAny<PayrollRun>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenUnitOfWorkConfigured_SnapshotNotFound_ThrowsKeyNotFoundException()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();

        repository.Setup(r => r.GetPayrollRunSnapshotAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);

        var service = new PayrollService(repository.Object, Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>(), unitOfWork.Object);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CancelPayrollRunAsync(999, new CancelPayrollRunDto("Valid reason"), "USER"));
    }

    [Fact]
    public async Task CancelPayrollRun_WhenUnitOfWorkConfigured_PeriodNotFound_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var run = new PayrollRun { Id = 14, Status = "CALCULATED" };

        repository.Setup(r => r.GetPayrollRunSnapshotAsync(14, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollPeriod?)null);

        var service = new PayrollService(repository.Object, Mock.Of<IPayrollCalculationEngine>(), Mock.Of<IPayrollValidationService>(), unitOfWork.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelPayrollRunAsync(14, new CancelPayrollRunDto("Valid reason"), "USER"));

        Assert.Contains("Payroll period was not found", ex.Message);
    }

    [Theory]
    [InlineData("SUPERSEDED")]
    [InlineData("POSTED")]
    public async Task CancelPayrollRun_WhenRunStatusNotDraftOrCalculated_ThrowsInvalidOperationException(string terminalStatus)
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun { Id = 15, Status = terminalStatus };
        repository.Setup(r => r.GetPayrollRunByIdAsync(15, It.IsAny<CancellationToken>())).ReturnsAsync(run);

        var service = Service(repository);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelPayrollRunAsync(15, new CancelPayrollRunDto("Valid reason"), "USER"));

        Assert.Contains("without approval or accounting effects can be cancelled", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenHasCorrectionRunMetadata_PreservesCorrectionIdentifiersInDto()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun
        {
            Id = 16,
            Status = "CALCULATED",
            CorrectionOfRunId = 10,
            CorrectionRootRunId = 5,
            CorrectionVersion = 2,
            CorrectionReason = "Prior calculation error"
        };
        var period = new PayrollPeriod { PeriodCode = "2026-11", Status = "OPEN" };

        repository.Setup(r => r.GetPayrollRunByIdAsync(16, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        repository.Setup(r => r.HasPayrollFinancialLinksAsync(16, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var service = Service(repository);
        var result = await service.CancelPayrollRunAsync(16, new CancelPayrollRunDto("Cancel correction version"), "CORRECTOR");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal(10, result.CorrectionOfRunId);
        Assert.Equal(5, result.CorrectionRootRunId);
        Assert.Equal(2, result.CorrectionVersion);
        Assert.Equal("Prior calculation error", result.CorrectionReason);
    }

    [Fact]
    public async Task CancelPayrollRun_MapsPayslipsAccuratelyInResultDto()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var emp = new Employee { EmployeeCode = "EMP_MAP", NameLocal = "خالد المحمد" };
        var line = new PayrollRunLine
        {
            Id = 701,
            EmployeeCode = "EMP_MAP",
            Employee = emp,
            BasicSalary = 1000m,
            GrossSalary = 1200m,
            TotalEarnings = 200m,
            SscEmployeeContrib = 90m,
            IncomeTaxWithheld = 30m,
            OtherDeductions = 10m,
            TotalDeductions = 130m,
            NetPay = 1070m,
            PaymentMethod = "BANK_TRANSFER",
            Status = "CALCULATED"
        };
        var run = new PayrollRun
        {
            Id = 17,
            Status = "CALCULATED",
            PayPeriod = "2026-11",
            TotalGrossSalary = 1200m,
            TotalNetSalary = 1070m,
            TotalEmployeeSsc = 90m,
            TotalIncomeTax = 30m,
            TotalOtherDeductions = 10m
        };
        run.Lines.Add(line);
        var period = new PayrollPeriod { PeriodCode = "2026-11", Status = "OPEN" };

        repository.Setup(r => r.GetPayrollRunByIdAsync(17, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        repository.Setup(r => r.HasPayrollFinancialLinksAsync(17, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var service = Service(repository);
        var result = await service.CancelPayrollRunAsync(17, new CancelPayrollRunDto("Cancel with payslip verify"), "USER");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Single(result.Payslips);
        var slip = result.Payslips[0];
        Assert.Equal(701, slip.RunLineId);
        Assert.Equal("EMP_MAP", slip.EmployeeCode);
        Assert.Equal("خالد المحمد", slip.EmployeeName);
        Assert.Equal(1000m, slip.BasicSalary);
        Assert.Equal(1200m, slip.GrossSalary);
        Assert.Equal(200m, slip.TotalEarnings);
        Assert.Equal(90m, slip.SscEmployeeContrib);
        Assert.Equal(30m, slip.IncomeTaxWithheld);
        Assert.Equal(10m, slip.OtherDeductions);
        Assert.Equal(130m, slip.TotalDeductions);
        Assert.Equal(1070m, slip.NetPay);
        Assert.Equal("BANK_TRANSFER", slip.PaymentMethod);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenNoLinesExist_CancelsRunCleanly()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun
        {
            Id = 18,
            Status = "CALCULATED",
            TotalGrossSalary = 0m,
            TotalNetSalary = 0m
        };
        var period = new PayrollPeriod { PeriodCode = "2026-11", Status = "OPEN" };

        repository.Setup(r => r.GetPayrollRunByIdAsync(18, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        repository.Setup(r => r.HasPayrollFinancialLinksAsync(18, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var service = Service(repository);
        var result = await service.CancelPayrollRunAsync(18, new CancelPayrollRunDto("Empty run cancellation"), "CLEANER");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Empty(result.Payslips);
        Assert.Equal(0, result.EmployeeCount);
        Assert.Equal(0m, result.TotalGross);
        Assert.Equal(0m, result.TotalNet);
    }

    [Fact]
    public async Task CancelPayrollRun_WhenUpdateThrows_PropagatesExceptionWithoutSaving()
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        var run = new PayrollRun { Id = 19, Status = "CALCULATED" };
        var period = new PayrollPeriod { PeriodCode = "2026-11", Status = "OPEN" };

        repository.Setup(r => r.GetPayrollRunByIdAsync(19, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        repository.Setup(r => r.HasPayrollFinancialLinksAsync(19, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(r => r.UpdatePayrollRunAsync(run, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB update conflict"));

        var service = Service(repository);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelPayrollRunAsync(19, new CancelPayrollRunDto("Conflict test"), "USER"));

        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
