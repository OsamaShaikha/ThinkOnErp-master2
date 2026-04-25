using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for SLA escalation monitoring and alerts.
/// Monitors tickets approaching or exceeding SLA deadlines and sends notifications.
/// </summary>
public class SlaEscalationService : ISlaEscalationService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketNotificationService _notificationService;
    private readonly ILogger<SlaEscalationService> _logger;
    private readonly IConfiguration _configuration;

    // Configuration keys
    private const string EscalationEnabledKey = "SlaEscalation:Enabled";
    private const string EscalationThresholdHoursKey = "SlaEscalation:ThresholdHours";

    public SlaEscalationService(
        ITicketRepository ticketRepository,
        ITicketNotificationService notificationService,
        ILogger<SlaEscalationService> logger,
        IConfiguration configuration)
    {
        _ticketRepository = ticketRepository;
        _notificationService = notificationService;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task CheckAndEscalateOverdueTicketsAsync()
    {
        if (!IsEscalationEnabled())
        {
            _logger.LogDebug("SLA escalation is disabled, skipping escalation check");
            return;
        }

        try
        {
            _logger.LogInformation("Starting SLA escalation check");

            var thresholdHours = GetEscalationThresholdHours();
            
            // Get tickets approaching deadline
            var approachingDeadline = await GetTicketsApproachingDeadlineAsync(thresholdHours);
            
            // Get overdue tickets
            var overdueTickets = await GetOverdueTicketsAsync();

            // Combine and deduplicate
            var ticketsToEscalate = approachingDeadline
                .Concat(overdueTickets)
                .GroupBy(t => t.RowId)
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation("Found {Count} tickets requiring SLA escalation", ticketsToEscalate.Count);

            // Send escalation alerts
            foreach (var ticket in ticketsToEscalate)
            {
                try
                {
                    await _notificationService.SendSlaEscalationAlertAsync(ticket);
                    _logger.LogInformation("SLA escalation alert sent for ticket {TicketId}", ticket.RowId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send SLA escalation alert for ticket {TicketId}", ticket.RowId);
                    // Continue with other tickets
                }
            }

            _logger.LogInformation("SLA escalation check completed. Processed {Count} tickets", ticketsToEscalate.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SLA escalation check");
            throw;
        }
    }

    public async Task<List<SysRequestTicket>> GetTicketsApproachingDeadlineAsync(int hoursBeforeDeadline = 2)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(hoursBeforeDeadline);
            
            // Get active tickets that have expected resolution date within threshold
            var tickets = await _ticketRepository.GetTicketsApproachingSlaDeadlineAsync(cutoffTime);
            
            _logger.LogDebug("Found {Count} tickets approaching SLA deadline within {Hours} hours", 
                tickets.Count, hoursBeforeDeadline);
            
            return tickets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tickets approaching SLA deadline");
            throw;
        }
    }

    public async Task<List<SysRequestTicket>> GetOverdueTicketsAsync()
    {
        try
        {
            var currentTime = DateTime.UtcNow;
            
            // Get active tickets that have passed their expected resolution date
            var tickets = await _ticketRepository.GetOverdueTicketsAsync(currentTime);
            
            _logger.LogDebug("Found {Count} overdue tickets", tickets.Count);
            
            return tickets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overdue tickets");
            throw;
        }
    }

    #region Private Helper Methods

    private bool IsEscalationEnabled()
    {
        return _configuration.GetValue<bool>(EscalationEnabledKey, true);
    }

    private int GetEscalationThresholdHours()
    {
        return _configuration.GetValue<int>(EscalationThresholdHoursKey, 2);
    }

    #endregion
}