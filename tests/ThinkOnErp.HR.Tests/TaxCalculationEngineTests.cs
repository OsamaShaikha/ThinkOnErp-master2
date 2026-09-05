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

public sealed class TaxCalculationEngineTests
{
    private readonly Mock<IPolicyRepository> _policyRepoMock = new();
    private readonly TaxCalculationEngine _engine;

    public TaxCalculationEngineTests()
    {
        _engine = new TaxCalculationEngine(_policyRepoMock.Object, NullLogger<TaxCalculationEngine>.Instance);
    }

    private TaxPolicy CreateJordanStandardTaxPolicy()
    {
        var policy = new TaxPolicy
        {
            CompanyId = 1,
            PersonalExemptionSelf = 9000m,
            PersonalExemptionDependent = 9000m,
            NationalContributionThreshold = 200000m,
            NationalContributionRate = 0.0100m,
            Brackets = new List<TaxBracket>
            {
                new() { BracketOrder = 1, LowerLimit = 0m, UpperLimit = 5000m, RatePercent = 0.05m },
                new() { BracketOrder = 2, LowerLimit = 5000m, UpperLimit = 10000m, RatePercent = 0.10m },
                new() { BracketOrder = 3, LowerLimit = 10000m, UpperLimit = 15000m, RatePercent = 0.15m },
                new() { BracketOrder = 4, LowerLimit = 15000m, UpperLimit = 20000m, RatePercent = 0.20m },
                new() { BracketOrder = 5, LowerLimit = 20000m, UpperLimit = null, RatePercent = 0.25m }
            }
        };
        return policy;
    }

    [Fact]
    public async Task CalculateTax_AnnualIncomeBelowExemption_ReturnsZeroTax()
    {
        // 600 JOD monthly taxable => 7,200 JOD annual <= 9,000 JOD exemption => 0 tax
        _policyRepoMock.Setup(r => r.GetEffectiveTaxPolicyAsync(1, It.IsAny<DateTime>(), default))
            .ReturnsAsync(CreateJordanStandardTaxPolicy());

        var result = await _engine.CalculateTaxAsync(
            monthlyTaxableGross: 600m,
            taxExemptionCount: 0,
            companyId: 1,
            calculationDate: new DateTime(2026, 8, 1));

        result.MonthlyIncomeTax.Should().Be(0m);
        result.MonthlyNationalContribution.Should().Be(0m);
        result.AnnualTaxableNet.Should().Be(0m);
    }

    [Fact]
    public async Task CalculateTax_AnnualIncomeInFirstBracket_Computes5PercentTax()
    {
        // 1,000 JOD monthly taxable => 12,000 JOD annual.
        // Exemption: 9,000 JOD => Annual Net Taxable: 3,000 JOD (in Bracket 1: 0 - 5000 at 5%)
        // Annual Tax: 3,000 * 5% = 150 JOD => Monthly Tax: 150 / 12 = 12.50 JOD
        _policyRepoMock.Setup(r => r.GetEffectiveTaxPolicyAsync(1, It.IsAny<DateTime>(), default))
            .ReturnsAsync(CreateJordanStandardTaxPolicy());

        var result = await _engine.CalculateTaxAsync(
            monthlyTaxableGross: 1000m,
            taxExemptionCount: 0,
            companyId: 1,
            calculationDate: new DateTime(2026, 8, 1));

        result.AnnualExemptionsApplied.Should().Be(9000m);
        result.AnnualTaxableNet.Should().Be(3000m);
        result.MonthlyIncomeTax.Should().Be(12.50m);
        result.MonthlyNationalContribution.Should().Be(0m);
    }
}
