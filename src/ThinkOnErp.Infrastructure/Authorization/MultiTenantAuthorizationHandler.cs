using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Infrastructure.Authorization;

/// <summary>
/// Authorization handler that enforces multi-tenant access control and detects unauthorized access attempts.
/// Validates that users can only access data within their assigned company and branch.
/// Integrates with SecurityMonitor to log unauthorized access attempts as security threats.
/// </summary>
public class MultiTenantAuthorizationHandler : AuthorizationHandler<MultiTenantAccessRequirement, MultiTenantResource>
{
    private readonly ISecurityMonitor _securityMonitor;
    private readonly ILogger<MultiTenantAuthorizationHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MultiTenantAuthorizationHandler(
        ISecurityMonitor securityMonitor,
        ILogger<MultiTenantAuthorizationHandler> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _securityMonitor = securityMonitor ?? throw new ArgumentNullException(nameof(securityMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MultiTenantAccessRequirement requirement,
        MultiTenantResource resource)
    {
        if (IsSuperAdmin(context.User))
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null &&
                httpContext.Items.TryGetValue(
                    TenantRequestContext.HttpContextItemKey,
                    out var tenantValue) &&
                tenantValue is TenantRequestContext tenantContext &&
                tenantContext.CompanyId == resource.CompanyId)
            {
                _logger.LogDebug(
                    "SuperAdmin authorized for selected Company {CompanyId}, Branch {BranchId}",
                    resource.CompanyId,
                    resource.BranchId);
                context.Succeed(requirement);
                return;
            }

            _logger.LogWarning(
                "SuperAdmin request denied because the target company was not explicitly selected");
            context.Fail();
            return;
        }

        // Extract user claims
        var userIdClaim = FindClaim(context.User, "userId", ClaimTypes.NameIdentifier);
        var userCompanyIdClaim = FindClaim(context.User, "companyId", "CompanyId");
        var userBranchIdClaim = FindClaim(context.User, "branchId", "BranchId");

        if (string.IsNullOrEmpty(userIdClaim) || 
            string.IsNullOrEmpty(userCompanyIdClaim))
        {
            _logger.LogWarning("User claims missing for multi-tenant authorization");
            context.Fail();
            return;
        }

        if (!long.TryParse(userIdClaim, out var userId) ||
            !long.TryParse(userCompanyIdClaim, out var userCompanyId))
        {
            _logger.LogWarning("Invalid user claims for multi-tenant authorization");
            context.Fail();
            return;
        }

        // Parse user's branch ID (may be null for company-level users)
        long? userBranchId = null;
        if (!string.IsNullOrEmpty(userBranchIdClaim) &&
            long.TryParse(userBranchIdClaim, out var parsedBranchId) &&
            parsedBranchId > 0)
        {
            userBranchId = parsedBranchId;
        }

        // Check if user is trying to access a different company
        if (resource.CompanyId != userCompanyId)
        {
            _logger.LogWarning(
                "Unauthorized access attempt: User {UserId} from Company {UserCompanyId} attempted to access Company {ResourceCompanyId}",
                userId, userCompanyId, resource.CompanyId);

            // Detect and log the unauthorized access attempt
            var threat = await _securityMonitor.DetectUnauthorizedAccessAsync(
                userId, 
                resource.CompanyId, 
                resource.BranchId ?? 0);

            if (threat != null)
            {
                // Trigger security alert
                await _securityMonitor.TriggerSecurityAlertAsync(threat);
                
                _logger.LogWarning(
                    "Security threat triggered for unauthorized access: {ThreatType}, Severity: {Severity}",
                    threat.ThreatType, threat.Severity);
            }

            context.Fail();
            return;
        }

        // Check if user is trying to access a different branch (if branch-level access control is required)
        if (resource.BranchId.HasValue && userBranchId.HasValue && resource.BranchId != userBranchId)
        {
            // Check if user has company-wide access (no branch restriction)
            // If user has a specific branch assigned, they can only access that branch
            _logger.LogWarning(
                "Unauthorized access attempt: User {UserId} from Branch {UserBranchId} attempted to access Branch {ResourceBranchId}",
                userId, userBranchId, resource.BranchId);

            // Detect and log the unauthorized access attempt
            var threat = await _securityMonitor.DetectUnauthorizedAccessAsync(
                userId, 
                resource.CompanyId, 
                resource.BranchId.Value);

            if (threat != null)
            {
                // Trigger security alert
                await _securityMonitor.TriggerSecurityAlertAsync(threat);
                
                _logger.LogWarning(
                    "Security threat triggered for unauthorized branch access: {ThreatType}, Severity: {Severity}",
                    threat.ThreatType, threat.Severity);
            }

            context.Fail();
            return;
        }

        // Access is authorized
        _logger.LogDebug(
            "Multi-tenant access authorized: User {UserId} accessing Company {CompanyId}, Branch {BranchId}",
            userId, resource.CompanyId, resource.BranchId);

        context.Succeed(requirement);
    }

    private static bool IsSuperAdmin(ClaimsPrincipal user) =>
        string.Equals(
            user.FindFirst("isSuperAdmin")?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static string? FindClaim(
        ClaimsPrincipal user,
        string preferredClaimType,
        string legacyClaimType) =>
        user.FindFirst(preferredClaimType)?.Value ??
        user.FindFirst(legacyClaimType)?.Value;
}

/// <summary>
/// Authorization requirement for multi-tenant access control.
/// </summary>
public class MultiTenantAccessRequirement : IAuthorizationRequirement
{
    public MultiTenantAccessRequirement()
    {
    }
}

/// <summary>
/// Represents a resource that belongs to a specific company and optionally a branch.
/// Used for multi-tenant authorization checks.
/// </summary>
public class MultiTenantResource
{
    public long CompanyId { get; set; }
    public long? BranchId { get; set; }

    public MultiTenantResource(long companyId, long? branchId = null)
    {
        CompanyId = companyId;
        BranchId = branchId;
    }
}
