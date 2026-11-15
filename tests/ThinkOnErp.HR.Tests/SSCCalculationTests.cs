using System;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class SSCCalculationTests
{
    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 2)]
    public void CalculateSSC_AppliesConfiguredBranchRounding(int roundingRule, decimal expected)
    {
        var service = new SSCCalculationService(Mock.Of<IPolicyRepository>());
        var result = service.CalculateSSC(5m, false,
            new SSCPolicy { EmployeeContribRate = 0.5m, EmployerContribRate = 0m, MonthlyCeilingCap = 0m },
            0, roundingRule);

        Assert.Equal(expected, result.EmployeeContribution);
    }

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

    [Fact]
    public void CalculateSSC_WhenPolicyNull_AppliesStatutoryDefaults()
    {
        // Null policy defaults: empRate = 0.075, emprRate = 0.1425, ceiling = 3617, code = "DEFAULT_SSC"
        var result = _service.CalculateSSC(1000m, isHighRiskRole: false, policy: null);

        Assert.Equal(1000m, result.EligibleSalary);
        Assert.Equal(75.000m, result.EmployeeContribution);
        Assert.Equal(142.500m, result.EmployerContribution);
        Assert.Equal(0.000m, result.HighRiskSurcharge);
        Assert.Equal("DEFAULT_SSC", result.PolicyCode);
        Assert.Equal(3617.000m, result.MonthlyCeilingCap);
    }

    [Fact]
    public void CalculateSSC_WhenSalaryZeroOrNegative_ReturnsZeroContributions()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.075m,
            EmployerContribRate = 0.1425m,
            MonthlyCeilingCap = 3617m
        };

        var zeroResult = _service.CalculateSSC(0m, false, policy);
        Assert.Equal(0m, zeroResult.EligibleSalary);
        Assert.Equal(0m, zeroResult.EmployeeContribution);
        Assert.Equal(0m, zeroResult.EmployerContribution);

        var negResult = _service.CalculateSSC(-500m, false, policy);
        Assert.Equal(0m, negResult.EligibleSalary);
        Assert.Equal(0m, negResult.EmployeeContribution);
        Assert.Equal(0m, negResult.EmployerContribution);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void CalculateSSC_WhenCeilingZeroOrNegative_TreatsAsUncapped(decimal ceiling)
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.05m,
            EmployerContribRate = 0.10m,
            MonthlyCeilingCap = ceiling // Uncapped
        };

        var result = _service.CalculateSSC(10000m, false, policy);

        Assert.Equal(10000m, result.EligibleSalary);
        Assert.Equal(500m, result.EmployeeContribution);
        Assert.Equal(1000m, result.EmployerContribution);
    }

    [Fact]
    public void CalculateSSC_WithCustomRoundingDecimals_AppliesPrecision()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.0755m,
            EmployerContribRate = 0.1425m,
            MonthlyCeilingCap = 3000m
        };

        // 2 decimals (e.g. SAR / USD)
        var result2 = _service.CalculateSSC(1234.56m, false, policy, roundingDecimals: 2);
        Assert.Equal(Math.Round(1234.56m * 0.0755m, 2), result2.EmployeeContribution);

        // 0 decimals (e.g. JPY / KRW)
        var result0 = _service.CalculateSSC(1234.56m, false, policy, roundingDecimals: 0);
        Assert.Equal(Math.Round(1234.56m * 0.0755m, 0), result0.EmployeeContribution);
    }

    [Fact]
    public async System.Threading.Tasks.Task CalculateSSCAsync_FetchesPolicyFromRepository()
    {
        var policy = new SSCPolicy
        {
            Code = "SSC_2026",
            EmployeeContribRate = 0.08m,
            EmployerContribRate = 0.12m,
            MonthlyCeilingCap = 5000m
        };

        _policyRepoMock.Setup(r => r.GetActiveSSCPolicyAsync(1, new DateTime(2026, 11, 1), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(policy);

        var result = await _service.CalculateSSCAsync(1, 2000m, false, new DateTime(2026, 11, 1));

        Assert.Equal("SSC_2026", result.PolicyCode);
        Assert.Equal(160.000m, result.EmployeeContribution);
        Assert.Equal(240.000m, result.EmployerContribution);
    }

    [Fact]
    public void CalculateSSC_WhenSalaryExactAtCeiling_ComputesExactCeilingContribution()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.10m,
            EmployerContribRate = 0.15m,
            MonthlyCeilingCap = 3000m
        };

        var result = _service.CalculateSSC(3000m, false, policy);

        Assert.Equal(3000m, result.EligibleSalary);
        Assert.Equal(300m, result.EmployeeContribution);
        Assert.Equal(450m, result.EmployerContribution);
    }

    [Fact]
    public void CalculateSSC_WhenSalaryBelowStatutoryCeiling_EligibleSalaryEqualsGrossSalary()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.075m,
            EmployerContribRate = 0.1425m,
            MonthlyCeilingCap = 3617m
        };

        var result = _service.CalculateSSC(2500m, false, policy);

        Assert.Equal(2500m, result.EligibleSalary);
        Assert.Equal(187.500m, result.EmployeeContribution);
        Assert.Equal(356.250m, result.EmployerContribution);
    }

    [Fact]
    public void CalculateSSC_WhenHighRiskSurchargeRateZero_NoHighRiskAddedEvenIfHighRiskRole()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.075m,
            EmployerContribRate = 0.1425m,
            HighRiskSurchargeRate = 0m,
            MonthlyCeilingCap = 3617m
        };

        var result = _service.CalculateSSC(1000m, isHighRiskRole: true, policy);

        Assert.Equal(0m, result.HighRiskSurcharge);
        Assert.Equal(142.500m, result.EmployerContribution);
    }

    [Theory]
    [InlineData(2, 75.50)]
    [InlineData(3, 75.500)]
    [InlineData(4, 75.5000)]
    public void CalculateSSC_Theory_MultipleCurrencyRoundings(int decimals, decimal expectedEmp)
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.0755m,
            EmployerContribRate = 0.1425m,
            MonthlyCeilingCap = 3617m
        };

        var result = _service.CalculateSSC(1000m, false, policy, roundingDecimals: decimals);

        Assert.Equal(expectedEmp, result.EmployeeContribution);
    }

    [Fact]
    public async System.Threading.Tasks.Task CalculateSSCAsync_WhenRepositoryReturnsNull_FallsBackToStatutoryDefaults()
    {
        _policyRepoMock.Setup(r => r.GetActiveSSCPolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync((SSCPolicy?)null);

        var result = await _service.CalculateSSCAsync(1, 1000m, false, new DateTime(2026, 11, 1));

        Assert.Equal("DEFAULT_SSC", result.PolicyCode);
        Assert.Equal(75.000m, result.EmployeeContribution);
        Assert.Equal(142.500m, result.EmployerContribution);
    }

    [Fact]
    public void CalculateSSC_WhenEmployerRateZero_ComputesZeroEmployerContribution()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.05m,
            EmployerContribRate = 0m,
            MonthlyCeilingCap = 3617m
        };

        var result = _service.CalculateSSC(1000m, false, policy);

        Assert.Equal(50.000m, result.EmployeeContribution);
        Assert.Equal(0.000m, result.EmployerContribution);
    }

    [Fact]
    public void CalculateSSC_WhenSalaryJustBelowCeiling_CalculatesContributionOnFullSalary()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.075m,
            EmployerContribRate = 0.1425m,
            MonthlyCeilingCap = 3617.000m
        };

        var salary = 3616.999m;
        var result = _service.CalculateSSC(salary, false, policy);

        Assert.Equal(salary, result.EligibleSalary);
        Assert.Equal(Math.Round(salary * 0.075m, 3), result.EmployeeContribution);
        Assert.Equal(Math.Round(salary * 0.1425m, 3), result.EmployerContribution);
    }

    [Fact]
    public void CalculateSSC_WhenSalaryJustAboveCeiling_CapsEligibleSalaryAtCeiling()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.075m,
            EmployerContribRate = 0.1425m,
            MonthlyCeilingCap = 3617.000m
        };

        var salary = 3617.001m;
        var result = _service.CalculateSSC(salary, false, policy);

        Assert.Equal(3617.000m, result.EligibleSalary);
        Assert.Equal(Math.Round(3617.000m * 0.075m, 3), result.EmployeeContribution);
        Assert.Equal(Math.Round(3617.000m * 0.1425m, 3), result.EmployerContribution);
    }

    [Fact]
    public void CalculateSSC_WhenSalaryDoubleCeiling_CapsAtExactCeiling()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.08m,
            EmployerContribRate = 0.12m,
            MonthlyCeilingCap = 3000.000m
        };

        var salary = 6000.000m;
        var result = _service.CalculateSSC(salary, false, policy);

        Assert.Equal(3000.000m, result.EligibleSalary);
        Assert.Equal(240.000m, result.EmployeeContribution);
        Assert.Equal(360.000m, result.EmployerContribution);
    }

    [Fact]
    public void CalculateSSC_FractionalRates_CalculatesExactContributions()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.0575m,      // 5.75%
            EmployerContribRate = 0.1125m,      // 11.25%
            HighRiskSurchargeRate = 0.0075m,    // 0.75%
            MonthlyCeilingCap = 5000.000m
        };

        var result = _service.CalculateSSC(1000m, isHighRiskRole: true, policy);

        Assert.Equal(1000m, result.EligibleSalary);
        Assert.Equal(57.500m, result.EmployeeContribution);
        Assert.Equal(7.500m, result.HighRiskSurcharge);
        Assert.Equal(120.000m, result.EmployerContribution); // 112.500 + 7.500
        Assert.Equal(0.1200m, result.EmployerRate);           // 0.1125 + 0.0075
    }

    [Fact]
    public void CalculateSSC_WhenHighRiskRoleAndCustomSurcharge_CalculatesHighRiskAndAddsToEmployer()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.075m,
            EmployerContribRate = 0.1425m,
            HighRiskSurchargeRate = 0.025m,     // 2.5% surcharge
            MonthlyCeilingCap = 4000.000m
        };

        var result = _service.CalculateSSC(2000m, isHighRiskRole: true, policy);

        Assert.Equal(2000m, result.EligibleSalary);
        Assert.Equal(150.000m, result.EmployeeContribution);
        Assert.Equal(50.000m, result.HighRiskSurcharge);      // 2000 * 0.025
        Assert.Equal(335.000m, result.EmployerContribution); // 285.000 + 50.000
        Assert.Equal(0.1675m, result.EmployerRate);           // 0.1425 + 0.025
    }

    [Fact]
    public void CalculateSSC_MultiCurrencyRounding_ZeroDecimals_JPY_RoundsToWholeNumber()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.0915m,
            EmployerContribRate = 0.0915m,
            MonthlyCeilingCap = 650000m
        };

        var result = _service.CalculateSSC(250123m, false, policy, roundingDecimals: 0);

        Assert.Equal(250123m, result.EligibleSalary);
        Assert.Equal(Math.Round(250123m * 0.0915m, 0), result.EmployeeContribution);
        Assert.Equal(Math.Round(250123m * 0.0915m, 0), result.EmployerContribution);
    }

    [Fact]
    public void CalculateSSC_MultiCurrencyRounding_ThreeDecimals_JOD_RoundsToThreePlaces()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.075m,
            EmployerContribRate = 0.1425m,
            MonthlyCeilingCap = 3617.000m
        };

        var result = _service.CalculateSSC(1234.567m, false, policy, roundingDecimals: 3);

        Assert.Equal(1234.567m, result.EligibleSalary);
        Assert.Equal(Math.Round(1234.567m * 0.075m, 3), result.EmployeeContribution);
        Assert.Equal(Math.Round(1234.567m * 0.1425m, 3), result.EmployerContribution);
    }

    [Fact]
    public void CalculateSSC_MultiCurrencyRounding_TwoDecimals_USD_RoundsToTwoPlaces()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.062m,
            EmployerContribRate = 0.062m,
            MonthlyCeilingCap = 10000m
        };

        var result = _service.CalculateSSC(4500.55m, false, policy, roundingDecimals: 2);

        Assert.Equal(4500.55m, result.EligibleSalary);
        Assert.Equal(Math.Round(4500.55m * 0.062m, 2), result.EmployeeContribution);
        Assert.Equal(Math.Round(4500.55m * 0.062m, 2), result.EmployerContribution);
    }

    [Fact]
    public async System.Threading.Tasks.Task CalculateSSCAsync_PropagatesCancellationToken_ToRepository()
    {
        using var cts = new System.Threading.CancellationTokenSource();
        var token = cts.Token;

        var policy = new SSCPolicy
        {
            Code = "SSC_CT_TEST",
            EmployeeContribRate = 0.075m,
            EmployerContribRate = 0.1425m,
            MonthlyCeilingCap = 3617m
        };

        _policyRepoMock.Setup(r => r.GetActiveSSCPolicyAsync(1, new DateTime(2026, 11, 10), token))
            .ReturnsAsync(policy);

        var result = await _service.CalculateSSCAsync(1, 1500m, false, new DateTime(2026, 11, 10), token);

        Assert.Equal("SSC_CT_TEST", result.PolicyCode);
        Assert.Equal(112.500m, result.EmployeeContribution);
        _policyRepoMock.Verify(r => r.GetActiveSSCPolicyAsync(1, new DateTime(2026, 11, 10), token), Times.Once);
    }

    [Fact]
    public async System.Threading.Tasks.Task CalculateSSCAsync_WithExplicitRoundingDecimals_AppliesRounding()
    {
        var policy = new SSCPolicy
        {
            Code = "SSC_ROUNDING",
            EmployeeContribRate = 0.0755m,
            EmployerContribRate = 0.1425m,
            MonthlyCeilingCap = 3000m
        };

        _policyRepoMock.Setup(r => r.GetActiveSSCPolicyAsync(1, new DateTime(2026, 11, 10), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(policy);

        var result = await _service.CalculateSSCAsync(1, 1234.56m, false, new DateTime(2026, 11, 10), roundingDecimals: 2);

        Assert.Equal(Math.Round(1234.56m * 0.0755m, 2), result.EmployeeContribution);
        Assert.Equal(Math.Round(1234.56m * 0.1425m, 2), result.EmployerContribution);
    }

    [Fact]
    public void CalculateSSC_PolicyCodeNull_DefaultsToDEFAULT_SSC()
    {
        var policy = new SSCPolicy
        {
            Code = null!,
            EmployeeContribRate = 0.05m,
            EmployerContribRate = 0.10m,
            MonthlyCeilingCap = 2000m
        };

        var result = _service.CalculateSSC(1000m, false, policy);

        Assert.Equal("DEFAULT_SSC", result.PolicyCode);
    }

    [Fact]
    public void CalculateSSC_PolicyCodePopulated_PreservesPolicyCode()
    {
        var policy = new SSCPolicy
        {
            Code = "SSC_SPECIAL_2026",
            EmployeeContribRate = 0.06m,
            EmployerContribRate = 0.12m,
            MonthlyCeilingCap = 4000m
        };

        var result = _service.CalculateSSC(2000m, false, policy);

        Assert.Equal("SSC_SPECIAL_2026", result.PolicyCode);
    }

    [Fact]
    public void CalculateSSC_EffectiveEmployerRate_WithoutHighRiskRole_MatchesEmployerRate()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.075m,
            EmployerContribRate = 0.1425m,
            HighRiskSurchargeRate = 0.015m,
            MonthlyCeilingCap = 3617m
        };

        var result = _service.CalculateSSC(1000m, isHighRiskRole: false, policy);

        Assert.Equal(0.1425m, result.EmployerRate);
        Assert.Equal(0.000m, result.HighRiskSurcharge);
    }

    [Fact]
    public void CalculateSSC_EffectiveEmployerRate_WithHighRiskRole_SumsEmployerAndSurchargeRate()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.075m,
            EmployerContribRate = 0.1425m,
            HighRiskSurchargeRate = 0.015m,
            MonthlyCeilingCap = 3617m
        };

        var result = _service.CalculateSSC(1000m, isHighRiskRole: true, policy);

        Assert.Equal(0.1575m, result.EmployerRate); // 0.1425 + 0.015
        Assert.Equal(15.000m, result.HighRiskSurcharge);
    }

    [Fact]
    public void CalculateSSC_DefaultOverloadWithoutRoundingDecimals_DefaultsToThreeDecimals()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.07555m,
            EmployerContribRate = 0.14255m,
            MonthlyCeilingCap = 3617m
        };

        var result = _service.CalculateSSC(1000m, false, policy);

        Assert.Equal(Math.Round(1000m * 0.07555m, 3), result.EmployeeContribution);
        Assert.Equal(Math.Round(1000m * 0.14255m, 3), result.EmployerContribution);
    }

    [Fact]
    public async System.Threading.Tasks.Task CalculateSSCAsync_DefaultOverloadWithoutRoundingDecimals_DefaultsToThreeDecimals()
    {
        var policy = new SSCPolicy
        {
            EmployeeContribRate = 0.07555m,
            EmployerContribRate = 0.14255m,
            MonthlyCeilingCap = 3617m
        };

        _policyRepoMock.Setup(r => r.GetActiveSSCPolicyAsync(1, new DateTime(2026, 11, 10), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(policy);

        var result = await _service.CalculateSSCAsync(1, 1000m, false, new DateTime(2026, 11, 10));

        Assert.Equal(Math.Round(1000m * 0.07555m, 3), result.EmployeeContribution);
        Assert.Equal(Math.Round(1000m * 0.14255m, 3), result.EmployerContribution);
    }
}
