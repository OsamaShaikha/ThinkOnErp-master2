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

public class EndOfServiceTests
{
    private readonly EndOfServiceCalculationEngine _engine = new();
    private readonly Mock<IEndOfServiceRepository> _eosRepoMock = new();
    private readonly Mock<IEmployeeRepository> _empRepoMock = new();
    private readonly EndOfServiceService _service;

    public EndOfServiceTests()
    {
        _service = new EndOfServiceService(_eosRepoMock.Object, _empRepoMock.Object, _engine);
    }

    private (Employee Employee, SalaryStructure Structure, EndOfServicePolicy Policy) CreateStandardFixture(
        DateTime hireDate, decimal basicSalary = 1000m, decimal allowance = 200m)
    {
        var employee = new Employee
        {
            EmployeeCode = "EMP_EOS",
            NameLocal = "علي حسن",
            HireDate = hireDate,
            IsActive = true,
            EmploymentStatus = "ACTIVE"
        };

        var structure = new SalaryStructure
        {
            EmployeeCode = employee.EmployeeCode,
            BasicSalary = basicSalary,
            CurrencyCode = "JOD",
            IsActive = true,
            Lines = new List<SalaryStructureLine>
            {
                new()
                {
                    ComponentCode = "TRANS",
                    Amount = allowance,
                    IsActive = true,
                    Component = new SalaryComponent { ComponentCode = "TRANS", ComponentType = "ALLOWANCE" }
                }
            }
        };
        employee.SalaryStructures.Add(structure);
        _empRepoMock.Setup(r => r.GetActiveSalaryStructureAsync(
                employee.EmployeeCode, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(structure);

        var policy = new EndOfServicePolicy
        {
            CompanyId = 1,
            CalculationBasis = "BASIC_SALARY",
            LeaveDailyRateDivisor = 30m,
            FirstFiveYearsRatePerYear = 0.5m,
            SubsequentYearsRatePerYear = 1.0m,
            ResignationFactorUnderTwoYears = 0.0m,
            ResignationFactorTwoToFiveYears = 0.3333m,
            ResignationFactorFiveToTenYears = 0.6667m,
            ResignationFactorTenYearsAndAbove = 1.0m
        };

        return (employee, structure, policy);
    }

    [Fact]
    public void CalculationEngine_ResignationUnderTwoYears_YieldsZeroSeverance()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2024, 1, 1));
        var lastWorkingDate = new DateTime(2025, 6, 30); // 1.5 years service

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            lastWorkingDate
        );

        Assert.True(settlement.ServiceYears < 2.0m);
        Assert.Equal(0m, settlement.SeverancePay);
        Assert.Equal(EndOfServiceTerminationTypes.Resignation, settlement.TerminationType);
    }

    [Fact]
    public void CalculationEngine_ResignationBetweenTwoAndFiveYears_YieldsOneThirdSeverance()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2022, 1, 1));
        var lastWorkingDate = new DateTime(2025, 1, 1); // 3.0 years service

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            lastWorkingDate
        );

        // 3 years * 0.5 month = 1.5 months = 1500 JOD
        // Resignation factor = 0.3333 -> 1500 * 0.3333 = 499.950 JOD
        Assert.Equal(3.0m, Math.Round(settlement.ServiceYears, 1));
        Assert.Equal(499.950m, settlement.SeverancePay);
    }

    [Fact]
    public void CalculationEngine_ResignationTenYearsAndAbove_YieldsFullSeverance()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2014, 1, 1));
        var lastWorkingDate = new DateTime(2026, 1, 1); // 12.0 years service

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            lastWorkingDate
        );

        // First 5 years at 0.5 = 2.5 months (2,500)
        // Next 7 years at 1.0 = 7.0 months (7,000)
        // Total = 9.5 months = 9,500 JOD
        // Resignation factor for 10+ years = 1.0 -> 9,500 JOD
        Assert.Equal(12.0m, Math.Round(settlement.ServiceYears, 1));
        Assert.Equal(9500m, settlement.SeverancePay);
    }

    [Fact]
    public void CalculationEngine_EmployerTermination_YieldsFullSeveranceRegardlessOfYears()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2022, 1, 1));
        var lastWorkingDate = new DateTime(2025, 1, 1); // 3.0 years service

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.TerminationEmployer,
            lastWorkingDate
        );

        // 3 years * 0.5 month = 1.5 months = 1500 JOD full severance
        Assert.Equal(1500m, settlement.SeverancePay);
    }

    [Fact]
    public void CalculationEngine_AggregatesLeaveEncashment_AndNoticePeriodPay()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2020, 1, 1));
        var lastWorkingDate = new DateTime(2025, 1, 31);

        // 15 days unused leave + 30 days notice pay
        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.ContractExpiry,
            lastWorkingDate,
            noticeCompensationDays: 30,
            remainingLeaveDays: 15m
        );

        // Daily rate = 1000 / 30 = 33.3333
        // Leave encashment = 15 * 33.3333 = 500.000 JOD
        // Notice pay = 30 * 33.3333 = 1000.000 JOD
        Assert.Equal(500.000m, settlement.LeaveEncashment);
        Assert.Equal(1000.000m, settlement.NoticePeriodPay);
    }

    [Fact]
    public void CalculationEngine_DeductsOutstandingLoansAndAdvances_Accurately()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2022, 1, 1));
        var lastWorkingDate = new DateTime(2025, 1, 31);

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.TerminationEmployer,
            lastWorkingDate,
            outstandingLoans: 600m,
            outstandingAdvances: 250m
        );

        Assert.Equal(600m, settlement.OutstandingLoans);
        Assert.Equal(250m, settlement.OutstandingAdvances);
        Assert.Equal(850m, settlement.TotalDeductions);

        // Net settlement = TotalEntitlements - 850
        Assert.Equal(settlement.TotalEntitlements - 850m, settlement.NetSettlementAmount);
    }

    [Fact]
    public void CalculationEngine_NegativeLeaveBalance_IsHandledAsDeduction()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2022, 1, 1));
        var lastWorkingDate = new DateTime(2025, 1, 31);

        // Employee took 6 excess leave days (-6)
        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            lastWorkingDate,
            remainingLeaveDays: -6m
        );

        // 6 * (1000/30) = 200 JOD deduction
        Assert.Equal(0m, settlement.LeaveEncashment);
        Assert.Equal(200.000m, settlement.NegativeLeaveDeduction);
        Assert.Contains(settlement.Lines, l => l.ComponentCode == "NEGATIVE_LEAVE_DEDUCTION" && l.Amount == 200.000m);
    }

    [Fact]
    public async Task Service_CalculateSettlement_CreatesDraftSettlement_WithAggregatedBalances()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2020, 1, 1));
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP_EOS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _eosRepoMock.Setup(r => r.GetActivePolicyAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _eosRepoMock.Setup(r => r.GetRemainingLeaveBalanceDaysAsync("EMP_EOS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(12m);
        _eosRepoMock.Setup(r => r.GetOutstandingLoanBalanceAsync("EMP_EOS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(300m);
        _eosRepoMock.Setup(r => r.GetOutstandingAdvanceBalanceAsync("EMP_EOS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(100m);

        var request = new CreateEndOfServiceRequest(
            "EMP_EOS",
            EndOfServiceTerminationTypes.TerminationEmployer,
            new DateTime(2025, 9, 30),
            CalculationNotes: "Restructuring redundancy"
        );

        var result = await _service.CalculateSettlementAsync(request, "HR_ADMIN", 1L);

        Assert.Equal("EMP_EOS", result.EmployeeCode);
        Assert.Equal(EndOfServiceSettlementStatus.Draft, result.Status);
        Assert.Equal(300m, result.OutstandingLoans);
        Assert.Equal(100m, result.OutstandingAdvances);
        Assert.True(result.LeaveEncashment > 0);
        Assert.True(result.NetSettlementAmount > 0);

        _eosRepoMock.Verify(r => r.AddAsync(It.IsAny<EndOfServiceSettlement>(), It.IsAny<CancellationToken>()), Times.Once);
        _eosRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Service_CalculateSettlement_UsesRequestedTenantPolicyAndCompanyId()
    {
        var (employee, _, policy) = CreateStandardFixture(new DateTime(2020, 1, 1));
        policy.CompanyId = 2;
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(employee.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _eosRepoMock.Setup(r => r.GetActivePolicyAsync(2L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _eosRepoMock.Setup(r => r.GetRemainingLeaveBalanceDaysAsync(employee.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _eosRepoMock.Setup(r => r.GetOutstandingLoanBalanceAsync(employee.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _eosRepoMock.Setup(r => r.GetOutstandingAdvanceBalanceAsync(employee.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        EndOfServiceSettlement? saved = null;
        _eosRepoMock.Setup(r => r.AddAsync(It.IsAny<EndOfServiceSettlement>(), It.IsAny<CancellationToken>()))
            .Callback<EndOfServiceSettlement, CancellationToken>((settlement, _) => saved = settlement)
            .Returns(Task.CompletedTask);

        var request = new CreateEndOfServiceRequest(
            employee.EmployeeCode,
            EndOfServiceTerminationTypes.TerminationEmployer,
            new DateTime(2025, 1, 1));
        var result = await _service.CalculateSettlementAsync(request, "HR_ADMIN", 2L);

        Assert.Equal(2L, result.CompanyId);
        Assert.NotNull(saved);
        Assert.Equal(2L, saved!.CompanyId);
        _eosRepoMock.Verify(r => r.GetActivePolicyAsync(2L, It.IsAny<CancellationToken>()), Times.Once);
        _eosRepoMock.Verify(r => r.GetActivePolicyAsync(1L, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Service_CalculateSettlementRejectsPolicyFromDifferentTenant()
    {
        var (employee, _, policy) = CreateStandardFixture(new DateTime(2020, 1, 1));
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(employee.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _eosRepoMock.Setup(r => r.GetActivePolicyAsync(2L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        var request = new CreateEndOfServiceRequest(
            employee.EmployeeCode,
            EndOfServiceTerminationTypes.TerminationEmployer,
            new DateTime(2025, 1, 1));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CalculateSettlementAsync(request, "HR_ADMIN", 2L));

        Assert.Contains("does not belong to the resolved tenant", exception.Message);
        _eosRepoMock.Verify(r => r.AddAsync(It.IsAny<EndOfServiceSettlement>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Service_ApproveSettlement_TransitionsStatus_AndUpdatesEmployeeToTerminated()
    {
        var (emp, _, _) = CreateStandardFixture(new DateTime(2020, 1, 1));
        var settlement = new EndOfServiceSettlement
        {
            Id = 88,
            EmployeeCode = emp.EmployeeCode,
            TerminationType = EndOfServiceTerminationTypes.Resignation,
            LastWorkingDate = new DateTime(2025, 10, 15),
            Status = EndOfServiceSettlementStatus.Draft
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);

        var result = await _service.ApproveSettlementAsync(88, "HR_DIRECTOR");

        Assert.Equal(EndOfServiceSettlementStatus.Approved, result.Status);
        Assert.Equal("HR_DIRECTOR", settlement.ApprovedBy);
        Assert.NotNull(settlement.ApprovedAtUtc);

        // EOS-05 Operational update on employee
        Assert.Equal("TERMINATED", emp.EmploymentStatus);
        Assert.False(emp.IsActive);
        Assert.Equal(new DateTime(2025, 10, 15), emp.TerminationDate);
        Assert.Equal(EndOfServiceTerminationTypes.Resignation, emp.TerminationReason);

        _eosRepoMock.Verify(r => r.UpdateAsync(settlement, It.IsAny<CancellationToken>()), Times.Once);
        _empRepoMock.Verify(r => r.UpdateEmployeeAsync(emp, It.IsAny<CancellationToken>()), Times.Once);
        _eosRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Service_ApproveSettlement_Throws_WhenStatusIsNotDraft()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 88,
            EmployeeCode = "EMP_EOS",
            Status = EndOfServiceSettlementStatus.Approved
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ApproveSettlementAsync(88, "HR_DIRECTOR"));

        Assert.Contains("Only DRAFT settlements can be approved", ex.Message);
    }

    [Fact]
    public async Task Service_CancelSettlement_RevertsEmployeeToActive_WhenPreviouslyApproved()
    {
        var (emp, _, _) = CreateStandardFixture(new DateTime(2020, 1, 1));
        emp.EmploymentStatus = "TERMINATED";
        emp.IsActive = false;
        emp.TerminationDate = new DateTime(2025, 10, 15);
        emp.TerminationReason = EndOfServiceTerminationTypes.Resignation;

        var settlement = new EndOfServiceSettlement
        {
            Id = 88,
            EmployeeCode = emp.EmployeeCode,
            Status = EndOfServiceSettlementStatus.Approved,
            LastWorkingDate = emp.TerminationDate!.Value,
            TerminationType = emp.TerminationReason!
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);

        var result = await _service.CancelSettlementAsync(88, "Resignation retracted by mutual agreement", "HR_DIRECTOR");

        Assert.Equal(EndOfServiceSettlementStatus.Cancelled, result.Status);
        Assert.Equal("HR_DIRECTOR", settlement.CancelledBy);
        Assert.Equal("Resignation retracted by mutual agreement", settlement.CancellationReason);

        // Reverted employee operational status
        Assert.Equal("ACTIVE", emp.EmploymentStatus);
        Assert.True(emp.IsActive);
        Assert.Null(emp.TerminationDate);
        Assert.Null(emp.TerminationReason);

        _eosRepoMock.Verify(r => r.UpdateAsync(settlement, It.IsAny<CancellationToken>()), Times.Once);
        _empRepoMock.Verify(r => r.UpdateEmployeeAsync(emp, It.IsAny<CancellationToken>()), Times.Once);
        _eosRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Service_CancelSettlement_Throws_WhenPosted()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 88,
            EmployeeCode = "EMP_EOS",
            Status = EndOfServiceSettlementStatus.Posted
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelSettlementAsync(88, "Attempting cancel on posted", "HR_DIRECTOR"));

        Assert.Contains("Cannot cancel a posted end of service settlement", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Service_CancelSettlement_Throws_WhenReasonIsMissing(string? reason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CancelSettlementAsync(88, reason!, "HR_DIRECTOR"));
    }

    [Fact]
    public async Task Service_CancelSettlement_Throws_WhenActiveAccountingHandoffExists()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 88,
            EmployeeCode = "EMP_EOS",
            Status = EndOfServiceSettlementStatus.Approved
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);
        _eosRepoMock.Setup(r => r.HasActiveAccountingHandoffAsync(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelSettlementAsync(88, "Attempting cancel with active handoff", "HR_DIRECTOR"));

        Assert.Contains("Cannot cancel an end of service settlement with an active accounting handoff", ex.Message);
    }

    [Fact]
    public async Task Service_CancelSettlement_Succeeds_WhenNoActiveAccountingHandoffExists()
    {
        var (emp, _, _) = CreateStandardFixture(new DateTime(2020, 1, 1));
        emp.IsActive = false;
        emp.EmploymentStatus = "TERMINATED";
        emp.TerminationDate = new DateTime(2025, 12, 31);
        emp.TerminationReason = EndOfServiceTerminationTypes.Resignation;
        var settlement = new EndOfServiceSettlement
        {
            Id = 88,
            EmployeeCode = emp.EmployeeCode,
            Status = EndOfServiceSettlementStatus.Approved,
            LastWorkingDate = emp.TerminationDate.Value,
            TerminationType = emp.TerminationReason
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);
        _eosRepoMock.Setup(r => r.HasActiveAccountingHandoffAsync(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);

        var result = await _service.CancelSettlementAsync(88, "Cancelled with no active handoff", "HR_DIRECTOR");

        Assert.Equal(EndOfServiceSettlementStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task Service_GetHandoffStatus_ReturnsMappedDto_WhenHandoffExists()
    {
        var handoff = new HrAccountingHandoff
        {
            Id = 42,
            HandoffReference = "HR-EOS-1-88-1",
            DeliveryStatus = "QUEUED",
            Operation = "FUND",
            SourceVersion = 1,
            CreatedAtUtc = new DateTime(2026, 11, 8, 20, 0, 0, DateTimeKind.Utc)
        };

        _eosRepoMock.Setup(r => r.GetLatestHandoffAsync(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(handoff);

        var status = await _service.GetHandoffStatusAsync(88);

        Assert.NotNull(status);
        Assert.Equal(42, status.HandoffId);
        Assert.Equal("HR-EOS-1-88-1", status.HandoffReference);
        Assert.Equal("QUEUED", status.DeliveryStatus);
        Assert.Equal("FUND", status.Operation);
        Assert.Equal(1, status.SourceVersion);
    }

    [Fact]
    public async Task Service_GetHandoffStatus_ReturnsNull_WhenNoHandoffExists()
    {
        _eosRepoMock.Setup(r => r.GetLatestHandoffAsync(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync((HrAccountingHandoff?)null);

        var status = await _service.GetHandoffStatusAsync(88);

        Assert.Null(status);
    }

    [Fact]
    public void CalculationEngine_WhenInvalidTerminationType_ThrowsArgumentException()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2022, 1, 1));
        var ex = Assert.Throws<ArgumentException>(() =>
            _engine.Calculate(emp, structure, policy, "INVALID_TERMINATION_TYPE", new DateTime(2025, 1, 1)));
        Assert.Contains("Invalid termination type", ex.Message);
    }

    [Fact]
    public void CalculationEngine_WhenLastWorkingDateBeforeHireDate_ThrowsArgumentException()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2024, 1, 1));
        var ex = Assert.Throws<ArgumentException>(() =>
            _engine.Calculate(emp, structure, policy, EndOfServiceTerminationTypes.Resignation, new DateTime(2023, 12, 31)));
        Assert.Contains("Last working date cannot be earlier than hire date", ex.Message);
    }

    [Fact]
    public void CalculationEngine_Retirement_YieldsFullSeverance()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2018, 1, 1));
        var lastWorkingDate = new DateTime(2024, 1, 1); // 6 years
        var settlement = _engine.Calculate(emp, structure, policy, EndOfServiceTerminationTypes.Retirement, lastWorkingDate);

        // 5 yrs * 0.5 = 2.5 months; 1 yr * 1.0 = 1 month -> Total 3.5 months * 1000 = 3500 JOD
        Assert.Equal(3500m, settlement.SeverancePay);
        Assert.Equal(EndOfServiceTerminationTypes.Retirement, settlement.TerminationType);
    }

    [Fact]
    public void CalculationEngine_Death_YieldsFullSeverance()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2020, 1, 1));
        var lastWorkingDate = new DateTime(2023, 1, 1); // 3 years
        var settlement = _engine.Calculate(emp, structure, policy, EndOfServiceTerminationTypes.Death, lastWorkingDate);

        // 3 yrs * 0.5 = 1.5 months * 1000 = 1500 JOD
        Assert.Equal(1500m, settlement.SeverancePay);
        Assert.Equal(EndOfServiceTerminationTypes.Death, settlement.TerminationType);
    }

    [Fact]
    public void CalculationEngine_JobAbandonment_YieldsZeroSeverance()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2015, 1, 1));
        var lastWorkingDate = new DateTime(2025, 1, 1); // 10 years
        var settlement = _engine.Calculate(emp, structure, policy, EndOfServiceTerminationTypes.JobAbandonment, lastWorkingDate);

        Assert.Equal(0m, settlement.SeverancePay);
        Assert.Equal(EndOfServiceTerminationTypes.JobAbandonment, settlement.TerminationType);
    }

    [Fact]
    public void CalculationEngine_GrossSalaryBasis_IncludesActiveAllowances()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2022, 1, 1), basicSalary: 1000m, allowance: 500m);
        policy.CalculationBasis = "GROSS_SALARY"; // Basis should be 1500m
        var lastWorkingDate = new DateTime(2024, 1, 1); // 2 years

        var settlement = _engine.Calculate(emp, structure, policy, EndOfServiceTerminationTypes.TerminationEmployer, lastWorkingDate);

        // 2 yrs * 0.5 * 1500 = 1500 JOD severance
        Assert.Equal(1500m, settlement.GrossSalarySnapshot);
        Assert.Equal(1500m, settlement.SeverancePay);
    }

    [Fact]
    public void CalculationEngine_WithOtherEntitlementsAndOtherDeductions_CalculatesNetCorrectly()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2022, 1, 1));
        var lastWorkingDate = new DateTime(2024, 1, 1);

        var settlement = _engine.Calculate(
            emp, structure, policy, EndOfServiceTerminationTypes.TerminationEmployer, lastWorkingDate,
            otherEntitlements: 250m,
            otherDeductions: 100m
        );

        Assert.Equal(250m, settlement.OtherEntitlements);
        Assert.Equal(100m, settlement.OtherDeductions);
        Assert.Equal(settlement.TotalEntitlements - settlement.TotalDeductions, settlement.NetSettlementAmount);
    }

    [Fact]
    public async Task Service_GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        _eosRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EndOfServiceSettlement?)null);

        var result = await _service.GetByIdAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task Service_GetByEmployeeCodeAsync_ReturnsSettlement()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 55,
            EmployeeCode = "EMP_XYZ",
            Status = EndOfServiceSettlementStatus.Approved
        };

        _eosRepoMock.Setup(r => r.GetByEmployeeCodeAsync("EMP_XYZ", It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        var result = await _service.GetByEmployeeCodeAsync("EMP_XYZ");
        Assert.NotNull(result);
        Assert.Equal(55, result.Id);
        Assert.Equal("EMP_XYZ", result.EmployeeCode);
    }

    [Fact]
    public async Task Service_GetListAsync_FiltersByCompanyAndBranchAndStatus()
    {
        var list = new List<EndOfServiceSettlement>
        {
            new() { Id = 1, CompanyId = 1, BranchId = 10, Status = EndOfServiceSettlementStatus.Approved, EmployeeCode = "EMP1" },
            new() { Id = 2, CompanyId = 1, BranchId = 10, Status = EndOfServiceSettlementStatus.Approved, EmployeeCode = "EMP2" }
        };

        _eosRepoMock.Setup(r => r.GetListAsync(1, 10, EndOfServiceSettlementStatus.Approved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var results = await _service.GetListAsync(1, 10, EndOfServiceSettlementStatus.Approved);
        Assert.Equal(2, results.Count);
        Assert.Equal("EMP1", results[0].EmployeeCode);
        Assert.Equal("EMP2", results[1].EmployeeCode);
    }

    [Fact]
    public async Task Service_CalculateSettlement_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.CalculateSettlementAsync(null!, "HR_ADMIN", 1L));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Service_CalculateSettlement_EmptyEmployeeCode_ThrowsArgumentException(string? employeeCode)
    {
        var request = new CreateEndOfServiceRequest(
            employeeCode!,
            EndOfServiceTerminationTypes.Resignation,
            DateTime.UtcNow
        );

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CalculateSettlementAsync(request, "HR_ADMIN", 1L));
    }

    [Fact]
    public async Task Service_CalculateSettlement_EmployeeNotFound_ThrowsKeyNotFoundException()
    {
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync("NOT_EXIST", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var request = new CreateEndOfServiceRequest(
            "NOT_EXIST",
            EndOfServiceTerminationTypes.Resignation,
            DateTime.UtcNow
        );

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CalculateSettlementAsync(request, "HR_ADMIN", 1L));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task Service_CalculateSettlement_MissingActiveSalaryStructure_ThrowsInvalidOperationException()
    {
        var employee = new Employee
        {
            EmployeeCode = "EMP_NO_SALARY",
            HireDate = new DateTime(2022, 1, 1),
            SalaryStructures = new List<SalaryStructure>() // No active structures
        };

        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP_NO_SALARY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var request = new CreateEndOfServiceRequest(
            "EMP_NO_SALARY",
            EndOfServiceTerminationTypes.Resignation,
            DateTime.UtcNow
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CalculateSettlementAsync(request, "HR_ADMIN", 1L));

        Assert.Contains("does not have an active salary structure", ex.Message);
    }

    [Fact]
    public async Task Service_CalculateSettlement_FallbackDefaultPolicy_WhenPolicyIsNull()
    {
        var (emp, structure, _) = CreateStandardFixture(new DateTime(2022, 1, 1));
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);

        _eosRepoMock.Setup(r => r.GetActivePolicyAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EndOfServicePolicy?)null); // Policy not in DB, fallback used

        _eosRepoMock.Setup(r => r.GetRemainingLeaveBalanceDaysAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _eosRepoMock.Setup(r => r.GetOutstandingLoanBalanceAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _eosRepoMock.Setup(r => r.GetOutstandingAdvanceBalanceAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var request = new CreateEndOfServiceRequest(
            emp.EmployeeCode,
            EndOfServiceTerminationTypes.Resignation,
            new DateTime(2025, 1, 1)
        );

        var result = await _service.CalculateSettlementAsync(request, "HR_ADMIN", 1L);

        Assert.NotNull(result);
        Assert.Equal(emp.EmployeeCode, result.EmployeeCode);
        Assert.Equal(EndOfServiceSettlementStatus.Draft, result.Status);
        _eosRepoMock.Verify(r => r.AddAsync(It.IsAny<EndOfServiceSettlement>(), It.IsAny<CancellationToken>()), Times.Once);
        _eosRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Service_ApproveSettlement_SettlementNotFound_ThrowsKeyNotFoundException()
    {
        _eosRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EndOfServiceSettlement?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ApproveSettlementAsync(999, "HR_APPROVER"));

        Assert.Contains("999 not found", ex.Message);
    }

    [Theory]
    [InlineData(EndOfServiceSettlementStatus.Approved)]
    [InlineData(EndOfServiceSettlementStatus.Cancelled)]
    [InlineData(EndOfServiceSettlementStatus.Posted)]
    public async Task Service_ApproveSettlement_NonDraftStatus_ThrowsInvalidOperationException(string nonDraftStatus)
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 101,
            EmployeeCode = "EMP_101",
            Status = nonDraftStatus
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ApproveSettlementAsync(101, "HR_APPROVER"));

        Assert.Contains("Only DRAFT settlements can be approved", ex.Message);
    }

    [Fact]
    public async Task Service_ApproveSettlement_EmployeeNotFound_ThrowsKeyNotFoundException()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 102,
            EmployeeCode = "EMP_MISSING",
            Status = EndOfServiceSettlementStatus.Draft
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(102, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP_MISSING", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ApproveSettlementAsync(102, "HR_APPROVER"));

        Assert.Contains("EMP_MISSING' not found", ex.Message);
    }

    [Fact]
    public async Task Service_ApproveSettlement_SuccessfullyTerminatesEmployeeAndSetsApprovedStatus()
    {
        var (emp, _, _) = CreateStandardFixture(new DateTime(2020, 1, 1));
        var settlement = new EndOfServiceSettlement
        {
            Id = 103,
            EmployeeCode = emp.EmployeeCode,
            Status = EndOfServiceSettlementStatus.Draft,
            LastWorkingDate = new DateTime(2025, 12, 31),
            TerminationType = EndOfServiceTerminationTypes.Resignation
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(103, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);

        var result = await _service.ApproveSettlementAsync(103, "HR_APPROVER");

        Assert.Equal(EndOfServiceSettlementStatus.Approved, result.Status);
        Assert.Equal("HR_APPROVER", result.ApprovedBy);
        Assert.NotNull(result.ApprovedAtUtc);

        // Verify employee operational termination (EOS-05)
        Assert.False(emp.IsActive);
        Assert.Equal("TERMINATED", emp.EmploymentStatus);
        Assert.Equal(settlement.LastWorkingDate, emp.TerminationDate);
        Assert.Equal(settlement.TerminationType, emp.TerminationReason);
        Assert.Equal("HR_APPROVER", emp.UpdateUser);

        _eosRepoMock.Verify(r => r.UpdateAsync(settlement, It.IsAny<CancellationToken>()), Times.Once);
        _empRepoMock.Verify(r => r.UpdateEmployeeAsync(emp, It.IsAny<CancellationToken>()), Times.Once);
        _eosRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Service_CancelSettlement_SettlementNotFound_ThrowsKeyNotFoundException()
    {
        _eosRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EndOfServiceSettlement?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CancelSettlementAsync(999, "Valid reason", "HR_CANCELLER"));

        Assert.Contains("999 not found", ex.Message);
    }

    [Fact]
    public async Task Service_CancelSettlement_AlreadyCancelled_ThrowsInvalidOperationException()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 104,
            EmployeeCode = "EMP_104",
            Status = EndOfServiceSettlementStatus.Cancelled
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(104, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelSettlementAsync(104, "Another reason", "HR_CANCELLER"));

        Assert.Contains("already cancelled", ex.Message);
    }

    [Fact]
    public async Task Service_CancelSettlement_PostedStatus_ThrowsInvalidOperationException()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 105,
            EmployeeCode = "EMP_105",
            Status = EndOfServiceSettlementStatus.Posted
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(105, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelSettlementAsync(105, "Reversal attempt", "HR_CANCELLER"));

        Assert.Contains("Cannot cancel a posted end of service settlement", ex.Message);
    }

    [Fact]
    public async Task Service_CancelSettlement_ApprovedSettlement_RevertsEmployeeToActive()
    {
        var (emp, _, _) = CreateStandardFixture(new DateTime(2020, 1, 1));
        emp.IsActive = false;
        emp.EmploymentStatus = "TERMINATED";
        emp.TerminationDate = new DateTime(2025, 12, 31);
        emp.TerminationReason = EndOfServiceTerminationTypes.Resignation;

        var settlement = new EndOfServiceSettlement
        {
            Id = 106,
            EmployeeCode = emp.EmployeeCode,
            Status = EndOfServiceSettlementStatus.Approved,
            LastWorkingDate = emp.TerminationDate.Value,
            TerminationType = emp.TerminationReason
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(106, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);
        _eosRepoMock.Setup(r => r.HasActiveAccountingHandoffAsync(106, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);

        var result = await _service.CancelSettlementAsync(106, "Termination rescinded by mutual agreement", "HR_CANCELLER");

        Assert.Equal(EndOfServiceSettlementStatus.Cancelled, result.Status);
        Assert.Equal("HR_CANCELLER", result.CancelledBy);
        Assert.Equal("Termination rescinded by mutual agreement", result.CancellationReason);

        // Employee should be reinstated to ACTIVE
        Assert.True(emp.IsActive);
        Assert.Equal("ACTIVE", emp.EmploymentStatus);
        Assert.Null(emp.TerminationDate);
        Assert.Null(emp.TerminationReason);
        Assert.Equal("HR_CANCELLER", emp.UpdateUser);

        _empRepoMock.Verify(r => r.UpdateEmployeeAsync(emp, It.IsAny<CancellationToken>()), Times.Once);
        _eosRepoMock.Verify(r => r.UpdateAsync(settlement, It.IsAny<CancellationToken>()), Times.Once);
        _eosRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Service_GetByEmployeeCodeAsync_WhenNotFound_ReturnsNull()
    {
        _eosRepoMock.Setup(r => r.GetByEmployeeCodeAsync("EMP_NONEXISTENT", It.IsAny<CancellationToken>()))
            .ReturnsAsync((EndOfServiceSettlement?)null);

        var result = await _service.GetByEmployeeCodeAsync("EMP_NONEXISTENT");

        Assert.Null(result);
    }

    [Fact]
    public async Task Service_ApproveSettlement_WhenAlreadyApproved_ThrowsInvalidOperationException()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 201,
            EmployeeCode = "EMP_201",
            Status = EndOfServiceSettlementStatus.Approved
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(201, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ApproveSettlementAsync(201, "HR_APPROVER"));

        Assert.Contains("Cannot approve end of service settlement in status 'APPROVED'", ex.Message);
    }

    [Fact]
    public async Task Service_ApproveSettlement_WhenCancelled_ThrowsInvalidOperationException()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 202,
            EmployeeCode = "EMP_202",
            Status = EndOfServiceSettlementStatus.Cancelled
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(202, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ApproveSettlementAsync(202, "HR_APPROVER"));

        Assert.Contains("Cannot approve end of service settlement in status 'CANCELLED'", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Service_CancelSettlement_EmptyOrWhitespaceReason_ThrowsArgumentException(string invalidReason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CancelSettlementAsync(203, invalidReason, "HR_CANCELLER"));
    }

    [Fact]
    public async Task Service_CancelSettlement_WhenHasActiveAccountingHandoff_ThrowsInvalidOperationException()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 204,
            EmployeeCode = "EMP_204",
            Status = EndOfServiceSettlementStatus.Approved
        };

        _eosRepoMock.Setup(r => r.GetByIdAsync(204, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);
        _eosRepoMock.Setup(r => r.HasActiveAccountingHandoffAsync(204, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Active handoff in Accounting

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CancelSettlementAsync(204, "Cancel with handoff", "HR_CANCELLER"));

        Assert.Contains("Cannot cancel an end of service settlement with an active accounting handoff", ex.Message);
    }

    [Fact]
    public void CalculationEngine_LeapYearHireDate_AccuratelyCalculatesServiceYears()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2020, 2, 29), basicSalary: 1200m);
        var lastWorkingDate = new DateTime(2024, 2, 29); // 4 calendar years across leap year cycle

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.ContractExpiry,
            lastWorkingDate
        );

        Assert.Equal(4.0m, Math.Round(settlement.ServiceYears, 1));
        Assert.Equal(48, settlement.ServiceMonths); // Total service months (4 * 12)
        Assert.True(settlement.SeverancePay >= 2400m);
    }

    [Fact]
    public void CalculationEngine_NegativeLeaveBalance_GeneratesNegativeLeaveDeduction()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2023, 1, 1), basicSalary: 900m);
        var lastWorkingDate = new DateTime(2024, 1, 1); // 1 year

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            lastWorkingDate,
            remainingLeaveDays: -5m // 5 days overdraft
        );

        // Daily rate: 900 / 30 = 30; Negative deduction: 5 * 30 = 150
        Assert.Equal(0m, settlement.LeaveEncashment);
        Assert.Equal(150m, settlement.NegativeLeaveDeduction);
        Assert.Equal(150m, settlement.TotalDeductions);
        Assert.Equal(settlement.TotalEntitlements - settlement.TotalDeductions, settlement.NetSettlementAmount);
    }

    [Fact]
    public void CalculationEngine_NoticePeriodPay_CalculatesProportionalToCompensationDays()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2022, 1, 1), basicSalary: 1500m);
        var lastWorkingDate = new DateTime(2024, 1, 1);

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.TerminationEmployer,
            lastWorkingDate,
            noticeCompensationDays: 15 // Half a month notice pay
        );

        // Daily rate = 1500 / 30 = 50; Notice period pay = 15 * 50 = 750
        Assert.Equal(750m, settlement.NoticePeriodPay);
        Assert.True(settlement.TotalEntitlements >= 750m);
    }

    [Fact]
    public void CalculationEngine_LeaveDailyRateDivisor_WhenZeroOrNegative_UsesStandardFallbackDivisor30()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2023, 1, 1), basicSalary: 600m);
        policy.LeaveDailyRateDivisor = 0m; // triggers fallback to 30m
        var lastWorkingDate = new DateTime(2024, 1, 1);

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            lastWorkingDate,
            remainingLeaveDays: 10m
        );

        // Daily rate: 600 / 30 = 20; Leave encashment = 10 * 20 = 200
        Assert.Equal(200m, settlement.LeaveEncashment);
    }

    [Fact]
    public void CalculationEngine_DeductionsExceedingEntitlements_ResultsInNegativeNetSettlement()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2024, 1, 1), basicSalary: 500m);
        var lastWorkingDate = new DateTime(2024, 6, 30); // 0.5 year, Resignation under 2 years gives 0 severance

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            lastWorkingDate,
            outstandingLoans: 800m,
            outstandingAdvances: 300m
        );

        Assert.Equal(0m, settlement.SeverancePay);
        Assert.Equal(1100m, settlement.TotalDeductions);
        Assert.True(settlement.NetSettlementAmount < 0m);
        Assert.Equal(settlement.TotalEntitlements - settlement.TotalDeductions, settlement.NetSettlementAmount);
    }

    [Fact]
    public void CalculationEngine_ZeroBasicSalary_YieldsZeroSeverance()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2020, 1, 1), basicSalary: 0m, allowance: 0m);
        var lastWorkingDate = new DateTime(2025, 1, 1); // 5 years

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.ContractExpiry,
            lastWorkingDate
        );

        Assert.Equal(0m, settlement.SeverancePay);
        Assert.Equal(0m, settlement.TotalEntitlements);
        Assert.Equal(0m, settlement.NetSettlementAmount);
    }

    [Fact]
    public void CalculationEngine_ResignationBetweenFiveAndTenYears_YieldsTwoThirdsSeverance()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2018, 1, 1), basicSalary: 1200m);
        var lastWorkingDate = new DateTime(2025, 1, 1); // 7.0 years service

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            lastWorkingDate
        );

        // 5 yrs * 0.5 = 2.5 months; 2 yrs * 1.0 = 2.0 months; Total = 4.5 months
        // Base severance = 4.5 * 1200 = 5400 JOD
        // Resignation factor = 0.6667 -> 5400 * 0.6667 = 3600.180 JOD
        Assert.Equal(7.0m, Math.Round(settlement.ServiceYears, 1));
        Assert.Equal(3600.180m, settlement.SeverancePay);
    }

    [Theory]
    [InlineData(EndOfServiceTerminationTypes.Death)]
    [InlineData(EndOfServiceTerminationTypes.Retirement)]
    [InlineData(EndOfServiceTerminationTypes.ContractExpiry)]
    public void CalculationEngine_SpecialTerminationTypes_YieldsFullSeveranceWithoutPenalty(string terminationType)
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2021, 1, 1), basicSalary: 1000m);
        var lastWorkingDate = new DateTime(2025, 1, 1); // 4.0 years service

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            terminationType,
            lastWorkingDate
        );

        // 4 yrs * 0.5 = 2.0 months * 1000 = 2000 JOD full severance
        Assert.Equal(2000m, settlement.SeverancePay);
    }

    [Fact]
    public void CalculationEngine_GrossSalaryBasis_IncludesOnlyActiveAllowances()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2022, 1, 1), basicSalary: 1000m, allowance: 200m);
        policy.CalculationBasis = "GROSS_SALARY";

        // Add inactive allowance and deduction to ensure they are excluded
        structure.Lines.Add(new SalaryStructureLine
        {
            ComponentCode = "INACTIVE_BONUS",
            Amount = 300m,
            IsActive = false,
            Component = new SalaryComponent { ComponentCode = "INACTIVE_BONUS", ComponentType = "ALLOWANCE" }
        });
        structure.Lines.Add(new SalaryStructureLine
        {
            ComponentCode = "TAX_DEDUCTION",
            Amount = 100m,
            IsActive = true,
            Component = new SalaryComponent { ComponentCode = "TAX_DEDUCTION", ComponentType = "DEDUCTION" }
        });

        var lastWorkingDate = new DateTime(2024, 1, 1); // 2 years service

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.TerminationEmployer,
            lastWorkingDate,
            remainingLeaveDays: 10m
        );

        // Expected gross = 1000 (basic) + 200 (active allowance) = 1200
        Assert.Equal(1000m, settlement.BaseSalarySnapshot);
        Assert.Equal(1200m, settlement.GrossSalarySnapshot);
        // Severance: 2 yrs * 0.5 = 1.0 month * 1200 = 1200 JOD
        Assert.Equal(1200m, settlement.SeverancePay);
        // Daily rate: 1200 / 30 = 40; Leave encashment: 10 * 40 = 400 JOD
        Assert.Equal(400m, settlement.LeaveEncashment);
    }

    [Theory]
    [InlineData(2024, 2, 29, 29)] // Leap year February
    [InlineData(2025, 2, 28, 28)] // Non-leap year February
    [InlineData(2025, 4, 30, 30)] // 30-day month
    [InlineData(2025, 12, 31, 31)] // 31-day month
    public void CalculationEngine_UnpaidSalary_FullMonthWorked_YieldsExactFullGrossSalary(int year, int month, int day, int expectedDaysInMonth)
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2020, 1, 1), basicSalary: 800m, allowance: 200m);
        var lastWorkingDate = new DateTime(year, month, day);

        Assert.Equal(expectedDaysInMonth, DateTime.DaysInMonth(year, month));

        var settlement = _engine.Calculate(
            emp,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            lastWorkingDate
        );

        // Gross salary = 800 + 200 = 1000m
        Assert.Equal(1000m, settlement.UnpaidSalary);
    }

    [Fact]
    public void CalculationEngine_ThrowsArgumentNullException_WhenEmployeeOrStructureOrPolicyIsNull()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2022, 1, 1));
        var lastWorkingDate = new DateTime(2024, 1, 1);

        Assert.Throws<ArgumentNullException>(() => _engine.Calculate(null!, structure, policy, EndOfServiceTerminationTypes.Resignation, lastWorkingDate));
        Assert.Throws<ArgumentNullException>(() => _engine.Calculate(emp, null!, policy, EndOfServiceTerminationTypes.Resignation, lastWorkingDate));
        Assert.Throws<ArgumentNullException>(() => _engine.Calculate(emp, structure, null!, EndOfServiceTerminationTypes.Resignation, lastWorkingDate));
    }

    [Theory]
    [InlineData("UNKNOWN_TYPE")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CalculationEngine_ThrowsArgumentException_WhenTerminationTypeIsInvalid(string? invalidType)
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2022, 1, 1));
        var lastWorkingDate = new DateTime(2024, 1, 1);

        Assert.Throws<ArgumentException>(() => _engine.Calculate(emp, structure, policy, invalidType!, lastWorkingDate));
    }

    [Fact]
    public void CalculationEngine_ThrowsArgumentException_WhenLastWorkingDateEarlierThanHireDate()
    {
        var (emp, structure, policy) = CreateStandardFixture(new DateTime(2024, 6, 1));
        var lastWorkingDate = new DateTime(2024, 5, 31); // 1 day before hire

        Assert.Throws<ArgumentException>(() => _engine.Calculate(emp, structure, policy, EndOfServiceTerminationTypes.Resignation, lastWorkingDate));
    }

    [Fact]
    public async Task Service_CalculateSettlement_ThrowsArgumentNullException_WhenRequestIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CalculateSettlementAsync(null!, "USER", 1L));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Service_CalculateSettlement_ThrowsArgumentException_WhenEmployeeCodeIsWhitespace(string? invalidCode)
    {
        var req = new CreateEndOfServiceRequest(invalidCode!, EndOfServiceTerminationTypes.Resignation, DateTime.UtcNow);
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CalculateSettlementAsync(req, "USER", 1L));
    }

    [Fact]
    public async Task Service_CalculateSettlement_ThrowsKeyNotFoundException_WhenEmployeeNotFound()
    {
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync("NOT_FOUND", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var req = new CreateEndOfServiceRequest("NOT_FOUND", EndOfServiceTerminationTypes.Resignation, DateTime.UtcNow);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CalculateSettlementAsync(req, "USER", 1L));
    }

    [Fact]
    public async Task Service_CalculateSettlement_ThrowsInvalidOperationException_WhenNoActiveSalaryStructure()
    {
        var (emp, structure, _) = CreateStandardFixture(new DateTime(2022, 1, 1));
        structure.IsActive = false; // Inactivate salary structure
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(emp.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _empRepoMock.Setup(r => r.GetActiveSalaryStructureAsync(emp.EmployeeCode, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SalaryStructure?)null);

        var req = new CreateEndOfServiceRequest(emp.EmployeeCode, EndOfServiceTerminationTypes.Resignation, DateTime.UtcNow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CalculateSettlementAsync(req, "USER", 1L));
    }

    [Fact]
    public async Task Service_ApproveSettlement_ThrowsKeyNotFoundException_WhenSettlementNotFound()
    {
        _eosRepoMock.Setup(r => r.GetByIdAsync(999L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EndOfServiceSettlement?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ApproveSettlementAsync(999L, "ADMIN"));
    }

    [Theory]
    [InlineData(EndOfServiceSettlementStatus.Approved)]
    [InlineData(EndOfServiceSettlementStatus.Cancelled)]
    [InlineData(EndOfServiceSettlementStatus.Posted)]
    public async Task Service_ApproveSettlement_ThrowsInvalidOperationException_WhenStatusIsNotDraft(string nonDraftStatus)
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 555L,
            EmployeeCode = "EMP_TEST",
            Status = nonDraftStatus
        };
        _eosRepoMock.Setup(r => r.GetByIdAsync(settlement.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ApproveSettlementAsync(settlement.Id, "ADMIN"));
    }

    [Fact]
    public async Task Service_ApproveSettlement_ThrowsKeyNotFoundException_WhenEmployeeNotFound()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 556L,
            EmployeeCode = "EMP_GHOST",
            Status = EndOfServiceSettlementStatus.Draft
        };
        _eosRepoMock.Setup(r => r.GetByIdAsync(settlement.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(settlement.EmployeeCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ApproveSettlementAsync(settlement.Id, "ADMIN"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Service_CancelSettlement_ThrowsArgumentException_WhenReasonIsWhitespace(string? invalidReason)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CancelSettlementAsync(100L, invalidReason!, "ADMIN"));
    }

    [Fact]
    public async Task Service_CancelSettlement_ThrowsKeyNotFoundException_WhenSettlementNotFound()
    {
        _eosRepoMock.Setup(r => r.GetByIdAsync(888L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EndOfServiceSettlement?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CancelSettlementAsync(888L, "Valid reason", "ADMIN"));
    }

    [Fact]
    public async Task Service_CancelSettlement_ThrowsInvalidOperationException_WhenStatusAlreadyCancelled()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 777L,
            EmployeeCode = "EMP_TEST",
            Status = EndOfServiceSettlementStatus.Cancelled
        };
        _eosRepoMock.Setup(r => r.GetByIdAsync(settlement.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelSettlementAsync(settlement.Id, "Valid reason", "ADMIN"));
    }

    [Fact]
    public async Task Service_CancelSettlement_ThrowsInvalidOperationException_WhenStatusIsPosted()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 778L,
            EmployeeCode = "EMP_TEST",
            Status = EndOfServiceSettlementStatus.Posted
        };
        _eosRepoMock.Setup(r => r.GetByIdAsync(settlement.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelSettlementAsync(settlement.Id, "Valid reason", "ADMIN"));
    }

    [Fact]
    public async Task Service_CancelSettlement_ThrowsInvalidOperationException_WhenHasActiveAccountingHandoff()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 779L,
            EmployeeCode = "EMP_TEST",
            Status = EndOfServiceSettlementStatus.Approved
        };
        _eosRepoMock.Setup(r => r.GetByIdAsync(settlement.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);
        _eosRepoMock.Setup(r => r.HasActiveAccountingHandoffAsync(settlement.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CancelSettlementAsync(settlement.Id, "Valid reason", "ADMIN"));
    }

    [Fact]
    public async Task Service_GetByIdAsync_ReturnsNullWhenNotFound_AndMappedDtoWhenPresent()
    {
        _eosRepoMock.Setup(r => r.GetByIdAsync(101L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EndOfServiceSettlement?)null);

        var nullResult = await _service.GetByIdAsync(101L);
        Assert.Null(nullResult);

        var settlement = new EndOfServiceSettlement
        {
            Id = 102L,
            EmployeeCode = "EMP_FOUND",
            Status = EndOfServiceSettlementStatus.Draft,
            CompanyId = 1,
            CurrencyCode = "JOD",
            NetSettlementAmount = 2500m
        };
        _eosRepoMock.Setup(r => r.GetByIdAsync(102L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        var foundResult = await _service.GetByIdAsync(102L);
        Assert.NotNull(foundResult);
        Assert.Equal(102L, foundResult.Id);
        Assert.Equal("EMP_FOUND", foundResult.EmployeeCode);
        Assert.Equal(2500m, foundResult.NetSettlementAmount);
    }

    [Fact]
    public async Task Service_GetByEmployeeCodeAsync_ReturnsNullWhenNotFound_AndMappedDtoWhenPresent()
    {
        _eosRepoMock.Setup(r => r.GetByEmployeeCodeAsync("EMP_NONE", It.IsAny<CancellationToken>()))
            .ReturnsAsync((EndOfServiceSettlement?)null);

        var nullResult = await _service.GetByEmployeeCodeAsync("EMP_NONE");
        Assert.Null(nullResult);

        var settlement = new EndOfServiceSettlement
        {
            Id = 202L,
            EmployeeCode = "EMP_EXISTS",
            Status = EndOfServiceSettlementStatus.Approved,
            CompanyId = 1,
            CurrencyCode = "USD",
            NetSettlementAmount = 3500m
        };
        _eosRepoMock.Setup(r => r.GetByEmployeeCodeAsync("EMP_EXISTS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        var foundResult = await _service.GetByEmployeeCodeAsync("EMP_EXISTS");
        Assert.NotNull(foundResult);
        Assert.Equal("EMP_EXISTS", foundResult.EmployeeCode);
        Assert.Equal(3500m, foundResult.NetSettlementAmount);
    }

    [Fact]
    public async Task Service_GetListAsync_ReturnsMappedDtoList()
    {
        var list = new List<EndOfServiceSettlement>
        {
            new() { Id = 1L, EmployeeCode = "E1", Status = EndOfServiceSettlementStatus.Draft },
            new() { Id = 2L, EmployeeCode = "E2", Status = EndOfServiceSettlementStatus.Approved }
        };
        _eosRepoMock.Setup(r => r.GetListAsync(1L, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var result = await _service.GetListAsync(1L);
        Assert.Equal(2, result.Count);
        Assert.Equal("E1", result[0].EmployeeCode);
        Assert.Equal("E2", result[1].EmployeeCode);
    }

    [Fact]
    public async Task Service_CalculateSettlementAsync_PropagatesCancellationToken()
    {
        var (employee, structure, policy) = CreateStandardFixture(new DateTime(2020, 1, 1));
        var request = new CreateEndOfServiceRequest(
            employee.EmployeeCode,
            EndOfServiceTerminationTypes.Resignation,
            new DateTime(2024, 6, 30)
        );

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync(employee.EmployeeCode, ct)).ReturnsAsync(employee);
        _eosRepoMock.Setup(r => r.GetActivePolicyAsync(1L, ct)).ReturnsAsync(policy);
        _eosRepoMock.Setup(r => r.GetRemainingLeaveBalanceDaysAsync(employee.EmployeeCode, ct)).ReturnsAsync(5m);
        _eosRepoMock.Setup(r => r.GetOutstandingLoanBalanceAsync(employee.EmployeeCode, ct)).ReturnsAsync(0m);
        _eosRepoMock.Setup(r => r.GetOutstandingAdvanceBalanceAsync(employee.EmployeeCode, ct)).ReturnsAsync(0m);

        var dto = await _service.CalculateSettlementAsync(request, "TEST_USER", 1L, ct);

        Assert.NotNull(dto);
        _eosRepoMock.Verify(r => r.AddAsync(It.IsAny<EndOfServiceSettlement>(), ct), Times.Once);
        _eosRepoMock.Verify(r => r.SaveChangesAsync(ct), Times.Once);
    }

    [Fact]
    public async Task Service_ApproveSettlementAsync_PropagatesCancellationToken()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 401L,
            EmployeeCode = "EMP_APP_CT",
            Status = EndOfServiceSettlementStatus.Draft,
            LastWorkingDate = new DateTime(2026, 6, 30),
            TerminationType = EndOfServiceTerminationTypes.TerminationEmployer
        };
        var emp = new Employee
        {
            EmployeeCode = "EMP_APP_CT",
            IsActive = true,
            EmploymentStatus = "ACTIVE"
        };

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _eosRepoMock.Setup(r => r.GetByIdAsync(401L, ct)).ReturnsAsync(settlement);
        _empRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP_APP_CT", ct)).ReturnsAsync(emp);

        var dto = await _service.ApproveSettlementAsync(401L, "APPROVER", ct);

        Assert.Equal(EndOfServiceSettlementStatus.Approved, dto.Status);
        _eosRepoMock.Verify(r => r.UpdateAsync(settlement, ct), Times.Once);
        _empRepoMock.Verify(r => r.UpdateEmployeeAsync(emp, ct), Times.Once);
        _eosRepoMock.Verify(r => r.SaveChangesAsync(ct), Times.Once);
    }

    [Fact]
    public async Task Service_CancelSettlementAsync_PropagatesCancellationToken()
    {
        var settlement = new EndOfServiceSettlement
        {
            Id = 501L,
            EmployeeCode = "EMP_CAN_CT",
            Status = EndOfServiceSettlementStatus.Draft
        };

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _eosRepoMock.Setup(r => r.GetByIdAsync(501L, ct)).ReturnsAsync(settlement);
        _eosRepoMock.Setup(r => r.HasActiveAccountingHandoffAsync(501L, ct)).ReturnsAsync(false);

        var dto = await _service.CancelSettlementAsync(501L, "Cancelled by user", "CANCELLER", ct);

        Assert.Equal(EndOfServiceSettlementStatus.Cancelled, dto.Status);
        _eosRepoMock.Verify(r => r.UpdateAsync(settlement, ct), Times.Once);
        _eosRepoMock.Verify(r => r.SaveChangesAsync(ct), Times.Once);
    }

    [Fact]
    public async Task Service_GetListAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _eosRepoMock.Setup(r => r.GetListAsync(1L, null, null, ct))
            .ReturnsAsync(new List<EndOfServiceSettlement>());

        var result = await _service.GetListAsync(1L, null, null, ct);

        Assert.Empty(result);
        _eosRepoMock.Verify(r => r.GetListAsync(1L, null, null, ct), Times.Once);
    }

    [Fact]
    public async Task Service_GetHandoffStatusAsync_WhenHandoffNull_ReturnsNull()
    {
        _eosRepoMock.Setup(r => r.GetLatestHandoffAsync(999L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((HrAccountingHandoff?)null);

        var status = await _service.GetHandoffStatusAsync(999L);
        Assert.Null(status);
    }

    [Fact]
    public async Task Service_GetHandoffStatusAsync_WhenHandoffExists_MapsAllFieldsAccurately()
    {
        var handoff = new HrAccountingHandoff
        {
            Id = 777L,
            HandoffReference = "HR-EOS-1-777-1",
            DeliveryStatus = "POSTED",
            Operation = "DISBURSE",
            SourceVersion = 1,
            CreatedAtUtc = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc)
        };

        _eosRepoMock.Setup(r => r.GetLatestHandoffAsync(777L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(handoff);

        var status = await _service.GetHandoffStatusAsync(777L);

        Assert.NotNull(status);
        Assert.Equal(777L, status.HandoffId);
        Assert.Equal("HR-EOS-1-777-1", status.HandoffReference);
        Assert.Equal("POSTED", status.DeliveryStatus);
        Assert.Equal("DISBURSE", status.Operation);
        Assert.Equal(1, status.SourceVersion);
        Assert.Equal(new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc), status.CreatedAtUtc);
    }

    [Fact]
    public async Task Engine_Calculate_WhenNegativeLeaveDaysPresent_AddsNegativeLeaveDeductionLine_AndCalculatesAccurately()
    {
        var (employee, structure, policy) = CreateStandardFixture(new DateTime(2020, 1, 1), basicSalary: 1200m);
        // dailyRate = 1200 / 30 = 40.0
        // negative leave = -5.0 days => negativeLeaveDeduction = 5.0 * 40.0 = 200.0

        var settlement = _engine.Calculate(
            employee,
            structure,
            policy,
            EndOfServiceTerminationTypes.TerminationEmployer,
            new DateTime(2023, 1, 1),
            remainingLeaveDays: -5m
        );

        Assert.Equal(200m, settlement.NegativeLeaveDeduction);
        Assert.Equal(0m, settlement.LeaveEncashment);
        Assert.True(settlement.TotalDeductions >= 200m);

        var deductionLine = settlement.Lines.Find(l => l.ComponentCode == "NEGATIVE_LEAVE_DEDUCTION");
        Assert.NotNull(deductionLine);
        Assert.Equal(200m, deductionLine.Amount);
        Assert.Equal("DEDUCTION", deductionLine.ComponentType);
    }

    [Fact]
    public async Task Engine_Calculate_WhenOtherEntitlementsAndOtherDeductionsPresent_AddsItemizedLines()
    {
        var (employee, structure, policy) = CreateStandardFixture(new DateTime(2020, 1, 1), basicSalary: 900m);

        var settlement = _engine.Calculate(
            employee,
            structure,
            policy,
            EndOfServiceTerminationTypes.TerminationEmployer,
            new DateTime(2023, 1, 1),
            otherEntitlements: 175.500m,
            otherDeductions: 80.250m
        );

        Assert.Equal(175.500m, settlement.OtherEntitlements);
        Assert.Equal(80.250m, settlement.OtherDeductions);

        var entitlementLine = settlement.Lines.Find(l => l.ComponentCode == "OTHER_ENTITLEMENT");
        Assert.NotNull(entitlementLine);
        Assert.Equal(175.500m, entitlementLine.Amount);

        var deductionLine = settlement.Lines.Find(l => l.ComponentCode == "OTHER_DEDUCTION");
        Assert.NotNull(deductionLine);
        Assert.Equal(80.250m, deductionLine.Amount);
    }

    [Fact]
    public async Task Engine_Calculate_WhenNoticeCompensationDaysPresent_AddsNoticePayLine_AndCalculatesAccurately()
    {
        var (employee, structure, policy) = CreateStandardFixture(new DateTime(2020, 1, 1), basicSalary: 1500m);
        // dailyRate = 1500 / 30 = 50.0
        // noticeDays = 30 => noticePeriodPay = 30 * 50 = 1500.0

        var settlement = _engine.Calculate(
            employee,
            structure,
            policy,
            EndOfServiceTerminationTypes.TerminationEmployer,
            new DateTime(2023, 1, 1),
            noticeCompensationDays: 30
        );

        Assert.Equal(1500m, settlement.NoticePeriodPay);
        var noticeLine = settlement.Lines.Find(l => l.ComponentCode == "NOTICE_PAY");
        Assert.NotNull(noticeLine);
        Assert.Equal(1500m, noticeLine.Amount);
        Assert.Equal("ENTITLEMENT", noticeLine.ComponentType);
    }

    [Fact]
    public async Task Engine_Calculate_Resignation_ServiceUnderTwoYears_AppliesZeroResignationFactor()
    {
        var (employee, structure, policy) = CreateStandardFixture(new DateTime(2022, 1, 1), basicSalary: 1000m);
        // 1.5 years service under resignation => resignation factor = 0.0 => severance = 0

        var settlement = _engine.Calculate(
            employee,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            new DateTime(2023, 7, 1) // 1.5 years
        );

        Assert.Equal(0m, settlement.SeverancePay);
    }

    [Fact]
    public void Engine_Calculate_Resignation_ServiceTwoToFiveYears_AppliesOneThirdResignationFactor()
    {
        var (employee, structure, policy) = CreateStandardFixture(new DateTime(2020, 1, 1), basicSalary: 1000m);
        // 3.0 years service: base = 3.0 * 0.5 = 1.5 months * 1000 = 1500
        // Standard policy resignation factor = 0.3333m => 1500 * 0.3333 = 499.950m
        var settlementDefault = _engine.Calculate(
            employee,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            new DateTime(2023, 1, 1)
        );
        Assert.Equal(499.950m, settlementDefault.SeverancePay);

        // When company configures exact 1/3 (0.33333333m)
        policy.ResignationFactorTwoToFiveYears = 1m / 3m;
        var settlementCustom = _engine.Calculate(
            employee,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            new DateTime(2023, 1, 1)
        );
        Assert.Equal(500m, settlementCustom.SeverancePay);
    }

    [Fact]
    public void Engine_Calculate_Resignation_ServiceBetweenFiveAndTenYears_AppliesTwoThirdsResignationFactor()
    {
        var (employee, structure, policy) = CreateStandardFixture(new DateTime(2016, 1, 1), basicSalary: 1000m);
        // 7.0 years service:
        // first 5 years * 0.5 = 2.5 months
        // subsequent 2 years * 1.0 = 2.0 months
        // base accrual = 4.5 months * 1000 = 4500
        // Standard policy resignation factor = 0.6667m => 4500 * 0.6667 = 3000.150m
        var settlementDefault = _engine.Calculate(
            employee,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            new DateTime(2023, 1, 1)
        );
        Assert.Equal(3000.150m, settlementDefault.SeverancePay);

        // When company configures exact 2/3 (0.66666667m)
        policy.ResignationFactorFiveToTenYears = 2m / 3m;
        var settlementCustom = _engine.Calculate(
            employee,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            new DateTime(2023, 1, 1)
        );
        Assert.Equal(3000m, settlementCustom.SeverancePay);
    }

    [Fact]
    public async Task Engine_Calculate_Resignation_ServiceTenYearsAndAbove_AppliesFullResignationFactor()
    {
        var (employee, structure, policy) = CreateStandardFixture(new DateTime(2011, 1, 1), basicSalary: 1000m);
        // 11.0 years service:
        // first 5 years * 0.5 = 2.5 months
        // subsequent 6 years * 1.0 = 6.0 months
        // base accrual = 8.5 months * 1000 = 8500
        // Resignation factor = 1.0 => severance = 8500

        var settlement = _engine.Calculate(
            employee,
            structure,
            policy,
            EndOfServiceTerminationTypes.Resignation,
            new DateTime(2022, 1, 1)
        );

        Assert.Equal(8500m, settlement.SeverancePay);
    }

    [Theory]
    [InlineData(EndOfServiceTerminationTypes.ContractExpiry)]
    [InlineData(EndOfServiceTerminationTypes.Retirement)]
    [InlineData(EndOfServiceTerminationTypes.Death)]
    [InlineData(EndOfServiceTerminationTypes.TerminationEmployer)]
    public void Engine_Calculate_NonResignationTerminationTypes_DoNotApplyResignationReduction(string termType)
    {
        var (employee, structure, policy) = CreateStandardFixture(new DateTime(2020, 1, 1), basicSalary: 1000m);
        // 3.0 years service: base = 3.0 * 0.5 = 1.5 months * 1000 = 1500
        // Since NOT resignation, full 1500 is paid without resignation factor reduction (which would be 500 for resignation)

        var settlement = _engine.Calculate(
            employee,
            structure,
            policy,
            termType,
            new DateTime(2023, 1, 1)
        );

        Assert.Equal(1500m, settlement.SeverancePay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Engine_Calculate_WhenLeaveDailyRateDivisorIsZeroOrNegative_DefaultsToThirty(decimal divisor)
    {
        var (employee, structure, policy) = CreateStandardFixture(new DateTime(2020, 1, 1), basicSalary: 3000m);
        policy.LeaveDailyRateDivisor = divisor;

        // remainingLeaveDays = 1.0 => dailyRate = 3000 / 30 = 100.0
        var settlement = _engine.Calculate(
            employee,
            structure,
            policy,
            EndOfServiceTerminationTypes.TerminationEmployer,
            new DateTime(2023, 1, 1),
            remainingLeaveDays: 1m
        );

        Assert.Equal(100m, settlement.LeaveEncashment);
    }
}
