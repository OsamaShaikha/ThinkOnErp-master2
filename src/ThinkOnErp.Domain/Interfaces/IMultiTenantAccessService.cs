namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Service for enforcing multi-tenant access control.
/// Validates that users can only access data within their assigned company and branch.
/// </summary>
public interface IMultiTenantAccessService
{
    /// <summary>
    /// Validates that the current user has access to the specified company and branch.
    /// Throws UnauthorizedAccessException if access is denied.
    /// Logs security threat if unauthorized access is detected.
    /// </summary>
    /// <param name="companyId">Company ID to validate access for</param>
    /// <param name="branchId">Optional branch ID to validate access for</param>
    /// <exception cref="UnauthorizedAccessException">Thrown when user does not have access</exception>
    Task ValidateAccessAsync(long companyId, long? branchId = null);

    /// <summary>
    /// Checks if the current user has access to the specified company and branch.
    /// Returns true if access is allowed, false otherwise.
    /// Logs security threat if unauthorized access is detected.
    /// </summary>
    /// <param name="companyId">Company ID to check access for</param>
    /// <param name="branchId">Optional branch ID to check access for</param>
    /// <returns>True if user has access, false otherwise</returns>
    Task<bool> HasAccessAsync(long companyId, long? branchId = null);

    /// <summary>
    /// Gets the current user's company ID from claims.
    /// </summary>
    /// <returns>Current user's company ID</returns>
    /// <exception cref="InvalidOperationException">Thrown when company ID claim is missing or invalid</exception>
    long GetCurrentUserCompanyId();

    /// <summary>
    /// Gets the current user's branch ID from claims (may be null for company-level users).
    /// </summary>
    /// <returns>Current user's branch ID or null</returns>
    long? GetCurrentUserBranchId();

    /// <summary>
    /// Gets the current user's ID from claims.
    /// </summary>
    /// <returns>Current user's ID</returns>
    /// <exception cref="InvalidOperationException">Thrown when user ID claim is missing or invalid</exception>
    long GetCurrentUserId();
}
