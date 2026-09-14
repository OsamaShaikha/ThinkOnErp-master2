using System;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class SSCCalculationTests
{
    private readonly Mock<IPolicyRepository> _policyRepoMock;
    private readonly SSCCalculationService _service;

    public SSCCalculationTests()
    {
        _policyRepoMock = new Mock<IPolicyRepository>();
        _service = new SSCCalculationService(_policyRepoMock.Object);
    }

    [Fact]
    public void StandardRole_CalculatesStatutoryRates()
    {
        // 1,000 JOD salary: Emp (7.5%) = 75.000, Empr (14.25%) = 142.500
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.075000m,
            EmployerContribRate = 0.142500m,
            HighRiskSurchargeRate = 0.010000m,
            MonthlyCeilingCap = 3617.000m
        };

        var result = _service.CalculateSSC(1000m, isHighRiskRole: false, policy);

        Assert.Equal(1000m, result.EligibleSalary);
        Assert.Equal(75.000m, result.EmployeeContribution);
        Assert.Equal(142.500m, result.EmployerContribution);
        Assert.Equal(0.000m, result.HighRiskSurcharge);
    }

    [Fact]
    public void HighRiskRole_AddsHighRiskSurchargeToEmployer()
    {
        // 1,000 JOD salary with high risk:
        // Employee: 7.5% = 75.000
        // Employer: 14.25% + 1% = 15.25% = 152.500 (142.500 base + 10.000 surcharge)
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.075000m,
            EmployerContribRate = 0.142500m,
            HighRiskSurchargeRate = 0.010000m,
            MonthlyCeilingCap = 3617.000m
        };

        var result = _service.CalculateSSC(1000m, isHighRiskRole: true, policy);

        Assert.Equal(1000m, result.EligibleSalary);
        Assert.Equal(75.000m, result.EmployeeContribution);
        Assert.Equal(152.500m, result.EmployerContribution);
        Assert.Equal(10.000m, result.HighRiskSurcharge);
    }

    [Fact]
    public void SalaryExceedingCeiling_IsCappedAtStatutoryCeiling()
    {
        // 5,000 JOD salary capped at 3,617 JOD
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.075000m,
            EmployerContribRate = 0.142500m,
            HighRiskSurchargeRate = 0.010000m,
            MonthlyCeilingCap = 3617.000m
        };

        var result = _service.CalculateSSC(5000m, isHighRiskRole: false, policy);

        Assert.Equal(3617.000m, result.EligibleSalary);
        Assert.Equal(Math.Round(3617.000m * 0.075m, 3), result.EmployeeContribution);
        Assert.Equal(Math.Round(3617.000m * 0.1425m, 3), result.EmployerContribution);
    }

    [Fact]
    public void DynamicPolicy_AppliesCustomRatesFromConfiguration()
    {
        // Custom rates e.g. future statutory change (8% emp, 15% empr, 4000 cap)
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.080000m,
            EmployerContribRate = 0.150000m,
            HighRiskSurchargeRate = 0.020000m,
            MonthlyCeilingCap = 4000.000m
        };

        var result = _service.CalculateSSC(2000m, isHighRiskRole: true, policy);

        Assert.Equal(2000m, result.EligibleSalary);
        Assert.Equal(160.000m, result.EmployeeContribution); // 2000 * 0.08
        Assert.Equal(340.000m, result.EmployerContribution); // 2000 * (0.15 + 0.02)
        Assert.Equal(40.000m, result.HighRiskSurcharge);     // 2000 * 0.02
    }
}
