using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class SSCCalculationServiceTests
{
    private readonly Mock<IPolicyRepository> _policyRepoMock = new();
    private readonly Mock<IStatutoryRuleService> _statutoryRuleServiceMock = new();
    private readonly SSCCalculationService _service;

    public SSCCalculationServiceTests()
    {
        _service = new SSCCalculationService(
            _policyRepoMock.Object,
            _statutoryRuleServiceMock.Object,
            NullLogger<SSCCalculationService>.Instance);
    }

    [Fact]
    public async Task CalculateSSC_StandardEmployee_ComputesExactContributions()
    {
        // 1000 JOD gross, 7.5% employee, 14.25% employer, cap 3349 JOD
        var policy = new SSCPolicy
        {
            CompanyId = 1,
            EmployeeContributionRate = 0.0750m,
            EmployerContributionRate = 0.1425m,
            HighRiskSurchargeRate = 0.0100m,
            MonthlyCeilingCap = 3349.00m
        };

        _policyRepoMock.Setup(r => r.GetEffectiveSSCPolicyAsync(1, It.IsAny<DateTime>(), default))
            .ReturnsAsync(policy);

        // Act
        var result = await _service.CalculateSSCAsync(1000m, isHighRiskRole: false, companyId: 1, calculationDate: new DateTime(2026, 8, 1));

        // Assert
        result.SscEligibleSalary.Should().Be(1000m);
        result.EmployeeContribution.Should().Be(75.00m);
        result.EmployerContribution.Should().Be(142.50m);
    }

    [Fact]
    public async Task CalculateSSC_HighRiskRole_AppliesSurchargeToEmployer()
    {
        // 1000 JOD gross, 7.5% employee, (14.25 + 1.0 = 15.25%) employer
        var policy = new SSCPolicy
        {
            CompanyId = 1,
            EmployeeContributionRate = 0.0750m,
            EmployerContributionRate = 0.1425m,
            HighRiskSurchargeRate = 0.0100m,
            MonthlyCeilingCap = 3349.00m
        };

        _policyRepoMock.Setup(r => r.GetEffectiveSSCPolicyAsync(1, It.IsAny<DateTime>(), default))
            .ReturnsAsync(policy);

        // Act
        var result = await _service.CalculateSSCAsync(1000m, isHighRiskRole: true, companyId: 1, calculationDate: new DateTime(2026, 8, 1));

        // Assert
        result.EmployeeContribution.Should().Be(75.00m);
        result.EmployerContribution.Should().Be(152.50m); // 1000 * 15.25%
    }

    [Fact]
    public async Task CalculateSSC_SalaryExceedingCeiling_CapsEligibleSalaryAtCeiling()
    {
        // 5000 JOD gross, cap 3349 JOD
        var policy = new SSCPolicy
        {
            CompanyId = 1,
            EmployeeContributionRate = 0.0750m,
            EmployerContributionRate = 0.1425m,
            HighRiskSurchargeRate = 0.0100m,
            MonthlyCeilingCap = 3349.00m
        };

        _policyRepoMock.Setup(r => r.GetEffectiveSSCPolicyAsync(1, It.IsAny<DateTime>(), default))
            .ReturnsAsync(policy);

        // Act
        var result = await _service.CalculateSSCAsync(5000m, isHighRiskRole: false, companyId: 1, calculationDate: new DateTime(2026, 8, 1));

        // Assert
        result.SscEligibleSalary.Should().Be(3349.00m);
        result.EmployeeContribution.Should().Be(Math.Round(3349m * 0.0750m, 3));
        result.EmployerContribution.Should().Be(Math.Round(3349m * 0.1425m, 3));
    }
}
