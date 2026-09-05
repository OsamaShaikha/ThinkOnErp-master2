using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class LoanDeductionServiceTests
{
    private readonly Mock<ILoanRepository> _loanRepoMock = new();
    private readonly Mock<IPolicyRepository> _policyRepoMock = new();
    private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
    private readonly LoanDeductionService _service;

    public LoanDeductionServiceTests()
    {
        _service = new LoanDeductionService(
            _loanRepoMock.Object,
            _policyRepoMock.Object,
            _employeeRepoMock.Object,
            NullLogger<LoanDeductionService>.Instance);
    }

    [Fact]
    public async Task ProcessDeductions_WithinCap_DeductsFullInstallment()
    {
        // Net before loans: 1000 JOD. Cap: 33% (Max deduction 330 JOD).
        // Active Loan installment: 200 JOD.
        var loan = new EmployeeLoan
        {
            Id = 10,
            EmployeeCode = "EMP-001",
            Status = "ACTIVE",
            RemainingBalance = 1000m
        };

        var schedule = new LoanRepaymentSchedule
        {
            Id = 101,
            EmployeeLoanId = 10,
            EmployeeLoan = loan,
            InstallmentNo = 1,
            PayPeriod = "2026-08",
            ScheduledAmount = 200m,
            PaidAmount = 0m,
            Status = "PENDING"
        };
        loan.Schedules = new List<LoanRepaymentSchedule> { schedule };

        _policyRepoMock.Setup(p => p.GetEffectiveDeductionPolicyAsync(1, It.IsAny<DateTime>(), default))
            .ReturnsAsync(new DeductionPolicy { CompanyId = 1, MaxDeductionPercentage = 33.00m, AutoCapAndCarryForward = true });

        _loanRepoMock.Setup(l => l.GetPendingSchedulesByPeriodAsync("2026-08", default))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });
        _loanRepoMock.Setup(l => l.GetAdvancesAsync("EMP-001", "2026-08", "APPROVED", default))
            .ReturnsAsync(new List<EmployeeAdvance>());

        // Act
        var result = await _service.ProcessPeriodLoanDeductionsAsync("EMP-001", "2026-08", 1000m, 1, new DateTime(2026, 8, 1));

        // Assert
        result.TotalDeductedAmount.Should().Be(200m);
        result.TotalCarriedForwardAmount.Should().Be(0m);
        schedule.PaidAmount.Should().Be(200m);
        schedule.Status.Should().Be("PAID");
    }

    [Fact]
    public async Task ProcessDeductions_ExceedingCap_CapsDeductionAndCarriesForwardRemainder()
    {
        // Net before loans: 500 JOD. Cap: 33% => Max allowed deduction = 165.00 JOD.
        // Active Loan installment: 200 JOD.
        // Expected: Deduct 165 JOD, carry forward 35 JOD to remaining balance.
        var loan = new EmployeeLoan
        {
            Id = 10,
            EmployeeCode = "EMP-001",
            Status = "ACTIVE",
            RemainingBalance = 1000m
        };

        var schedule = new LoanRepaymentSchedule
        {
            Id = 101,
            EmployeeLoanId = 10,
            EmployeeLoan = loan,
            InstallmentNo = 1,
            PayPeriod = "2026-08",
            ScheduledAmount = 200m,
            PaidAmount = 0m,
            Status = "PENDING"
        };
        loan.Schedules = new List<LoanRepaymentSchedule> { schedule };

        _policyRepoMock.Setup(p => p.GetEffectiveDeductionPolicyAsync(1, It.IsAny<DateTime>(), default))
            .ReturnsAsync(new DeductionPolicy { CompanyId = 1, MaxDeductionPercentage = 33.00m, AutoCapAndCarryForward = true });

        _loanRepoMock.Setup(l => l.GetPendingSchedulesByPeriodAsync("2026-08", default))
            .ReturnsAsync(new List<LoanRepaymentSchedule> { schedule });
        _loanRepoMock.Setup(l => l.GetAdvancesAsync("EMP-001", "2026-08", "APPROVED", default))
            .ReturnsAsync(new List<EmployeeAdvance>());

        // Act
        var result = await _service.ProcessPeriodLoanDeductionsAsync("EMP-001", "2026-08", 500m, 1, new DateTime(2026, 8, 1));

        // Assert
        result.TotalDeductedAmount.Should().Be(165.00m);
        result.TotalCarriedForwardAmount.Should().Be(35.00m);
        schedule.PaidAmount.Should().Be(165.00m);
        schedule.CarriedForwardAmount.Should().Be(35.00m);
        schedule.Status.Should().Be("PARTIAL");
    }
}
