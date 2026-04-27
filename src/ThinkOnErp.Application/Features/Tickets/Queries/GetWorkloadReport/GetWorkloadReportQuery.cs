using MediatR;
using ThinkOnErp.Application.DTOs.Ticket;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetWorkloadReport;

/// <summary>
/// Query for retrieving workload reports for assignee analysis.
/// Shows ticket distribution and metrics per assigned user.
/// </summary>
public class GetWorkloadReportQuery : IRequest<List<WorkloadReportDto>>
{
    /// <summary>
    /// Report start date (optional)
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Report end date (optional)
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Filter by company ID (optional, 0 = all companies)
    /// </summary>
    public Int64 CompanyId { get; set; } = 0;
}
