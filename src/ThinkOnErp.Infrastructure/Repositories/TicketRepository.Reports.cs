using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Infrastructure.Repositories;

public partial class TicketRepository
{
    private IQueryable<SysRequestTicket> ReportQuery(DateTime? from, DateTime? to, long company = 0)
    {
        if (from.HasValue && to.HasValue && from > to) throw new ArgumentException("Start date must precede end date.");
        var query = _context.SysRequestTickets.AsNoTracking().Include(t => t.Company)
            .Include(t => t.TicketStatus).Include(t => t.TicketType).Include(t => t.TicketPriority).Include(t => t.Assignee).AsQueryable();
        if (company > 0) query = query.Where(t => t.CompanyId == company);
        if (from.HasValue) query = query.Where(t => t.CreationDate >= from);
        if (to.HasValue) query = query.Where(t => t.CreationDate <= to);
        return query;
    }

    private static DateTime Period(DateTime date, string mode) => mode.ToUpperInvariant() switch {
        "DAILY" => date.Date,
        "WEEKLY" => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        "MONTHLY" => new DateTime(date.Year, date.Month, 1),
        _ => throw new ArgumentException("Invalid period grouping.") };

    private static Dictionary<string, object> Metrics(IEnumerable<SysRequestTicket> source)
    {
        var rows = source.ToList(); var now = DateTime.UtcNow;
        var resolved = rows.Where(t => TicketWorkflow.IsResolved(t.TicketStatus) && t.ActualResolutionDate.HasValue).ToList();
        var measured = resolved.Where(t => t.ExpectedResolutionDate.HasValue).ToList();
        var onTime = measured.Count(t => t.ActualResolutionDate <= t.ExpectedResolutionDate);
        return new() {
            ["TOTAL_TICKETS"] = rows.Count,
            ["OPEN_TICKETS"] = rows.Count(t => TicketWorkflow.Code(t.TicketStatus) == "OPEN"),
            ["IN_PROGRESS_TICKETS"] = rows.Count(t => TicketWorkflow.Code(t.TicketStatus) == "IN_PROGRESS"),
            ["RESOLVED_TICKETS"] = rows.Count(t => TicketWorkflow.Code(t.TicketStatus) == "RESOLVED"),
            ["CLOSED_TICKETS"] = rows.Count(t => TicketWorkflow.Code(t.TicketStatus) == "CLOSED"),
            ["CRITICAL_TICKETS"] = rows.Count(t => t.TicketPriority?.PriorityLevel == 1),
            ["HIGH_TICKETS"] = rows.Count(t => t.TicketPriority?.PriorityLevel == 2),
            ["MEDIUM_TICKETS"] = rows.Count(t => t.TicketPriority?.PriorityLevel == 3),
            ["LOW_TICKETS"] = rows.Count(t => t.TicketPriority?.PriorityLevel == 4),
            ["ON_TIME_RESOLVED"] = onTime,
            ["OVERDUE_RESOLVED"] = measured.Count - onTime,
            ["CURRENTLY_OVERDUE"] = rows.Count(t => TicketWorkflow.IsActiveWork(t.TicketStatus) && t.ActualResolutionDate == null && t.ExpectedResolutionDate < now),
            ["SLA_COMPLIANCE_PERCENTAGE"] = measured.Count == 0 ? 0m : Math.Round(100m * onTime / measured.Count, 2),
            ["AVG_RESOLUTION_HOURS"] = resolved.Any(t => t.CreationDate.HasValue)
                ? (decimal)resolved.Where(t => t.CreationDate.HasValue).Average(t => (t.ActualResolutionDate!.Value - t.CreationDate!.Value).TotalHours) : 0m
        };
    }

    public async Task<List<Dictionary<string, object>>> GetTicketVolumeReportAsync(DateTime startDate, DateTime endDate, long companyId = 0, long ticketTypeId = 0, string groupBy = "DAILY")
    {
        var mode = groupBy.ToUpperInvariant();
        if (mode is not ("DAILY" or "WEEKLY" or "MONTHLY" or "COMPANY" or "TYPE")) throw new ArgumentException("Invalid report grouping.");
        var query = ReportQuery(startDate, endDate, companyId);
        if (ticketTypeId > 0) query = query.Where(t => t.TicketTypeId == ticketTypeId);
        var rows = await query.ToListAsync();
        return rows.GroupBy(t => mode == "COMPANY" ? $"C{t.CompanyId}" : mode == "TYPE" ? $"T{t.TicketTypeId}" : Period(t.CreationDate!.Value, mode).ToString("yyyy-MM-dd"))
            .OrderBy(g => g.Key).Select(g => {
                var data = Metrics(g); var first = g.First();
                data["PERIOD_LABEL"] = g.Key;
                if (mode == "COMPANY") { data["COMPANY_ID"] = first.CompanyId; data["COMPANY_NAME"] = first.Company?.CompanyNameEn ?? ""; }
                else if (mode == "TYPE") { data["TICKET_TYPE_ID"] = first.TicketTypeId; data["TYPE_NAME"] = first.TicketType?.TypeNameEn ?? ""; }
                else data["PERIOD_DATE"] = Period(first.CreationDate!.Value, mode);
                return data;
            }).ToList();
    }

    public async Task<List<Dictionary<string, object>>> GetSlaComplianceReportAsync(DateTime startDate, DateTime endDate, long companyId = 0)
    {
        var rows = await ReportQuery(startDate, endDate, companyId).ToListAsync();
        return rows.GroupBy(t => new { t.TicketPriorityId, t.TicketTypeId }).Select(g => {
            var data = Metrics(g); var first = g.First();
            data["PRIORITY_NAME"] = first.TicketPriority?.PriorityNameEn ?? "";
            data["PRIORITY_LEVEL"] = first.TicketPriority?.PriorityLevel ?? 0;
            data["TYPE_NAME"] = first.TicketType?.TypeNameEn ?? "";
            return data;
        }).ToList();
    }

    public async Task<List<Dictionary<string, object>>> GetWorkloadReportAsync(DateTime? startDate = null, DateTime? endDate = null, long companyId = 0)
    {
        var rows = await ReportQuery(startDate, endDate, companyId).ToListAsync();
        return rows.GroupBy(t => t.AssigneeId).Select(g => {
            var data = Metrics(g); var user = g.First().Assignee;
            data["ASSIGNEE_ID"] = g.Key ?? 0;
            data["ASSIGNEE_NAME"] = user?.FullNameEn ?? "Unassigned";
            data["ASSIGNEE_USERNAME"] = user?.UserName ?? "";
            data["ASSIGNEE_EMAIL"] = user?.Email ?? "";
            data["TOTAL_ASSIGNED_TICKETS"] = g.Count();
            data["ACTIVE_TICKETS"] = g.Count(t => TicketWorkflow.IsActiveWork(t.TicketStatus));
            data["OVERDUE_TICKETS"] = data["CURRENTLY_OVERDUE"];
            return data;
        }).ToList();
    }

    public async Task<List<Dictionary<string, object>>> GetTicketTrendsReportAsync(DateTime startDate, DateTime endDate, string periodType = "DAILY")
    {
        _ = Period(startDate, periodType);
        if (startDate > endDate) throw new ArgumentException("Start date must precede end date.");
        var rows = await ReportQuery(null, null).Where(t =>
            (t.CreationDate >= startDate && t.CreationDate <= endDate) ||
            (t.ActualResolutionDate >= startDate && t.ActualResolutionDate <= endDate)).ToListAsync();
        var periods = rows.SelectMany(t => new[] { t.CreationDate, t.ActualResolutionDate })
            .Where(d => d.HasValue && d >= startDate && d <= endDate).Select(d => Period(d!.Value, periodType)).Distinct().OrderBy(d => d);
        return periods.Select(p => {
            var created = rows.Where(t => t.CreationDate >= startDate && t.CreationDate <= endDate && Period(t.CreationDate!.Value, periodType) == p).ToList();
            var resolved = rows.Where(t => TicketWorkflow.IsResolved(t.TicketStatus) && t.ActualResolutionDate >= startDate && t.ActualResolutionDate <= endDate && Period(t.ActualResolutionDate!.Value, periodType) == p).ToList();
            var data = Metrics(resolved);
            data["PERIOD_DATE"] = p; data["PERIOD_LABEL"] = p.ToString("yyyy-MM-dd");
            data["TICKETS_CREATED"] = created.Count; data["TICKETS_RESOLVED"] = resolved.Count;
            data["CRITICAL_CREATED"] = created.Count(t => t.TicketPriority?.PriorityLevel == 1);
            data["HIGH_CREATED"] = created.Count(t => t.TicketPriority?.PriorityLevel == 2);
            data["AVG_SATISFACTION_RATING"] = 0m;
            return data;
        }).ToList();
    }
}
