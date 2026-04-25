namespace ThinkOnErp.Application.DTOs.SuperAdmin;

/// <summary>
/// Data transfer object for dashboard statistics
/// Validates Requirements: 1.4, 4.5, 5.3, 6.3
/// </summary>
public class DashboardStatsDto
{
    /// <summary>
    /// Total number of companies in the system
    /// </summary>
    public int TotalCompanies { get; set; }

    /// <summary>
    /// Number of active companies
    /// </summary>
    public int ActiveCompanies { get; set; }

    /// <summary>
    /// Number of inactive companies
    /// </summary>
    public int InactiveCompanies { get; set; }

    /// <summary>
    /// Total number of branches in the system
    /// </summary>
    public int TotalBranches { get; set; }

    /// <summary>
    /// Number of active branches
    /// </summary>
    public int ActiveBranches { get; set; }

    /// <summary>
    /// Total number of system administrators
    /// </summary>
    public int TotalSystemAdmins { get; set; }
}
