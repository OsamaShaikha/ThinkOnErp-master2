using MediatR;
using ThinkOnErp.Application.DTOs.Ticket;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetSlaComplianceReport;

/// <summary>
/// Query for retrieving SLA compliance reports with priority breakdown.
/// Shows compliance metrics grouped by priority and ticket type.
/// </summary>
public class GetSlaComplianceReportQuery : IRequest<List<SlaComplianceReportDto>>
{
    /// <summary>
    /// Report start date
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Report end date
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Filter by company ID (optional, 0 = all companies)
    /// </summary>
    public Int64 CompanyId { get; set; } = 0;
}
