using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

// Regression tests for the defects identified in the initial HR audit.
public sealed class HrAuditReproductionTests
{
    private static Mock<ILoanRepository> CreateLoanRepositoryMock()
    {
        var repository = new Mock<ILoanRepository>();
        repository.Setup(r => r.GetCurrencyRulesForEmployeeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((3, (int?)null));
        return repository;
    }

    [Fact]
    public async Task PartialRepaymentDeductsOnlyRemainingAmount()
    {
        var repository = CreateLoanRepositoryMock();
        var loan = new EmployeeLoan { EmployeeCode = "E1", Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 60, TotalPaidAmount = 40 };
        var schedule = new LoanRepaymentSchedule { EmployeeLoan = loan, ScheduledAmount = 100, PaidAmount = 40, Status = "PARTIAL" };
        repository.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-10", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { schedule });
        repository.Setup(r => r.GetAdvancesAsync("E1", "2026-10", It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<EmployeeAdvance>());
        var result = await new LoanDeductionService(repository.Object).CalculateAndApplyDeductionsAsync(1, "E1", "2026-10", 1000, null, false, default, "JOD");
        Assert.Equal(60m, result.TotalScheduledDeduction);
        Assert.Equal(60m, result.TotalActualDeduction);
        Assert.Equal(100m, Assert.Single(result.UpdatedSchedules).PaidAmount);
        Assert.Equal(40m, schedule.PaidAmount);
        Assert.Equal(0m, Assert.Single(result.UpdatedSchedules).EmployeeLoan!.RemainingBalance);
        Assert.Equal(60m, loan.RemainingBalance);
        Assert.Equal("PAID", Assert.Single(result.UpdatedSchedules).Status);
        Assert.Equal("PARTIAL", schedule.Status);
    }

    [Fact]
    public async Task LoanPreviewPreservesTrackedLoanAndSchedule()
    {
        var repository = CreateLoanRepositoryMock();
        var loan = new EmployeeLoan { EmployeeCode = "E1", Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 500 };
        var schedule = new LoanRepaymentSchedule { EmployeeLoan = loan, ScheduledAmount = 100, Status = "PENDING" };
        repository.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-10", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { schedule });
        repository.Setup(r => r.GetAdvancesAsync("E1", "2026-10", It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<EmployeeAdvance>());
        await new LoanDeductionService(repository.Object).CalculateAndApplyDeductionsAsync(
            null, "E1", "2026-10", 1000, null, persistChanges: false, payrollCurrencyCode: "JOD");
        Assert.Equal(500m, loan.RemainingBalance);
        Assert.Equal("PENDING", schedule.Status);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdvanceDeductionPrioritizedOverLoanAndRecoversAdvance()
    {
        var repository = CreateLoanRepositoryMock();
        var advance = new EmployeeAdvance
        {
            Id = 1,
            EmployeeCode = "E1",
            Status = "ACTIVE",
            CurrencyCode = "JOD",
            AdvanceAmount = 100m,
            RemainingBalance = 40m,
            DeductedAmount = 60m
        };
        var loan = new EmployeeLoan
        {
            Id = 2,
            EmployeeCode = "E1",
            Status = "ACTIVE",
            CurrencyCode = "JOD",
            RemainingBalance = 200m,
            TotalPaidAmount = 50m
        };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 10,
            EmployeeLoanId = 2,
            EmployeeLoan = loan,
            ScheduledAmount = 80m,
            PaidAmount = 0m,
            Status = "PENDING",
            InstallmentNo = 1,
            PayPeriod = "2026-10"
        };

        repository.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { schedule });
        repository.Setup(r => r.GetAdvancesAsync("E1", "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { advance });

        // Net pay cap is 100 JOD (via policy min net pay guarantee or direct net pay)
        var policy = new DeductionPolicy
        {
            MaxDeductionPercentage = 100m,
            MinNetPayGuarantee = 0m,
            AllowNegativeNetPay = false
        };

        var service = new LoanDeductionService(repository.Object);
        var result = await service.CalculateAndApplyDeductionsAsync(1, "E1", "2026-10", 100m, policy, persistChanges: false, payrollCurrencyCode: "JOD");

        // Advance (40) is deducted first and recovered; remaining cap (60) goes to schedule (80 - 60 = 20 left)
        Assert.Equal(120m, result.TotalScheduledDeduction); // 40 advance + 80 schedule
        Assert.Equal(100m, result.TotalActualDeduction);    // 40 advance + 60 partial loan
        Assert.Single(result.UpdatedAdvances);
        Assert.Equal(0m, result.UpdatedAdvances[0].RemainingBalance);
        Assert.Equal(100m, result.UpdatedAdvances[0].DeductedAmount);
        Assert.Equal("RECOVERED", result.UpdatedAdvances[0].Status);

        Assert.Single(result.UpdatedSchedules);
        Assert.Equal(60m, result.UpdatedSchedules[0].PaidAmount);
        Assert.Equal("PARTIAL", result.UpdatedSchedules[0].Status);
        Assert.Equal(140m, result.UpdatedSchedules[0].EmployeeLoan!.RemainingBalance);

        // Untracked original objects preserved
        Assert.Equal(40m, advance.RemainingBalance);
        Assert.Equal("ACTIVE", advance.Status);
        Assert.Equal(200m, loan.RemainingBalance);
        Assert.Equal("PENDING", schedule.Status);
    }

    [Fact]
    public async Task AdvancePreviewPreservesTrackedAdvance()
    {
        var repository = CreateLoanRepositoryMock();
        var advance = new EmployeeAdvance
        {
            Id = 5,
            EmployeeCode = "E1",
            Status = "ACTIVE",
            CurrencyCode = "JOD",
            RemainingBalance = 150m,
            AdvanceAmount = 150m
        };
        repository.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LoanRepaymentSchedule>());
        repository.Setup(r => r.GetAdvancesAsync("E1", "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { advance });

        var service = new LoanDeductionService(repository.Object);
        var result = await service.CalculateAndApplyDeductionsAsync(null, "E1", "2026-10", 200m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(150m, result.TotalActualDeduction);
        Assert.Equal(150m, advance.RemainingBalance);
        Assert.Equal("ACTIVE", advance.Status);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoanDeductionThrowsIfPersistChangesTrueWithoutPostingReceipt()
    {
        var repository = CreateLoanRepositoryMock();
        var service = new LoanDeductionService(repository.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CalculateAndApplyDeductionsAsync(1, "E1", "2026-10", 1000m, null, persistChanges: true));
        Assert.Contains("requires a confirmed Accounting posting receipt", ex.Message);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InactiveOrSuspendedLoanIsExcludedFromPayrollDeductions()
    {
        var repository = CreateLoanRepositoryMock();
        var pendingLoan = new EmployeeLoan { EmployeeCode = "E1", Status = "PENDING", RemainingBalance = 100m };
        var suspendedLoan = new EmployeeLoan { EmployeeCode = "E1", Status = "SUSPENDED", RemainingBalance = 100m };
        var completedLoan = new EmployeeLoan { EmployeeCode = "E1", Status = "COMPLETED", RemainingBalance = 0m };

        var sched1 = new LoanRepaymentSchedule { EmployeeLoan = pendingLoan, ScheduledAmount = 50m, Status = "PENDING" };
        var sched2 = new LoanRepaymentSchedule { EmployeeLoan = suspendedLoan, ScheduledAmount = 50m, Status = "PENDING" };
        var sched3 = new LoanRepaymentSchedule { EmployeeLoan = completedLoan, ScheduledAmount = 50m, Status = "PENDING" };

        repository.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { sched1, sched2, sched3 });
        repository.Setup(r => r.GetAdvancesAsync("E1", "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeeAdvance>());

        var result = await new LoanDeductionService(repository.Object)
            .CalculateAndApplyDeductionsAsync(null, "E1", "2026-10", 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(0m, result.TotalScheduledDeduction);
        Assert.Equal(0m, result.TotalActualDeduction);
        Assert.Empty(result.UpdatedSchedules);
    }

    [Fact]
    public async Task AdvanceExcludedWhenAlreadyReservedByHandoff()
    {
        var repository = CreateLoanRepositoryMock();
        var reservedAdvance = new EmployeeAdvance
        {
            Id = 1,
            EmployeeCode = "E1",
            Status = "ACTIVE",
            CurrencyCode = "JOD",
            RemainingBalance = 100m,
            ReservedByHandoffId = 99L
        };
        repository.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LoanRepaymentSchedule>());
        repository.Setup(r => r.GetAdvancesAsync("E1", "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { reservedAdvance });

        var result = await new LoanDeductionService(repository.Object)
            .CalculateAndApplyDeductionsAsync(null, "E1", "2026-10", 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(0m, result.TotalScheduledDeduction);
        Assert.Empty(result.UpdatedAdvances);
    }

    [Fact]
    public async Task LoanScheduleExcludedWhenAlreadyReservedByHandoff()
    {
        var repository = CreateLoanRepositoryMock();
        var loan = new EmployeeLoan { EmployeeCode = "E1", Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 500m };
        var reservedSchedule = new LoanRepaymentSchedule
        {
            EmployeeLoan = loan,
            ScheduledAmount = 100m,
            Status = "PENDING",
            ReservedByHandoffId = 88L
        };
        repository.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { reservedSchedule });
        repository.Setup(r => r.GetAdvancesAsync("E1", "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeeAdvance>());

        var result = await new LoanDeductionService(repository.Object)
            .CalculateAndApplyDeductionsAsync(null, "E1", "2026-10", 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(0m, result.TotalScheduledDeduction);
        Assert.Empty(result.UpdatedSchedules);
    }

    [Fact]
    public async Task BackdatedSalaryRejectsFutureStructureOverlap()
    {
        var repository = new Mock<IEmployeeRepository>();
        var future = new SalaryStructure { EmployeeCode = "E1", EffectiveFrom = new DateTime(2026, 11, 1) };
        repository.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(new Employee { EmployeeCode = "E1" });
        repository.Setup(r => r.GetSalaryStructuresAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { future });
        repository.Setup(r => r.ValidateSalaryMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new EmployeeService(repository.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignSalaryStructureAsync("E1", new AssignSalaryStructureDto(
            new DateTime(2026, 10, 1), null, 500, "JOD", "BANK_TRANSFER", null), "audit"));
        Assert.Null(future.EffectiveTo);
    }

    [Fact]
    public async Task SalaryStructureAssignmentRejectsNegativeBasicSalary()
    {
        var repository = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(new Employee { EmployeeCode = "E1" });
        repository.Setup(r => r.ValidateSalaryMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new EmployeeService(repository.Object);

        var dto = new AssignSalaryStructureDto(new DateTime(2026, 10, 1), null, -100m, "JOD", "BANK_TRANSFER", null);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.AssignSalaryStructureAsync("E1", dto, "audit"));
        Assert.Contains("Salary amount and effective date range are invalid", ex.Message);
    }

    [Fact]
    public async Task SalaryStructureAssignmentRejectsInvalidDateRange()
    {
        var repository = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(new Employee { EmployeeCode = "E1" });
        repository.Setup(r => r.ValidateSalaryMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new EmployeeService(repository.Object);

        var dto = new AssignSalaryStructureDto(new DateTime(2026, 10, 15), new DateTime(2026, 10, 10), 500m, "JOD", "BANK_TRANSFER", null);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.AssignSalaryStructureAsync("E1", dto, "audit"));
        Assert.Contains("Salary amount and effective date range are invalid", ex.Message);
    }

    [Fact]
    public async Task SalaryStructureAssignmentRejectsUnsupportedPaymentMethod()
    {
        var repository = new Mock<IEmployeeRepository>();
        repository.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(new Employee { EmployeeCode = "E1" });
        var service = new EmployeeService(repository.Object);

        var dto = new AssignSalaryStructureDto(new DateTime(2026, 10, 1), null, 500m, "JOD", "CRYPTO_WALLET", null);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.AssignSalaryStructureAsync("E1", dto, "audit"));
        Assert.Contains("PaymentMethod must be BANK_TRANSFER, CASH or CHEQUE", ex.Message);
    }

    [Fact]
    public async Task SalaryStructureAssignmentTruncatesPrecedingActiveStructure()
    {
        var repository = new Mock<IEmployeeRepository>();
        var preceding = new SalaryStructure
        {
            Id = 1,
            EmployeeCode = "E1",
            EffectiveFrom = new DateTime(2026, 1, 1),
            EffectiveTo = null,
            BasicSalary = 400m,
            IsActive = true
        };
        repository.Setup(r => r.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(new Employee { EmployeeCode = "E1" });
        repository.Setup(r => r.GetSalaryStructuresAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { preceding });
        repository.Setup(r => r.ValidateSalaryMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(r => r.AddSalaryStructureAsync(It.IsAny<SalaryStructure>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = new EmployeeService(repository.Object);
        var dto = new AssignSalaryStructureDto(new DateTime(2026, 11, 1), null, 600m, "JOD", "BANK_TRANSFER", null);
        var result = await service.AssignSalaryStructureAsync("E1", dto, "audit_user");

        Assert.Equal(new DateTime(2026, 10, 31), preceding.EffectiveTo);
        Assert.Equal("audit_user", preceding.UpdateUser);
        Assert.NotNull(preceding.UpdateDate);
        Assert.Equal(600m, result.BasicSalary);
        Assert.Equal(new DateTime(2026, 11, 1), result.EffectiveFrom);
    }

    [Fact]
    public async Task LeaveApprovalRejectsInsufficientBalance()
    {
        var repository = new Mock<ILeaveRepository>();
        var employeeRepository = new Mock<IEmployeeRepository>();
        var balance = new LeaveBalance { EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2026, AccruedDays = 5, UsedDays = 4 };
        var request = new LeaveRequest
        {
            Id = 1, EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", StartDate = new DateTime(2026, 10, 1),
            DaysRequested = 3, LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", IsPaid = true }
        };
        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveRequestDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetLeaveBalanceAsync("E1", "ANNUAL", 2026, It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new HrLeaveSettings());
        await Assert.ThrowsAsync<InvalidOperationException>(() => new LeaveService(repository.Object, employeeRepository.Object)
            .ProcessLeaveRequestAsync(1, new ProcessLeaveRequestDto(true, null), "audit"));
        Assert.Equal("PENDING", request.Status);
        Assert.Equal(4m, balance.UsedDays);
    }

    [Fact]
    public async Task LeaveRejectionPreservesBalanceAndDoesNotCreateMovements()
    {
        var repository = new Mock<ILeaveRepository>();
        var employeeRepository = new Mock<IEmployeeRepository>();
        var balance = new LeaveBalance { EmployeeCode = "E1", LeaveTypeCode = "ANNUAL", YearNo = 2026, AccruedDays = 10, UsedDays = 2 };
        var request = new LeaveRequest
        {
            Id = 7,
            EmployeeCode = "E1",
            LeaveTypeCode = "ANNUAL",
            StartDate = new DateTime(2026, 10, 1),
            EndDate = new DateTime(2026, 10, 5),
            DaysRequested = 4,
            Status = "PENDING",
            LeaveType = new LeaveType { LeaveTypeCode = "ANNUAL", IsPaid = true, TracksBalance = true }
        };

        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.GetLeaveRequestByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveRequestDto>> action, CancellationToken _) => action());
        repository.Setup(r => r.GetLeaveBalanceAsync("E1", "ANNUAL", 2026, It.IsAny<CancellationToken>())).ReturnsAsync(balance);
        repository.Setup(r => r.UpdateLeaveRequestAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = new LeaveService(repository.Object, employeeRepository.Object);
        var result = await service.ProcessLeaveRequestAsync(7, new ProcessLeaveRequestDto(false, "Operational shortage"), "supervisor");

        Assert.Equal("REJECTED", result.Status);
        Assert.Equal("REJECTED", request.Status);
        Assert.Equal("Operational shortage", request.RejectionReason);
        Assert.Equal("supervisor", request.ApprovedBy);
        Assert.Equal(2m, balance.UsedDays); // untouched
        repository.Verify(r => r.UpdateLeaveBalanceAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.RecordMovementAsync(It.IsAny<LeaveBalance>(), It.IsAny<LeaveBalanceState>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("APPROVED")]
    [InlineData("REJECTED")]
    [InlineData("CANCELLED")]
    public async Task AlreadyProcessedLeaveRequestThrowsOnSecondProcessing(string status)
    {
        var repository = new Mock<ILeaveRepository>();
        var employeeRepository = new Mock<IEmployeeRepository>();
        var request = new LeaveRequest
        {
            Id = 8,
            EmployeeCode = "E1",
            LeaveTypeCode = "ANNUAL",
            Status = status,
            DaysRequested = 2
        };

        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.GetLeaveRequestByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveRequestDto>> action, CancellationToken _) => action());

        var service = new LeaveService(repository.Object, employeeRepository.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessLeaveRequestAsync(8, new ProcessLeaveRequestDto(true, null), "manager"));
        Assert.Contains($"has already been processed with status '{status}'", ex.Message);
    }

    [Fact]
    public async Task LeaveApprovalForUntrackedBalanceTypeDoesNotRequireBalance()
    {
        var repository = new Mock<ILeaveRepository>();
        var employeeRepository = new Mock<IEmployeeRepository>();
        var employee = new Employee { EmployeeCode = "E1", HireDate = new DateTime(2025, 1, 1) };
        var settings = new HrLeaveSettings();
        var request = new LeaveRequest
        {
            Id = 9,
            EmployeeCode = "E1",
            LeaveTypeCode = "BEREAVEMENT",
            StartDate = new DateTime(2026, 10, 1),
            EndDate = new DateTime(2026, 10, 3),
            DaysRequested = 3,
            Status = "PENDING",
            LeaveType = new LeaveType { LeaveTypeCode = "BEREAVEMENT", IsPaid = true, TracksBalance = false }
        };

        repository.Setup(r => r.GetLeaveRequestSnapshotAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.GetLeaveRequestByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        repository.Setup(r => r.ExecuteForLeaveEmployeeAsync("E1", It.IsAny<Func<Task<LeaveRequestDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<LeaveRequestDto>> action, CancellationToken _) => action());
        employeeRepository.Setup(e => e.GetEmployeeByCodeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        repository.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        repository.Setup(r => r.GetLeavePolicyByTypeCodeAsync("BEREAVEMENT", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync((LeavePolicy?)null);
        repository.Setup(r => r.EnsureLeaveCycleOpenAsync("E1", "BEREAVEMENT", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.UpdateLeaveRequestAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = new LeaveService(repository.Object, employeeRepository.Object);
        var result = await service.ProcessLeaveRequestAsync(9, new ProcessLeaveRequestDto(true, null), "manager");

        Assert.Equal("APPROVED", result.Status);
        Assert.Equal("APPROVED", request.Status);
        Assert.Equal("manager", request.ApprovedBy);
        // Verify no balance query was made
        repository.Verify(r => r.GetLeaveBalanceByCycleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.RecordMovementAsync(It.IsAny<LeaveBalance>(), It.IsAny<LeaveBalanceState>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
