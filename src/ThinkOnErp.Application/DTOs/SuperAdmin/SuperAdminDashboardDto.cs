namespace ThinkOnErp.Application.DTOs.SuperAdmin;

/// <summary>
/// Data transfer object for SuperAdmin dashboard containing system metrics, alerts, and activity summaries
/// </summary>
public class SuperAdminDashboardDto
{
    /// <summary>
    /// Dashboard statistics including company, branch, and admin counts
    /// </summary>
    public DashboardStatsDto Stats { get; set; } = new();

    /// <summary>
    /// List of recently created companies (top 4)
    /// </summary>
    public List<RecentCompanyDto> RecentCompanies { get; set; } = new();

    /// <summary>
    /// List of recent branch activities (top 4)
    /// </summary>
    public List<RecentBranchActivityDto> RecentBranches { get; set; } = new();

    /// <summary>
    /// List of pending requests requiring SuperAdmin action (future implementation)
    /// </summary>
    public List<PendingRequestDto> PendingRequests { get; set; } = new();

    /// <summary>
    /// System alerts requiring SuperAdmin attention
    /// </summary>
    public List<string> Alerts { get; set; } = new();
}
