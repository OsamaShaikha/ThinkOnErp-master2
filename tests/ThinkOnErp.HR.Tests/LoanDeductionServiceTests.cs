using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
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
        _service = new LoanDeductionService(_loanRepoMock.Object);
    }

    [Fact]
    public async Task DeductionsWithinLimit_AreFullyDeducted()
    {
        var empCode = "EMP001";
        var payPeriod = "2026-09";
        decimal netPayBeforeLoans = 1000m;

        var loan = new EmployeeLoan { Id = 1, EmployeeCode = empCode, RemainingBalance = 500m };
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

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, netPayBeforeLoans, policy);

        Assert.Equal(200m, result.TotalScheduledDeduction);
        Assert.Equal(200m, result.TotalActualDeduction);
        Assert.Equal(0m, result.TotalCarriedForward);
        Assert.Equal("PAID", schedule.Status);
        Assert.Equal(200m, schedule.PaidAmount);
    }

    [Fact]
    public async Task ExceedingMaxDeductionPercent_IsCappedAndCarriedForward()
    {
        var empCode = "EMP002";
        var payPeriod = "2026-09";
        decimal netPayBeforeLoans = 1000m;

        var loan = new EmployeeLoan { Id = 2, EmployeeCode = empCode, RemainingBalance = 1500m };
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

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, netPayBeforeLoans, policy);

        Assert.Equal(600m, result.TotalScheduledDeduction);
        Assert.Equal(400m, result.TotalActualDeduction);
        Assert.Equal(200m, result.TotalCarriedForward);
        Assert.Equal("PARTIAL", schedule.Status);
        Assert.Equal(400m, schedule.PaidAmount);
        Assert.Equal(200m, schedule.CarriedForwardAmount);
    }

    [Fact]
    public async Task MinNetPayGuarantee_TakesPrecedenceToProtectEmployee()
    {
        var empCode = "EMP003";
        var payPeriod = "2026-09";
        decimal netPayBeforeLoans = 500m;

        var loan = new EmployeeLoan { Id = 3, EmployeeCode = empCode, RemainingBalance = 800m };
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

        var result = await _service.CalculateAndApplyDeductionsAsync(null, empCode, payPeriod, netPayBeforeLoans, policy);

        Assert.Equal(250m, result.TotalScheduledDeduction);
        Assert.Equal(150m, result.TotalActualDeduction);
        Assert.Equal(100m, result.TotalCarriedForward);
        Assert.Equal("PARTIAL", schedule.Status);
        Assert.Equal(150m, schedule.PaidAmount);
        Assert.Equal(100m, schedule.CarriedForwardAmount);
    }
}
