using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using static ThinkOnErp.Domain.Interfaces.SlaStatus;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for calculating SLA targets, tracking compliance, and monitoring escalation thresholds.
/// Implements business hours calculation excluding weekends and holidays.
/// </summary>
public class SlaCalculationService : ISlaCalculationService
{
    private readonly ITicketPriorityRepository _priorityRepository;
    private readonly ILogger<SlaCalculationService> _logger;

    // Business hours configuration (8 AM to 5 PM)
    private const int BusinessHoursStart = 8;
    private const int BusinessHoursEnd = 17;
    private const int BusinessHoursPerDay = 9; // 8 AM to 5 PM = 9 hours

    public SlaCalculationService(
        ITicketPriorityRepository priorityRepository,
        ILogger<SlaCalculationService> logger)
    {
        _priorityRepository = priorityRepository ?? throw new ArgumentNullException(nameof(priorityRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Calculates the SLA deadline for a ticket based on priority and creation date.
    /// Excludes weekends and holidays from the calculation.
    /// </summary>
    public async Task<DateTime> CalculateSlaDeadlineAsync(
        Int64 priorityId,
        DateTime creationDate,
        bool excludeWeekends = true,
        bool excludeHolidays = true)
    {
        var priority = await _priorityRepository.GetByIdAsync(priorityId);
        if (priority == null)
        {
            _logger.LogError("Priority with ID {PriorityId} not found", priorityId);
            throw new ArgumentException($"Priority with ID {priorityId} not found");
        }

        return CalculateSlaDeadlineWithBusinessHours(
            creationDate,
            priority.SlaTargetHours,
            excludeWeekends,
            excludeHolidays);
    }

    /// <summary>
    /// Calculates the escalation alert time based on priority and creation date.
    /// </summary>
    public async Task<DateTime> CalculateEscalationAlertTimeAsync(
        Int64 priorityId,
        DateTime creationDate,
        bool excludeWeekends = true,
        bool excludeHolidays = true)
    {
        var priority = await _priorityRepository.GetByIdAsync(priorityId);
        if (priority == null)
        {
            _logger.LogError("Priority with ID {PriorityId} not found", priorityId);
            throw new ArgumentException($"Priority with ID {priorityId} not found");
        }

        return CalculateSlaDeadlineWithBusinessHours(
            creationDate,
            priority.EscalationThresholdHours,
            excludeWeekends,
            excludeHolidays);
    }

    /// <summary>
    /// Determines the SLA status of a ticket.
    /// </summary>
    public SlaStatus GetSlaStatus(
        DateTime? expectedResolutionDate,
        DateTime? actualResolutionDate,
        bool isResolved)
    {
        if (expectedResolutionDate == null)
        {
            return SlaStatus.NotSet;
        }

        // If ticket is resolved, check if it was resolved on time
        if (isResolved && actualResolutionDate.HasValue)
        {
            return actualResolutionDate.Value <= expectedResolutionDate.Value
                ? SlaStatus.Met
                : SlaStatus.Breached;
        }

        // For open tickets, check current time against deadline
        var now = DateTime.Now;
        if (now > expectedResolutionDate.Value)
        {
            return SlaStatus.Breached;
        }

        // Check if approaching deadline (within 20% of SLA time)
        var timeRemaining = expectedResolutionDate.Value - now;
        var totalSlaTime = expectedResolutionDate.Value - (expectedResolutionDate.Value.AddHours(-24)); // Approximate
        
        if (timeRemaining.TotalHours < totalSlaTime.TotalHours * 0.2)
        {
            return SlaStatus.AtRisk;
        }

        return SlaStatus.OnTrack;
    }

    /// <summary>
    /// Checks if a ticket needs escalation based on its current status and SLA deadline.
    /// </summary>
    public async Task<bool> NeedsEscalationAsync(
        Int64 priorityId,
        DateTime creationDate,
        DateTime? lastUpdateDate,
        bool isResolved)
    {
        if (isResolved)
        {
            return false; // Resolved tickets don't need escalation
        }

        var escalationTime = await CalculateEscalationAlertTimeAsync(priorityId, creationDate);
        var now = DateTime.Now;

        // Escalate if we've passed the escalation threshold
        return now >= escalationTime;
    }

    /// <summary>
    /// Calculates SLA compliance rate for a set of tickets.
    /// </summary>
    public decimal CalculateSlaComplianceRate(
        int totalTickets,
        int ticketsMetSla)
    {
        if (totalTickets == 0)
        {
            return 100m; // No tickets means 100% compliance
        }

        return Math.Round((decimal)ticketsMetSla / totalTickets * 100, 2);
    }

    /// <summary>
    /// Gets the time remaining until SLA breach.
    /// </summary>
    public TimeSpan? GetTimeRemainingUntilBreach(DateTime? expectedResolutionDate)
    {
        if (!expectedResolutionDate.HasValue)
        {
            return null;
        }

        var timeRemaining = expectedResolutionDate.Value - DateTime.Now;
        return timeRemaining.TotalSeconds > 0 ? timeRemaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Calculates business hours between two dates, excluding weekends and holidays.
    /// </summary>
    public int CalculateBusinessHoursBetween(
        DateTime startDate,
        DateTime endDate,
        bool excludeWeekends = true,
        bool excludeHolidays = true)
    {
        if (startDate >= endDate)
        {
            return 0;
        }

        int totalBusinessHours = 0;
        var currentDate = startDate;

        while (currentDate < endDate)
        {
            // Skip weekends if configured
            if (excludeWeekends && (currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday))
            {
                currentDate = currentDate.AddDays(1).Date;
                continue;
            }

            // Skip holidays if configured
            if (excludeHolidays && IsHoliday(currentDate))
            {
                currentDate = currentDate.AddDays(1).Date;
                continue;
            }

            // Calculate hours for this day
            var dayStart = currentDate.Date.AddHours(BusinessHoursStart);
            var dayEnd = currentDate.Date.AddHours(BusinessHoursEnd);

            var effectiveStart = currentDate < dayStart ? dayStart : currentDate;
            var effectiveEnd = endDate < dayEnd ? endDate : dayEnd;

            if (effectiveStart < effectiveEnd)
            {
                totalBusinessHours += (int)(effectiveEnd - effectiveStart).TotalHours;
            }

            currentDate = currentDate.AddDays(1).Date;
        }

        return totalBusinessHours;
    }

    /// <summary>
    /// Calculates the deadline by adding business hours to a start date.
    /// </summary>
    private DateTime CalculateSlaDeadlineWithBusinessHours(
        DateTime startDate,
        decimal targetHours,
        bool excludeWeekends,
        bool excludeHolidays)
    {
        var remainingHours = (double)targetHours;
        var currentDate = startDate;

        // If start date is outside business hours, move to next business hour
        currentDate = AdjustToNextBusinessHour(currentDate, excludeWeekends, excludeHolidays);

        while (remainingHours > 0)
        {
            // Skip weekends if configured
            if (excludeWeekends && (currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday))
            {
                currentDate = currentDate.AddDays(1).Date.AddHours(BusinessHoursStart);
                continue;
            }

            // Skip holidays if configured
            if (excludeHolidays && IsHoliday(currentDate))
            {
                currentDate = currentDate.AddDays(1).Date.AddHours(BusinessHoursStart);
                continue;
            }

            // Calculate hours available in current day
            var dayEnd = currentDate.Date.AddHours(BusinessHoursEnd);
            var hoursAvailableToday = (dayEnd - currentDate).TotalHours;

            if (hoursAvailableToday <= 0)
            {
                // Move to next business day
                currentDate = currentDate.AddDays(1).Date.AddHours(BusinessHoursStart);
                continue;
            }

            if (remainingHours <= hoursAvailableToday)
            {
                // Can complete within today
                currentDate = currentDate.AddHours(remainingHours);
                remainingHours = 0;
            }
            else
            {
                // Use all available hours today and continue tomorrow
                remainingHours -= hoursAvailableToday;
                currentDate = currentDate.AddDays(1).Date.AddHours(BusinessHoursStart);
            }
        }

        return currentDate;
    }

    /// <summary>
    /// Adjusts a date to the next business hour if it falls outside business hours.
    /// </summary>
    private DateTime AdjustToNextBusinessHour(
        DateTime date,
        bool excludeWeekends,
        bool excludeHolidays)
    {
        var currentDate = date;

        // Move to next business day if on weekend
        while (excludeWeekends && (currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday))
        {
            currentDate = currentDate.AddDays(1).Date.AddHours(BusinessHoursStart);
        }

        // Move to next business day if on holiday
        while (excludeHolidays && IsHoliday(currentDate))
        {
            currentDate = currentDate.AddDays(1).Date.AddHours(BusinessHoursStart);
        }

        // If before business hours, move to start of business day
        if (currentDate.Hour < BusinessHoursStart)
        {
            currentDate = currentDate.Date.AddHours(BusinessHoursStart);
        }
        // If after business hours, move to start of next business day
        else if (currentDate.Hour >= BusinessHoursEnd)
        {
            currentDate = currentDate.AddDays(1).Date.AddHours(BusinessHoursStart);
            // Recursively check if next day is also non-business day
            currentDate = AdjustToNextBusinessHour(currentDate, excludeWeekends, excludeHolidays);
        }

        return currentDate;
    }

    /// <summary>
    /// Checks if a date is a holiday.
    /// This is a placeholder implementation - in production, this would check against a holiday calendar table.
    /// </summary>
    private bool IsHoliday(DateTime date)
    {
        // TODO: Implement holiday calendar lookup from database
        // For now, return false (no holidays configured)
        return false;
    }
}
