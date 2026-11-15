using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class PayrollValidationServiceTests
{
    private readonly Mock<IPayrollPeriodRepository> _periodRepoMock = new();
    private readonly Mock<IPolicyRepository> _policyRepoMock = new();
    private readonly Mock<IAttendanceDayContextRepository> _attendanceContextMock = new();
    private readonly PayrollValidationService _service;

    public PayrollValidationServiceTests()
    {
        _service = new PayrollValidationService(
            _periodRepoMock.Object,
            _policyRepoMock.Object,
            _attendanceContextMock.Object);
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenPeriodNotFound_ReturnsError()
    {
        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollPeriod?)null);

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("does not exist for company 1", result.Errors[0]);
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenPeriodClosed_ReturnsError()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "CLOSED",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee>());

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("is already CLOSED"));
    }

    [Theory]
    [InlineData("CALCULATED")]
    [InlineData("APPROVED")]
    [InlineData("POSTED")]
    public async Task ValidatePreCalculationAsync_WhenExistingRunNotCancelled_ReturnsError(string runStatus)
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var existingRun = new PayrollRun
        {
            Id = 99,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = runStatus
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRun);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee>());

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains($"already has a {runStatus} run"));
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenExistingRunIsCancelled_AllowsCalculation()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var cancelledRun = new PayrollRun
        {
            Id = 99,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "CANCELLED"
        };
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            NameLocal = "أحمد",
            SalaryStructures = new List<SalaryStructure>
            {
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 1, 1), BasicSalary = 1000m, CurrencyCode = "JOD" }
            }
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cancelledRun);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { emp });
        _policyRepoMock.Setup(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationPolicy { Method = "CALENDAR_DAYS" });
        _policyRepoMock.Setup(p => p.GetActiveSSCPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SSCPolicy { Id = 1 });
        _policyRepoMock.Setup(p => p.GetActiveTaxPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxPolicy { Id = 1 });

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenPoliciesMissing_AddsWarnings()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            NameLocal = "أحمد",
            SalaryStructures = new List<SalaryStructure>
            {
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 1, 1), BasicSalary = 1000m, CurrencyCode = "JOD" }
            }
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { emp });
        _policyRepoMock.Setup(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProrationPolicy?)null);
        _policyRepoMock.Setup(p => p.GetActiveSSCPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SSCPolicy?)null);
        _policyRepoMock.Setup(p => p.GetActiveTaxPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxPolicy?)null);

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.True(result.IsValid);
        Assert.Equal(3, result.Warnings.Count);
        Assert.Contains(result.Warnings, w => w.Contains("No active Proration Policy"));
        Assert.Contains(result.Warnings, w => w.Contains("No active SSC Policy"));
        Assert.Contains(result.Warnings, w => w.Contains("No active Tax Policy"));
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenNoActiveEmployeesFound_ReturnsError()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee>()); // 0 employees

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("No active employees found eligible"));
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenEmployeeHasNoActiveSalary_AddsWarningAndError()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var empWithoutSalary = new Employee
        {
            EmployeeCode = "EMP_NOSAL",
            NameLocal = "بدون راتب",
            HireDate = new DateTime(2020, 1, 1),
            SalaryStructures = new List<SalaryStructure>()
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { empWithoutSalary });

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.False(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("has no active salary structure"));
        Assert.Contains(result.Errors, e => e.Contains("has no salary covering all eligible payroll dates"));
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenFixed30TransitionWithoutAllocationPolicy_ReturnsError()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        // 2 salary segments within the month:
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            NameLocal = "علي",
            SalaryStructures = new List<SalaryStructure>
            {
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2026, 11, 14), BasicSalary = 1000m, CurrencyCode = "JOD" },
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 11, 15), EffectiveTo = null, BasicSalary = 1200m, CurrencyCode = "JOD" }
            }
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { emp });
        // FIXED_30 without CALENDAR_WEIGHTED or WORKING_WEIGHTED
        _policyRepoMock.Setup(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationPolicy { Method = "FIXED_30", SalarySegmentAllocation = "INVALID_OR_NULL" });

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Configure a segment allocation policy before calculation"));
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenAttendanceContextHasReviewDays_ReturnsError()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            NameLocal = "محمود",
            SalaryStructures = new List<SalaryStructure>
            {
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 1, 1), BasicSalary = 1000m, CurrencyCode = "JOD" }
            }
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { emp });
        _attendanceContextMock.Setup(a => a.HasReviewDaysAsync(It.IsAny<string[]>(), period.StartDate, period.EndDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Attendance has unresolved review-required days"));
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_WhenRunNotFound_ReturnsError()
    {
        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);

        var result = await _service.ValidatePostApprovalAsync(999);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Payroll run with ID 999 not found"));
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_WhenRunStatusNotCalculated_ReturnsError()
    {
        var run = new PayrollRun
        {
            Id = 10,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "DRAFT",
            Lines = new List<PayrollRunLine> { new() { Id = 1, EmployeeCode = "EMP01", CurrencyCode = "JOD", NetPay = 500m } }
        };
        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _periodRepoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(new PayrollPeriod { Status = "OPEN" });

        var result = await _service.ValidatePostApprovalAsync(10);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("is in 'DRAFT' status and cannot be approved"));
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_WhenRunHasNoLines_ReturnsError()
    {
        var run = new PayrollRun
        {
            Id = 11,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>()
        };
        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _periodRepoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(new PayrollPeriod { Status = "OPEN" });

        var result = await _service.ValidatePostApprovalAsync(11);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("has no lines to approve"));
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_WhenMixedCurrenciesInLines_ReturnsError()
    {
        var run = new PayrollRun
        {
            Id = 12,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>
            {
                new() { Id = 1, EmployeeCode = "EMP01", CurrencyCode = "JOD", NetPay = 500m },
                new() { Id = 2, EmployeeCode = "EMP02", CurrencyCode = "USD", NetPay = 700m }
            }
        };
        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _periodRepoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(new PayrollPeriod { Status = "OPEN" });

        var result = await _service.ValidatePostApprovalAsync(12);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Payroll cannot combine different currencies"));
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_WhenOtherActiveRunExists_ReturnsError()
    {
        var run = new PayrollRun
        {
            Id = 13,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>
            {
                new() { Id = 1, EmployeeCode = "EMP01", CurrencyCode = "JOD", NetPay = 500m }
            }
        };
        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(13, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _periodRepoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(new PayrollPeriod { Status = "OPEN" });
        _periodRepoMock.Setup(r => r.HasOtherActivePayrollRunAsync("2026-11", 7, 13, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.ValidatePostApprovalAsync(13);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Another active payroll run exists"));
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_WhenPeriodMissingOrClosed_ReturnsError()
    {
        var run = new PayrollRun
        {
            Id = 14,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>
            {
                new() { Id = 1, EmployeeCode = "EMP01", CurrencyCode = "JOD", NetPay = 500m }
            }
        };
        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(14, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _periodRepoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollPeriod?)null);

        var result = await _service.ValidatePostApprovalAsync(14);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("The payroll period is missing or closed"));
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_WhenNegativeNetPayLinesExist_AddsWarningWithoutFailing()
    {
        var run = new PayrollRun
        {
            Id = 15,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>
            {
                new() { Id = 1, EmployeeCode = "EMP01", CurrencyCode = "JOD", NetPay = -25m },
                new() { Id = 2, EmployeeCode = "EMP02", CurrencyCode = "JOD", NetPay = 500m }
            }
        };
        var period = new PayrollPeriod { Status = "OPEN", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };

        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(15, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _periodRepoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.HasOtherActivePayrollRunAsync("2026-11", 7, 15, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _attendanceContextMock.Setup(a => a.HasReviewDaysAsync(It.IsAny<string[]>(), period.StartDate, period.EndDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ValidatePostApprovalAsync(15);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings);
        Assert.Contains("1 employee(s) with negative net pay", result.Warnings[0]);
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_WhenAttendanceContextHasReviewDays_ReturnsError()
    {
        var run = new PayrollRun
        {
            Id = 16,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>
            {
                new() { Id = 1, EmployeeCode = "EMP01", CurrencyCode = "JOD", NetPay = 500m }
            }
        };
        var period = new PayrollPeriod { Status = "OPEN", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };

        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(16, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _periodRepoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.HasOtherActivePayrollRunAsync("2026-11", 7, 16, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _attendanceContextMock.Setup(a => a.HasReviewDaysAsync(It.IsAny<string[]>(), period.StartDate, period.EndDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.ValidatePostApprovalAsync(16);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Attendance has unresolved review-required days"));
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_HappyPath_ReturnsValidWithNoErrors()
    {
        var run = new PayrollRun
        {
            Id = 17,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>
            {
                new() { Id = 1, EmployeeCode = "EMP01", CurrencyCode = "JOD", NetPay = 500m },
                new() { Id = 2, EmployeeCode = "EMP02", CurrencyCode = "JOD", NetPay = 600m }
            }
        };
        var period = new PayrollPeriod { Status = "OPEN", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };

        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(17, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _periodRepoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.HasOtherActivePayrollRunAsync("2026-11", 7, 17, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _attendanceContextMock.Setup(a => a.HasReviewDaysAsync(It.IsAny<string[]>(), period.StartDate, period.EndDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ValidatePostApprovalAsync(17);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ValidatePreCalculationAsync(1, "2026-11", 7, cts.Token));
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(99, cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ValidatePostApprovalAsync(99, cts.Token));
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenMultipleEmployeesHaveSalaryGaps_AggregatesAllErrors()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var emp1 = new Employee
        {
            EmployeeCode = "EMP01",
            NameLocal = "موظف 1",
            SalaryStructures = new List<SalaryStructure>()
        };
        var emp2 = new Employee
        {
            EmployeeCode = "EMP02",
            NameLocal = "موظف 2",
            SalaryStructures = new List<SalaryStructure>()
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee> { emp1, emp2 });
        _policyRepoMock.Setup(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationPolicy { Method = "CALENDAR_DAYS" });
        _policyRepoMock.Setup(p => p.GetActiveSSCPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SSCPolicy { Id = 1 });
        _policyRepoMock.Setup(p => p.GetActiveTaxPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxPolicy { Id = 1 });

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Warnings.Count);
        Assert.Contains(result.Warnings, w => w.Contains("EMP01"));
        Assert.Contains(result.Warnings, w => w.Contains("EMP02"));
        Assert.Contains(result.Errors, e => e.Contains("EMP01"));
        Assert.Contains(result.Errors, e => e.Contains("EMP02"));
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenBranchIdIsNull_ValidatesCompanyLevelEmployees()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var emp = new Employee
        {
            EmployeeCode = "EMP_ALL",
            NameLocal = "موظف عام",
            SalaryStructures = new List<SalaryStructure>
            {
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 1, 1), BasicSalary = 1000m, CurrencyCode = "JOD" }
            }
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", null, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee> { emp });
        _policyRepoMock.Setup(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationPolicy { Method = "CALENDAR_DAYS" });
        _policyRepoMock.Setup(p => p.GetActiveSSCPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SSCPolicy { Id = 1 });
        _policyRepoMock.Setup(p => p.GetActiveTaxPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxPolicy { Id = 1 });

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", null);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenAttendanceContextIsNull_SkipsReviewDaysCheck()
    {
        var serviceWithoutAttendance = new PayrollValidationService(_periodRepoMock.Object, _policyRepoMock.Object, attendanceContext: null);
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            NameLocal = "أحمد",
            SalaryStructures = new List<SalaryStructure>
            {
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 1, 1), BasicSalary = 1000m, CurrencyCode = "JOD" }
            }
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee> { emp });
        _policyRepoMock.Setup(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationPolicy { Method = "CALENDAR_DAYS" });
        _policyRepoMock.Setup(p => p.GetActiveSSCPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SSCPolicy { Id = 1 });
        _policyRepoMock.Setup(p => p.GetActiveTaxPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxPolicy { Id = 1 });

        var result = await serviceWithoutAttendance.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_WhenAttendanceContextIsNull_SkipsReviewDaysCheck()
    {
        var serviceWithoutAttendance = new PayrollValidationService(_periodRepoMock.Object, _policyRepoMock.Object, attendanceContext: null);
        var run = new PayrollRun
        {
            Id = 18,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>
            {
                new() { Id = 1, EmployeeCode = "EMP01", CurrencyCode = "JOD", NetPay = 500m }
            }
        };
        var period = new PayrollPeriod { Status = "OPEN", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };

        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(18, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _periodRepoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.HasOtherActivePayrollRunAsync("2026-11", 7, 18, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await serviceWithoutAttendance.ValidatePostApprovalAsync(18);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenSalaryStructureHasMixedCurrencies_CapturesExceptionMessage()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var emp = new Employee
        {
            EmployeeCode = "EMP_MIXED",
            NameLocal = "عملات متعددة",
            SalaryStructures = new List<SalaryStructure>
            {
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2026, 11, 14), BasicSalary = 1000m, CurrencyCode = "JOD" },
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 11, 15), EffectiveTo = null, BasicSalary = 1500m, CurrencyCode = "USD" }
            }
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee> { emp });
        _policyRepoMock.Setup(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationPolicy { Method = "CALENDAR_DAYS" });

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("cannot combine different currencies"));
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_WhenMultipleNegativeNetPayLines_AccuratelyCountsNegativeEmployees()
    {
        var run = new PayrollRun
        {
            Id = 19,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>
            {
                new() { Id = 1, EmployeeCode = "EMP01", CurrencyCode = "JOD", NetPay = -100m },
                new() { Id = 2, EmployeeCode = "EMP02", CurrencyCode = "JOD", NetPay = -50m },
                new() { Id = 3, EmployeeCode = "EMP03", CurrencyCode = "JOD", NetPay = -0.01m },
                new() { Id = 4, EmployeeCode = "EMP04", CurrencyCode = "JOD", NetPay = 500m }
            }
        };
        var period = new PayrollPeriod { Status = "OPEN", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };

        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(19, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _periodRepoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.HasOtherActivePayrollRunAsync("2026-11", 7, 19, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _attendanceContextMock.Setup(a => a.HasReviewDaysAsync(It.IsAny<string[]>(), period.StartDate, period.EndDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ValidatePostApprovalAsync(19);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings);
        Assert.Contains("3 employee(s) with negative net pay", result.Warnings[0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidatePostApprovalAsync_WhenRunLinesHaveBlankCurrency_ReturnsError(string blankCurrency)
    {
        var run = new PayrollRun
        {
            Id = 20,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>
            {
                new() { Id = 1, EmployeeCode = "EMP01", CurrencyCode = blankCurrency, NetPay = 500m }
            }
        };
        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _periodRepoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(new PayrollPeriod { Status = "OPEN" });

        var result = await _service.ValidatePostApprovalAsync(20);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Payroll currency is missing"));
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_WhenPeriodClosed_ReturnsError()
    {
        var run = new PayrollRun
        {
            Id = 21,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>
            {
                new() { Id = 1, EmployeeCode = "EMP01", CurrencyCode = "JOD", NetPay = 500m }
            }
        };
        var closedPeriod = new PayrollPeriod { Status = "CLOSED", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };

        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(21, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _periodRepoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(closedPeriod);

        var result = await _service.ValidatePostApprovalAsync(21);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("The payroll period is missing or closed"));
    }

    [Theory]
    [InlineData("CALENDAR_WEIGHTED")]
    [InlineData("WORKING_WEIGHTED")]
    public async Task ValidatePreCalculationAsync_WhenFixed30TransitionHasWeightedAllocation_PassesValidation(string allocationMethod)
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            NameLocal = "علي",
            SalaryStructures = new List<SalaryStructure>
            {
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2026, 11, 14), BasicSalary = 1000m, CurrencyCode = "JOD" },
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 11, 15), EffectiveTo = null, BasicSalary = 1200m, CurrencyCode = "JOD" }
            }
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee> { emp });
        _policyRepoMock.Setup(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationPolicy { Method = "FIXED_30", SalarySegmentAllocation = allocationMethod });
        _policyRepoMock.Setup(p => p.GetActiveSSCPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SSCPolicy { Id = 1 });
        _policyRepoMock.Setup(p => p.GetActiveTaxPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxPolicy { Id = 1 });

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ValidatePreCalculationAsync(1, "2026-11", 7, cts.Token));
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_WhenCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(99, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ValidatePostApprovalAsync(99, cts.Token));
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenAllPoliciesActive_DoesNotAddPolicyWarnings()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            NameLocal = "أحمد",
            SalaryStructures = new List<SalaryStructure>
            {
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = null, BasicSalary = 1000m, CurrencyCode = "JOD" }
            }
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee> { emp });
        _policyRepoMock.Setup(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationPolicy { Method = "CALENDAR_DAYS" });
        _policyRepoMock.Setup(p => p.GetActiveSSCPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SSCPolicy { Id = 1 });
        _policyRepoMock.Setup(p => p.GetActiveTaxPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxPolicy { Id = 1 });

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenOnlySSCPolicyMissing_AddsOnlySSCWarning()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            NameLocal = "أحمد",
            SalaryStructures = new List<SalaryStructure>
            {
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = null, BasicSalary = 1000m, CurrencyCode = "JOD" }
            }
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee> { emp });
        _policyRepoMock.Setup(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationPolicy { Method = "CALENDAR_DAYS" });
        _policyRepoMock.Setup(p => p.GetActiveSSCPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SSCPolicy?)null);
        _policyRepoMock.Setup(p => p.GetActiveTaxPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxPolicy { Id = 1 });

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings);
        Assert.Contains("No active SSC Policy found", result.Warnings[0]);
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenOnlyTaxPolicyMissing_AddsOnlyTaxWarning()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            NameLocal = "أحمد",
            SalaryStructures = new List<SalaryStructure>
            {
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = null, BasicSalary = 1000m, CurrencyCode = "JOD" }
            }
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee> { emp });
        _policyRepoMock.Setup(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationPolicy { Method = "CALENDAR_DAYS" });
        _policyRepoMock.Setup(p => p.GetActiveSSCPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SSCPolicy { Id = 1 });
        _policyRepoMock.Setup(p => p.GetActiveTaxPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxPolicy?)null);

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings);
        Assert.Contains("No active Tax Policy found", result.Warnings[0]);
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_WhenOnlyProrationPolicyMissing_AddsOnlyProrationWarning()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            NameLocal = "أحمد",
            SalaryStructures = new List<SalaryStructure>
            {
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = null, BasicSalary = 1000m, CurrencyCode = "JOD" }
            }
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee> { emp });
        _policyRepoMock.Setup(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProrationPolicy?)null);
        _policyRepoMock.Setup(p => p.GetActiveSSCPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SSCPolicy { Id = 1 });
        _policyRepoMock.Setup(p => p.GetActiveTaxPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxPolicy { Id = 1 });

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings);
        Assert.Contains("No active Proration Policy found", result.Warnings[0]);
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_WhenNetPayExactlyZero_DoesNotAddNegativeNetPayWarning()
    {
        var run = new PayrollRun
        {
            Id = 30,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>
            {
                new() { Id = 1, EmployeeCode = "EMP01", CurrencyCode = "JOD", NetPay = 0.00m },
                new() { Id = 2, EmployeeCode = "EMP02", CurrencyCode = "JOD", NetPay = 1500m }
            }
        };
        var period = new PayrollPeriod { Status = "OPEN", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 30) };

        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(30, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _periodRepoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.HasOtherActivePayrollRunAsync("2026-11", 7, 30, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _service.ValidatePostApprovalAsync(30);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ValidatePostApprovalAsync_WhenPeriodNotFound_ReturnsMissingOrClosedError()
    {
        var run = new PayrollRun
        {
            Id = 31,
            PayPeriod = "2026-11",
            BranchId = 7,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>
            {
                new() { Id = 1, EmployeeCode = "EMP01", CurrencyCode = "JOD", NetPay = 500m }
            }
        };

        _periodRepoMock.Setup(r => r.GetPayrollRunByIdAsync(31, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _periodRepoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollPeriod?)null);

        var result = await _service.ValidatePostApprovalAsync(31);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("The payroll period is missing or closed and cannot be approved."));
    }

    [Fact]
    public async Task ValidatePreCalculationAsync_MultipleEmployees_OneValidOneWithoutSalary_AddsWarningForInvalidOnly()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-11",
            Status = "OPEN",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };
        var validEmp = new Employee
        {
            EmployeeCode = "EMP01",
            NameLocal = "سعيد",
            SalaryStructures = new List<SalaryStructure>
            {
                new() { IsActive = true, EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = null, BasicSalary = 1000m, CurrencyCode = "JOD" }
            }
        };
        var invalidEmp = new Employee
        {
            EmployeeCode = "EMP02",
            NameLocal = "خالد",
            SalaryStructures = new List<SalaryStructure>()
        };

        _periodRepoMock.Setup(r => r.GetPeriodByCodeAsync(1, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _periodRepoMock.Setup(r => r.GetPayrollRunByPeriodAsync("2026-11", 7L, It.IsAny<CancellationToken>())).ReturnsAsync((PayrollRun?)null);
        _periodRepoMock.Setup(r => r.GetActiveEmployeesAsync(7L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { validEmp, invalidEmp });
        _policyRepoMock.Setup(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationPolicy { Method = "CALENDAR_DAYS" });
        _policyRepoMock.Setup(p => p.GetActiveSSCPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SSCPolicy { Id = 1 });
        _policyRepoMock.Setup(p => p.GetActiveTaxPolicyAsync(1, period.StartDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxPolicy { Id = 1 });

        var result = await _service.ValidatePreCalculationAsync(1, "2026-11", 7);

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("EMP02", warning);
        Assert.Contains("خالد", warning);
    }
}
