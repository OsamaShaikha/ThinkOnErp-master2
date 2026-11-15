using System;
using System.Collections.Generic;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class TaxCalculationEngineTests
{
    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 2)]
    public void CalculateIncomeTax_AppliesConfiguredBranchRounding(int roundingRule, decimal expected)
    {
        var engine = new TaxCalculationEngine(Mock.Of<IPolicyRepository>());
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m,
            PersonalExemptionDependent = 0m,
            IsSscTaxDeductible = false,
            NationalContribThreshold = decimal.MaxValue,
            Brackets = [new TaxBracket { BracketOrder = 1, LowerLimit = 0m, RatePercent = 50m }]
        };

        var result = engine.CalculateIncomeTax(5m, 0m, 0, policy, 0, roundingRule);

        Assert.Equal(expected, result.MonthlyTax);
    }

    private readonly Mock<IPolicyRepository> _policyRepoMock;
    private readonly TaxCalculationEngine _engine;

    public TaxCalculationEngineTests()
    {
        _policyRepoMock = new Mock<IPolicyRepository>();
        _engine = new TaxCalculationEngine(_policyRepoMock.Object);
    }

    [Fact]
    public void SalaryBelowExemptions_ResultsInZeroTax()
    {
        // Monthly gross 700 JOD (annualized 8,400 JOD)
        // Personal exemption is 9,000 JOD -> Taxable net is 0
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 9000m,
            PersonalExemptionDependent = 1000m,
            NationalContribThreshold = 200000m,
            IsSscTaxDeductible = true
        };

        var result = _engine.CalculateIncomeTax(700m, employeeSscContrib: 52.5m, dependentCount: 0, policy);

        Assert.Equal(0m, result.MonthlyTax);
        Assert.Equal(0m, result.AnnualTaxableNet);
    }

    [Fact]
    public void SscDeductibility_ReducesTaxableBase()
    {
        // Gross 2,000 JOD, SSC 150 JOD
        // If SSC is deductible: monthly base = 1,850 JOD
        // If SSC is not deductible: monthly base = 2,000 JOD
        var policyDeductible = new TaxPolicy
        {
            PersonalExemptionSelf = 9000m,
            PersonalExemptionDependent = 1000m,
            IsSscTaxDeductible = true
        };

        var policyNonDeductible = new TaxPolicy
        {
            PersonalExemptionSelf = 9000m,
            PersonalExemptionDependent = 1000m,
            IsSscTaxDeductible = false
        };

        var resDeductible = _engine.CalculateIncomeTax(2000m, employeeSscContrib: 150m, dependentCount: 0, policyDeductible);
        var resNonDeductible = _engine.CalculateIncomeTax(2000m, employeeSscContrib: 150m, dependentCount: 0, policyNonDeductible);

        Assert.True(resDeductible.MonthlyTaxableGross < resNonDeductible.MonthlyTaxableGross);
        Assert.True(resDeductible.MonthlyTax < resNonDeductible.MonthlyTax);
    }

    [Fact]
    public void DependentExemptions_ReduceTaxAmount()
    {
        // Gross 1,500 JOD, 0 dependents vs 3 dependents (max 3,000 JOD extra exemption)
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 9000m,
            PersonalExemptionDependent = 1000m,
            IsSscTaxDeductible = true
        };

        var res0 = _engine.CalculateIncomeTax(1500m, employeeSscContrib: 112.5m, dependentCount: 0, policy);
        var res3 = _engine.CalculateIncomeTax(1500m, employeeSscContrib: 112.5m, dependentCount: 3, policy);

        Assert.Equal(9000m, res0.TotalAnnualExemptions);
        Assert.Equal(12000m, res3.TotalAnnualExemptions);
        Assert.True(res3.MonthlyTax < res0.MonthlyTax);
    }

    [Fact]
    public void ProgressiveBrackets_CalculatesCorrectMarginalTax()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m, // 0 exemptions to test pure brackets
            PersonalExemptionDependent = 0m,
            IsSscTaxDeductible = false,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = 5000, RatePercent = 5.0m },
                new() { BracketOrder = 2, LowerLimit = 5000, UpperLimit = 10000, RatePercent = 10.0m }
            }
        };

        // Annualized gross = 6,000 JOD (monthly 500 JOD)
        // Bracket 1: 0 - 5,000 at 5% = 250 JOD
        // Bracket 2: 5,000 - 6,000 (1,000) at 10% = 100 JOD
        // Total annual tax = 350 JOD -> Monthly = 350 / 12 = 29.167 JOD
        var result = _engine.CalculateIncomeTax(500m, employeeSscContrib: 0m, dependentCount: 0, policy);

        Assert.Equal(29.167m, result.MonthlyTax);
    }

    [Fact]
    public void HighIncome_AppliesNationalSolidarityContribution()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 9000m,
            PersonalExemptionDependent = 0m,
            NationalContribThreshold = 200000m,
            NationalContribRate = 0.010000m,
            IsSscTaxDeductible = false
        };

        // Annual gross: 300,000 JOD (monthly 25,000 JOD)
        // Net taxable = 300,000 - 9,000 = 291,000 JOD
        // Excess over 200,000 = 91,000 JOD
        // National Solidarity = 91,000 * 1% = 910 JOD / 12 = 75.833 JOD/month
        var result = _engine.CalculateIncomeTax(25000m, employeeSscContrib: 0m, dependentCount: 0, policy);

        Assert.Equal(75.833m, result.MonthlyNationalSolidarityContrib);
    }

    [Fact]
    public void CalculateIncomeTax_WhenPolicyNull_AppliesStatutoryDefaults()
    {
        // Null policy defaults: PersonalExemptionSelf = 9000, PersonalExemptionDependent = 1000 (max 3 dependents), code = "DEFAULT_TAX"
        var result = _engine.CalculateIncomeTax(1500m, employeeSscContrib: 112.5m, dependentCount: 2, policy: null);

        // Deductible base = 1500 - 112.5 = 1387.5
        // Total exemptions = 9000 + 2000 = 11000
        Assert.Equal("DEFAULT_TAX", result.PolicyCode);
        Assert.Equal(11000m, result.TotalAnnualExemptions);
        Assert.True(result.MonthlyTax > 0);
    }

    [Fact]
    public void CalculateIncomeTax_WhenTaxableBaseNegativeOrZero_ReturnsZeroTax()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 9000m,
            IsSscTaxDeductible = true
        };

        // SSC is 200 JOD, gross is only 150 JOD -> taxableBase = -50 JOD <= 0
        var result = _engine.CalculateIncomeTax(150m, employeeSscContrib: 200m, dependentCount: 0, policy);

        Assert.Equal(0m, result.MonthlyTax);
        Assert.Equal(0m, result.AnnualTaxableNet);
        Assert.Equal(0m, result.MonthlyTaxableGross);
    }

    [Fact]
    public void CalculateIncomeTax_WhenMaxExemptedDependentsExplicitlyZero_IgnoresDependents()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 9000m,
            PersonalExemptionDependent = 1000m,
            MaxExemptedDependents = 0 // Explicitly disallowed
        };

        var result = _engine.CalculateIncomeTax(1500m, employeeSscContrib: 0m, dependentCount: 4, policy);

        // Only personal self exemption is granted
        Assert.Equal(9000m, result.TotalAnnualExemptions);
    }

    [Fact]
    public void CalculateIncomeTax_WhenMaxExemptedDependentsConfigured_ClampsToMaxLimit()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 9000m,
            PersonalExemptionDependent = 1000m,
            MaxExemptedDependents = 2 // Max 2
        };

        // 5 dependents provided
        var result = _engine.CalculateIncomeTax(1500m, employeeSscContrib: 0m, dependentCount: 5, policy);

        // 9000 + (2 * 1000) = 11000
        Assert.Equal(11000m, result.TotalAnnualExemptions);
    }

    [Fact]
    public void CalculateIncomeTax_WithCustomRoundingDecimals_RoundsAccurately()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = null, RatePercent = 7.333333m }
            }
        };

        // 2 decimals
        var res2 = _engine.CalculateIncomeTax(1000m, 0m, 0, policy, roundingDecimals: 2);
        Assert.Equal(2, BitConverter.GetBytes(decimal.GetBits(res2.MonthlyTax)[3])[2]); // 2 decimal places

        // 0 decimals
        var res0 = _engine.CalculateIncomeTax(1000m, 0m, 0, policy, roundingDecimals: 0);
        Assert.Equal(Math.Round(res2.MonthlyTax, 0), res0.MonthlyTax);
    }

    [Fact]
    public async System.Threading.Tasks.Task CalculateIncomeTaxAsync_FetchesPolicyFromRepository()
    {
        var policy = new TaxPolicy
        {
            Code = "TAX_2026",
            PersonalExemptionSelf = 10000m,
            PersonalExemptionDependent = 1500m
        };

        _policyRepoMock.Setup(r => r.GetActiveTaxPolicyAsync(1, new DateTime(2026, 11, 1), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(policy);

        var result = await _engine.CalculateIncomeTaxAsync(1, 1500m, 0m, 1, new DateTime(2026, 11, 1));

        Assert.Equal("TAX_2026", result.PolicyCode);
        Assert.Equal(11500m, result.TotalAnnualExemptions);
    }

    [Fact]
    public void CalculateIncomeTax_ProgressiveBrackets_HandlesTopBracketWithNoUpperLimit()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m,
            PersonalExemptionDependent = 0m,
            IsSscTaxDeductible = false,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = 10000, RatePercent = 10.0m },
                new() { BracketOrder = 2, LowerLimit = 10000, UpperLimit = null, RatePercent = 25.0m }
            }
        };

        // Monthly 2000 -> Annual 24,000 JOD
        // Bracket 1: 10,000 * 10% = 1,000 JOD
        // Bracket 2: (24,000 - 10,000) * 25% = 14,000 * 25% = 3,500 JOD
        // Total annual = 4,500 JOD -> Monthly = 4,500 / 12 = 375.000 JOD
        var result = _engine.CalculateIncomeTax(2000m, 0m, 0, policy);

        Assert.Equal(375.000m, result.MonthlyTax);
        Assert.Equal(24000m, result.AnnualTaxableNet);
    }

    [Fact]
    public void CalculateIncomeTax_WhenNationalThresholdNotMet_ReturnsZeroSolidarityContrib()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 9000m,
            NationalContribThreshold = 200000m,
            NationalContribRate = 0.01m,
            IsSscTaxDeductible = false
        };

        // Monthly 3000 -> Annual 36,000 - 9,000 = 27,000 < 200,000
        var result = _engine.CalculateIncomeTax(3000m, 0m, 0, policy);

        Assert.Equal(0m, result.MonthlyNationalSolidarityContrib);
        Assert.True(result.MonthlyTax > 0);
    }

    [Fact]
    public void CalculateIncomeTax_WhenFlatBracketWithZeroRate_CalculatesZeroTax()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m,
            PersonalExemptionDependent = 0m,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = null, RatePercent = 0.0m }
            }
        };

        var result = _engine.CalculateIncomeTax(5000m, 0m, 0, policy);

        Assert.Equal(0m, result.MonthlyTax);
        Assert.Equal(60000m, result.AnnualTaxableNet);
    }

    [Fact]
    public async System.Threading.Tasks.Task CalculateIncomeTaxAsync_WhenRepositoryReturnsNull_AppliesStatutoryDefaults()
    {
        _policyRepoMock.Setup(r => r.GetActiveTaxPolicyAsync(1, It.IsAny<DateTime>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync((TaxPolicy?)null);

        var result = await _engine.CalculateIncomeTaxAsync(1, 1500m, 0m, 1, new DateTime(2026, 11, 1));

        Assert.Equal("DEFAULT_TAX", result.PolicyCode);
        Assert.Equal(10000m, result.TotalAnnualExemptions); // 9000 self + 1000 dependent
    }

    [Fact]
    public void CalculateIncomeTax_WhenDependentsClampedByNullPolicy_LimitsToThreeDependents()
    {
        // 5 dependents with null policy should clamp to 3
        var result = _engine.CalculateIncomeTax(2000m, 0m, 5, policy: null);

        Assert.Equal(12000m, result.TotalAnnualExemptions); // 9000 + 3*1000
    }

    [Fact]
    public void CalculateIncomeTax_WithRoundingDecimals4_MaintainsFourDecimalPlaces()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = null, RatePercent = 7.3333m }
            }
        };

        var result = _engine.CalculateIncomeTax(1000m, 0m, 0, policy, roundingDecimals: 4);

        // 12000 * 0.073333 = 879.996 / 12 = 73.3330
        Assert.Equal(4, BitConverter.GetBytes(decimal.GetBits(result.MonthlyTax)[3])[2]);
    }

    [Fact]
    public void CalculateIncomeTax_MultiTierSixProgressiveBrackets_CalculatesCorrectMarginalTax()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m,
            PersonalExemptionDependent = 0m,
            IsSscTaxDeductible = false,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = 5000, RatePercent = 5.0m },      // 250
                new() { BracketOrder = 2, LowerLimit = 5000, UpperLimit = 10000, RatePercent = 10.0m }, // 500
                new() { BracketOrder = 3, LowerLimit = 10000, UpperLimit = 15000, RatePercent = 15.0m },// 750
                new() { BracketOrder = 4, LowerLimit = 15000, UpperLimit = 20000, RatePercent = 20.0m },// 1000
                new() { BracketOrder = 5, LowerLimit = 20000, UpperLimit = 50000, RatePercent = 25.0m },// 7500
                new() { BracketOrder = 6, LowerLimit = 50000, UpperLimit = null, RatePercent = 30.0m }   // 30% of excess
            }
        };

        // Monthly salary: 5,000 JOD -> Annual: 60,000 JOD
        // Bracket 1: 5000 * 5% = 250
        // Bracket 2: 5000 * 10% = 500
        // Bracket 3: 5000 * 15% = 750
        // Bracket 4: 5000 * 20% = 1000
        // Bracket 5: 30000 * 25% = 7500
        // Bracket 6: 10000 * 30% = 3000
        // Total annual tax = 250 + 500 + 750 + 1000 + 7500 + 3000 = 13,000 JOD
        // Monthly tax = 13,000 / 12 = 1,083.333 JOD
        var result = _engine.CalculateIncomeTax(5000m, 0m, 0, policy);

        Assert.Equal(60000m, result.AnnualTaxableNet);
        Assert.Equal(1083.333m, result.MonthlyTax);
    }

    [Fact]
    public void CalculateIncomeTax_NationalSolidarity_ExactlyAtThreshold_ReturnsZeroContribution()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m,
            PersonalExemptionDependent = 0m,
            NationalContribThreshold = 240000m,
            NationalContribRate = 0.01m,
            IsSscTaxDeductible = false
        };

        // Monthly: 20,000 JOD -> Annual: 240,000 JOD, exactly at threshold
        var result = _engine.CalculateIncomeTax(20000m, 0m, 0, policy);

        Assert.Equal(240000m, result.AnnualTaxableNet);
        Assert.Equal(0m, result.MonthlyNationalSolidarityContrib);
        Assert.True(result.MonthlyTax > 0);
    }

    [Fact]
    public void CalculateIncomeTax_NationalSolidarity_AboveThreshold_CalculatesCorrectMarginalContrib()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m,
            PersonalExemptionDependent = 0m,
            NationalContribThreshold = 100000m,
            NationalContribRate = 0.02m, // 2%
            IsSscTaxDeductible = false
        };

        // Monthly 10,000 JOD -> Annual 120,000 JOD
        // Excess = 120,000 - 100,000 = 20,000 JOD
        // Annual solidarity = 20,000 * 2% = 400 JOD -> Monthly = 400 / 12 = 33.333 JOD
        var result = _engine.CalculateIncomeTax(10000m, 0m, 0, policy);

        Assert.Equal(33.333m, result.MonthlyNationalSolidarityContrib);
    }

    [Fact]
    public void CalculateIncomeTax_WhenGrossEqualsSscContrib_TaxableBaseZeroReturnsZeroTax()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 9000m,
            IsSscTaxDeductible = true
        };

        var result = _engine.CalculateIncomeTax(150m, employeeSscContrib: 150m, dependentCount: 0, policy);

        Assert.Equal(0m, result.MonthlyTaxableGross);
        Assert.Equal(0m, result.AnnualTaxableNet);
        Assert.Equal(0m, result.MonthlyTax);
    }

    [Fact]
    public void CalculateIncomeTax_WhenSscNotDeductible_TaxableBaseEqualsGross()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 9000m,
            IsSscTaxDeductible = false
        };

        var result = _engine.CalculateIncomeTax(1000m, employeeSscContrib: 75m, dependentCount: 0, policy);

        Assert.Equal(1000m, result.MonthlyTaxableGross);
        Assert.Equal(12000m, result.AnnualGross);
    }

    [Fact]
    public void CalculateIncomeTax_WhenPersonalExemptionExceedsAnnualGross_AnnualTaxableNetIsZero()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 20000m,
            PersonalExemptionDependent = 2000m,
            IsSscTaxDeductible = false
        };

        // Monthly 1000 -> Annual 12,000 < 20,000
        var result = _engine.CalculateIncomeTax(1000m, 0m, 0, policy);

        Assert.Equal(0m, result.AnnualTaxableNet);
        Assert.Equal(0m, result.MonthlyTax);
        Assert.Equal(1000m, result.MonthlyTaxableGross);
    }

    [Fact]
    public void CalculateIncomeTax_WhenMaxExemptedDependentsNegative_ClampsToZero()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 9000m,
            PersonalExemptionDependent = 1000m,
            MaxExemptedDependents = -1 // Non-positive config should clamp to 0
        };

        var result = _engine.CalculateIncomeTax(1500m, 0m, dependentCount: 3, policy);

        // Clamped to 0 dependents -> only self exemption
        Assert.Equal(9000m, result.TotalAnnualExemptions);
    }

    [Fact]
    public async System.Threading.Tasks.Task CalculateIncomeTaxAsync_PropagatesCancellationToken_ToRepository()
    {
        using var cts = new System.Threading.CancellationTokenSource();
        var token = cts.Token;

        var policy = new TaxPolicy
        {
            Code = "TAX_TOKEN_TEST",
            PersonalExemptionSelf = 9000m,
            PersonalExemptionDependent = 1000m
        };

        _policyRepoMock.Setup(r => r.GetActiveTaxPolicyAsync(1, new DateTime(2026, 11, 10), token))
            .ReturnsAsync(policy);

        var result = await _engine.CalculateIncomeTaxAsync(1, 1500m, 0m, 1, new DateTime(2026, 11, 10), token);

        Assert.Equal("TAX_TOKEN_TEST", result.PolicyCode);
        _policyRepoMock.Verify(r => r.GetActiveTaxPolicyAsync(1, new DateTime(2026, 11, 10), token), Times.Once);
    }

    [Fact]
    public async System.Threading.Tasks.Task CalculateIncomeTaxAsync_WithCustomRoundingDecimals_AppliesRoundingPrecision()
    {
        var policy = new TaxPolicy
        {
            Code = "TAX_ROUNDING",
            PersonalExemptionSelf = 0m,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = null, RatePercent = 7.5555m }
            }
        };

        _policyRepoMock.Setup(r => r.GetActiveTaxPolicyAsync(1, new DateTime(2026, 11, 10), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(policy);

        var result = await _engine.CalculateIncomeTaxAsync(1, 1000m, 0m, 0, new DateTime(2026, 11, 10), roundingDecimals: 2);

        // 12000 * 0.075555 = 906.66 / 12 = 75.555 -> rounded to 2 places: 75.56
        Assert.Equal(75.56m, result.MonthlyTax);
    }

    [Fact]
    public void CalculateIncomeTax_ZeroDecimals_JPY_RoundsToWholeCurrency()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = null, RatePercent = 10.0m }
            }
        };

        var result = _engine.CalculateIncomeTax(333333m, 0m, 0, policy, roundingDecimals: 0);

        Assert.Equal(Math.Round(333333m * 0.10m, 0), result.MonthlyTax);
    }

    [Fact]
    public void CalculateIncomeTax_ThreeDecimals_JOD_RoundsToThreePlaces()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = null, RatePercent = 7.555m }
            }
        };

        var result = _engine.CalculateIncomeTax(1234.567m, 0m, 0, policy, roundingDecimals: 3);

        Assert.Equal(Math.Round(1234.567m * 0.07555m, 3), result.MonthlyTax);
    }

    [Fact]
    public void CalculateIncomeTax_TwoDecimals_USD_RoundsToTwoPlaces()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = null, RatePercent = 8.5m }
            }
        };

        var result = _engine.CalculateIncomeTax(4500.50m, 0m, 0, policy, roundingDecimals: 2);

        Assert.Equal(Math.Round(4500.50m * 0.085m, 2), result.MonthlyTax);
    }

    [Fact]
    public void CalculateIncomeTax_DefaultOverloadWithoutRoundingDecimals_DefaultsToThreeDecimals()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = null, RatePercent = 7.5555m }
            }
        };

        var result = _engine.CalculateIncomeTax(1000m, 0m, 0, policy);

        // Default overload rounds to 3 decimals: 75.555
        Assert.Equal(75.555m, result.MonthlyTax);
    }

    [Fact]
    public async System.Threading.Tasks.Task CalculateIncomeTaxAsync_DefaultOverloadWithoutRoundingDecimals_DefaultsToThreeDecimals()
    {
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = null, RatePercent = 7.5555m }
            }
        };

        _policyRepoMock.Setup(r => r.GetActiveTaxPolicyAsync(1, new DateTime(2026, 11, 10), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(policy);

        var result = await _engine.CalculateIncomeTaxAsync(1, 1000m, 0m, 0, new DateTime(2026, 11, 10));

        Assert.Equal(75.555m, result.MonthlyTax);
    }

    [Fact]
    public void CalculateIncomeTax_PolicyWithUnorderedBrackets_SortsByBracketOrder()
    {
        // Unordered list of brackets
        var policy = new TaxPolicy
        {
            PersonalExemptionSelf = 0m,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 3, LowerLimit = 10000, UpperLimit = null, RatePercent = 20.0m },
                new() { BracketOrder = 1, LowerLimit = 0, UpperLimit = 5000, RatePercent = 5.0m },
                new() { BracketOrder = 2, LowerLimit = 5000, UpperLimit = 10000, RatePercent = 10.0m }
            }
        };

        // Monthly 1000 -> Annual 12,000 JOD
        // Bracket 1 (0-5k): 5000 * 5% = 250
        // Bracket 2 (5k-10k): 5000 * 10% = 500
        // Bracket 3 (>10k): 2000 * 20% = 400
        // Total = 1,150 -> Monthly = 1150 / 12 = 95.833 JOD
        var result = _engine.CalculateIncomeTax(1000m, 0m, 0, policy);

        Assert.Equal(95.833m, result.MonthlyTax);
    }
}
