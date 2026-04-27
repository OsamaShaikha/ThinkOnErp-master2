using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ThinkOnErp.Infrastructure.Authorization;

/// <summary>
/// Authorization handler for audit data access control.
/// Implements role-based access control (RBAC) to ensure only authorized users can access audit data.
/// Enforces multi-tenant isolation by filtering audit data based on user's company and branch access.
/// </summary>
/// <remarks>
/// Authorization Levels:
/// - SuperAdmin: Can access all audit data across all companies and branches
/// - CompanyAdmin: Can access audit data for their company and all branches within it
/// - User: Can only access their own audit data (self-access)
/// 
/// This handler integrates with ASP.NET Core's authorization framework and enforces
/// Property 8 (Multi-Tenant Isolation) from the requirements: "FOR ALL audit log queries,
/// results SHALL only include entries for the requesting user's company and authorized branches"
/// </remarks>
public class AuditDataAuthorizationHandler : AuthorizationHandler<AuditDataAccessRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditDataAuthorizationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the AuditDataAuthorizationHandler class.
    /// </summary>
    /// <param name="httpContextAccessor">HTTP context accessor for accessing request context</param>
    /// <param name="logger">Logger for authorization decisions</param>
    public AuditDataAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditDataAuthorizationHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the authorization requirement for audit data access.
    /// Evaluates user's role and permissions to determine if they can access audit data.
    /// </summary>
    /// <param name="context">Authorization context containing user claims and resource</param>
    /// <param name="requirement">The audit data access requirement to evaluate</param>
    /// <returns>Task representing the asynchronous operation</returns>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AuditDataAccessRequirement requirement)
    {
        var user = context.User;

        // Extract user claims
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdminClaim = user.FindFirst("isAdmin")?.Value;
        var roleClaim = user.FindFirst("role")?.Value;
        var userCompanyIdClaim = user.FindFirst("CompanyId")?.Value;
        var userBranchIdClaim = user.FindFirst("BranchId")?.Value;

        // Validate required claims
        if (string.IsNullOrEmpty(userIdClaim))
        {
            _logger.LogWarning("User ID claim missing for audit data authorization");
            context.Fail();
            return Task.CompletedTask;
        }

        if (!long.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Invalid user ID claim for audit data authorization: {UserIdClaim}", userIdClaim);
            context.Fail();
            return Task.CompletedTask;
        }

        // SuperAdmins can access all audit data
        if (isAdminClaim == "true")
        {
            _logger.LogDebug(
                "SuperAdmin user {UserId} granted access to all audit data",
                userId);
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Company admins can access their company's audit data
        if (roleClaim == "COMPANY_ADMIN")
        {
            if (string.IsNullOrEmpty(userCompanyIdClaim) || !long.TryParse(userCompanyIdClaim, out var userCompanyId))
            {
                _logger.LogWarning(
                    "CompanyAdmin user {UserId} has invalid or missing CompanyId claim",
                    userId);
                context.Fail();
                return Task.CompletedTask;
            }

            // Company admins can access audit data for their company
            // The actual filtering by company will be enforced at the service/repository level
            _logger.LogDebug(
                "CompanyAdmin user {UserId} granted access to audit data for Company {CompanyId}",
                userId,
                userCompanyId);
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Regular users can only access their own audit data if AllowSelfAccess is enabled
        if (requirement.AllowSelfAccess)
        {
            // Regular users have limited access to their own audit data
            // The actual filtering by user ID will be enforced at the service/repository level
            _logger.LogDebug(
                "Regular user {UserId} granted self-access to their own audit data",
                userId);
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // If we reach here, the user doesn't have permission
        _logger.LogWarning(
            "User {UserId} denied access to audit data. Role: {Role}, IsAdmin: {IsAdmin}",
            userId,
            roleClaim ?? "none",
            isAdminClaim ?? "false");
        context.Fail();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Authorization requirement for audit data access.
/// Defines the policy requirements for accessing audit log data.
/// </summary>
public class AuditDataAccessRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Gets or sets a value indicating whether regular users can access their own audit data.
    /// When true, users can view audit logs for actions they performed.
    /// When false, only admins can access audit data.
    /// Default is true to support user self-service scenarios.
    /// </summary>
    public bool AllowSelfAccess { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the AuditDataAccessRequirement class.
    /// </summary>
    /// <param name="allowSelfAccess">Whether to allow users to access their own audit data</param>
    public AuditDataAccessRequirement(bool allowSelfAccess = true)
    {
        AllowSelfAccess = allowSelfAccess;
    }
}

/// <summary>
/// Authorization attribute for audit data access.
/// Applies the AuditDataAccess policy to controllers or actions.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireAuditDataAccessAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Policy name for audit data access authorization.
    /// </summary>
    public const string PolicyName = "AuditDataAccess";

    /// <summary>
    /// Initializes a new instance of the RequireAuditDataAccessAttribute class.
    /// </summary>
    public RequireAuditDataAccessAttribute()
    {
        Policy = PolicyName;
    }
}

/// <summary>
/// Authorization attribute for admin-only audit data access.
/// Applies the AdminOnlyAuditDataAccess policy to controllers or actions.
/// This policy does not allow self-access and requires admin privileges.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireAdminAuditDataAccessAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Policy name for admin-only audit data access authorization.
    /// </summary>
    public const string PolicyName = "AdminOnlyAuditDataAccess";

    /// <summary>
    /// Initializes a new instance of the RequireAdminAuditDataAccessAttribute class.
    /// </summary>
    public RequireAdminAuditDataAccessAttribute()
    {
        Policy = PolicyName;
    }
}
