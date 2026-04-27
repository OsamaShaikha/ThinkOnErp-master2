using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetTicketVolumeReport;

/// <summary>
/// Handler for GetTicketVolumeReportQuery.
/// Retrieves ticket volume statistics grouped by time period, company, or type.
/// </summary>
public class GetTicketVolumeReportQueryHandler : IRequestHandler<GetTicketVolumeReportQuery, List<TicketVolumeReportDto>>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<GetTicketVolumeReportQueryHandler> _logger;

    public GetTicketVolumeReportQueryHandler(
        ITicketRepository ticketRepository,
        ILogger<GetTicketVolumeReportQueryHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<List<TicketVolumeReportDto>> Handle(GetTicketVolumeReportQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating ticket volume report - StartDate: {StartDate}, EndDate: {EndDate}, GroupBy: {GroupBy}",
            request.StartDate, request.EndDate, request.GroupBy);

        try
        {
            // Get report data from repository
            var reportData = await _ticketRepository.GetTicketVolumeReportAsync(
                request.StartDate,
                request.EndDate,
                request.CompanyId,
                request.TicketTypeId,
                request.GroupBy);

            // Map to DTOs
            var results = reportData.Select(row => new TicketVolumeReportDto
            {
                PeriodDate = row.ContainsKey("PERIOD_DATE") && row["PERIOD_DATE"] != null
                    ? Convert.ToDateTime(row["PERIOD_DATE"])
                    : null,
                PeriodLabel = row.ContainsKey("PERIOD_LABEL") && row["PERIOD_LABEL"] != null
                    ? row["PERIOD_LABEL"].ToString() ?? string.Empty
                    : string.Empty,
                CompanyId = row.ContainsKey("COMPANY_ID") && row["COMPANY_ID"] != null
                    ? Convert.ToInt64(row["COMPANY_ID"])
                    : null,
                CompanyName = row.ContainsKey("COMPANY_NAME") && row["COMPANY_NAME"] != null
                    ? row["COMPANY_NAME"].ToString()
                    : null,
                TicketTypeId = row.ContainsKey("TICKET_TYPE_ID") && row["TICKET_TYPE_ID"] != null
                    ? Convert.ToInt64(row["TICKET_TYPE_ID"])
                    : null,
                TypeName = row.ContainsKey("TYPE_NAME") && row["TYPE_NAME"] != null
                    ? row["TYPE_NAME"].ToString()
                    : null,
                TotalTickets = row.ContainsKey("TOTAL_TICKETS") && row["TOTAL_TICKETS"] != null
                    ? Convert.ToInt32(row["TOTAL_TICKETS"])
                    : 0,
                OpenTickets = row.ContainsKey("OPEN_TICKETS") && row["OPEN_TICKETS"] != null
                    ? Convert.ToInt32(row["OPEN_TICKETS"])
                    : 0,
                InProgressTickets = row.ContainsKey("IN_PROGRESS_TICKETS") && row["IN_PROGRESS_TICKETS"] != null
                    ? Convert.ToInt32(row["IN_PROGRESS_TICKETS"])
                    : 0,
                ResolvedTickets = row.ContainsKey("RESOLVED_TICKETS") && row["RESOLVED_TICKETS"] != null
                    ? Convert.ToInt32(row["RESOLVED_TICKETS"])
                    : 0,
                ClosedTickets = row.ContainsKey("CLOSED_TICKETS") && row["CLOSED_TICKETS"] != null
                    ? Convert.ToInt32(row["CLOSED_TICKETS"])
                    : 0,
                CriticalTickets = row.ContainsKey("CRITICAL_TICKETS") && row["CRITICAL_TICKETS"] != null
                    ? Convert.ToInt32(row["CRITICAL_TICKETS"])
                    : 0,
                HighTickets = row.ContainsKey("HIGH_TICKETS") && row["HIGH_TICKETS"] != null
                    ? Convert.ToInt32(row["HIGH_TICKETS"])
                    : 0,
                MediumTickets = row.ContainsKey("MEDIUM_TICKETS") && row["MEDIUM_TICKETS"] != null
                    ? Convert.ToInt32(row["MEDIUM_TICKETS"])
                    : 0,
                LowTickets = row.ContainsKey("LOW_TICKETS") && row["LOW_TICKETS"] != null
                    ? Convert.ToInt32(row["LOW_TICKETS"])
                    : 0
            }).ToList();

            _logger.LogInformation("Generated ticket volume report with {Count} data points", results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating ticket volume report");
            throw;
        }
    }
}
