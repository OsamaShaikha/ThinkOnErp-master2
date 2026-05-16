using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.SuperAdmin;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SuperAdmins.Queries.GetSuperAdminDashboard;

/// <summary>
/// Query handler for retrieving SuperAdmin dashboard data.
/// Aggregates data from multiple repositories and generates system metrics, alerts, and activity summaries.
/// Validates Requirements: 1.3, 10.1, 10.2
/// </summary>
public class GetSuperAdminDashboardQueryHandler : IRequestHandler<GetSuperAdminDashboardQuery, SuperAdminDashboardDto>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<GetSuperAdminDashboardQueryHandler> _logger;

    public GetSuperAdminDashboardQueryHandler(
        ICompanyRepository companyRepository,
        IBranchRepository branchRepository,
        IUserRepository userRepository,
        ITicketRepository ticketRepository,
        ILogger<GetSuperAdminDashboardQueryHandler> logger)
    {
        _companyRepository = companyRepository;
        _branchRepository = branchRepository;
        _userRepository = userRepository;
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<SuperAdminDashboardDto> Handle(GetSuperAdminDashboardQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Parallel repository calls with error handling
            // Validates Requirements: 1.3, 13.1, 13.5
            var companiesTask = _companyRepository.GetAllAsync();
            var branchesTask = _branchRepository.GetAllAsync();
            var usersTask = _userRepository.GetAllAsync();
            // Get pending tickets (unresolved) with pagination
            var ticketTask = _ticketRepository.GetAllAsync(
                page: 1,
                pageSize: 100, // Get enough tickets to filter
                sortBy: "CreationDate",
                sortDirection: "DESC");

            await Task.WhenAll(companiesTask, branchesTask, usersTask, ticketTask);

            if (companiesTask.IsFaulted || branchesTask.IsFaulted || usersTask.IsFaulted || ticketTask.IsFaulted)
            {
                var exceptions = new List<Exception>();
                if (companiesTask.Exception != null) exceptions.Add(companiesTask.Exception);
                if (branchesTask.Exception != null) exceptions.Add(branchesTask.Exception);
                if (usersTask.Exception != null) exceptions.Add(usersTask.Exception);
                if (ticketTask.Exception != null) exceptions.Add(ticketTask.Exception);

                _logger.LogError(new AggregateException(exceptions),
                    "Error retrieving data from repositories");
                throw new ApplicationException(
                    "Failed to retrieve dashboard data",
                    new AggregateException(exceptions));
            }

            // Null safety checks with warning logs for empty data
            // Validates Requirements: 11.5
            var companies = companiesTask.Result ?? new List<Domain.Entities.SysCompany>();
            var branches = branchesTask.Result ?? new List<Domain.Entities.SysBranch>();
            var users = usersTask.Result ?? new List<Domain.Entities.SysUser>();
            var ticketResult = ticketTask.Result;
            var tickets = ticketResult.Tickets ?? new List<Domain.Entities.SysRequestTicket>();

            // Log warnings for empty data
            if (companies.Count == 0)
            {
                _logger.LogWarning("No companies found in the system");
            }
            if (branches.Count == 0)
            {
                _logger.LogWarning("No branches found in the system");
            }
            if (users.Count == 0)
            {
                _logger.LogWarning("No users found in the system");
            }
            if (tickets.Count == 0)
            {
                _logger.LogWarning("No tickets found in the system");
            }

            // Calculate metrics using LINQ
            // Validates Requirements: 1.4, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 6.1, 6.2
            var pendingTicketsCount = tickets.Count(t => t.IsActive && !t.IsResolved);
            
            var stats = new DashboardStatsDto
            {
                TotalCompanies = companies.Count,
                ActiveCompanies = companies.Count(c => c.IsActive),
                InactiveCompanies = companies.Count(c => !c.IsActive),
                TotalBranches = branches.Count,
                ActiveBranches = branches.Count(b => b.IsActive),
                TotalSystemAdmins = users.Count(u => u.IsAdmin),
                PendingRequests = pendingTicketsCount
            };

            // Get recent companies
            // Validates Requirements: 1.5, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7
            var recentCompanies = companies
                .OrderByDescending(c => c.CreationDate)
                .Take(4)
                .Select(c => new RecentCompanyDto
                {
                    NameAr = c.RowDesc,
                    NameEn = c.RowDescE,
                    Country = null, // Country name not available in entity, only CountryId
                    BranchCount = branches.Count(b => b.ParRowId == c.RowId),
                    Status = c.IsActive ? "Active" : "Inactive",
                    CreatedDate = c.CreationDate ?? DateTime.MinValue
                })
                .ToList();

            // Get recent branch activities
            // Validates Requirements: 1.6, 8.1, 8.2, 8.3, 8.4, 8.5, 8.6
            var recentBranches = branches
                .OrderByDescending(b => b.UpdateDate)
                .Take(4)
                .Select(b =>
                {
                    var company = companies.FirstOrDefault(c => c.RowId == b.ParRowId);
                    return new RecentBranchActivityDto
                    {
                        BranchNameAr = b.RowDesc,
                        BranchNameEn = b.RowDescE,
                        CompanyNameAr = company?.RowDesc ?? "",
                        CompanyNameEn = company?.RowDescE ?? "",
                        ActivityType = b.CreationDate == b.UpdateDate ? "New" : "Update",
                        ActivityDate = b.UpdateDate ?? DateTime.MinValue
                    };
                })
                .ToList();
            var recentTickets = tickets
                .Where(t => t.IsActive && !t.IsResolved) // Only active and unresolved tickets
                .OrderByDescending(t => t.CreationDate)
                .Take(8) // Get top 8 pending requests
                .Select(t => new PendingRequestDto
                {
                    TicketId = t.RowId,
                    CompanyNameAr = t.Company?.RowDesc ?? "",
                    CompanyNameEn = t.Company?.RowDescE ?? "",
                    RequestTypeAr = t.TitleAr,
                    RequestTypeEn = t.TitleEn,
                    Description = t.Description,
                    Priority = t.TicketPriority?.PriorityNameAr ?? "متوسط",
                    PriorityCode = GetPriorityCode(t.TicketPriority?.PriorityNameEn ?? "Medium"),
                    RequestDate = t.CreationDate ?? DateTime.MinValue,
                    BranchNameAr = t.Branch?.RowDesc ?? "",
                    BranchNameEn = t.Branch?.RowDescE ?? "",
                    Status = t.TicketStatus?.StatusNameAr ?? "قيد الانتظار"
                })
                .ToList();

            // Generate system alerts
            // Validates Requirements: 1.7, 3.1, 3.2, 3.3, 3.4
            var alerts = new List<string>();
            if (stats.InactiveCompanies > 0)
            {
                alerts.Add($"{stats.InactiveCompanies} companies are currently inactive");
            }
            if (pendingTicketsCount > 0)
            {
                alerts.Add($"{pendingTicketsCount} pending requests require Super Admin action");
            }

            return new SuperAdminDashboardDto
            {
                Stats = stats,
                RecentCompanies = recentCompanies,
                RecentBranches = recentBranches,
                PendingRequests = recentTickets,
                Alerts = alerts
            };
        }
        catch (ApplicationException ex)
        {
            _logger.LogError(ex, "Application error in dashboard query handler");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Unexpected error in dashboard query handler");
            throw new ApplicationException(
                "An unexpected error occurred while processing dashboard data",
                ex);
        }
    }

    /// <summary>
    /// Maps priority name to priority code for UI styling
    /// </summary>
    private static string GetPriorityCode(string priorityName)
    {
        return priorityName?.ToLower() switch
        {
            "high" or "urgent" or "critical" => "high",
            "low" => "low",
            _ => "medium"
        };
    }
}
