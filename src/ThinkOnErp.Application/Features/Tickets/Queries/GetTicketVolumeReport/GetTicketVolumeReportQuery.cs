using MediatR;
using ThinkOnErp.Application.DTOs.Ticket;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetTicketVolumeReport;

/// <summary>
/// Query for retrieving ticket volume reports with time-based filtering.
/// Supports grouping by daily, weekly, monthly, company, or type.
/// </summary>
public class GetTicketVolumeReportQuery : IRequest<List<TicketVolumeReportDto>>
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

    /// <summary>
    /// Filter by ticket type ID (optional, 0 = all types)
    /// </summary>
    public Int64 TicketTypeId { get; set; } = 0;

    /// <summary>
    /// Grouping option: DAILY, WEEKLY, MONTHLY, COMPANY, TYPE
    /// </summary>
    public string GroupBy { get; set; } = "DAILY";
}
