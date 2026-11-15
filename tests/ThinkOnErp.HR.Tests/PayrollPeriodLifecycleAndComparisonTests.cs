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

public class PayrollPeriodLifecycleAndComparisonTests
{
    private readonly Mock<IPayrollPeriodRepository> _repoMock = new();
    private readonly Mock<IPayrollCalculationEngine> _engineMock = new();
    private readonly Mock<IPayrollValidationService> _validationMock = new();
    private readonly PayrollService _service;

    public PayrollPeriodLifecycleAndComparisonTests()
    {
        _service = new PayrollService(
            _repoMock.Object,
            _engineMock.Object,
            _validationMock.Object
        );
    }

    [Fact]
    public async Task ClosePeriod_SuccessfullyCloses_WhenAllRunsFinalizedAndNoPendingAdjustments()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 10,
            PeriodCode = "2026-10",
            Status = "OPEN",
            FiscalYear = 2026,
            Month = 10
        };

        var runs = new List<PayrollRun>
        {
            new() { Id = 101, PayPeriod = "2026-10", Status = "APPROVED" },
            new() { Id = 102, PayPeriod = "2026-10", Status = "SUPERSEDED" }
        };

        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _repoMock.Setup(r => r.GetRunsByPeriodAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);
        _repoMock.Setup(r => r.HasPendingAdjustmentsForPeriodAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ClosePeriodAsync(10, "2026-10", "End of month payroll finalized", "HR_ADMIN");

        Assert.Equal("CLOSED", result.Status);
        Assert.Equal("HR_ADMIN", result.UpdateUser);
        Assert.NotNull(result.UpdateDate);
        _repoMock.Verify(r => r.UpdatePeriodAsync(period, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClosePeriod_Throws_WhenPeriodAlreadyClosed()
    {
        var period = new PayrollPeriod { Id = 1, CompanyId = 10, PeriodCode = "2026-10", Status = "CLOSED" };
        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ClosePeriodAsync(10, "2026-10", "Closing again", "HR_ADMIN"));

        Assert.Contains("already closed", ex.Message);
    }

    [Fact]
    public async Task ClosePeriod_Throws_WhenUnfinalizedRunsExist()
    {
        var period = new PayrollPeriod { Id = 1, CompanyId = 10, PeriodCode = "2026-10", Status = "OPEN" };
        var runs = new List<PayrollRun>
        {
            new() { Id = 101, PayPeriod = "2026-10", Status = "APPROVED" },
            new() { Id = 102, PayPeriod = "2026-10", Status = "CALCULATED" }
        };

        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _repoMock.Setup(r => r.GetRunsByPeriodAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ClosePeriodAsync(10, "2026-10", "Closing with draft", "HR_ADMIN"));

        Assert.Contains("unfinalized payroll runs", ex.Message);
        Assert.Contains("102", ex.Message);
    }

    [Fact]
    public async Task ClosePeriod_Throws_WhenPendingAdjustmentsExist()
    {
        var period = new PayrollPeriod { Id = 1, CompanyId = 10, PeriodCode = "2026-10", Status = "OPEN" };
        var runs = new List<PayrollRun> { new() { Id = 101, PayPeriod = "2026-10", Status = "APPROVED" } };

        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _repoMock.Setup(r => r.GetRunsByPeriodAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);
        _repoMock.Setup(r => r.HasPendingAdjustmentsForPeriodAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ClosePeriodAsync(10, "2026-10", "Closing with pending", "HR_ADMIN"));

        Assert.Contains("pending payroll adjustments", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ClosePeriod_Throws_WhenReasonIsMissing(string? reason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ClosePeriodAsync(10, "2026-10", reason!, "HR_ADMIN"));
    }

    [Fact]
    public async Task ReopenPeriod_SuccessfullyReopens_WhenNoSubsequentClosedPeriods()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 10,
            PeriodCode = "2026-10",
            Status = "CLOSED",
            FiscalYear = 2026,
            Month = 10
        };

        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _repoMock.Setup(r => r.HasSubsequentClosedPeriodsAsync(10, 2026, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ReopenPeriodAsync(10, "2026-10", "Audit correction requested by CFO", "HR_ADMIN");

        Assert.Equal("OPEN", result.Status);
        Assert.Equal("HR_ADMIN", result.UpdateUser);
        Assert.NotNull(result.UpdateDate);
        _repoMock.Verify(r => r.UpdatePeriodAsync(period, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReopenPeriod_Throws_WhenPeriodNotClosed()
    {
        var period = new PayrollPeriod { Id = 1, CompanyId = 10, PeriodCode = "2026-10", Status = "OPEN" };
        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ReopenPeriodAsync(10, "2026-10", "Legitimate reason for reopen", "HR_ADMIN"));

        Assert.Contains("is not closed", ex.Message);
    }

    [Fact]
    public async Task ReopenPeriod_Throws_WhenSubsequentClosedPeriodsExist()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 10,
            PeriodCode = "2026-09",
            Status = "CLOSED",
            FiscalYear = 2026,
            Month = 9
        };

        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-09", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _repoMock.Setup(r => r.HasSubsequentClosedPeriodsAsync(10, 2026, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ReopenPeriodAsync(10, "2026-09", "Need to adjust September after October closed", "HR_ADMIN"));

        Assert.Contains("subsequent periods are already closed", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad")] // less than 5 characters
    [InlineData("   ")]
    public async Task ReopenPeriod_Throws_WhenReasonIsInvalid(string reason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ReopenPeriodAsync(10, "2026-10", reason, "HR_ADMIN"));
    }

    [Fact]
    public async Task ComparePayrollRun_InitialRun_AllEmployeesAreNew()
    {
        var run = new PayrollRun
        {
            Id = 501,
            PayPeriod = "2026-10",
            BranchId = 2,
            Status = "CALCULATED",
            CorrectionVersion = 1,
            TotalGrossSalary = 2500m,
            TotalNetSalary = 2100m,
            TotalEmployeeSsc = 150m,
            TotalIncomeTax = 100m,
            TotalOtherDeductions = 150m,
            Lines = new List<PayrollRunLine>
            {
                new()
                {
                    EmployeeCode = "EMP01",
                    BeneficiaryName = "Ali",
                    BasicSalary = 1000m,
                    GrossSalary = 1200m,
                    TotalEarnings = 200m,
                    NetPay = 1050m
                },
                new()
                {
                    EmployeeCode = "EMP02",
                    BeneficiaryName = "Sara",
                    BasicSalary = 1100m,
                    GrossSalary = 1300m,
                    TotalEarnings = 200m,
                    NetPay = 1050m
                }
            }
        };

        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(501, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        _repoMock.Setup(r => r.GetPreviousPayrollRunAsync(2, "2026-10", 501, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);

        var comparison = await _service.ComparePayrollRunAsync(501);

        Assert.Equal(501, comparison.CurrentRunId);
        Assert.Null(comparison.BaselineRunId);
        Assert.Equal(2, comparison.Summary.CurrentEmployeeCount);
        Assert.Equal(0, comparison.Summary.BaselineEmployeeCount);
        Assert.Equal(2, comparison.Summary.AddedEmployeesCount);
        Assert.Equal(0, comparison.Summary.RemovedEmployeesCount);
        Assert.Equal(2500m, comparison.Summary.GrossSalaryVariance);
        Assert.Equal(2100m, comparison.Summary.NetPayVariance);

        Assert.All(comparison.EmployeeVariances, v => Assert.Equal("NEW", v.ChangeType));
    }

    [Fact]
    public async Task ComparePayrollRun_WithBaseline_DetectsVariancesAndReasons()
    {
        var baseline = new PayrollRun
        {
            Id = 401,
            PayPeriod = "2026-09",
            BranchId = 2,
            Status = "APPROVED",
            CorrectionVersion = 1,
            TotalGrossSalary = 3000m,
            TotalNetSalary = 2600m,
            TotalEmployeeSsc = 200m,
            TotalIncomeTax = 100m,
            TotalOtherDeductions = 100m,
            Lines = new List<PayrollRunLine>
            {
                new()
                {
                    EmployeeCode = "EMP01",
                    BeneficiaryName = "Ali",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    NetPay = 900m
                },
                new()
                {
                    EmployeeCode = "EMP02",
                    BeneficiaryName = "Sara",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    NetPay = 900m
                },
                new()
                {
                    EmployeeCode = "EMP03_LEAVING",
                    BeneficiaryName = "Omar",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    NetPay = 800m
                }
            }
        };

        var current = new PayrollRun
        {
            Id = 501,
            PayPeriod = "2026-10",
            BranchId = 2,
            Status = "CALCULATED",
            CorrectionVersion = 1,
            TotalGrossSalary = 3100m,
            TotalNetSalary = 2650m,
            TotalEmployeeSsc = 200m,
            TotalIncomeTax = 110m,
            TotalOtherDeductions = 140m,
            Lines = new List<PayrollRunLine>
            {
                new()
                {
                    EmployeeCode = "EMP01", // Basic increased from 1000 to 1200
                    BeneficiaryName = "Ali",
                    BasicSalary = 1200m,
                    GrossSalary = 1200m,
                    NetPay = 1050m
                },
                new()
                {
                    EmployeeCode = "EMP02", // Unpaid leave deduction applied
                    BeneficiaryName = "Sara",
                    BasicSalary = 1000m,
                    GrossSalary = 900m,
                    NetPay = 800m,
                    Components = new List<PayrollRunLineComponent>
                    {
                        new() { ComponentCode = "UNPAID_LEAVE", Amount = 100m }
                    }
                },
                new()
                {
                    EmployeeCode = "EMP04_JOINING", // New employee
                    BeneficiaryName = "Layla",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    NetPay = 800m
                }
            }
        };

        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(501, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(401, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        var comparison = await _service.ComparePayrollRunAsync(501, baselineRunId: 401);

        Assert.Equal(501, comparison.CurrentRunId);
        Assert.Equal(401, comparison.BaselineRunId);
        Assert.Equal(3, comparison.Summary.CurrentEmployeeCount);
        Assert.Equal(3, comparison.Summary.BaselineEmployeeCount);
        Assert.Equal(1, comparison.Summary.AddedEmployeesCount); // Layla
        Assert.Equal(1, comparison.Summary.RemovedEmployeesCount); // Omar
        Assert.Equal(2, comparison.Summary.RetainedEmployeesCount); // Ali, Sara

        // Check specific employee variances
        var ali = Assert.Single(comparison.EmployeeVariances, v => v.EmployeeCode == "EMP01");
        Assert.Equal("INCREASED", ali.ChangeType);
        Assert.Equal(150m, ali.NetPayVariance);
        Assert.Contains("Basic salary changed from 1000", ali.PrimaryReason);

        var sara = Assert.Single(comparison.EmployeeVariances, v => v.EmployeeCode == "EMP02");
        Assert.Equal("DECREASED", sara.ChangeType);
        Assert.Equal(-100m, sara.NetPayVariance);
        Assert.Contains("Unpaid leave", sara.PrimaryReason);

        var layla = Assert.Single(comparison.EmployeeVariances, v => v.EmployeeCode == "EMP04_JOINING");
        Assert.Equal("NEW", layla.ChangeType);
        Assert.Equal(800m, layla.NetPayVariance);

        var omar = Assert.Single(comparison.EmployeeVariances, v => v.EmployeeCode == "EMP03_LEAVING");
        Assert.Equal("REMOVED", omar.ChangeType);
        Assert.Equal(-800m, omar.NetPayVariance);
    }

    [Fact]
    public async Task ClosePeriod_Throws_WhenPeriodNotFound()
    {
        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollPeriod?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ClosePeriodAsync(10, "2026-10", "Period not found close", "HR_ADMIN"));

        Assert.Contains("Payroll period '2026-10' not found", ex.Message);
    }

    [Fact]
    public async Task ReopenPeriod_Throws_WhenPeriodNotFound()
    {
        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollPeriod?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ReopenPeriodAsync(10, "2026-10", "Period not found reopen", "HR_ADMIN"));

        Assert.Contains("Payroll period '2026-10' not found", ex.Message);
    }

    [Fact]
    public async Task ComparePayrollRun_Throws_WhenRunNotFound()
    {
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ComparePayrollRunAsync(999));

        Assert.Contains("Payroll run 999 not found", ex.Message);
    }

    [Fact]
    public async Task ComparePayrollRun_WithBaseline_AllEmployeesUnchanged_ProducesZeroVariance()
    {
        var run1 = new PayrollRun
        {
            Id = 601,
            PayPeriod = "2026-09",
            BranchId = 3,
            Status = "APPROVED",
            TotalGrossSalary = 2000m,
            TotalNetSalary = 1800m,
            Lines = new List<PayrollRunLine>
            {
                new()
                {
                    EmployeeCode = "EMP10",
                    BeneficiaryName = "Khalid",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    TotalEarnings = 0m,
                    OtherDeductions = 0m,
                    IncomeTaxWithheld = 50m,
                    SscEmployeeContrib = 50m,
                    NetPay = 900m
                },
                new()
                {
                    EmployeeCode = "EMP20",
                    BeneficiaryName = "Nour",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    TotalEarnings = 0m,
                    OtherDeductions = 0m,
                    IncomeTaxWithheld = 50m,
                    SscEmployeeContrib = 50m,
                    NetPay = 900m
                }
            }
        };

        var run2 = new PayrollRun
        {
            Id = 602,
            PayPeriod = "2026-10",
            BranchId = 3,
            Status = "CALCULATED",
            TotalGrossSalary = 2000m,
            TotalNetSalary = 1800m,
            Lines = new List<PayrollRunLine>
            {
                new()
                {
                    EmployeeCode = "EMP10",
                    BeneficiaryName = "Khalid",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    TotalEarnings = 0m,
                    OtherDeductions = 0m,
                    IncomeTaxWithheld = 50m,
                    SscEmployeeContrib = 50m,
                    NetPay = 900m
                },
                new()
                {
                    EmployeeCode = "EMP20",
                    BeneficiaryName = "Nour",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    TotalEarnings = 0m,
                    OtherDeductions = 0m,
                    IncomeTaxWithheld = 50m,
                    SscEmployeeContrib = 50m,
                    NetPay = 900m
                }
            }
        };

        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(602, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run2);
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(601, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run1);

        var comparison = await _service.ComparePayrollRunAsync(602, baselineRunId: 601);

        Assert.Equal(602, comparison.CurrentRunId);
        Assert.Equal(601, comparison.BaselineRunId);
        Assert.Equal(0, comparison.Summary.AddedEmployeesCount);
        Assert.Equal(0, comparison.Summary.RemovedEmployeesCount);
        Assert.Equal(2, comparison.Summary.RetainedEmployeesCount);
        Assert.Equal(0m, comparison.Summary.NetPayVariance);
        Assert.Equal(0m, comparison.Summary.GrossSalaryVariance);

        Assert.All(comparison.EmployeeVariances, v =>
        {
            Assert.Equal("UNCHANGED", v.ChangeType);
            Assert.Equal(0m, v.NetPayVariance);
            Assert.Equal("No variance", v.PrimaryReason);
        });
    }

    [Fact]
    public async Task ComparePayrollRun_DetectsAllowanceAndOtherDeductionsAndTaxVariances()
    {
        var baseline = new PayrollRun
        {
            Id = 701,
            PayPeriod = "2026-09",
            BranchId = 3,
            Status = "APPROVED",
            TotalGrossSalary = 3000m,
            TotalNetSalary = 2700m,
            Lines = new List<PayrollRunLine>
            {
                new()
                {
                    EmployeeCode = "EMP_ALLOWANCE",
                    BeneficiaryName = "Employee A",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    TotalEarnings = 0m,
                    OtherDeductions = 0m,
                    NetPay = 900m
                },
                new()
                {
                    EmployeeCode = "EMP_LOAN",
                    BeneficiaryName = "Employee B",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    TotalEarnings = 0m,
                    OtherDeductions = 0m,
                    NetPay = 900m
                },
                new()
                {
                    EmployeeCode = "EMP_STATUTORY",
                    BeneficiaryName = "Employee C",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    TotalEarnings = 0m,
                    OtherDeductions = 0m,
                    IncomeTaxWithheld = 50m,
                    SscEmployeeContrib = 50m,
                    NetPay = 900m
                }
            }
        };

        var current = new PayrollRun
        {
            Id = 702,
            PayPeriod = "2026-10",
            BranchId = 3,
            Status = "CALCULATED",
            TotalGrossSalary = 3200m,
            TotalNetSalary = 2650m,
            Lines = new List<PayrollRunLine>
            {
                new()
                {
                    EmployeeCode = "EMP_ALLOWANCE", // TotalEarnings increased by 200
                    BeneficiaryName = "Employee A",
                    BasicSalary = 1000m,
                    GrossSalary = 1200m,
                    TotalEarnings = 200m,
                    OtherDeductions = 0m,
                    NetPay = 1100m
                },
                new()
                {
                    EmployeeCode = "EMP_LOAN", // OtherDeductions increased by 100
                    BeneficiaryName = "Employee B",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    TotalEarnings = 0m,
                    OtherDeductions = 100m,
                    NetPay = 800m
                },
                new()
                {
                    EmployeeCode = "EMP_STATUTORY", // IncomeTax increased by 50
                    BeneficiaryName = "Employee C",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    TotalEarnings = 0m,
                    OtherDeductions = 0m,
                    IncomeTaxWithheld = 100m,
                    SscEmployeeContrib = 50m,
                    NetPay = 850m
                }
            }
        };

        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(702, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(701, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        var comparison = await _service.ComparePayrollRunAsync(702, baselineRunId: 701);

        var empA = Assert.Single(comparison.EmployeeVariances, v => v.EmployeeCode == "EMP_ALLOWANCE");
        Assert.Equal("INCREASED", empA.ChangeType);
        Assert.Equal("Allowance / earnings variance", empA.PrimaryReason);

        var empB = Assert.Single(comparison.EmployeeVariances, v => v.EmployeeCode == "EMP_LOAN");
        Assert.Equal("DECREASED", empB.ChangeType);
        Assert.Equal("Loan / other deduction variance", empB.PrimaryReason);

        var empC = Assert.Single(comparison.EmployeeVariances, v => v.EmployeeCode == "EMP_STATUTORY");
        Assert.Equal("DECREASED", empC.ChangeType);
        Assert.Equal("Statutory deduction (Tax/SSC) variance", empC.PrimaryReason);
    }

    [Fact]
    public async Task ComparePayrollRun_AutoResolvesPreviousRun_WhenBaselineRunIdNotProvided()
    {
        var baseline = new PayrollRun
        {
            Id = 801,
            PayPeriod = "2026-09",
            BranchId = 4,
            Status = "APPROVED",
            Lines = new List<PayrollRunLine>
            {
                new() { EmployeeCode = "EMP_AUTO", BeneficiaryName = "Tariq", BasicSalary = 1000m, GrossSalary = 1000m, NetPay = 900m }
            }
        };

        var current = new PayrollRun
        {
            Id = 802,
            PayPeriod = "2026-10",
            BranchId = 4,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>
            {
                new() { EmployeeCode = "EMP_AUTO", BeneficiaryName = "Tariq", BasicSalary = 1100m, GrossSalary = 1100m, NetPay = 1000m }
            }
        };

        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(802, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repoMock.Setup(r => r.GetPreviousPayrollRunAsync(4, "2026-10", 802, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        var comparison = await _service.ComparePayrollRunAsync(802, baselineRunId: null);

        Assert.Equal(802, comparison.CurrentRunId);
        Assert.Equal(801, comparison.BaselineRunId);
        _repoMock.Verify(r => r.GetPreviousPayrollRunAsync(4, "2026-10", 802, It.IsAny<CancellationToken>()), Times.Once);

        var emp = Assert.Single(comparison.EmployeeVariances);
        Assert.Equal("INCREASED", emp.ChangeType);
        Assert.Equal(100m, emp.NetPayVariance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ClosePeriod_WhenReasonNullOrWhitespace_ThrowsArgumentException(string? invalidReason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ClosePeriodAsync(10, "2026-10", invalidReason!, "HR_ADMIN"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("fix")]
    [InlineData("1234")]
    public async Task ReopenPeriod_WhenReasonNullOrTooShort_ThrowsArgumentException(string? invalidReason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ReopenPeriodAsync(10, "2026-10", invalidReason!, "HR_ADMIN"));
    }

    [Fact]
    public async Task ReopenPeriod_WhenPeriodNotFound_ThrowsKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-99", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollPeriod?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ReopenPeriodAsync(10, "2026-99", "Audit correction requested", "HR_ADMIN"));
    }

    [Fact]
    public async Task ReopenPeriod_WhenPeriodNotClosed_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod { Id = 1, CompanyId = 10, PeriodCode = "2026-10", Status = "OPEN" };
        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ReopenPeriodAsync(10, "2026-10", "Already open request", "HR_ADMIN"));

        Assert.Contains("is not closed (current status: OPEN)", ex.Message);
    }

    [Fact]
    public async Task ReopenPeriod_WhenSubsequentClosedPeriodsExist_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 10,
            PeriodCode = "2026-08",
            Status = "CLOSED",
            FiscalYear = 2026,
            Month = 8
        };

        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-08", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _repoMock.Setup(r => r.HasSubsequentClosedPeriodsAsync(10, 2026, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ReopenPeriodAsync(10, "2026-08", "Retroactive retroactive payroll fix", "HR_ADMIN"));

        Assert.Contains("Periods must be reopened in reverse chronological order", ex.Message);
    }

    [Fact]
    public async Task ReopenPeriod_SuccessfullyReopens_WhenClosedAndNoSubsequentClosedPeriods()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 10,
            PeriodCode = "2026-10",
            Status = "CLOSED",
            FiscalYear = 2026,
            Month = 10
        };

        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _repoMock.Setup(r => r.HasSubsequentClosedPeriodsAsync(10, 2026, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ReopenPeriodAsync(10, "2026-10", "Auditor requested recalculation", "HR_ADMIN");

        Assert.Equal("OPEN", result.Status);
        Assert.Equal("HR_ADMIN", result.UpdateUser);
        Assert.NotNull(result.UpdateDate);
        _repoMock.Verify(r => r.UpdatePeriodAsync(period, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePeriodAsync_PersistsPeriodAndReturnsIt()
    {
        var newPeriod = new PayrollPeriod
        {
            CompanyId = 10,
            PeriodCode = "2026-11",
            FiscalYear = 2026,
            Month = 11,
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30),
            Status = "OPEN"
        };

        var created = await _service.CreatePeriodAsync(newPeriod);

        Assert.Same(newPeriod, created);
        _repoMock.Verify(r => r.AddPeriodAsync(newPeriod, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ComparePayrollRun_WhenCurrentRunNotFound_ThrowsKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(9999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ComparePayrollRunAsync(9999));
    }

    [Fact]
    public async Task ComparePayrollRun_WhenExplicitBaselineNotFound_ThrowsKeyNotFoundException()
    {
        var current = new PayrollRun { Id = 100, PayPeriod = "2026-10" };
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(8888, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ComparePayrollRunAsync(100, baselineRunId: 8888));
    }

    [Fact]
    public async Task ComparePayrollRun_WhenBaselineHasSeparatedEmployee_ReportsRemovedWithNegativeVariance()
    {
        var baseline = new PayrollRun
        {
            Id = 901,
            PayPeriod = "2026-09",
            BranchId = 1,
            Status = "APPROVED",
            TotalGrossSalary = 1000m,
            TotalNetSalary = 900m,
            Lines = new List<PayrollRunLine>
            {
                new() { EmployeeCode = "EMP_TERM", BeneficiaryName = "Departed Employee", BasicSalary = 1000m, GrossSalary = 1000m, NetPay = 900m }
            }
        };

        var current = new PayrollRun
        {
            Id = 902,
            PayPeriod = "2026-10",
            BranchId = 1,
            Status = "CALCULATED",
            TotalGrossSalary = 0m,
            TotalNetSalary = 0m,
            Lines = new List<PayrollRunLine>() // Empty lines
        };

        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(902, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(901, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        var comparison = await _service.ComparePayrollRunAsync(902, baselineRunId: 901);

        Assert.Equal(0, comparison.Summary.CurrentEmployeeCount);
        Assert.Equal(1, comparison.Summary.BaselineEmployeeCount);
        Assert.Equal(1, comparison.Summary.RemovedEmployeesCount);

        var emp = Assert.Single(comparison.EmployeeVariances);
        Assert.Equal("EMP_TERM", emp.EmployeeCode);
        Assert.Equal("REMOVED", emp.ChangeType);
        Assert.Equal(-1000m, emp.GrossVariance);
        Assert.Equal(-900m, emp.NetPayVariance);
        Assert.Equal("Terminated / excluded from current payroll", emp.PrimaryReason);
    }

    [Fact]
    public async Task ComparePayrollRun_WhenUnpaidLeavePresent_IdentifiesUnpaidLeaveVarianceReason()
    {
        var baseline = new PayrollRun
        {
            Id = 911,
            PayPeriod = "2026-09",
            BranchId = 1,
            Status = "APPROVED",
            Lines = new List<PayrollRunLine>
            {
                new() { EmployeeCode = "EMP_LEAVE", BasicSalary = 1000m, GrossSalary = 1000m, NetPay = 900m }
            }
        };

        var current = new PayrollRun
        {
            Id = 912,
            PayPeriod = "2026-10",
            BranchId = 1,
            Status = "CALCULATED",
            Lines = new List<PayrollRunLine>
            {
                new()
                {
                    EmployeeCode = "EMP_LEAVE",
                    BasicSalary = 1000m,
                    GrossSalary = 900m,
                    NetPay = 810m,
                    Components = new List<PayrollRunLineComponent>
                    {
                        new() { ComponentCode = "UNPAID_LEAVE", Amount = 100m }
                    }
                }
            }
        };

        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(912, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(911, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        var comparison = await _service.ComparePayrollRunAsync(912, baselineRunId: 911);

        var emp = Assert.Single(comparison.EmployeeVariances);
        Assert.Equal("DECREASED", emp.ChangeType);
        Assert.Equal("Unpaid leave deduction variance", emp.PrimaryReason);
    }

    [Fact]
    public async Task ComparePayrollRun_WhenNoDifferencesExist_ReportsUnchangedAndZeroVariances()
    {
        var baseline = new PayrollRun
        {
            Id = 921,
            PayPeriod = "2026-09",
            BranchId = 1,
            Status = "APPROVED",
            TotalGrossSalary = 1200m,
            TotalNetSalary = 1050m,
            Lines = new List<PayrollRunLine>
            {
                new() { EmployeeCode = "EMP_STABLE", BeneficiaryName = "Same Same", BasicSalary = 1000m, GrossSalary = 1200m, NetPay = 1050m }
            }
        };

        var current = new PayrollRun
        {
            Id = 922,
            PayPeriod = "2026-10",
            BranchId = 1,
            Status = "CALCULATED",
            TotalGrossSalary = 1200m,
            TotalNetSalary = 1050m,
            Lines = new List<PayrollRunLine>
            {
                new() { EmployeeCode = "EMP_STABLE", BeneficiaryName = "Same Same", BasicSalary = 1000m, GrossSalary = 1200m, NetPay = 1050m }
            }
        };

        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(922, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(921, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        var comparison = await _service.ComparePayrollRunAsync(922, baselineRunId: 921);

        var emp = Assert.Single(comparison.EmployeeVariances);
        Assert.Equal("UNCHANGED", emp.ChangeType);
        Assert.Equal("No variance", emp.PrimaryReason);
        Assert.Equal(0m, emp.GrossVariance);
        Assert.Equal(0m, emp.NetPayVariance);
        Assert.Equal(0m, comparison.Summary.GrossSalaryVariance);
        Assert.Equal(0m, comparison.Summary.NetPayVariance);
    }

    [Fact]
    public async Task CreatePeriodAsync_CallsAddPeriodAndSaveChangesAsync()
    {
        var period = new PayrollPeriod
        {
            Id = 15,
            CompanyId = 10,
            PeriodCode = "2026-11",
            Status = "OPEN",
            FiscalYear = 2026,
            Month = 11,
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30)
        };

        _repoMock.Setup(r => r.AddPeriodAsync(period, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.CreatePeriodAsync(period);

        Assert.NotNull(result);
        Assert.Equal("2026-11", result.PeriodCode);
        _repoMock.Verify(r => r.AddPeriodAsync(period, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPeriodsAsync_PassesCompanyIdAndYearToRepository()
    {
        var expected = new List<PayrollPeriod>
        {
            new() { Id = 1, CompanyId = 10, PeriodCode = "2026-01", FiscalYear = 2026, Month = 1 }
        };

        _repoMock.Setup(r => r.GetPeriodsAsync(10, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetPeriodsAsync(10, 2026);

        Assert.Single(result);
        Assert.Equal("2026-01", result[0].PeriodCode);
        _repoMock.Verify(r => r.GetPeriodsAsync(10, 2026, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClosePeriod_WhenPeriodNotFound_ThrowsKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-99", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollPeriod?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ClosePeriodAsync(10, "2026-99", "Closing non-existent", "HR_ADMIN"));
        Assert.Contains("Payroll period '2026-99' not found", ex.Message);
    }

    [Fact]
    public async Task ClosePeriod_WhenUnitOfWorkPresent_ExecutesUnderPayrollPeriodLock()
    {
        var period = new PayrollPeriod { Id = 1, CompanyId = 10, PeriodCode = "2026-10", Status = "OPEN" };
        var runs = new List<PayrollRun> { new() { Id = 101, PayPeriod = "2026-10", Status = "APPROVED" } };

        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _repoMock.Setup(r => r.GetRunsByPeriodAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);
        _repoMock.Setup(r => r.HasPendingAdjustmentsForPeriodAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var uowMock = new Mock<IHrUnitOfWork>();
        uowMock.Setup(u => u.ExecuteForPayrollPeriodAsync(10, "2026-10", It.IsAny<Func<Task<PayrollPeriod>>>(), It.IsAny<CancellationToken>()))
            .Returns<long, string, Func<Task<PayrollPeriod>>, CancellationToken>((_, _, action, _) => action());

        var lockedService = new PayrollService(_repoMock.Object, _engineMock.Object, _validationMock.Object, uowMock.Object);

        var result = await lockedService.ClosePeriodAsync(10, "2026-10", "Final month close under lock", "HR_ADMIN");

        Assert.Equal("CLOSED", result.Status);
        uowMock.Verify(u => u.ExecuteForPayrollPeriodAsync(10, "2026-10", It.IsAny<Func<Task<PayrollPeriod>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReopenPeriod_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.ReopenPeriodAsync(10, "2026-10", "Audit correction requested", "HR_ADMIN", cts.Token));
    }

    [Fact]
    public async Task ReopenPeriod_WhenUnitOfWorkPresent_ExecutesUnderPayrollPeriodLock()
    {
        var period = new PayrollPeriod { Id = 1, CompanyId = 10, PeriodCode = "2026-10", Status = "CLOSED", FiscalYear = 2026, Month = 10 };

        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _repoMock.Setup(r => r.HasSubsequentClosedPeriodsAsync(10, 2026, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var uowMock = new Mock<IHrUnitOfWork>();
        uowMock.Setup(u => u.ExecuteForPayrollPeriodAsync(10, "2026-10", It.IsAny<Func<Task<PayrollPeriod>>>(), It.IsAny<CancellationToken>()))
            .Returns<long, string, Func<Task<PayrollPeriod>>, CancellationToken>((_, _, action, _) => action());

        var lockedService = new PayrollService(_repoMock.Object, _engineMock.Object, _validationMock.Object, uowMock.Object);

        var result = await lockedService.ReopenPeriodAsync(10, "2026-10", "Audit correction requested by finance", "HR_ADMIN");

        Assert.Equal("OPEN", result.Status);
        uowMock.Verify(u => u.ExecuteForPayrollPeriodAsync(10, "2026-10", It.IsAny<Func<Task<PayrollPeriod>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClosePeriod_WhenAllRunsApprovedOrCancelledOrSuperseded_Succeeds()
    {
        var period = new PayrollPeriod { Id = 1, CompanyId = 10, PeriodCode = "2026-10", Status = "OPEN" };
        var runs = new List<PayrollRun>
        {
            new() { Id = 101, PayPeriod = "2026-10", Status = "APPROVED" },
            new() { Id = 102, PayPeriod = "2026-10", Status = "POSTED" },
            new() { Id = 103, PayPeriod = "2026-10", Status = "CANCELLED" },
            new() { Id = 104, PayPeriod = "2026-10", Status = "SUPERSEDED" }
        };

        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _repoMock.Setup(r => r.GetRunsByPeriodAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);
        _repoMock.Setup(r => r.HasPendingAdjustmentsForPeriodAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ClosePeriodAsync(10, "2026-10", "All runs approved, posted, or superseded", "HR_ADMIN");

        Assert.Equal("CLOSED", result.Status);
    }

    [Fact]
    public async Task ClosePeriod_WhenNoRunsExist_AndNoPendingAdjustments_SuccessfullyCloses()
    {
        var period = new PayrollPeriod { Id = 1, CompanyId = 10, PeriodCode = "2026-10", Status = "OPEN" };

        _repoMock.Setup(r => r.GetPeriodByCodeAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _repoMock.Setup(r => r.GetRunsByPeriodAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollRun>());
        _repoMock.Setup(r => r.HasPendingAdjustmentsForPeriodAsync(10, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ClosePeriodAsync(10, "2026-10", "Zero runs in period", "HR_ADMIN");

        Assert.Equal("CLOSED", result.Status);
    }

    [Fact]
    public async Task ComparePayrollRun_WhenBranchHasNoPreviousRun_BaselineIsNullAndAllEmployeesNew()
    {
        var current = new PayrollRun
        {
            Id = 501,
            PayPeriod = "2026-10",
            BranchId = 5,
            Lines = new List<PayrollRunLine>
            {
                new() { EmployeeCode = "EMP01", BeneficiaryName = "Ali", BasicSalary = 1000m, GrossSalary = 1000m, NetPay = 900m }
            }
        };

        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(501, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repoMock.Setup(r => r.GetPreviousPayrollRunAsync(5, "2026-10", 501, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);

        var comparison = await _service.ComparePayrollRunAsync(501);

        Assert.Null(comparison.BaselineRunId);
        var emp = Assert.Single(comparison.EmployeeVariances);
        Assert.Equal("NEW", emp.ChangeType);
    }

    [Fact]
    public async Task ComparePayrollRun_WhenCorrectionOfRunIdPresent_UsesCorrectionRunAsBaseline()
    {
        var baseline = new PayrollRun
        {
            Id = 401,
            PayPeriod = "2026-10",
            Lines = new List<PayrollRunLine>
            {
                new() { EmployeeCode = "EMP01", BeneficiaryName = "Ali", BasicSalary = 1000m, GrossSalary = 1000m, NetPay = 900m }
            }
        };

        var current = new PayrollRun
        {
            Id = 501,
            PayPeriod = "2026-10",
            CorrectionOfRunId = 401,
            Lines = new List<PayrollRunLine>
            {
                new() { EmployeeCode = "EMP01", BeneficiaryName = "Ali", BasicSalary = 1100m, GrossSalary = 1100m, NetPay = 1000m }
            }
        };

        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(501, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(401, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        var comparison = await _service.ComparePayrollRunAsync(501); // baselineRunId omitted

        Assert.Equal(501, comparison.CurrentRunId);
        Assert.Equal(401, comparison.BaselineRunId);
        var emp = Assert.Single(comparison.EmployeeVariances);
        Assert.Equal("INCREASED", emp.ChangeType);
        Assert.Equal(100m, emp.NetPayVariance);
    }

    [Fact]
    public async Task ComparePayrollRun_WhenEmployeeBasicSalaryDecreased_DetectsDecreasedVariance()
    {
        var baseline = new PayrollRun
        {
            Id = 401,
            PayPeriod = "2026-10",
            Lines = new List<PayrollRunLine>
            {
                new() { EmployeeCode = "EMP01", BeneficiaryName = "Ali", BasicSalary = 1200m, GrossSalary = 1200m, NetPay = 1000m }
            }
        };

        var current = new PayrollRun
        {
            Id = 501,
            PayPeriod = "2026-10",
            Lines = new List<PayrollRunLine>
            {
                new() { EmployeeCode = "EMP01", BeneficiaryName = "Ali", BasicSalary = 1000m, GrossSalary = 1000m, NetPay = 800m }
            }
        };

        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(501, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(401, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        var comparison = await _service.ComparePayrollRunAsync(501, baselineRunId: 401);

        var emp = Assert.Single(comparison.EmployeeVariances);
        Assert.Equal("DECREASED", emp.ChangeType);
        Assert.Equal(-200m, emp.GrossVariance);
        Assert.Equal(-200m, emp.NetPayVariance);
    }

    [Fact]
    public async Task ComparePayrollRun_WhenEmployeeHasNoBeneficiaryNameAndNoEmployee_FallsBackToEmployeeCode()
    {
        var current = new PayrollRun
        {
            Id = 501,
            PayPeriod = "2026-10",
            Lines = new List<PayrollRunLine>
            {
                new() { EmployeeCode = "EMP_CODE_ONLY", BeneficiaryName = "", Employee = null, BasicSalary = 1000m, GrossSalary = 1000m, NetPay = 900m }
            }
        };

        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(501, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        var comparison = await _service.ComparePayrollRunAsync(501);

        var emp = Assert.Single(comparison.EmployeeVariances);
        Assert.Equal("EMP_CODE_ONLY", emp.EmployeeName);
    }

    [Fact]
    public async Task CancelPayrollRunAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.CancelPayrollRunAsync(101, null!, "HR_ADMIN"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelPayrollRunAsync_WhenReasonNullOrWhitespace_ThrowsArgumentException(string? invalidReason)
    {
        var dto = new CancelPayrollRunDto(invalidReason!);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CancelPayrollRunAsync(101, dto, "HR_ADMIN"));
        Assert.Contains("A cancellation reason of at most 1000 characters is required", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRunAsync_WhenReasonExceeds1000Chars_ThrowsArgumentException()
    {
        var longReason = new string('r', 1001);
        var dto = new CancelPayrollRunDto(longReason);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CancelPayrollRunAsync(101, dto, "HR_ADMIN"));
        Assert.Contains("A cancellation reason of at most 1000 characters is required", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRunAsync_WhenRunNotFound_ThrowsKeyNotFoundException()
    {
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);

        var dto = new CancelPayrollRunDto("Calculation had wrong hours");
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CancelPayrollRunAsync(999, dto, "HR_ADMIN"));
    }

    [Fact]
    public async Task CancelPayrollRunAsync_WhenAlreadyCancelled_ReturnsMappedResultImmediately()
    {
        var run = new PayrollRun
        {
            Id = 101,
            PayPeriod = "2026-10",
            Status = "CANCELLED",
            CancellationReason = "Already cancelled earlier",
            CancelledBy = "PREV_ADMIN",
            Lines = []
        };
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var dto = new CancelPayrollRunDto("Duplicate cancellation request");
        var result = await _service.CancelPayrollRunAsync(101, dto, "HR_ADMIN");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal("Already cancelled earlier", result.CancellationReason);
        _repoMock.Verify(r => r.UpdatePayrollRunAsync(It.IsAny<PayrollRun>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("APPROVED", null)]
    [InlineData("POSTED", null)]
    [InlineData("DRAFT", 777L)]
    public async Task CancelPayrollRunAsync_WhenRunApprovedOrHasJournalVoucher_ThrowsInvalidOperationException(string status, long? jvId)
    {
        var run = new PayrollRun
        {
            Id = 101,
            PayPeriod = "2026-10",
            Status = status,
            JournalVoucherId = jvId,
            ApprovalDate = status == "APPROVED" ? DateTime.UtcNow : null,
            Lines = []
        };
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var dto = new CancelPayrollRunDto("Cancel attempt on financial run");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelPayrollRunAsync(101, dto, "HR_ADMIN"));
        Assert.Contains("Only draft or calculated payroll without approval or accounting effects can be cancelled", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRunAsync_WhenRunLinesProcessed_ThrowsInvalidOperationException()
    {
        var run = new PayrollRun
        {
            Id = 101,
            PayPeriod = "2026-10",
            Status = "CALCULATED",
            Lines =
            [
                new PayrollRunLine { Id = 1, Status = "CALCULATED" },
                new PayrollRunLine { Id = 2, Status = "PAID" }
            ]
        };
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var dto = new CancelPayrollRunDto("Cancel attempt with paid line");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelPayrollRunAsync(101, dto, "HR_ADMIN"));
        Assert.Contains("One or more payroll lines are already processed", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRunAsync_WhenPeriodNotFound_ThrowsInvalidOperationException()
    {
        var run = new PayrollRun
        {
            Id = 101,
            PayPeriod = "2026-10",
            Status = "CALCULATED",
            Lines = [new PayrollRunLine { Id = 1, Status = "CALCULATED" }]
        };
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        _repoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollPeriod?)null);

        var dto = new CancelPayrollRunDto("Valid cancellation reason");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelPayrollRunAsync(101, dto, "HR_ADMIN"));
        Assert.Contains("Payroll period was not found", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRunAsync_WhenPeriodClosed_ThrowsInvalidOperationException()
    {
        var run = new PayrollRun
        {
            Id = 101,
            PayPeriod = "2026-10",
            Status = "CALCULATED",
            Lines = [new PayrollRunLine { Id = 1, Status = "CALCULATED" }]
        };
        var period = new PayrollPeriod { Status = "CLOSED", PeriodCode = "2026-10" };
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        _repoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);

        var dto = new CancelPayrollRunDto("Cancel in closed period");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelPayrollRunAsync(101, dto, "HR_ADMIN"));
        Assert.Contains("Reopen the closed payroll period before cancelling this calculation", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRunAsync_WhenHasPayrollFinancialLinks_ThrowsInvalidOperationException()
    {
        var run = new PayrollRun
        {
            Id = 101,
            PayPeriod = "2026-10",
            Status = "CALCULATED",
            Lines = [new PayrollRunLine { Id = 1, Status = "CALCULATED" }]
        };
        var period = new PayrollPeriod { Status = "OPEN", PeriodCode = "2026-10" };
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        _repoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _repoMock.Setup(r => r.HasPayrollFinancialLinksAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var dto = new CancelPayrollRunDto("Cancel with loan link");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelPayrollRunAsync(101, dto, "HR_ADMIN"));
        Assert.Contains("This run has linked loan or adjustment settlements", ex.Message);
    }

    [Fact]
    public async Task CancelPayrollRunAsync_WhenValid_CancelsRunAndAllLinesAndSaves()
    {
        var line1 = new PayrollRunLine { Id = 1, Status = "CALCULATED", EmployeeCode = "EMP01" };
        var line2 = new PayrollRunLine { Id = 2, Status = "DRAFT", EmployeeCode = "EMP02" };
        var run = new PayrollRun
        {
            Id = 101,
            PayPeriod = "2026-10",
            Status = "CALCULATED",
            Lines = [line1, line2]
        };
        var period = new PayrollPeriod { Status = "OPEN", PeriodCode = "2026-10" };
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        _repoMock.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _repoMock.Setup(r => r.HasPayrollFinancialLinksAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var dto = new CancelPayrollRunDto("Calculation errors discovered in review");
        var result = await _service.CancelPayrollRunAsync(101, dto, "HR_ADMIN");

        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal("CANCELLED", run.Status);
        Assert.Equal("HR_ADMIN", run.CancelledBy);
        Assert.Equal("Calculation errors discovered in review", run.CancellationReason);
        Assert.Equal("CANCELLED", line1.Status);
        Assert.Equal("CANCELLED", line2.Status);
        _repoMock.Verify(r => r.UpdatePayrollRunAsync(run, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelPayrollRunAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var dto = new CancelPayrollRunDto("Cancelling run");

        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(101, cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.CancelPayrollRunAsync(101, dto, "HR_ADMIN", cts.Token));
    }

    [Fact]
    public async Task GetPayrollRunByIdAsync_WhenFound_ReturnsMappedDto()
    {
        var line = new PayrollRunLine
        {
            Id = 11,
            EmployeeCode = "EMP01",
            Employee = new Employee { EmployeeCode = "EMP01", NameLocal = "Ali Hasan" },
            BeneficiaryName = "Ali Hasan",
            BasicSalary = 1000m,
            GrossSalary = 1200m,
            TotalEarnings = 200m,
            TotalDeductions = 100m,
            NetPay = 1100m,
            IncomeTaxWithheld = 50m,
            SscEmployeeContrib = 50m,
            SscEmployerContrib = 100m,
            Status = "CALCULATED"
        };
        var run = new PayrollRun
        {
            Id = 101,
            PayPeriod = "2026-10",
            BranchId = 2,
            CalculationDate = DateTime.UtcNow,
            Status = "CALCULATED",
            TotalGrossSalary = 1200m,
            TotalNetSalary = 1100m,
            TotalIncomeTax = 50m,
            TotalEmployeeSsc = 50m,
            TotalEmployerSsc = 100m,
            TotalOtherDeductions = 0m,
            Lines = [line]
        };
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var result = await _service.GetPayrollRunByIdAsync(101);

        Assert.NotNull(result);
        Assert.Equal(101, result.PayrollRunId);
        Assert.Equal("2026-10", result.PayPeriod);
        Assert.Equal(1, result.EmployeeCount);
        var resLine = Assert.Single(result.Payslips);
        Assert.Equal("EMP01", resLine.EmployeeCode);
        Assert.Equal("Ali Hasan", resLine.EmployeeName);
        Assert.Equal(1100m, resLine.NetPay);
    }

    [Fact]
    public async Task GetPayrollRunByIdAsync_WhenNotFound_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);

        var result = await _service.GetPayrollRunByIdAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task ComparePayrollRun_WhenEmployeeHasLocalName_PrefersLocalNameOverBeneficiaryName()
    {
        var emp = new Employee { EmployeeCode = "EMP01", NameLocal = "علي حسن" };
        var current = new PayrollRun
        {
            Id = 501,
            PayPeriod = "2026-10",
            Lines =
            [
                new PayrollRunLine
                {
                    EmployeeCode = "EMP01",
                    BeneficiaryName = "Ali Hasan",
                    Employee = emp,
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    NetPay = 900m
                }
            ]
        };
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(501, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        var comparison = await _service.ComparePayrollRunAsync(501);

        var v = Assert.Single(comparison.EmployeeVariances);
        Assert.Equal("علي حسن", v.EmployeeName);
    }

    [Fact]
    public async Task ComparePayrollRun_WhenBasicSalaryUnchanged_DetectsLoanOrOtherDeductionVariance()
    {
        var baseline = new PayrollRun
        {
            Id = 401,
            PayPeriod = "2026-09",
            Lines =
            [
                new PayrollRunLine
                {
                    EmployeeCode = "EMP01",
                    BeneficiaryName = "Ali",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    TotalEarnings = 0m,
                    OtherDeductions = 0m,
                    IncomeTaxWithheld = 50m,
                    SscEmployeeContrib = 50m,
                    NetPay = 900m
                }
            ]
        };
        var current = new PayrollRun
        {
            Id = 501,
            PayPeriod = "2026-10",
            Lines =
            [
                new PayrollRunLine
                {
                    EmployeeCode = "EMP01",
                    BeneficiaryName = "Ali",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    TotalEarnings = 0m,
                    OtherDeductions = 100m,
                    IncomeTaxWithheld = 50m,
                    SscEmployeeContrib = 50m,
                    NetPay = 800m
                }
            ]
        };
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(501, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(401, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        var comparison = await _service.ComparePayrollRunAsync(501, baselineRunId: 401);

        var v = Assert.Single(comparison.EmployeeVariances);
        Assert.Equal("DECREASED", v.ChangeType);
        Assert.Equal("Loan / other deduction variance", v.PrimaryReason);
    }

    [Fact]
    public async Task ComparePayrollRun_WhenBasicSalaryUnchanged_DetectsStatutoryDeductionTaxOrSscVariance()
    {
        var baseline = new PayrollRun
        {
            Id = 401,
            PayPeriod = "2026-09",
            Lines =
            [
                new PayrollRunLine
                {
                    EmployeeCode = "EMP01",
                    BeneficiaryName = "Ali",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    TotalEarnings = 0m,
                    OtherDeductions = 0m,
                    IncomeTaxWithheld = 50m,
                    SscEmployeeContrib = 50m,
                    NetPay = 900m
                }
            ]
        };
        var current = new PayrollRun
        {
            Id = 501,
            PayPeriod = "2026-10",
            Lines =
            [
                new PayrollRunLine
                {
                    EmployeeCode = "EMP01",
                    BeneficiaryName = "Ali",
                    BasicSalary = 1000m,
                    GrossSalary = 1000m,
                    TotalEarnings = 0m,
                    OtherDeductions = 0m,
                    IncomeTaxWithheld = 80m,
                    SscEmployeeContrib = 50m,
                    NetPay = 870m
                }
            ]
        };
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(501, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(401, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        var comparison = await _service.ComparePayrollRunAsync(501, baselineRunId: 401);

        var v = Assert.Single(comparison.EmployeeVariances);
        Assert.Equal("DECREASED", v.ChangeType);
        Assert.Equal("Statutory deduction (Tax/SSC) variance", v.PrimaryReason);
    }

    [Fact]
    public async Task ComparePayrollRun_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _repoMock.Setup(r => r.GetPayrollRunByIdAsync(501, cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ComparePayrollRunAsync(501, cancellationToken: cts.Token));
    }
}
