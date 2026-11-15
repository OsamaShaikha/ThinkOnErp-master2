using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class LoanDeductionServiceTests
{
    private readonly Mock<ILoanRepository> _loanRepoMock;
    private readonly LoanDeductionService _service;

    public LoanDeductionServiceTests()
    {
        _loanRepoMock = new Mock<ILoanRepository>();
        _loanRepoMock.Setup(r => r.GetCurrencyRulesForEmployeeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((3, (int?)null));
        _service = new LoanDeductionService(_loanRepoMock.Object);
    }

    [Fact]
    public async Task DeductionsWithinLimit_AreFullyDeducted()
    {
        var empCode = "EMP001";
        var payPeriod = "2026-09";
        decimal netPayBeforeLoans = 1000m;

        var loan = new EmployeeLoan { Id = 1, EmployeeCode = empCode, Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 500m };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 101,
            EmployeeLoanId = 1,
            EmployeeLoan = loan,
            ScheduledAmount = 200m,
            Status = "PENDING"
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var policy = new DeductionPolicy
        {
            MaxDeductionPercentage = 50m, // Up to 500 JOD
            MinNetPayGuarantee = 200m,
            AutoCapAndCarryForward = true,
            AllowNegativeNetPay = false
        };

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, netPayBeforeLoans, policy, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(200m, result.TotalScheduledDeduction);
        Assert.Equal(200m, result.TotalActualDeduction);
        Assert.Equal(0m, result.TotalCarriedForward);
        Assert.Equal("PAID", Assert.Single(result.UpdatedSchedules).Status);
        Assert.Equal("PENDING", schedule.Status);
        Assert.Equal(200m, Assert.Single(result.UpdatedSchedules).PaidAmount);
        Assert.Equal(0m, schedule.PaidAmount);
    }

    [Fact]
    public async Task ExceedingMaxDeductionPercent_IsCappedAndCarriedForward()
    {
        var empCode = "EMP002";
        var payPeriod = "2026-09";
        decimal netPayBeforeLoans = 1000m;

        var loan = new EmployeeLoan { Id = 2, EmployeeCode = empCode, Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 1500m };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 201,
            EmployeeLoanId = 2,
            EmployeeLoan = loan,
            ScheduledAmount = 600m, // 600 JOD scheduled
            Status = "PENDING"
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        // Max deduction is 40% of net pay = 400 JOD max
        var policy = new DeductionPolicy
        {
            MaxDeductionPercentage = 40m,
            MinNetPayGuarantee = 100m,
            AutoCapAndCarryForward = true,
            AllowNegativeNetPay = false
        };

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, netPayBeforeLoans, policy, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(600m, result.TotalScheduledDeduction);
        Assert.Equal(400m, result.TotalActualDeduction);
        Assert.Equal(200m, result.TotalCarriedForward);
        Assert.Equal("PARTIAL", Assert.Single(result.UpdatedSchedules).Status);
        Assert.Equal("PENDING", schedule.Status);
        Assert.Equal(400m, Assert.Single(result.UpdatedSchedules).PaidAmount);
        Assert.Equal(0m, schedule.PaidAmount);
        Assert.Equal(200m, Assert.Single(result.UpdatedSchedules).CarriedForwardAmount);
    }

    [Fact]
    public async Task MinNetPayGuarantee_TakesPrecedenceToProtectEmployee()
    {
        var empCode = "EMP003";
        var payPeriod = "2026-09";
        decimal netPayBeforeLoans = 500m;

        var loan = new EmployeeLoan { Id = 3, EmployeeCode = empCode, Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 800m };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 301,
            EmployeeLoanId = 3,
            EmployeeLoan = loan,
            ScheduledAmount = 250m,
            Status = "PENDING"
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        // 50% percent cap would allow 250 JOD, but MinNetPayGuarantee is 350 JOD
        // Guarantee cap = 500 - 350 = 150 JOD max allowable deduction!
        var policy = new DeductionPolicy
        {
            MaxDeductionPercentage = 50m,
            MinNetPayGuarantee = 350m,
            AutoCapAndCarryForward = true,
            AllowNegativeNetPay = false
        };

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, netPayBeforeLoans, policy, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(250m, result.TotalScheduledDeduction);
        Assert.Equal(150m, result.TotalActualDeduction);
        Assert.Equal(100m, result.TotalCarriedForward);
        Assert.Equal("PARTIAL", Assert.Single(result.UpdatedSchedules).Status);
        Assert.Equal("PENDING", schedule.Status);
        Assert.Equal(150m, Assert.Single(result.UpdatedSchedules).PaidAmount);
        Assert.Equal(0m, schedule.PaidAmount);
        Assert.Equal(100m, Assert.Single(result.UpdatedSchedules).CarriedForwardAmount);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenPersistChangesTrue_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CalculateAndApplyDeductionsAsync(null, "EMP001", "2026-09", 1000m, null, persistChanges: true));

        Assert.Equal("Loan settlement requires a confirmed Accounting posting receipt; HR calculation and approval cannot persist repayments.", ex.Message);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenTotalScheduledZero_ReturnsEmptyResultsWithoutError()
    {
        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-09", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule>());

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync("EMP_EMPTY", "2026-09", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var result = await _service.CalculateAndApplyDeductionsAsync(null, "EMP_EMPTY", "2026-09", 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(0m, result.TotalScheduledDeduction);
        Assert.Equal(0m, result.TotalActualDeduction);
        Assert.Equal(0m, result.TotalCarriedForward);
        Assert.Empty(result.UpdatedSchedules);
        Assert.Empty(result.UpdatedAdvances);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenAdvancesAndLoansExist_PrioritizesAdvancesFirst()
    {
        var empCode = "EMP004";
        var payPeriod = "2026-09";
        decimal netPayBeforeLoans = 1000m;

        var advance = new EmployeeAdvance
        {
            Id = 401,
            EmployeeCode = empCode,
            AdvanceAmount = 300m,
            RemainingBalance = 300m,
            Status = "ACTIVE",
                CurrencyCode = "JOD",
        };

        var loan = new EmployeeLoan
        {
            Id = 4,
            EmployeeCode = empCode,
            Status = "ACTIVE",
                CurrencyCode = "JOD",
            RemainingBalance = 1000m
        };

        var schedule = new LoanRepaymentSchedule
        {
            Id = 402,
            EmployeeLoanId = 4,
            EmployeeLoan = loan,
            ScheduledAmount = 200m,
            Status = "PENDING"
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance> { advance });

        // Cap at 30% of 1000 = 300 JOD
        var policy = new DeductionPolicy
        {
            MaxDeductionPercentage = 30m,
            MinNetPayGuarantee = 100m,
            AutoCapAndCarryForward = true,
            AllowNegativeNetPay = false
        };

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, netPayBeforeLoans, policy, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(500m, result.TotalScheduledDeduction);
        Assert.Equal(300m, result.TotalActualDeduction);
        Assert.Equal(200m, result.TotalCarriedForward);

        // Advance was priority 1 and got full 300 JOD
        var updatedAdvance = Assert.Single(result.UpdatedAdvances);
        Assert.Equal(300m, updatedAdvance.DeductedAmount);
        Assert.Equal(0m, updatedAdvance.RemainingBalance);
        Assert.Equal("RECOVERED", updatedAdvance.Status);

        // Loan schedule got 0 JOD because cap was exhausted
        var updatedSchedule = Assert.Single(result.UpdatedSchedules);
        Assert.Equal(0m, updatedSchedule.PaidAmount);
        Assert.Equal(200m, updatedSchedule.CarriedForwardAmount);
        Assert.Equal("SKIPPED", updatedSchedule.Status);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenAdvancePartiallyCovered_LeavesLoanUntouched()
    {
        var empCode = "EMP005";
        var payPeriod = "2026-09";
        decimal netPayBeforeLoans = 500m;

        var advance = new EmployeeAdvance
        {
            Id = 501,
            EmployeeCode = empCode,
            AdvanceAmount = 400m,
            RemainingBalance = 400m,
            Status = "ACTIVE",
                CurrencyCode = "JOD",
        };

        var loan = new EmployeeLoan
        {
            Id = 5,
            EmployeeCode = empCode,
            Status = "ACTIVE",
                CurrencyCode = "JOD",
            RemainingBalance = 1000m
        };

        var schedule = new LoanRepaymentSchedule
        {
            Id = 502,
            EmployeeLoanId = 5,
            EmployeeLoan = loan,
            ScheduledAmount = 200m,
            Status = "PENDING"
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance> { advance });

        // Cap at 50% of 500 = 250 JOD
        var policy = new DeductionPolicy
        {
            MaxDeductionPercentage = 50m,
            MinNetPayGuarantee = 100m,
            AutoCapAndCarryForward = true,
            AllowNegativeNetPay = false
        };

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, netPayBeforeLoans, policy, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(600m, result.TotalScheduledDeduction);
        Assert.Equal(250m, result.TotalActualDeduction);
        Assert.Equal(350m, result.TotalCarriedForward);

        var updatedAdvance = Assert.Single(result.UpdatedAdvances);
        Assert.Equal(250m, updatedAdvance.DeductedAmount);
        Assert.Equal(150m, updatedAdvance.RemainingBalance);
        Assert.Equal("ACTIVE", updatedAdvance.Status);

        var updatedSchedule = Assert.Single(result.UpdatedSchedules);
        Assert.Equal(0m, updatedSchedule.PaidAmount);
        Assert.Equal(200m, updatedSchedule.CarriedForwardAmount);
        Assert.Equal("SKIPPED", updatedSchedule.Status);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenLoanRemainingBalanceFullyPaid_MarksLoanCompleted()
    {
        var empCode = "EMP006";
        var payPeriod = "2026-09";
        decimal netPayBeforeLoans = 1000m;

        var loan = new EmployeeLoan
        {
            Id = 6,
            EmployeeCode = empCode,
            Status = "ACTIVE",
                CurrencyCode = "JOD",
            RemainingBalance = 200m,
            TotalPaidAmount = 800m
        };

        var schedule = new LoanRepaymentSchedule
        {
            Id = 601,
            EmployeeLoanId = 6,
            EmployeeLoan = loan,
            ScheduledAmount = 200m,
            Status = "PENDING"
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, netPayBeforeLoans, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(200m, result.TotalActualDeduction);
        var updatedSchedule = Assert.Single(result.UpdatedSchedules);
        Assert.Equal("PAID", updatedSchedule.Status);
        Assert.NotNull(updatedSchedule.EmployeeLoan);
        Assert.Equal(0m, updatedSchedule.EmployeeLoan.RemainingBalance);
        Assert.Equal(1000m, updatedSchedule.EmployeeLoan.TotalPaidAmount);
        Assert.Equal("COMPLETED", updatedSchedule.EmployeeLoan.Status);
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("CANCELLED")]
    [InlineData("COMPLETED")]
    public async Task CalculateAndApplyDeductionsAsync_WhenLoanNotActive_IsExcludedFromCalculation(string loanStatus)
    {
        var empCode = "EMP007";
        var payPeriod = "2026-09";

        var loan = new EmployeeLoan
        {
            Id = 7,
            EmployeeCode = empCode,
            Status = loanStatus,
            RemainingBalance = 500m
        };

        var schedule = new LoanRepaymentSchedule
        {
            Id = 701,
            EmployeeLoanId = 7,
            EmployeeLoan = loan,
            ScheduledAmount = 200m,
            Status = "PENDING"
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(0m, result.TotalScheduledDeduction);
        Assert.Equal(0m, result.TotalActualDeduction);
        Assert.Empty(result.UpdatedSchedules);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenAdvanceNotActiveOrZeroRemaining_IsExcluded()
    {
        var empCode = "EMP008";
        var payPeriod = "2026-09";

        var pendingAdvance = new EmployeeAdvance
        {
            Id = 801,
            EmployeeCode = empCode,
            AdvanceAmount = 300m,
            RemainingBalance = 300m,
            Status = "PENDING" // Not ACTIVE
        };

        var zeroAdvance = new EmployeeAdvance
        {
            Id = 802,
            EmployeeCode = empCode,
            AdvanceAmount = 200m,
            RemainingBalance = 0m, // Zero remaining
            Status = "ACTIVE",
                CurrencyCode = "JOD",
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule>());

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance> { pendingAdvance, zeroAdvance });

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(0m, result.TotalScheduledDeduction);
        Assert.Empty(result.UpdatedAdvances);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenScheduleReservedByHandoff_IsExcluded()
    {
        var empCode = "EMP009";
        var payPeriod = "2026-09";

        var loan = new EmployeeLoan { Id = 9, EmployeeCode = empCode, Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 500m };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 901,
            EmployeeLoanId = 9,
            EmployeeLoan = loan,
            ScheduledAmount = 200m,
            Status = "PENDING",
            ReservedByHandoffId = 999 // Reserved by handoff
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(0m, result.TotalScheduledDeduction);
        Assert.Empty(result.UpdatedSchedules);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenAdvanceReservedByHandoff_IsExcluded()
    {
        var empCode = "EMP010";
        var payPeriod = "2026-09";

        var advance = new EmployeeAdvance
        {
            Id = 1001,
            EmployeeCode = empCode,
            AdvanceAmount = 300m,
            RemainingBalance = 300m,
            Status = "ACTIVE",
                CurrencyCode = "JOD",
            ReservedByHandoffId = 777 // Reserved by handoff
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule>());

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance> { advance });

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(0m, result.TotalScheduledDeduction);
        Assert.Empty(result.UpdatedAdvances);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenPolicyNull_DeductsEntireScheduledAmount()
    {
        var empCode = "EMP011";
        var payPeriod = "2026-09";

        var loan = new EmployeeLoan { Id = 11, EmployeeCode = empCode, Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 500m };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 1101,
            EmployeeLoanId = 11,
            EmployeeLoan = loan,
            ScheduledAmount = 350m,
            Status = "PENDING"
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, 1000m, policy: null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(350m, result.TotalScheduledDeduction);
        Assert.Equal(350m, result.TotalActualDeduction);
        Assert.Equal(0m, result.TotalCarriedForward);
        Assert.Equal("PAID", Assert.Single(result.UpdatedSchedules).Status);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenAllowNegativeNetPayFalseAndNetPayZero_CarriesForwardAll()
    {
        var empCode = "EMP012";
        var payPeriod = "2026-09";

        var loan = new EmployeeLoan { Id = 12, EmployeeCode = empCode, Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 500m };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 1201,
            EmployeeLoanId = 12,
            EmployeeLoan = loan,
            ScheduledAmount = 250m,
            Status = "PENDING"
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var policy = new DeductionPolicy
        {
            MaxDeductionPercentage = 50m,
            MinNetPayGuarantee = 0m,
            AutoCapAndCarryForward = true,
            AllowNegativeNetPay = false
        };

        // Zero net pay before loans
        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, netPayBeforeLoans: 0m, policy, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(250m, result.TotalScheduledDeduction);
        Assert.Equal(0m, result.TotalActualDeduction);
        Assert.Equal(250m, result.TotalCarriedForward);

        var updatedSchedule = Assert.Single(result.UpdatedSchedules);
        Assert.Equal("SKIPPED", updatedSchedule.Status);
        Assert.Equal(0m, updatedSchedule.PaidAmount);
        Assert.Equal(250m, updatedSchedule.CarriedForwardAmount);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_DoesNotMutateOriginalEntityInstances()
    {
        var empCode = "EMP013";
        var payPeriod = "2026-09";

        var originalLoan = new EmployeeLoan { Id = 13, EmployeeCode = empCode, Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 500m };
        var originalSchedule = new LoanRepaymentSchedule
        {
            Id = 1301,
            EmployeeLoanId = 13,
            EmployeeLoan = originalLoan,
            ScheduledAmount = 200m,
            PaidAmount = 0m,
            Status = "PENDING"
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { originalSchedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        // Result updated schedule was modified
        var updatedSchedule = Assert.Single(result.UpdatedSchedules);
        Assert.Equal(200m, updatedSchedule.PaidAmount);
        Assert.Equal("PAID", updatedSchedule.Status);

        // Original objects remain untouched
        Assert.Equal(0m, originalSchedule.PaidAmount);
        Assert.Equal("PENDING", originalSchedule.Status);
        Assert.Equal(500m, originalLoan.RemainingBalance);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenMultipleAdvancesAndLoans_PrioritizesAdvancesBeforeLoans()
    {
        var empCode = "EMP_PRIO";
        var payPeriod = "2026-09";
        decimal netPayBeforeLoans = 400m;

        var loan = new EmployeeLoan { Id = 1, EmployeeCode = empCode, Status = "ACTIVE", CurrencyCode = "USD", RemainingBalance = 500m };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 101,
            EmployeeLoanId = 1,
            EmployeeLoan = loan,
            ScheduledAmount = 300m,
            Status = "PENDING"
        };

        var advance = new EmployeeAdvance
        {
            Id = 51,
            EmployeeCode = empCode,
            Status = "ACTIVE",
            RemainingBalance = 200m,
            DeductedAmount = 0m,
            CurrencyCode = "USD"
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance> { advance });

        // Policy: max 80% of net pay = 320m cap, min guarantee 50m (350m cap). Lowest cap is 320m.
        var policy = new DeductionPolicy
        {
            MaxDeductionPercentage = 80m,
            MinNetPayGuarantee = 50m,
            AutoCapAndCarryForward = true,
            AllowNegativeNetPay = false
        };

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, netPayBeforeLoans, policy, persistChanges: false, payrollCurrencyCode: "USD");

        // Advance (priority 1) consumes 200m -> remaining cap = 120m
        // Loan schedule (priority 2) consumes remaining 120m -> carried forward = 180m
        Assert.Equal(500m, result.TotalScheduledDeduction);
        Assert.Equal(320m, result.TotalActualDeduction);
        Assert.Equal(180m, result.TotalCarriedForward);

        var updatedAdvance = Assert.Single(result.UpdatedAdvances);
        Assert.Equal("RECOVERED", updatedAdvance.Status);
        Assert.Equal(0m, updatedAdvance.RemainingBalance);
        Assert.Equal(200m, updatedAdvance.DeductedAmount);

        var updatedSchedule = Assert.Single(result.UpdatedSchedules);
        Assert.Equal("PARTIAL", updatedSchedule.Status);
        Assert.Equal(120m, updatedSchedule.PaidAmount);
        Assert.Equal(180m, updatedSchedule.CarriedForwardAmount);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenGuaranteeCapIsStricterThanPercentageCap_RestrictsDeduction()
    {
        var empCode = "EMP_GUAR";
        var payPeriod = "2026-09";
        decimal netPay = 1000m;

        var loan = new EmployeeLoan { Id = 2, EmployeeCode = empCode, Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 1000m };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 201,
            EmployeeLoanId = 2,
            EmployeeLoan = loan,
            ScheduledAmount = 500m,
            Status = "PENDING"
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        // Max percentage = 80% (800m), but guarantee = 700m leaves at most 300m deduction
        var policy = new DeductionPolicy
        {
            MaxDeductionPercentage = 80m,
            MinNetPayGuarantee = 700m,
            AutoCapAndCarryForward = true,
            AllowNegativeNetPay = false
        };

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, netPay, policy, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(500m, result.TotalScheduledDeduction);
        Assert.Equal(300m, result.TotalActualDeduction);
        Assert.Equal(200m, result.TotalCarriedForward);

        var updatedSchedule = Assert.Single(result.UpdatedSchedules);
        Assert.Equal("PARTIAL", updatedSchedule.Status);
        Assert.Equal(300m, updatedSchedule.PaidAmount);
        Assert.Equal(200m, updatedSchedule.CarriedForwardAmount);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenInstallmentReservedByHandoff_ExcludesFromDeduction()
    {
        var empCode = "EMP_RES";
        var payPeriod = "2026-09";

        var loan = new EmployeeLoan { Id = 3, EmployeeCode = empCode, Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 500m };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 301,
            EmployeeLoanId = 3,
            EmployeeLoan = loan,
            ScheduledAmount = 250m,
            Status = "PENDING",
            ReservedByHandoffId = 9999 // Reserved by active payroll handoff
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(0m, result.TotalScheduledDeduction);
        Assert.Equal(0m, result.TotalActualDeduction);
        Assert.Empty(result.UpdatedSchedules);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenAdvanceReservedByHandoff_ExcludesFromDeduction()
    {
        var empCode = "EMP_RES_ADV";
        var payPeriod = "2026-09";

        var advance = new EmployeeAdvance
        {
            Id = 71,
            EmployeeCode = empCode,
            Status = "ACTIVE",
                CurrencyCode = "JOD",
            RemainingBalance = 300m,
            ReservedByHandoffId = 8888 // Reserved
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule>());

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance> { advance });

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(0m, result.TotalScheduledDeduction);
        Assert.Empty(result.UpdatedAdvances);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenLoanRemainingBalanceFullyCovered_SetsLoanStatusToCompleted()
    {
        var empCode = "EMP_COMP";
        var payPeriod = "2026-09";

        var loan = new EmployeeLoan
        {
            Id = 4,
            EmployeeCode = empCode,
            Status = "ACTIVE",
                CurrencyCode = "JOD",
            PrincipalAmount = 150m,
            RemainingBalance = 150m,
            TotalPaidAmount = 0m
        };

        var schedule = new LoanRepaymentSchedule
        {
            Id = 401,
            EmployeeLoanId = 4,
            EmployeeLoan = loan,
            ScheduledAmount = 150m,
            Status = "PENDING"
        };

        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        var updatedSchedule = Assert.Single(result.UpdatedSchedules);
        Assert.Equal("PAID", updatedSchedule.Status);
        Assert.Equal(150m, updatedSchedule.PaidAmount);
        Assert.NotNull(updatedSchedule.EmployeeLoan);
        Assert.Equal(0m, updatedSchedule.EmployeeLoan.RemainingBalance);
        Assert.Equal(150m, updatedSchedule.EmployeeLoan.TotalPaidAmount);
        Assert.Equal("COMPLETED", updatedSchedule.EmployeeLoan.Status);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenMultipleInstallments_ProcessesInChronologicalOrder()
    {
        var empCode = "EMP_CHRONO";
        var payPeriod = "2026-09";

        var loan = new EmployeeLoan { Id = 5, EmployeeCode = empCode, Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 400m };

        // Schedule 1: 2026-08 (deferred from previous month)
        var sch1 = new LoanRepaymentSchedule
        {
            Id = 501,
            EmployeeLoanId = 5,
            EmployeeLoan = loan,
            InstallmentNo = 1,
            PayPeriod = "2026-08",
            ScheduledAmount = 200m,
            Status = "PENDING"
        };

        // Schedule 2: 2026-09 (current month)
        var sch2 = new LoanRepaymentSchedule
        {
            Id = 502,
            EmployeeLoanId = 5,
            EmployeeLoan = loan,
            InstallmentNo = 2,
            PayPeriod = "2026-09",
            ScheduledAmount = 200m,
            Status = "PENDING"
        };

        // Pass out of order in mock to verify chronological sorting
        _loanRepoMock
            .Setup(r => r.GetPendingSchedulesForPeriodAsync(payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { sch2, sch1 });

        _loanRepoMock
            .Setup(r => r.GetAdvancesAsync(empCode, payPeriod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        // Cap deduction to 250m
        var policy = new DeductionPolicy
        {
            MaxDeductionPercentage = 25m, // 250m of 1000m
            MinNetPayGuarantee = 0m,
            AutoCapAndCarryForward = true,
            AllowNegativeNetPay = false
        };

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, 1000m, policy, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(400m, result.TotalScheduledDeduction);
        Assert.Equal(250m, result.TotalActualDeduction);
        Assert.Equal(150m, result.TotalCarriedForward);

        Assert.Equal(2, result.UpdatedSchedules.Count);
        // sch1 (2026-08) was processed first and fully paid (200m)
        var firstProcessed = result.UpdatedSchedules[0];
        Assert.Equal(501, firstProcessed.Id);
        Assert.Equal("PAID", firstProcessed.Status);
        Assert.Equal(200m, firstProcessed.PaidAmount);

        // sch2 (2026-09) was processed second with remaining 50m cap (PARTIAL)
        var secondProcessed = result.UpdatedSchedules[1];
        Assert.Equal(502, secondProcessed.Id);
        Assert.Equal("PARTIAL", secondProcessed.Status);
        Assert.Equal(50m, secondProcessed.PaidAmount);
        Assert.Equal(150m, secondProcessed.CarriedForwardAmount);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenScheduleAlreadyHasPartialPayment_DeductsRemainingScheduledAmount()
    {
        var loan = new EmployeeLoan { Id = 1, EmployeeCode = "E1", Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 500m };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 15,
            EmployeeLoanId = 1,
            EmployeeLoan = loan,
            ScheduledAmount = 300m,
            PaidAmount = 100m, // Already paid 100m, so remaining due is 200m
            Status = "PARTIAL"
        };

        _loanRepoMock.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });
        _loanRepoMock.Setup(r => r.GetAdvancesAsync("E1", "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var result = await _service.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(200m, result.TotalScheduledDeduction);
        Assert.Equal(200m, result.TotalActualDeduction);
        Assert.Equal(0m, result.TotalCarriedForward);
        var updated = Assert.Single(result.UpdatedSchedules);
        Assert.Equal("PAID", updated.Status);
        Assert.Equal(300m, updated.PaidAmount); // 100m + 200m
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenNoSchedulesOrAdvances_ReturnsZeroResult()
    {
        _loanRepoMock.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule>());
        _loanRepoMock.Setup(r => r.GetAdvancesAsync("E1", "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var result = await _service.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(0m, result.TotalScheduledDeduction);
        Assert.Equal(0m, result.TotalActualDeduction);
        Assert.Equal(0m, result.TotalCarriedForward);
        Assert.Empty(result.UpdatedSchedules);
        Assert.Empty(result.UpdatedAdvances);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenLoanCompleted_SetsLoanStatusCompleted()
    {
        var loan = new EmployeeLoan { Id = 1, EmployeeCode = "E1", Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 150m, TotalPaidAmount = 850m };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 11,
            EmployeeLoanId = 1,
            EmployeeLoan = loan,
            ScheduledAmount = 150m,
            PaidAmount = 0m,
            Status = "PENDING"
        };

        _loanRepoMock.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });
        _loanRepoMock.Setup(r => r.GetAdvancesAsync("E1", "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var result = await _service.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(150m, result.TotalActualDeduction);
        var updated = Assert.Single(result.UpdatedSchedules);
        Assert.Equal("PAID", updated.Status);
        Assert.Equal("COMPLETED", updated.EmployeeLoan?.Status);
        Assert.Equal(0m, updated.EmployeeLoan?.RemainingBalance);
        Assert.Equal(1000m, updated.EmployeeLoan?.TotalPaidAmount);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenAdvanceRemainingBalanceZero_SetsStatusRecovered()
    {
        var advance = new EmployeeAdvance
        {
            Id = 20,
            EmployeeCode = "E1",
            Status = "ACTIVE",
                CurrencyCode = "JOD",
            AdvanceAmount = 200m,
            RemainingBalance = 200m,
            DeductedAmount = 0m
        };

        _loanRepoMock.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule>());
        _loanRepoMock.Setup(r => r.GetAdvancesAsync("E1", "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance> { advance });

        var result = await _service.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", 1000m, null, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(200m, result.TotalActualDeduction);
        var updatedAdv = Assert.Single(result.UpdatedAdvances);
        Assert.Equal("RECOVERED", updatedAdv.Status);
        Assert.Equal(0m, updatedAdv.RemainingBalance);
        Assert.Equal(200m, updatedAdv.DeductedAmount);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenZeroCapFromPolicy_SetsAllSchedulesToSkipped()
    {
        var loan = new EmployeeLoan { Id = 1, EmployeeCode = "E1", Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 500m };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 12,
            EmployeeLoanId = 1,
            EmployeeLoan = loan,
            ScheduledAmount = 250m,
            PaidAmount = 0m,
            Status = "PENDING"
        };

        _loanRepoMock.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });
        _loanRepoMock.Setup(r => r.GetAdvancesAsync("E1", "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var policy = new DeductionPolicy
        {
            MaxDeductionPercentage = 0m, // 0% deduction allowed
            MinNetPayGuarantee = 1000m,
            AllowNegativeNetPay = false
        };

        var result = await _service.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", 1000m, policy, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(250m, result.TotalScheduledDeduction);
        Assert.Equal(0m, result.TotalActualDeduction);
        Assert.Equal(250m, result.TotalCarriedForward);
        var updated = Assert.Single(result.UpdatedSchedules);
        Assert.Equal("SKIPPED", updated.Status);
        Assert.Equal(0m, updated.PaidAmount);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenPolicyDisallowsNegativeNetPay_CapsDeductionAtNetPay()
    {
        var loan = new EmployeeLoan { Id = 1, EmployeeCode = "E1", Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 500m };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 13,
            EmployeeLoanId = 1,
            EmployeeLoan = loan,
            ScheduledAmount = 500m,
            PaidAmount = 0m,
            Status = "PENDING"
        };

        _loanRepoMock.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });
        _loanRepoMock.Setup(r => r.GetAdvancesAsync("E1", "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var policy = new DeductionPolicy
        {
            MaxDeductionPercentage = 100m,
            MinNetPayGuarantee = 0m,
            AllowNegativeNetPay = false
        };

        // Net pay before loans is only 300m, so max deduction capped at 300m
        var result = await _service.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", 300m, policy, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(500m, result.TotalScheduledDeduction);
        Assert.Equal(300m, result.TotalActualDeduction);
        Assert.Equal(200m, result.TotalCarriedForward);
        var updated = Assert.Single(result.UpdatedSchedules);
        Assert.Equal("PARTIAL", updated.Status);
        Assert.Equal(300m, updated.PaidAmount);
        Assert.Equal(200m, updated.CarriedForwardAmount);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_WhenAdvancesExhaustCap_LeavesLoanSchedulesSkipped()
    {
        var advance = new EmployeeAdvance
        {
            Id = 30,
            EmployeeCode = "E1",
            Status = "ACTIVE",
                CurrencyCode = "JOD",
            AdvanceAmount = 300m,
            RemainingBalance = 300m,
            DeductedAmount = 0m
        };

        var loan = new EmployeeLoan { Id = 1, EmployeeCode = "E1", Status = "ACTIVE", CurrencyCode = "JOD", RemainingBalance = 400m };
        var schedule = new LoanRepaymentSchedule
        {
            Id = 14,
            EmployeeLoanId = 1,
            EmployeeLoan = loan,
            ScheduledAmount = 200m,
            PaidAmount = 0m,
            Status = "PENDING"
        };

        _loanRepoMock.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });
        _loanRepoMock.Setup(r => r.GetAdvancesAsync("E1", "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance> { advance });

        // Cap deduction to 300m via policy (30% of 1000m)
        var policy = new DeductionPolicy
        {
            MaxDeductionPercentage = 30m,
            MinNetPayGuarantee = 0m,
            AllowNegativeNetPay = false
        };

        var result = await _service.CalculateAndApplyDeductionsAsync(null, "E1", "2026-11", 1000m, policy, persistChanges: false, payrollCurrencyCode: "JOD");

        Assert.Equal(500m, result.TotalScheduledDeduction);
        Assert.Equal(300m, result.TotalActualDeduction);
        Assert.Equal(200m, result.TotalCarriedForward);

        var updatedAdv = Assert.Single(result.UpdatedAdvances);
        Assert.Equal("RECOVERED", updatedAdv.Status);
        Assert.Equal(300m, updatedAdv.DeductedAmount);

        var updatedSch = Assert.Single(result.UpdatedSchedules);
        Assert.Equal("SKIPPED", updatedSch.Status);
        Assert.Equal(0m, updatedSch.PaidAmount);
        Assert.Equal(200m, updatedSch.CarriedForwardAmount);
    }

    [Fact]
    public async Task CalculateAndApplyDeductionsAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _loanRepoMock.Setup(r => r.GetPendingSchedulesForPeriodAsync("2026-11", ct))
            .ReturnsAsync(new List<LoanRepaymentSchedule>());
        _loanRepoMock.Setup(r => r.GetAdvancesAsync("EMP_CT", "2026-11", ct))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var result = await _service.CalculateAndApplyDeductionsAsync(null, "EMP_CT", "2026-11", 1000m, null, false, ct);

        Assert.Equal(0m, result.TotalScheduledDeduction);
        _loanRepoMock.Verify(r => r.GetPendingSchedulesForPeriodAsync("2026-11", ct), Times.Once);
        _loanRepoMock.Verify(r => r.GetAdvancesAsync("EMP_CT", "2026-11", ct), Times.Once);
    }

    [Fact]
    public async Task CreateLoanApplicationAsync_CalculatesInstallmentsAndAbsorbsRoundingOnLastInstallment()
    {
        var dto = new CreateLoanApplicationDto(
            "EMP_LOAN",
            "PERSONAL",
            1000m,
            3,
            new DateTime(2026, 1, 1),
            "Application note",
            " jod "
        );

        var loan = await _service.CreateLoanApplicationAsync(dto, "loan-officer");

        Assert.Equal("EMP_LOAN", loan.EmployeeCode);
        Assert.Equal("JOD", loan.CurrencyCode);
        Assert.Equal(333.333m, loan.MonthlyInstallmentAmount);
        Assert.Equal("PENDING", loan.Status);
        Assert.Equal("loan-officer", loan.CreationUser);
        Assert.Equal(3, loan.Schedules.Count);

        Assert.Equal(333.333m, loan.Schedules[0].ScheduledAmount);
        Assert.Equal("2026-01", loan.Schedules[0].PayPeriod);
        Assert.Equal(333.333m, loan.Schedules[1].ScheduledAmount);
        Assert.Equal("2026-02", loan.Schedules[1].PayPeriod);
        Assert.Equal(333.334m, loan.Schedules[2].ScheduledAmount);
        Assert.Equal("2026-03", loan.Schedules[2].PayPeriod);
        Assert.Equal(1000m, loan.Schedules.Sum(s => s.ScheduledAmount));

        _loanRepoMock.Verify(r => r.AddLoanAsync(loan, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateLoanApplicationAsync_WhenZeroInstallments_RejectsInvalidSchedule()
    {
        var dto = new CreateLoanApplicationDto(
            "EMP_ZERO",
            "EMERGENCY",
            500m,
            0,
            new DateTime(2026, 5, 1),
            null,
            "JOD"
        );

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.CreateLoanApplicationAsync(dto, "admin"));
        _loanRepoMock.Verify(r => r.AddLoanAsync(It.IsAny<EmployeeLoan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveLoanAsync_WhenLoanPending_ApprovesAndSaves()
    {
        var loan = new EmployeeLoan { Id = 50, Status = "PENDING", CurrencyCode = "JOD" };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var approved = await _service.ApproveLoanAsync(50, "approver-user");

        Assert.Equal("APPROVED", approved.Status);
        Assert.Equal("approver-user", approved.ApprovedBy);
        Assert.Equal("approver-user", approved.UpdateUser);
        Assert.NotNull(approved.ApprovalDate);
        _loanRepoMock.Verify(r => r.UpdateLoanAsync(loan, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveLoanAsync_WhenLoanAlreadyApproved_ReturnsIdempotently()
    {
        var loan = new EmployeeLoan { Id = 51, Status = "APPROVED", CurrencyCode = "JOD" };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(51, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var result = await _service.ApproveLoanAsync(51, "approver-user");

        Assert.Same(loan, result);
        _loanRepoMock.Verify(r => r.UpdateLoanAsync(It.IsAny<EmployeeLoan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveLoanAsync_WhenLoanNotFound_ThrowsKeyNotFoundException()
    {
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeLoan?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ApproveLoanAsync(999, "approver-user"));
    }

    [Theory]
    [InlineData("ACTIVE")]
    [InlineData("REJECTED")]
    [InlineData("CANCELLED")]
    [InlineData("COMPLETED")]
    public async Task ApproveLoanAsync_WhenLoanNotPending_ThrowsInvalidOperationException(string status)
    {
        var loan = new EmployeeLoan { Id = 52, Status = status };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(52, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ApproveLoanAsync(52, "approver-user"));
        Assert.Contains("Only a pending loan request can be approved", ex.Message);
    }

    [Fact]
    public async Task CreateAdvanceRequestAsync_NormalizesCurrencyAndInitializesFields()
    {
        var dto = new CreateAdvanceRequestDto(
            "EMP_ADV",
            250m,
            "2026-12",
            "Medical expense",
            " usd "
        );

        var advance = await _service.CreateAdvanceRequestAsync(dto, "adv-user");

        Assert.Equal("EMP_ADV", advance.EmployeeCode);
        Assert.Equal("USD", advance.CurrencyCode);
        Assert.Equal(250m, advance.AdvanceAmount);
        Assert.Equal(250m, advance.RemainingBalance);
        Assert.Equal(0m, advance.DeductedAmount);
        Assert.Equal("PENDING", advance.Status);
        Assert.Equal("adv-user", advance.CreationUser);

        _loanRepoMock.Verify(r => r.AddAdvanceAsync(advance, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAdvanceAsync_WhenAdvancePending_ApprovesAndSaves()
    {
        var advance = new EmployeeAdvance { Id = 60, Status = "PENDING", CurrencyCode = "JOD" };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(60, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var approved = await _service.ApproveAdvanceAsync(60, "approver-user");

        Assert.Equal("APPROVED", approved.Status);
        Assert.Equal("approver-user", approved.ApprovedBy);
        Assert.NotNull(approved.ApprovalDate);
        _loanRepoMock.Verify(r => r.UpdateAdvanceAsync(advance, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAdvanceAsync_WhenAdvanceAlreadyApproved_ReturnsIdempotently()
    {
        var advance = new EmployeeAdvance { Id = 61, Status = "APPROVED", CurrencyCode = "JOD" };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(61, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var result = await _service.ApproveAdvanceAsync(61, "approver-user");

        Assert.Same(advance, result);
        _loanRepoMock.Verify(r => r.UpdateAdvanceAsync(It.IsAny<EmployeeAdvance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveAdvanceAsync_WhenAdvanceNotFound_ThrowsKeyNotFoundException()
    {
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeAdvance?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ApproveAdvanceAsync(999, "approver-user"));
    }

    [Theory]
    [InlineData("ACTIVE")]
    [InlineData("REJECTED")]
    [InlineData("CANCELLED")]
    [InlineData("RECOVERED")]
    public async Task ApproveAdvanceAsync_WhenAdvanceNotPending_ThrowsInvalidOperationException(string status)
    {
        var advance = new EmployeeAdvance { Id = 62, Status = status };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(62, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ApproveAdvanceAsync(62, "approver-user"));
        Assert.Contains("Only a pending advance request can be approved", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SkipInstallmentAsync_WhenReasonNullOrWhitespace_ThrowsArgumentException(string? reason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SkipInstallmentAsync(10, reason!, "user"));
    }

    [Fact]
    public async Task SkipInstallmentAsync_WhenScheduleNotFound_ThrowsKeyNotFoundException()
    {
        _loanRepoMock.Setup(r => r.GetScheduleByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoanRepaymentSchedule?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.SkipInstallmentAsync(999, "Valid reason", "user"));
    }

    [Theory]
    [InlineData("PAID")]
    [InlineData("PARTIAL")]
    [InlineData("SKIPPED")]
    [InlineData("CANCELLED")]
    public async Task SkipInstallmentAsync_WhenScheduleNotPending_ThrowsInvalidOperationException(string status)
    {
        var schedule = new LoanRepaymentSchedule { Id = 70, Status = status };
        _loanRepoMock.Setup(r => r.GetScheduleByIdAsync(70, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schedule);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SkipInstallmentAsync(70, "Valid reason", "user"));
        Assert.Contains("Only PENDING installments can be skipped", ex.Message);
    }

    [Fact]
    public async Task SkipInstallmentAsync_WhenScheduleReservedByHandoff_ThrowsInvalidOperationException()
    {
        var schedule = new LoanRepaymentSchedule { Id = 71, Status = "PENDING", ReservedByHandoffId = 888L };
        _loanRepoMock.Setup(r => r.GetScheduleByIdAsync(71, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schedule);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SkipInstallmentAsync(71, "Valid reason", "user"));
        Assert.Contains("currently reserved by a payroll handoff", ex.Message);
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("APPROVED")]
    [InlineData("COMPLETED")]
    [InlineData("CANCELLED")]
    public async Task SkipInstallmentAsync_WhenParentLoanNotActive_ThrowsInvalidOperationException(string loanStatus)
    {
        var loan = new EmployeeLoan { Id = 5, Status = loanStatus };
        var schedule = new LoanRepaymentSchedule { Id = 72, Status = "PENDING", EmployeeLoanId = 5, EmployeeLoan = loan };
        _loanRepoMock.Setup(r => r.GetScheduleByIdAsync(72, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schedule);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SkipInstallmentAsync(72, "Valid reason", "user"));
        Assert.Contains("Only ACTIVE loans can have installments skipped", ex.Message);
    }

    [Fact]
    public async Task SkipInstallmentAsync_WhenValid_AppendsInstallmentToLoanAndDefersDate()
    {
        var loan = new EmployeeLoan
        {
            Id = 6,
            Status = "ACTIVE",
                CurrencyCode = "JOD",
            Notes = "Existing note",
            Schedules = new List<LoanRepaymentSchedule>()
        };
        var sch1 = new LoanRepaymentSchedule
        {
            Id = 81,
            EmployeeLoanId = 6,
            EmployeeLoan = loan,
            InstallmentNo = 1,
            PayPeriod = "2026-10",
            ScheduledAmount = 100m,
            PaidAmount = 0m,
            Status = "PENDING"
        };
        loan.Schedules.Add(sch1);

        _loanRepoMock.Setup(r => r.GetScheduleByIdAsync(81, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sch1);

        var skipped = await _service.SkipInstallmentAsync(81, "Financial hardship", "officer");

        Assert.Equal("SKIPPED", skipped.Status);
        Assert.Equal(2, loan.Schedules.Count);

        var newSch = loan.Schedules.Last();
        Assert.Equal(2, newSch.InstallmentNo);
        Assert.Equal("2026-11", newSch.PayPeriod);
        Assert.Equal(100m, newSch.ScheduledAmount);
        Assert.Equal("PENDING", newSch.Status);
        Assert.Contains("[SKIPPED: Installment 1 deferred to 2026-11. Reason: Financial hardship]", loan.Notes);

        _loanRepoMock.Verify(r => r.UpdateLoanAsync(loan, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelLoanAsync_WhenReasonNullOrWhitespace_ThrowsArgumentException(string? reason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CancelLoanAsync(10, reason!, "user"));
    }

    [Fact]
    public async Task CancelLoanAsync_WhenLoanNotFound_ThrowsKeyNotFoundException()
    {
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeLoan?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CancelLoanAsync(999, "Valid reason", "user"));
    }

    [Theory]
    [InlineData("ACTIVE")]
    [InlineData("SETTLED")]
    public async Task CancelLoanAsync_WhenLoanActiveOrSettled_ThrowsInvalidOperationException(string status)
    {
        var loan = new EmployeeLoan { Id = 7, Status = status };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLoanAsync(7, "Valid reason", "user"));
        Assert.Contains("Active or funded loans must be reversed via Accounting", ex.Message);
    }

    [Fact]
    public async Task CancelLoanAsync_WhenAlreadyCancelled_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan { Id = 8, Status = "CANCELLED" };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLoanAsync(8, "Valid reason", "user"));
        Assert.Contains("Loan is already cancelled", ex.Message);
    }

    [Fact]
    public async Task CancelLoanAsync_WhenInstallmentReservedByHandoff_ThrowsInvalidOperationException()
    {
        var loan = new EmployeeLoan
        {
            Id = 9,
            Status = "PENDING",
            Schedules = new List<LoanRepaymentSchedule>
            {
                new() { Id = 91, Status = "PENDING", ReservedByHandoffId = 777L }
            }
        };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelLoanAsync(9, "Valid reason", "user"));
        Assert.Contains("installments are reserved by a payroll handoff", ex.Message);
    }

    [Fact]
    public async Task CancelLoanAsync_WhenPending_CancelsLoanAndPendingSchedules()
    {
        var loan = new EmployeeLoan
        {
            Id = 10,
            Status = "PENDING",
            TotalPaidAmount = 0m,
            Schedules = new List<LoanRepaymentSchedule>
            {
                new() { Id = 92, Status = "PENDING" },
                new() { Id = 93, Status = "PENDING" }
            }
        };
        _loanRepoMock.Setup(r => r.GetLoanByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var cancelled = await _service.CancelLoanAsync(10, "Employee resignation", "hr-admin");

        Assert.Equal("CANCELLED", cancelled.Status);
        Assert.All(cancelled.Schedules, s => Assert.Equal("CANCELLED", s.Status));
        Assert.Contains("[CANCELLED: Employee resignation]", cancelled.Notes);

        _loanRepoMock.Verify(r => r.UpdateLoanAsync(loan, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelAdvanceAsync_WhenReasonNullOrWhitespace_ThrowsArgumentException(string? reason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CancelAdvanceAsync(10, reason!, "user"));
    }

    [Fact]
    public async Task CancelAdvanceAsync_WhenAdvanceNotFound_ThrowsKeyNotFoundException()
    {
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeAdvance?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CancelAdvanceAsync(999, "Valid reason", "user"));
    }

    [Theory]
    [InlineData("ACTIVE")]
    [InlineData("RECOVERED")]
    public async Task CancelAdvanceAsync_WhenAdvanceActiveOrRecovered_ThrowsInvalidOperationException(string status)
    {
        var advance = new EmployeeAdvance { Id = 11, Status = status };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelAdvanceAsync(11, "Valid reason", "user"));
        Assert.Contains("Active or funded advances must be reversed via Accounting", ex.Message);
    }

    [Fact]
    public async Task CancelAdvanceAsync_WhenAdvanceHasDeductions_ThrowsInvalidOperationException()
    {
        var advance = new EmployeeAdvance { Id = 12, Status = "PENDING", DeductedAmount = 50m };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelAdvanceAsync(12, "Valid reason", "user"));
        Assert.Contains("has recorded deductions", ex.Message);
    }

    [Fact]
    public async Task CancelAdvanceAsync_WhenReservedByHandoff_ThrowsInvalidOperationException()
    {
        var advance = new EmployeeAdvance { Id = 13, Status = "PENDING", ReservedByHandoffId = 666L };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(13, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelAdvanceAsync(13, "Valid reason", "user"));
        Assert.Contains("reserved by a payroll handoff", ex.Message);
    }

    [Fact]
    public async Task CancelAdvanceAsync_WhenPending_CancelsAndSaves()
    {
        var advance = new EmployeeAdvance { Id = 14, Status = "PENDING", DeductedAmount = 0m };
        _loanRepoMock.Setup(r => r.GetAdvanceByIdAsync(14, It.IsAny<CancellationToken>()))
            .ReturnsAsync(advance);

        var cancelled = await _service.CancelAdvanceAsync(14, "No longer needed", "hr-admin");

        Assert.Equal("CANCELLED", cancelled.Status);
        Assert.Contains("[CANCELLED: No longer needed]", cancelled.Reason);

        _loanRepoMock.Verify(r => r.UpdateAdvanceAsync(advance, It.IsAny<CancellationToken>()), Times.Once);
        _loanRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLoansAsync_And_GetAdvancesAsync_PropagateParametersAndCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _loanRepoMock.Setup(r => r.GetLoansAsync("E1", "ACTIVE", ct))
            .ReturnsAsync(new List<EmployeeLoan>());
        _loanRepoMock.Setup(r => r.GetAdvancesAsync("E1", "2026-10", ct))
            .ReturnsAsync(new List<EmployeeAdvance>());

        var loans = await _service.GetLoansAsync("E1", "ACTIVE", ct);
        var advances = await _service.GetAdvancesAsync("E1", "2026-10", ct);

        Assert.NotNull(loans);
        Assert.NotNull(advances);
        _loanRepoMock.Verify(r => r.GetLoansAsync("E1", "ACTIVE", ct), Times.Once);
        _loanRepoMock.Verify(r => r.GetAdvancesAsync("E1", "2026-10", ct), Times.Once);
    }
}
