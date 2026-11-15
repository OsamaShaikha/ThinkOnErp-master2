using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class PayrollCorrectionWorkflowTests
{
    [Theory]
    [InlineData("RETURNED")]
    [InlineData("WITHDRAWN")]
    [InlineData("REVERSED")]
    public async Task PayrollCorrectionCreatesNextVersionAndSupersedesPriorRunsWhenHandoffIsReturnedWithdrawnOrReversed(string handoffStatus)
    {
        var harness = new CorrectionHarness(handoffStatus: handoffStatus);
        var service = harness.CreateService();

        var result = await service.CreatePayrollCorrectionAsync(10, "Missing shift differential adjustment", "hr-admin");

        Assert.Equal(11, result.PayrollRunId);
        Assert.Equal("2026-11", result.PayPeriod);
        Assert.Equal(10, result.CorrectionOfRunId);
        Assert.Equal(10, result.CorrectionRootRunId);
        Assert.Equal(2, result.CorrectionVersion);
        Assert.Equal("Missing shift differential adjustment", result.CorrectionReason);
        Assert.Equal("CALCULATED", result.Status);

        harness.PayrollPeriodRepo.Verify(r => r.SupersedePayrollCorrectionFamilyAsync(10, "hr-admin", It.IsAny<CancellationToken>()), Times.Once);
        harness.PayrollPeriodRepo.Verify(r => r.AddPayrollRunAsync(It.Is<PayrollRun>(run =>
            run.CorrectionOfRunId == 10 &&
            run.CorrectionRootRunId == 10 &&
            run.CorrectionVersion == 2 &&
            run.CorrectionReason == "Missing shift differential adjustment"), It.IsAny<CancellationToken>()), Times.Once);
        harness.PayrollPeriodRepo.Verify(r => r.LinkApprovedAdjustmentsToRunAsync(3, It.IsAny<PayrollRun>(), "hr-admin", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("SUBMITTED")]
    [InlineData("ACCEPTED")]
    [InlineData("POSTED")]
    public async Task PayrollCorrectionFailsIfHandoffIsNotWithdrawnReturnedOrReversed(string activeStatus)
    {
        var harness = new CorrectionHarness(handoffStatus: activeStatus);
        var service = harness.CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePayrollCorrectionAsync(10, "Correction attempt", "hr-admin"));

        Assert.Contains("Accounting must return or withdraw the unposted handoff, or reverse its posted journal", ex.Message);
    }

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("CALCULATED")]
    [InlineData("CANCELLED")]
    public async Task PayrollCorrectionFailsIfRunIsNotApproved(string nonApprovedStatus)
    {
        var harness = new CorrectionHarness(handoffStatus: "RETURNED");
        harness.OriginalRun.Status = nonApprovedStatus;
        if (nonApprovedStatus != "CANCELLED")
            harness.OriginalRun.ApprovalDate = null;
        var service = harness.CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePayrollCorrectionAsync(10, "Correction attempt", "hr-admin"));

        Assert.Contains("Only an approved payroll run can be corrected", ex.Message);
    }

    [Fact]
    public async Task PayrollCorrectionFailsIfPeriodIsClosed()
    {
        var harness = new CorrectionHarness(handoffStatus: "RETURNED", periodStatus: "CLOSED");
        var service = harness.CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePayrollCorrectionAsync(10, "Correction attempt", "hr-admin"));

        Assert.Contains("Reopen the payroll period through the authorized period workflow", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    public async Task PayrollCorrectionFailsIfReasonIsInvalid(string? invalidReason)
    {
        var harness = new CorrectionHarness(handoffStatus: "RETURNED");
        var service = harness.CreateService();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreatePayrollCorrectionAsync(10, invalidReason!, "hr-admin"));

        Assert.Contains("A payroll correction reason of 3..1000 characters is required", ex.Message);
    }

    [Fact]
    public async Task PayrollCorrectionFailsIfNotLatestVersion()
    {
        var harness = new CorrectionHarness(handoffStatus: "RETURNED");
        // Latest version in the family is already version 2 with Id 20
        harness.LatestCorrection = new PayrollRun
        {
            Id = 20,
            CorrectionRootRunId = 10,
            CorrectionVersion = 2,
            Status = "APPROVED",
            ApprovalDate = DateTime.UtcNow
        };
        var service = harness.CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePayrollCorrectionAsync(10, "Correction of old run", "hr-admin"));

        Assert.Contains("Correct the latest payroll version only", ex.Message);
    }

    [Fact]
    public async Task MultiGenerationalCorrectionPreservesRootRunIdAndIncrementsVersion()
    {
        var harness = new CorrectionHarness(handoffStatus: "REVERSED");
        var version2Run = new PayrollRun
        {
            Id = 20,
            PayPeriod = "2026-11",
            BranchId = 2,
            Status = "APPROVED",
            ApprovalDate = new DateTime(2026, 11, 10),
            CorrectionVersion = 2,
            CorrectionRootRunId = 10,
            CorrectionOfRunId = 10,
            TotalGrossSalary = 1100m,
            TotalNetSalary = 880m
        };
        version2Run.Lines.Add(new PayrollRunLine
        {
            Id = 201,
            EmployeeCode = "EMP-1",
            BasicSalary = 1100m,
            GrossSalary = 1100m,
            NetPay = 880m
        });

        harness.LatestCorrection = version2Run;
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunSnapshotAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version2Run);
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunByIdAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version2Run);
        harness.AccountingHandoffs.Setup(h => h.GetPayrollHandoffStatusAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollHandoffStatusDto(
                21, "HR-PAY-3-20-2", 1, "REVERSED", "2026-11",
                new DateTime(2026, 11, 10), new DateTime(2026, 11, 10), DateTime.UtcNow, "Second correction needed"));

        var service = harness.CreateService();

        var result = await service.CreatePayrollCorrectionAsync(20, "Second round retro adjustment", "hr-admin");

        Assert.Equal(10, result.CorrectionRootRunId);
        Assert.Equal(20, result.CorrectionOfRunId);
        Assert.Equal(3, result.CorrectionVersion);
        Assert.Equal("CALCULATED", result.Status);

        harness.PayrollPeriodRepo.Verify(r => r.SupersedePayrollCorrectionFamilyAsync(10, "hr-admin", It.IsAny<CancellationToken>()), Times.Once);
        harness.PayrollPeriodRepo.Verify(r => r.AddPayrollRunAsync(It.Is<PayrollRun>(run =>
            run.CorrectionRootRunId == 10 &&
            run.CorrectionOfRunId == 20 &&
            run.CorrectionVersion == 3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CorrectionAllowedWhenLatestDraftWasCancelled()
    {
        var harness = new CorrectionHarness(handoffStatus: "RETURNED");
        // Latest correction in the family was cancelled draft v2 of Run 10
        var cancelledDraft = new PayrollRun
        {
            Id = 11,
            PayPeriod = "2026-11",
            BranchId = 2,
            Status = "CANCELLED",
            CorrectionVersion = 2,
            CorrectionRootRunId = 10,
            CorrectionOfRunId = 10,
            CancellationDate = DateTime.UtcNow,
            CancellationReason = "Draft calculation discarded"
        };
        harness.LatestCorrection = cancelledDraft;

        var service = harness.CreateService();

        var result = await service.CreatePayrollCorrectionAsync(10, "Re-attempting correction after draft cancellation", "hr-admin");

        Assert.Equal(10, result.CorrectionRootRunId);
        Assert.Equal(10, result.CorrectionOfRunId);
        Assert.Equal(3, result.CorrectionVersion);
        Assert.Equal("CALCULATED", result.Status);
    }

    [Fact]
    public async Task PayrollCorrectionFailsIfAccountingHandoffIsMissing()
    {
        var harness = new CorrectionHarness();
        harness.AccountingHandoffs.Setup(h => h.GetPayrollHandoffStatusAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollHandoffStatusDto?)null);
        var service = harness.CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePayrollCorrectionAsync(10, "Correction attempt", "hr-admin"));

        Assert.Contains("Payroll correction requires a saved Accounting handoff", ex.Message);
    }

    [Fact]
    public async Task PayrollCorrectionFailsIfHandoffPeriodDoesNotMatchRunPeriod()
    {
        var harness = new CorrectionHarness();
        harness.AccountingHandoffs.Setup(h => h.GetPayrollHandoffStatusAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollHandoffStatusDto(
                11, "HR-PAY-3-10-1", 1, "RETURNED", "2026-12",
                new DateTime(2026, 11, 5), new DateTime(2026, 11, 5), DateTime.UtcNow, null));
        var service = harness.CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePayrollCorrectionAsync(10, "Correction attempt", "hr-admin"));

        Assert.Contains("Accounting handoff period does not match the source payroll run", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelPayrollRunRejectsInvalidReason(string? invalidReason)
    {
        var harness = new CorrectionHarness();
        var service = harness.CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CancelPayrollRunAsync(10, new CancelPayrollRunDto(invalidReason!), "hr-admin"));
    }

    [Fact]
    public async Task CancelPayrollRunFailsIfRunIsApprovedOrHasJournal()
    {
        var harness = new CorrectionHarness();
        var service = harness.CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelPayrollRunAsync(10, new CancelPayrollRunDto("Cancelled due to re-work"), "hr-admin"));

        Assert.Contains("Only draft or calculated payroll without approval or accounting effects can be cancelled", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRunFailsIfLinesAreAlreadyProcessed()
    {
        var harness = new CorrectionHarness();
        var calculatedRun = new PayrollRun
        {
            Id = 50,
            PayPeriod = "2026-11",
            Status = "CALCULATED",
            ApprovalDate = null
        };
        calculatedRun.Lines.Add(new PayrollRunLine { Id = 501, Status = "PAID" });
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunSnapshotAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calculatedRun);
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calculatedRun);
        var service = harness.CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelPayrollRunAsync(50, new CancelPayrollRunDto("Cancelled"), "hr-admin"));

        Assert.Contains("One or more payroll lines are already processed", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRunFailsIfPeriodIsClosed()
    {
        var harness = new CorrectionHarness();
        var closedPeriod = new PayrollPeriod { Id = 100, CompanyId = 3, PeriodCode = "2026-11", Status = "CLOSED" };
        var calculatedRun = new PayrollRun
        {
            Id = 50,
            PayPeriod = "2026-11",
            Status = "CALCULATED",
            ApprovalDate = null
        };
        calculatedRun.Lines.Add(new PayrollRunLine { Id = 501, Status = "CALCULATED" });
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunSnapshotAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calculatedRun);
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calculatedRun);
        harness.PayrollPeriodRepo.Setup(r => r.GetPeriodForRunAsync(calculatedRun, It.IsAny<CancellationToken>()))
            .ReturnsAsync(closedPeriod);
        var service = harness.CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelPayrollRunAsync(50, new CancelPayrollRunDto("Cancelled"), "hr-admin"));

        Assert.Contains("Reopen the closed payroll period before cancelling this calculation", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRunFailsIfFinancialLinksExist()
    {
        var harness = new CorrectionHarness();
        var calculatedRun = new PayrollRun
        {
            Id = 50,
            PayPeriod = "2026-11",
            Status = "CALCULATED",
            ApprovalDate = null
        };
        calculatedRun.Lines.Add(new PayrollRunLine { Id = 501, Status = "CALCULATED" });
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunSnapshotAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calculatedRun);
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calculatedRun);
        harness.PayrollPeriodRepo.Setup(r => r.HasPayrollFinancialLinksAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = harness.CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelPayrollRunAsync(50, new CancelPayrollRunDto("Cancelled"), "hr-admin"));

        Assert.Contains("This run has linked loan or adjustment settlements", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRunSucceedsAndTransitionsRunAndLinesToCancelled()
    {
        var harness = new CorrectionHarness();
        var calculatedRun = new PayrollRun
        {
            Id = 50,
            PayPeriod = "2026-11",
            Status = "CALCULATED",
            ApprovalDate = null
        };
        calculatedRun.Lines.Add(new PayrollRunLine { Id = 501, Status = "CALCULATED" });
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunSnapshotAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calculatedRun);
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calculatedRun);
        harness.PayrollPeriodRepo.Setup(r => r.HasPayrollFinancialLinksAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = harness.CreateService();

        var result = await service.CancelPayrollRunAsync(50, new CancelPayrollRunDto("Calculation had errors"), "hr-admin");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal("CANCELLED", calculatedRun.Status);
        Assert.Equal("hr-admin", calculatedRun.CancelledBy);
        Assert.Equal("Calculation had errors", calculatedRun.CancellationReason);
        Assert.All(calculatedRun.Lines, l => Assert.Equal("CANCELLED", l.Status));
        harness.PayrollPeriodRepo.Verify(r => r.UpdatePayrollRunAsync(calculatedRun, It.IsAny<CancellationToken>()), Times.Once);
        harness.PayrollPeriodRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelPayrollRunIsIdempotentWhenAlreadyCancelled()
    {
        var harness = new CorrectionHarness();
        var alreadyCancelled = new PayrollRun
        {
            Id = 50,
            PayPeriod = "2026-11",
            Status = "CANCELLED",
            CancelledBy = "prior-admin",
            CancellationReason = "Prior reason"
        };
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunSnapshotAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alreadyCancelled);
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alreadyCancelled);
        var service = harness.CreateService();

        var result = await service.CancelPayrollRunAsync(50, new CancelPayrollRunDto("Repeat cancel"), "hr-admin");

        Assert.Equal("CANCELLED", result.Status);
        harness.PayrollPeriodRepo.Verify(r => r.UpdatePayrollRunAsync(It.IsAny<PayrollRun>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePayrollCorrection_ThrowsKeyNotFoundException_WhenRunNotFound()
    {
        var harness = new CorrectionHarness();
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunSnapshotAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);
        var service = harness.CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreatePayrollCorrectionAsync(999, "Correction attempt", "hr-admin"));
    }

    [Fact]
    public async Task CreatePayrollCorrection_ThrowsInvalidOperationException_WhenPeriodNotFound()
    {
        var harness = new CorrectionHarness();
        harness.PayrollPeriodRepo.Setup(r => r.GetPeriodForRunAsync(It.IsAny<PayrollRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollPeriod?)null);
        var service = harness.CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePayrollCorrectionAsync(10, "Correction attempt", "hr-admin"));

        Assert.Contains("Payroll period was not found", ex.Message);
    }

    [Fact]
    public async Task CreatePayrollCorrection_ThrowsInvalidOperationException_WhenPreCalculationValidationFails()
    {
        var harness = new CorrectionHarness();
        harness.ValidationService.Setup(v => v.ValidatePreCalculationAsync(3, "2026-11", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(false, new List<string> { "Salary structure missing for employee" }, new()));
        var service = harness.CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePayrollCorrectionAsync(10, "Correction attempt", "hr-admin"));

        Assert.Contains("Payroll validation failed", ex.Message);
        Assert.Contains("Salary structure missing", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRun_ThrowsKeyNotFoundException_WhenRunNotFound()
    {
        var harness = new CorrectionHarness();
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunSnapshotAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);
        var service = harness.CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CancelPayrollRunAsync(999, new CancelPayrollRunDto("Cancelled"), "hr-admin"));
    }

    [Fact]
    public async Task CancelPayrollRun_ThrowsInvalidOperationException_WhenPeriodNotFound()
    {
        var harness = new CorrectionHarness();
        var calculatedRun = new PayrollRun { Id = 50, PayPeriod = "2026-11", Status = "CALCULATED" };
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunSnapshotAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calculatedRun);
        harness.PayrollPeriodRepo.Setup(r => r.GetPayrollRunByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(calculatedRun);
        harness.PayrollPeriodRepo.Setup(r => r.GetPeriodForRunAsync(calculatedRun, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollPeriod?)null);
        var service = harness.CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelPayrollRunAsync(50, new CancelPayrollRunDto("Cancelled"), "hr-admin"));

        Assert.Contains("Payroll period was not found", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRun_ThrowsArgumentNullException_WhenDtoIsNull()
    {
        var harness = new CorrectionHarness();
        var service = harness.CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.CancelPayrollRunAsync(50, null!, "hr-admin"));
    }

    [Fact]
    public async Task CancelPayrollRun_ThrowsArgumentException_WhenReasonExceeds1000Chars()
    {
        var harness = new CorrectionHarness();
        var service = harness.CreateService();

        var veryLongReason = new string('C', 1001);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CancelPayrollRunAsync(50, new CancelPayrollRunDto(veryLongReason), "hr-admin"));

        Assert.Contains("A cancellation reason of at most 1000 characters is required", ex.Message);
    }

    private sealed class CorrectionHarness
    {
        public Mock<IPayrollPeriodRepository> PayrollPeriodRepo { get; } = new();
        public Mock<IPayrollCalculationEngine> CalculationEngine { get; } = new();
        public Mock<IPayrollValidationService> ValidationService { get; } = new();
        public Mock<IHrUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IHrAccountingHandoffService> AccountingHandoffs { get; } = new();

        public PayrollRun OriginalRun { get; }
        public PayrollPeriod Period { get; }
        public PayrollRun? LatestCorrection { get; set; }

        public CorrectionHarness(string handoffStatus = "RETURNED", string periodStatus = "OPEN")
        {
            Period = new PayrollPeriod
            {
                Id = 100,
                CompanyId = 3,
                PeriodCode = "2026-11",
                Status = periodStatus,
                StartDate = new DateTime(2026, 11, 1),
                EndDate = new DateTime(2026, 11, 30)
            };

            OriginalRun = new PayrollRun
            {
                Id = 10,
                PayPeriod = "2026-11",
                BranchId = 2,
                Status = "APPROVED",
                ApprovalDate = new DateTime(2026, 11, 5),
                CorrectionVersion = 1,
                CorrectionRootRunId = null,
                CorrectionOfRunId = null,
                TotalGrossSalary = 1000m,
                TotalNetSalary = 800m
            };
            OriginalRun.Lines.Add(new PayrollRunLine
            {
                Id = 101,
                EmployeeCode = "EMP-1",
                BasicSalary = 1000m,
                GrossSalary = 1000m,
                NetPay = 800m
            });

            LatestCorrection = OriginalRun;

            UnitOfWork.Setup(u => u.ExecuteForPayrollPeriodAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<Func<Task<PayrollCalculationResultDto>>>(),
                It.IsAny<CancellationToken>()))
                .Returns<long, string, Func<Task<PayrollCalculationResultDto>>, CancellationToken>((_, _, action, _) => action());

            PayrollPeriodRepo.Setup(r => r.GetPayrollRunSnapshotAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(OriginalRun);
            PayrollPeriodRepo.Setup(r => r.GetPayrollRunByIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(OriginalRun);
            PayrollPeriodRepo.Setup(r => r.GetPeriodForRunAsync(It.IsAny<PayrollRun>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Period);
            PayrollPeriodRepo.Setup(r => r.GetPeriodByCodeAsync(3, "2026-11", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Period);
            PayrollPeriodRepo.Setup(r => r.GetLatestPayrollCorrectionAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => LatestCorrection);
            PayrollPeriodRepo.Setup(r => r.GetActiveEmployeesAsync(2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Employee>());

            ValidationService.Setup(v => v.ValidatePreCalculationAsync(3, "2026-11", 2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PayrollValidationResultDto(true, new(), new()));

            var calculatedRun = new PayrollRun
            {
                Id = 11,
                PayPeriod = "2026-11",
                BranchId = 2,
                Status = "CALCULATED",
                TotalGrossSalary = 1100m,
                TotalNetSalary = 880m
            };
            calculatedRun.Lines.Add(new PayrollRunLine
            {
                Id = 102,
                EmployeeCode = "EMP-1",
                BasicSalary = 1100m,
                GrossSalary = 1100m,
                NetPay = 880m
            });

            CalculationEngine.Setup(c => c.CalculatePayrollForPeriodAsync(
                Period,
                It.IsAny<IReadOnlyList<Employee>>(),
                2,
                "hr-admin",
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(calculatedRun);

            AccountingHandoffs.Setup(h => h.GetPayrollHandoffStatusAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PayrollHandoffStatusDto(
                    11, "HR-PAY-3-10-1", 1, handoffStatus, "2026-11",
                    new DateTime(2026, 11, 5), new DateTime(2026, 11, 5), DateTime.UtcNow, "Review reason"));
        }

        public PayrollService CreateService() => new(
            PayrollPeriodRepo.Object,
            CalculationEngine.Object,
            ValidationService.Object,
            unitOfWork: UnitOfWork.Object,
            inputSnapshots: null,
            accountingHandoffs: AccountingHandoffs.Object);
    }
}
