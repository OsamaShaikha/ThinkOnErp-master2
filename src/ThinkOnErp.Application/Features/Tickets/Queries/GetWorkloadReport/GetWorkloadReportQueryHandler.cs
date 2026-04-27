using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetWorkloadReport;

/// <summary>
/// Handler for GetWorkloadReportQuery.
/// Retrieves workload statistics per assignee showing active and resolved tickets.
/// </summary>
public class GetWorkloadReportQueryHandler : IRequestHandler<GetWorkloadReportQuery, List<WorkloadReportDto>>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<GetWorkloadReportQueryHandler> _logger;

    public GetWorkloadReportQueryHandler(
        ITicketRepository ticketRepository,
        ILogger<GetWorkloadReportQueryHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<List<WorkloadReportDto>> Handle(GetWorkloadReportQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating workload report - StartDate: {StartDate}, EndDate: {EndDate}",
            request.StartDate, request.EndDate);

        try
        {
            // Get report data from repository
            var reportData = await _ticketRepository.GetWorkloadReportAsync(
                request.StartDate,
                request.EndDate,
                request.CompanyId);

            // Map to DTOs
            var results = reportData.Select(row => new WorkloadReportDto
            {
                AssigneeId = row.ContainsKey("ASSIGNEE_ID") && row["ASSIGNEE_ID"] != null
                    ? Convert.ToInt64(row["ASSIGNEE_ID"])
                    : 0,
                AssigneeName = row.ContainsKey("ASSIGNEE_NAME") && row["ASSIGNEE_NAME"] != null
                    ? row["ASSIGNEE_NAME"].ToString() ?? string.Empty
                    : string.Empty,
                AssigneeUsername = row.ContainsKey("ASSIGNEE_USERNAME") && row["ASSIGNEE_USERNAME"] != null
                    ? row["ASSIGNEE_USERNAME"].ToString() ?? string.Empty
                    : string.Empty,
                AssigneeEmail = row.ContainsKey("ASSIGNEE_EMAIL") && row["ASSIGNEE_EMAIL"] != null
                    ? row["ASSIGNEE_EMAIL"].ToString() ?? string.Empty
                    : string.Empty,
                TotalAssignedTickets = row.ContainsKey("TOTAL_ASSIGNED_TICKETS") && row["TOTAL_ASSIGNED_TICKETS"] != null
                    ? Convert.ToInt32(row["TOTAL_ASSIGNED_TICKETS"])
                    : 0,
                ActiveTickets = row.ContainsKey("ACTIVE_TICKETS") && row["ACTIVE_TICKETS"] != null
                    ? Convert.ToInt32(row["ACTIVE_TICKETS"])
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
                OverdueTickets = row.ContainsKey("OVERDUE_TICKETS") && row["OVERDUE_TICKETS"] != null
                    ? Convert.ToInt32(row["OVERDUE_TICKETS"])
                    : 0,
                AvgResolutionHours = row.ContainsKey("AVG_RESOLUTION_HOURS") && row["AVG_RESOLUTION_HOURS"] != null
                    ? Convert.ToDecimal(row["AVG_RESOLUTION_HOURS"])
                    : 0,
                SlaCompliancePercentage = row.ContainsKey("SLA_COMPLIANCE_PERCENTAGE") && row["SLA_COMPLIANCE_PERCENTAGE"] != null
                    ? Convert.ToDecimal(row["SLA_COMPLIANCE_PERCENTAGE"])
                    : 0
            }).ToList();

            _logger.LogInformation("Generated workload report with {Count} assignees", results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating workload report");
            throw;
        }
    }
}
