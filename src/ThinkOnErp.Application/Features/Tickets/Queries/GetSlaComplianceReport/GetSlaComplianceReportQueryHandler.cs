using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetSlaComplianceReport;

/// <summary>
/// Handler for GetSlaComplianceReportQuery.
/// Retrieves SLA compliance statistics grouped by priority and type.
/// </summary>
public class GetSlaComplianceReportQueryHandler : IRequestHandler<GetSlaComplianceReportQuery, List<SlaComplianceReportDto>>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<GetSlaComplianceReportQueryHandler> _logger;

    public GetSlaComplianceReportQueryHandler(
        ITicketRepository ticketRepository,
        ILogger<GetSlaComplianceReportQueryHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<List<SlaComplianceReportDto>> Handle(GetSlaComplianceReportQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating SLA compliance report - StartDate: {StartDate}, EndDate: {EndDate}",
            request.StartDate, request.EndDate);

        try
        {
            // Get report data from repository
            var reportData = await _ticketRepository.GetSlaComplianceReportAsync(
                request.StartDate,
                request.EndDate,
                request.CompanyId);

            // Map to DTOs
            var results = reportData.Select(row => new SlaComplianceReportDto
            {
                PriorityName = row.ContainsKey("PRIORITY_NAME") && row["PRIORITY_NAME"] != null
                    ? row["PRIORITY_NAME"].ToString() ?? string.Empty
                    : string.Empty,
                PriorityLevel = row.ContainsKey("PRIORITY_LEVEL") && row["PRIORITY_LEVEL"] != null
                    ? Convert.ToInt32(row["PRIORITY_LEVEL"])
                    : 0,
                TypeName = row.ContainsKey("TYPE_NAME") && row["TYPE_NAME"] != null
                    ? row["TYPE_NAME"].ToString() ?? string.Empty
                    : string.Empty,
                TotalTickets = row.ContainsKey("TOTAL_TICKETS") && row["TOTAL_TICKETS"] != null
                    ? Convert.ToInt32(row["TOTAL_TICKETS"])
                    : 0,
                OnTimeResolved = row.ContainsKey("ON_TIME_RESOLVED") && row["ON_TIME_RESOLVED"] != null
                    ? Convert.ToInt32(row["ON_TIME_RESOLVED"])
                    : 0,
                OverdueResolved = row.ContainsKey("OVERDUE_RESOLVED") && row["OVERDUE_RESOLVED"] != null
                    ? Convert.ToInt32(row["OVERDUE_RESOLVED"])
                    : 0,
                CurrentlyOverdue = row.ContainsKey("CURRENTLY_OVERDUE") && row["CURRENTLY_OVERDUE"] != null
                    ? Convert.ToInt32(row["CURRENTLY_OVERDUE"])
                    : 0,
                SlaCompliancePercentage = row.ContainsKey("SLA_COMPLIANCE_PERCENTAGE") && row["SLA_COMPLIANCE_PERCENTAGE"] != null
                    ? Convert.ToDecimal(row["SLA_COMPLIANCE_PERCENTAGE"])
                    : 0,
                AvgResolutionHours = row.ContainsKey("AVG_RESOLUTION_HOURS") && row["AVG_RESOLUTION_HOURS"] != null
                    ? Convert.ToDecimal(row["AVG_RESOLUTION_HOURS"])
                    : 0
            }).ToList();

            _logger.LogInformation("Generated SLA compliance report with {Count} data points", results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SLA compliance report");
            throw;
        }
    }
}
