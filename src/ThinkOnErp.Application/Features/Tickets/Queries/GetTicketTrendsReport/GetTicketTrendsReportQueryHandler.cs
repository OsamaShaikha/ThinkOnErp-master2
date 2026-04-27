using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetTicketTrendsReport;

/// <summary>
/// Handler for GetTicketTrendsReportQuery.
/// Retrieves trend analysis showing ticket creation and resolution patterns over time.
/// </summary>
public class GetTicketTrendsReportQueryHandler : IRequestHandler<GetTicketTrendsReportQuery, List<TicketTrendsReportDto>>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<GetTicketTrendsReportQueryHandler> _logger;

    public GetTicketTrendsReportQueryHandler(
        ITicketRepository ticketRepository,
        ILogger<GetTicketTrendsReportQueryHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<List<TicketTrendsReportDto>> Handle(GetTicketTrendsReportQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating ticket trends report - StartDate: {StartDate}, EndDate: {EndDate}, PeriodType: {PeriodType}",
            request.StartDate, request.EndDate, request.PeriodType);

        try
        {
            // Get report data from repository
            var reportData = await _ticketRepository.GetTicketTrendsReportAsync(
                request.StartDate,
                request.EndDate,
                request.PeriodType);

            // Map to DTOs
            var results = reportData.Select(row => new TicketTrendsReportDto
            {
                PeriodDate = row.ContainsKey("PERIOD_DATE") && row["PERIOD_DATE"] != null
                    ? Convert.ToDateTime(row["PERIOD_DATE"])
                    : DateTime.MinValue,
                PeriodLabel = row.ContainsKey("PERIOD_LABEL") && row["PERIOD_LABEL"] != null
                    ? row["PERIOD_LABEL"].ToString() ?? string.Empty
                    : string.Empty,
                TicketsCreated = row.ContainsKey("TICKETS_CREATED") && row["TICKETS_CREATED"] != null
                    ? Convert.ToInt32(row["TICKETS_CREATED"])
                    : 0,
                TicketsResolved = row.ContainsKey("TICKETS_RESOLVED") && row["TICKETS_RESOLVED"] != null
                    ? Convert.ToInt32(row["TICKETS_RESOLVED"])
                    : 0,
                CriticalCreated = row.ContainsKey("CRITICAL_CREATED") && row["CRITICAL_CREATED"] != null
                    ? Convert.ToInt32(row["CRITICAL_CREATED"])
                    : 0,
                HighCreated = row.ContainsKey("HIGH_CREATED") && row["HIGH_CREATED"] != null
                    ? Convert.ToInt32(row["HIGH_CREATED"])
                    : 0,
                OnTimeResolved = row.ContainsKey("ON_TIME_RESOLVED") && row["ON_TIME_RESOLVED"] != null
                    ? Convert.ToInt32(row["ON_TIME_RESOLVED"])
                    : 0,
                AvgSlaHours = row.ContainsKey("AVG_SLA_HOURS") && row["AVG_SLA_HOURS"] != null
                    ? Convert.ToDecimal(row["AVG_SLA_HOURS"])
                    : 0,
                AvgResolutionHours = row.ContainsKey("AVG_RESOLUTION_HOURS") && row["AVG_RESOLUTION_HOURS"] != null
                    ? Convert.ToDecimal(row["AVG_RESOLUTION_HOURS"])
                    : 0,
                SlaCompliancePercentage = row.ContainsKey("SLA_COMPLIANCE_PERCENTAGE") && row["SLA_COMPLIANCE_PERCENTAGE"] != null
                    ? Convert.ToDecimal(row["SLA_COMPLIANCE_PERCENTAGE"])
                    : 0,
                NetTicketChange = row.ContainsKey("NET_TICKET_CHANGE") && row["NET_TICKET_CHANGE"] != null
                    ? Convert.ToInt32(row["NET_TICKET_CHANGE"])
                    : 0
            }).ToList();

            _logger.LogInformation("Generated ticket trends report with {Count} data points", results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating ticket trends report");
            throw;
        }
    }
}
