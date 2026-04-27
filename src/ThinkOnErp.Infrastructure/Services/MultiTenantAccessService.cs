using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for enforcing multi-tenant access control at the service/repository level.
/// Validates that users can only access data within their assigned company and branch.
/// Integrates with SecurityMonitor to detect and log unauthorized access attempts.
/// </summary>
public class MultiTenantAccessService : IMultiTenantAccessService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISecurityMonitor _securityMonitor;
    private readonly ILogger<MultiTenantAccessService> _logger;

    public MultiTenantAccessService(
        IHttpContextAccessor httpContextAccessor,
        ISecurityMonitor securityMonitor,
        ILogger<MultiTenantAccessService> logger)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _securityMonitor = securityMonitor ?? throw new ArgumentNullException(nameof(securityMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates that the current user has access to the specified company and branch.
    /// Throws UnauthorizedAccessException if access is denied.
    /// Logs security threat if unauthorized access is detected.
    /// </summary>
    public async Task ValidateAccessAsync(long companyId, long? branchId = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            throw new InvalidOperationException("HttpContext is not available");
        }

        var user = httpContext.User;
        if (user == null || !user.Identity?.IsAuthenticated == true)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        // Extract user claims
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userCompanyIdClaim = user.FindFirst("CompanyId")?.Value;
        var userBranchIdClaim = user.FindFirst("BranchId")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(userCompanyIdClaim))
        {
            throw new UnauthorizedAccessException("User claims are missing");
        }

        if (!long.TryParse(userIdClaim, out var userId) ||
            !long.TryParse(userCompanyIdClaim, out var userCompanyId))
        {
            throw new UnauthorizedAccessException("Invalid user claims");
        }

        // Parse user's branch ID (may be null for company-level users)
        long? userBranchId = null;
        if (!string.IsNullOrEmpty(userBranchIdClaim) && long.TryParse(userBranchIdClaim, out var parsedBranchId))
        {
            userBranchId = parsedBranchId;
        }

        // Check company access
        if (companyId != userCompanyId)
        {
            _logger.LogWarning(
                "Unauthorized company access attempt: User {UserId} from Company {UserCompanyId} attempted to access Company {TargetCompanyId}",
                userId, userCompanyId, companyId);

            // Detect and log the unauthorized access attempt
            var threat = await _securityMonitor.DetectUnauthorizedAccessAsync(
                userId,
                companyId,
                branchId ?? 0);

            if (threat != null)
            {
                await _securityMonitor.TriggerSecurityAlertAsync(threat);
            }

            throw new UnauthorizedAccessException(
                $"User does not have access to company {companyId}");
        }

        // Check branch access if specified
        if (branchId.HasValue && userBranchId.HasValue && branchId.Value != userBranchId.Value)
        {
            _logger.LogWarning(
                "Unauthorized branch access attempt: User {UserId} from Branch {UserBranchId} attempted to access Branch {TargetBranchId}",
                userId, userBranchId, branchId);

            // Detect and log the unauthorized access attempt
            var threat = await _securityMonitor.DetectUnauthorizedAccessAsync(
                userId,
                companyId,
                branchId.Value);

            if (threat != null)
            {
                await _securityMonitor.TriggerSecurityAlertAsync(threat);
            }

            throw new UnauthorizedAccessException(
                $"User does not have access to branch {branchId}");
        }

        _logger.LogDebug(
            "Multi-tenant access validated: User {UserId} accessing Company {CompanyId}, Branch {BranchId}",
            userId, companyId, branchId);
    }

    /// <summary>
    /// Checks if the current user has access to the specified company and branch.
    /// Returns true if access is allowed, false otherwise.
    /// Logs security threat if unauthorized access is detected.
    /// </summary>
    public async Task<bool> HasAccessAsync(long companyId, long? branchId = null)
    {
        try
        {
            await ValidateAccessAsync(companyId, branchId);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking multi-tenant access");
            return false;
        }
    }

    /// <summary>
    /// Gets the current user's company ID from claims.
    /// </summary>
    public long GetCurrentUserCompanyId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            throw new InvalidOperationException("HttpContext is not available");
        }

        var user = httpContext.User;
        var companyIdClaim = user?.FindFirst("CompanyId")?.Value;

        if (string.IsNullOrEmpty(companyIdClaim) || !long.TryParse(companyIdClaim, out var companyId))
        {
            throw new InvalidOperationException("User company ID claim is missing or invalid");
        }

        return companyId;
    }

    /// <summary>
    /// Gets the current user's branch ID from claims (may be null for company-level users).
    /// </summary>
    public long? GetCurrentUserBranchId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            throw new InvalidOperationException("HttpContext is not available");
        }

        var user = httpContext.User;
        var branchIdClaim = user?.FindFirst("BranchId")?.Value;

        if (string.IsNullOrEmpty(branchIdClaim))
        {
            return null;
        }

        if (long.TryParse(branchIdClaim, out var branchId))
        {
            return branchId;
        }

        return null;
    }

    /// <summary>
    /// Gets the current user's ID from claims.
    /// </summary>
    public long GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            throw new InvalidOperationException("HttpContext is not available");
        }

        var user = httpContext.User;
        var userIdClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("User ID claim is missing or invalid");
        }

        return userId;
    }
}
