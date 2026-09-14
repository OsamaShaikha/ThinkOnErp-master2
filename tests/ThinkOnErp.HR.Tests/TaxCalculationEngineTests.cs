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
}
