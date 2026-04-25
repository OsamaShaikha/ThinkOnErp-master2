using MediatR;
using ThinkOnErp.Application.DTOs.SuperAdmin;

namespace ThinkOnErp.Application.Features.SuperAdmins.Queries.GetSuperAdminDashboard;

/// <summary>
/// Query to retrieve SuperAdmin dashboard data including system metrics, alerts, and activity summaries.
/// No parameters required - retrieves all dashboard data in a single optimized request.
/// Validates Requirements: 10.1, 10.2
/// </summary>
public class GetSuperAdminDashboardQuery : IRequest<SuperAdminDashboardDto>
{
    // No parameters - retrieves all dashboard data
}
