using System;
using ThinkOnErp.Domain.Entities.Audit;

namespace ThinkOnErp.Examples;

/// <summary>
/// Example usage of AuthenticationAuditEvent class for different authentication scenarios
/// </summary>
public class AuthenticationAuditEventUsageExample
{
    /// <summary>
    /// Example: Successful user login
    /// </summary>
    public static AuthenticationAuditEvent CreateSuccessfulLoginEvent(long userId, long companyId, long branchId, string ipAddress, string userAgent, string tokenId)
    {
        return new AuthenticationAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = userId,
            CompanyId = companyId,
            BranchId = branchId,
            Action = "LOGIN",
            EntityType = "Authentication",
            EntityId = null, // Not applicable for authentication events
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Authentication-specific properties
            Success = true,
            FailureReason = null,
            TokenId = tokenId,
            SessionDuration = null // Not applicable for login
        };
    }

    /// <summary>
    /// Example: Failed user login attempt
    /// </summary>
    public static AuthenticationAuditEvent CreateFailedLoginEvent(string attemptedUsername, string ipAddress, string userAgent, string failureReason)
    {
        return new AuthenticationAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 0, // Unknown user for failed login
            CompanyId = null, // Unknown company for failed login
            BranchId = null, // Unknown branch for failed login
            Action = "LOGIN_FAILED",
            EntityType = "Authentication",
            EntityId = null,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Authentication-specific properties
            Success = false,
            FailureReason = failureReason, // e.g., "Invalid username or password"
            TokenId = null, // No token for failed login
            SessionDuration = null
        };
    }

    /// <summary>
    /// Example: User logout with session duration tracking
    /// </summary>
    public static AuthenticationAuditEvent CreateLogoutEvent(long userId, long companyId, long branchId, string ipAddress, string userAgent, string tokenId, DateTime loginTime)
    {
        var sessionDuration = DateTime.UtcNow - loginTime;
        
        return new AuthenticationAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = userId,
            CompanyId = companyId,
            BranchId = branchId,
            Action = "LOGOUT",
            EntityType = "Authentication",
            EntityId = null,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Authentication-specific properties
            Success = true,
            FailureReason = null,
            TokenId = tokenId,
            SessionDuration = sessionDuration // Track how long the user was logged in
        };
    }

    /// <summary>
    /// Example: Successful JWT token refresh
    /// </summary>
    public static AuthenticationAuditEvent CreateTokenRefreshEvent(long userId, long companyId, long branchId, string ipAddress, string userAgent, string oldTokenId, string newTokenId)
    {
        return new AuthenticationAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = userId,
            CompanyId = companyId,
            BranchId = branchId,
            Action = "TOKEN_REFRESH",
            EntityType = "Authentication",
            EntityId = null,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Authentication-specific properties
            Success = true,
            FailureReason = null,
            TokenId = newTokenId, // Store the new token ID
            SessionDuration = null // Not applicable for token refresh
        };
    }

    /// <summary>
    /// Example: Failed token refresh (expired refresh token)
    /// </summary>
    public static AuthenticationAuditEvent CreateFailedTokenRefreshEvent(long userId, long companyId, long branchId, string ipAddress, string userAgent, string expiredTokenId)
    {
        return new AuthenticationAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = userId,
            CompanyId = companyId,
            BranchId = branchId,
            Action = "TOKEN_REFRESH_FAILED",
            EntityType = "Authentication",
            EntityId = null,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Authentication-specific properties
            Success = false,
            FailureReason = "Refresh token expired",
            TokenId = expiredTokenId, // Store the expired token ID for investigation
            SessionDuration = null
        };
    }

    /// <summary>
    /// Example: Token revocation (force logout)
    /// </summary>
    public static AuthenticationAuditEvent CreateTokenRevocationEvent(long userId, long companyId, long branchId, string tokenId, string reason)
    {
        return new AuthenticationAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "SYSTEM", // System-initiated revocation
            ActorId = 0, // System actor
            CompanyId = companyId,
            BranchId = branchId,
            Action = "TOKEN_REVOKED",
            EntityType = "Authentication",
            EntityId = userId, // The user whose token was revoked
            IpAddress = null, // System action, no IP
            UserAgent = null, // System action, no user agent
            Timestamp = DateTime.UtcNow,
            
            // Authentication-specific properties
            Success = true,
            FailureReason = reason, // e.g., "Security policy violation", "Admin force logout"
            TokenId = tokenId,
            SessionDuration = null
        };
    }

    /// <summary>
    /// Example: Super admin login (different actor type)
    /// </summary>
    public static AuthenticationAuditEvent CreateSuperAdminLoginEvent(long adminId, string ipAddress, string userAgent, string tokenId)
    {
        return new AuthenticationAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "SUPER_ADMIN",
            ActorId = adminId,
            CompanyId = null, // Super admin is not tied to a specific company
            BranchId = null, // Super admin is not tied to a specific branch
            Action = "LOGIN",
            EntityType = "Authentication",
            EntityId = null,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            
            // Authentication-specific properties
            Success = true,
            FailureReason = null,
            TokenId = tokenId,
            SessionDuration = null
        };
    }

    /// <summary>
    /// Common failure reasons for authentication events
    /// </summary>
    public static class FailureReasons
    {
        public const string InvalidCredentials = "Invalid username or password";
        public const string AccountLocked = "Account locked due to multiple failed attempts";
        public const string AccountDisabled = "Account has been disabled";
        public const string PasswordExpired = "Password has expired";
        public const string TwoFactorRequired = "Two-factor authentication required";
        public const string InvalidTwoFactor = "Invalid two-factor authentication code";
        public const string TokenExpired = "Authentication token has expired";
        public const string RefreshTokenExpired = "Refresh token has expired";
        public const string TokenRevoked = "Token has been revoked";
        public const string InvalidTokenSignature = "Invalid token signature";
        public const string InsufficientPermissions = "Insufficient permissions for this action";
        public const string SecurityPolicyViolation = "Security policy violation detected";
    }

    /// <summary>
    /// Common action types for authentication events
    /// </summary>
    public static class ActionTypes
    {
        public const string Login = "LOGIN";
        public const string LoginFailed = "LOGIN_FAILED";
        public const string Logout = "LOGOUT";
        public const string TokenRefresh = "TOKEN_REFRESH";
        public const string TokenRefreshFailed = "TOKEN_REFRESH_FAILED";
        public const string TokenRevoked = "TOKEN_REVOKED";
        public const string PasswordChanged = "PASSWORD_CHANGED";
        public const string TwoFactorEnabled = "TWO_FACTOR_ENABLED";
        public const string TwoFactorDisabled = "TWO_FACTOR_DISABLED";
    }
}