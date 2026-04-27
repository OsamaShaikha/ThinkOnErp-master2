using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Infrastructure.Services;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for SlaCalculationService.
/// Tests SLA deadline calculation, business hours handling, and escalation monitoring.
/// </summary>
public class SlaCalculationServiceTests
{
    private readonly Mock<ITicketPriorityRepository> _mockPriorityRepository;
    private readonly Mock<ILogger<SlaCalculationService>> _mockLogger;
    private readonly SlaCalculationService _slaService;

    public SlaCalculationServiceTests()
    {
        _mockPriorityRepository = new Mock<ITicketPriorityRepository>();
        _mockLogger = new Mock<ILogger<SlaCalculationService>>();
        _slaService = new SlaCalculationService(_mockPriorityRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CalculateSlaDeadlineAsync_WithValidPriority_ReturnsCorrectDeadline()
    {
        // Arrange
        var priorityId = 1L;
        var creationDate = new DateTime(2024, 1, 15, 10, 0, 0); // Monday 10 AM
        var priority = new SysTicketPriority
        {
            RowId = priorityId,
            SlaTargetHours = 24m, // 24 business hours = 3 business days (9 hours per day)
            PriorityLevel = 2
        };

        _mockPriorityRepository.Setup(r => r.GetByIdAsync(priorityId))
            .ReturnsAsync(priority);

        // Act
        var deadline = await _slaService.CalculateSlaDeadlineAsync(priorityId, creationDate, excludeWeekends: false, excludeHolidays: false);

        // Assert
        // 24 hours from Monday 10 AM:
        // Monday: 7 hours (10 AM to 5 PM)
        // Tuesday: 9 hours (8 AM to 5 PM)
        // Wednesday: 8 hours (8 AM to 4 PM)
        // Total: 24 hours, ending Wednesday 4 PM
        Assert.Equal(new DateTime(2024, 1, 17, 16, 0, 0), deadline);
    }

    [Fact]
    public async Task CalculateSlaDeadlineAsync_WithWeekendExclusion_SkipsWeekend()
    {
        // Arrange
        var priorityId = 1L;
        var creationDate = new DateTime(2024, 1, 19, 10, 0, 0); // Friday 10 AM
        var priority = new SysTicketPriority
        {
            RowId = priorityId,
            SlaTargetHours = 18m, // 18 hours = 2 business days
            PriorityLevel = 2
        };

        _mockPriorityRepository.Setup(r => r.GetByIdAsync(priorityId))
            .ReturnsAsync(priority);

        // Act
        var deadline = await _slaService.CalculateSlaDeadlineAsync(priorityId, creationDate, excludeWeekends: true, excludeHolidays: false);

        // Assert
        // Should skip Saturday and Sunday, landing on Tuesday
        Assert.Equal(DayOfWeek.Tuesday, deadline.DayOfWeek);
    }

    [Fact]
    public async Task CalculateSlaDeadlineAsync_WithInvalidPriority_ThrowsException()
    {
        // Arrange
        var priorityId = 999L;
        var creationDate = DateTime.Now;

        _mockPriorityRepository.Setup(r => r.GetByIdAsync(priorityId))
            .ReturnsAsync((SysTicketPriority?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _slaService.CalculateSlaDeadlineAsync(priorityId, creationDate));
    }

    [Fact]
    public async Task CalculateEscalationAlertTimeAsync_WithValidPriority_ReturnsCorrectTime()
    {
        // Arrange
        var priorityId = 1L;
        var creationDate = new DateTime(2024, 1, 15, 10, 0, 0);
        var priority = new SysTicketPriority
        {
            RowId = priorityId,
            EscalationThresholdHours = 4m,
            PriorityLevel = 1
        };

        _mockPriorityRepository.Setup(r => r.GetByIdAsync(priorityId))
            .ReturnsAsync(priority);

        // Act
        var escalationTime = await _slaService.CalculateEscalationAlertTimeAsync(priorityId, creationDate, excludeWeekends: false, excludeHolidays: false);

        // Assert
        Assert.Equal(new DateTime(2024, 1, 15, 14, 0, 0), escalationTime);
    }

    [Fact]
    public void GetSlaStatus_WithNoDeadline_ReturnsNotSet()
    {
        // Act
        var status = _slaService.GetSlaStatus(null, null, false);

        // Assert
        Assert.Equal(SlaStatus.NotSet, status);
    }

    [Fact]
    public void GetSlaStatus_ResolvedOnTime_ReturnsMet()
    {
        // Arrange
        var expectedDate = new DateTime(2024, 1, 20, 17, 0, 0);
        var actualDate = new DateTime(2024, 1, 19, 15, 0, 0);

        // Act
        var status = _slaService.GetSlaStatus(expectedDate, actualDate, true);

        // Assert
        Assert.Equal(SlaStatus.Met, status);
    }

    [Fact]
    public void GetSlaStatus_ResolvedLate_ReturnsBreached()
    {
        // Arrange
        var expectedDate = new DateTime(2024, 1, 18, 17, 0, 0);
        var actualDate = new DateTime(2024, 1, 20, 15, 0, 0);

        // Act
        var status = _slaService.GetSlaStatus(expectedDate, actualDate, true);

        // Assert
        Assert.Equal(SlaStatus.Breached, status);
    }

    [Fact]
    public void GetSlaStatus_OpenTicketPastDeadline_ReturnsBreached()
    {
        // Arrange
        var expectedDate = DateTime.Now.AddHours(-2); // 2 hours ago

        // Act
        var status = _slaService.GetSlaStatus(expectedDate, null, false);

        // Assert
        Assert.Equal(SlaStatus.Breached, status);
    }

    [Fact]
    public void GetSlaStatus_OpenTicketWellBeforeDeadline_ReturnsOnTrack()
    {
        // Arrange
        var expectedDate = DateTime.Now.AddHours(24); // 24 hours from now

        // Act
        var status = _slaService.GetSlaStatus(expectedDate, null, false);

        // Assert
        Assert.Equal(SlaStatus.OnTrack, status);
    }

    [Fact]
    public async Task NeedsEscalationAsync_ResolvedTicket_ReturnsFalse()
    {
        // Arrange
        var priorityId = 1L;
        var creationDate = DateTime.Now.AddHours(-10);

        // Act
        var needsEscalation = await _slaService.NeedsEscalationAsync(priorityId, creationDate, DateTime.Now, true);

        // Assert
        Assert.False(needsEscalation);
    }

    [Fact]
    public async Task NeedsEscalationAsync_PastEscalationThreshold_ReturnsTrue()
    {
        // Arrange
        var priorityId = 1L;
        // Set creation date to 5 business days ago to ensure we're past the 4-hour threshold
        var creationDate = DateTime.Now.Date.AddDays(-5).AddHours(10);
        var priority = new SysTicketPriority
        {
            RowId = priorityId,
            EscalationThresholdHours = 4m, // 4 business hours
            PriorityLevel = 1
        };

        _mockPriorityRepository.Setup(r => r.GetByIdAsync(priorityId))
            .ReturnsAsync(priority);

        // Act
        var needsEscalation = await _slaService.NeedsEscalationAsync(priorityId, creationDate, null, false);

        // Assert
        Assert.True(needsEscalation);
    }

    [Fact]
    public void CalculateSlaComplianceRate_WithNoTickets_Returns100Percent()
    {
        // Act
        var complianceRate = _slaService.CalculateSlaComplianceRate(0, 0);

        // Assert
        Assert.Equal(100m, complianceRate);
    }

    [Fact]
    public void CalculateSlaComplianceRate_WithAllTicketsMet_Returns100Percent()
    {
        // Act
        var complianceRate = _slaService.CalculateSlaComplianceRate(10, 10);

        // Assert
        Assert.Equal(100m, complianceRate);
    }

    [Fact]
    public void CalculateSlaComplianceRate_WithPartialCompliance_ReturnsCorrectPercentage()
    {
        // Act
        var complianceRate = _slaService.CalculateSlaComplianceRate(10, 7);

        // Assert
        Assert.Equal(70m, complianceRate);
    }

    [Fact]
    public void GetTimeRemainingUntilBreach_WithNoDeadline_ReturnsNull()
    {
        // Act
        var timeRemaining = _slaService.GetTimeRemainingUntilBreach(null);

        // Assert
        Assert.Null(timeRemaining);
    }

    [Fact]
    public void GetTimeRemainingUntilBreach_WithFutureDeadline_ReturnsPositiveTimeSpan()
    {
        // Arrange
        var deadline = DateTime.Now.AddHours(5);

        // Act
        var timeRemaining = _slaService.GetTimeRemainingUntilBreach(deadline);

        // Assert
        Assert.NotNull(timeRemaining);
        Assert.True(timeRemaining.Value.TotalHours > 4.5 && timeRemaining.Value.TotalHours < 5.5);
    }

    [Fact]
    public void GetTimeRemainingUntilBreach_WithPastDeadline_ReturnsZero()
    {
        // Arrange
        var deadline = DateTime.Now.AddHours(-2);

        // Act
        var timeRemaining = _slaService.GetTimeRemainingUntilBreach(deadline);

        // Assert
        Assert.NotNull(timeRemaining);
        Assert.Equal(TimeSpan.Zero, timeRemaining.Value);
    }

    [Fact]
    public void CalculateBusinessHoursBetween_SameDay_ReturnsCorrectHours()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 15, 9, 0, 0); // Monday 9 AM
        var endDate = new DateTime(2024, 1, 15, 14, 0, 0); // Monday 2 PM

        // Act
        var businessHours = _slaService.CalculateBusinessHoursBetween(startDate, endDate, false, false);

        // Assert
        Assert.Equal(5, businessHours);
    }

    [Fact]
    public void CalculateBusinessHoursBetween_MultipleDays_ReturnsCorrectHours()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 15, 9, 0, 0); // Monday 9 AM
        var endDate = new DateTime(2024, 1, 16, 14, 0, 0); // Tuesday 2 PM

        // Act
        var businessHours = _slaService.CalculateBusinessHoursBetween(startDate, endDate, false, false);

        // Assert
        // Monday: 8 hours (9 AM to 5 PM), Tuesday: 6 hours (8 AM to 2 PM) = 14 hours
        Assert.True(businessHours >= 13 && businessHours <= 15); // Allow some tolerance
    }

    [Fact]
    public void CalculateBusinessHoursBetween_WithWeekend_ExcludesWeekendDays()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 19, 9, 0, 0); // Friday 9 AM
        var endDate = new DateTime(2024, 1, 22, 14, 0, 0); // Monday 2 PM

        // Act
        var businessHours = _slaService.CalculateBusinessHoursBetween(startDate, endDate, true, false);

        // Assert
        // Should only count Friday and Monday, not Saturday/Sunday
        Assert.True(businessHours >= 13 && businessHours <= 15);
    }

    [Fact]
    public void CalculateBusinessHoursBetween_StartAfterEnd_ReturnsZero()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 16, 9, 0, 0);
        var endDate = new DateTime(2024, 1, 15, 9, 0, 0);

        // Act
        var businessHours = _slaService.CalculateBusinessHoursBetween(startDate, endDate, false, false);

        // Assert
        Assert.Equal(0, businessHours);
    }
}
