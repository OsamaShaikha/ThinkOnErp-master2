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

public sealed class PayrollProrationServiceTests
{
    private readonly Mock<IPolicyRepository> _policyRepoMock = new();
    private readonly Mock<IWorkCalendarRepository> _workCalendarRepoMock = new();
    private readonly PayrollProrationService _service;

    public PayrollProrationServiceTests()
    {
        _service = new PayrollProrationService(
            _policyRepoMock.Object,
            _workCalendarRepoMock.Object,
            NullLogger<PayrollProrationService>.Instance);
    }

    [Fact]
    public async Task CalculateProration_FullMonthActiveEmployee_ReturnsFactorOne()
    {
        // Arrange
        var hireDate = new DateTime(2024, 1, 1);
        var periodStart = new DateTime(2026, 8, 1);
        var periodEnd = new DateTime(2026, 8, 31);

        _policyRepoMock.Setup(r => r.GetEffectiveProrationPolicyAsync(1, periodStart, default))
            .ReturnsAsync(new ProrationPolicy { Method = "CALENDAR_DAYS", CompanyId = 1 });

        // Act
        var result = await _service.CalculateProrationFactorAsync("EMP-001", hireDate, null, periodStart, periodEnd, 1);

        // Assert
        result.ProrationFactor.Should().Be(1.0m);
        result.EligibleDays.Should().Be(30m);
        result.MethodUsed.Should().Be("CALENDAR_DAYS");
    }

    [Fact]
    public async Task CalculateProration_MidMonthJoin_CalendarDays_CalculatesExactRatio()
    {
        // Joined Aug 16 in a 31-day month => 16 days active (Aug 16 to 31 inclusive)
        var hireDate = new DateTime(2026, 8, 16);
        var periodStart = new DateTime(2026, 8, 1);
        var periodEnd = new DateTime(2026, 8, 31);

        _policyRepoMock.Setup(r => r.GetEffectiveProrationPolicyAsync(1, periodStart, default))
            .ReturnsAsync(new ProrationPolicy { Method = "CALENDAR_DAYS", CompanyId = 1 });

        // Act
        var result = await _service.CalculateProrationFactorAsync("EMP-001", hireDate, null, periodStart, periodEnd, 1);

        // Assert
        result.EligibleDays.Should().Be(16m);
        result.TotalBaseDays.Should().Be(31m);
        result.ProrationFactor.Should().BeApproximately(16m / 31m, 0.0001m);
    }

    [Fact]
    public async Task CalculateProration_MidMonthJoin_Fixed30Days_CalculatesFixed30Ratio()
    {
        // Joined Aug 16 => Aug 16 to Aug 31 is 16 calendar days, capped at 30 days => 16/30
        var hireDate = new DateTime(2026, 8, 16);
        var periodStart = new DateTime(2026, 8, 1);
        var periodEnd = new DateTime(2026, 8, 31);

        _policyRepoMock.Setup(r => r.GetEffectiveProrationPolicyAsync(1, periodStart, default))
            .ReturnsAsync(new ProrationPolicy { Method = "FIXED_30", CompanyId = 1 });

        // Act
        var result = await _service.CalculateProrationFactorAsync("EMP-001", hireDate, null, periodStart, periodEnd, 1);

        // Assert
        result.TotalBaseDays.Should().Be(30m);
        result.EligibleDays.Should().Be(16m);
        result.ProrationFactor.Should().BeApproximately(16m / 30m, 0.0001m);
    }
}
