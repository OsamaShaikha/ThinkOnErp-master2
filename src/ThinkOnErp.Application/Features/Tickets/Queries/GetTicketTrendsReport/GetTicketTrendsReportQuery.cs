using MediatR;
using ThinkOnErp.Application.DTOs.Ticket;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetTicketTrendsReport;

/// <summary>
/// Query for retrieving ticket trends reports for analytics.
/// Shows creation and resolution patterns over time.
/// </summary>
public class GetTicketTrendsReportQuery : IRequest<List<TicketTrendsReportDto>>
{
    /// <summary>
    /// Analysis start date
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Analysis end date
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Period grouping: DAILY, WEEKLY, MONTHLY
    /// </summary>
    public string PeriodType { get; set; } = "DAILY";
}
