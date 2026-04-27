using ThinkOnErp.Domain.Entities.Audit;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Domain;

/// <summary>
/// Unit tests for AuthenticationAuditEvent class
/// </summary>
public class AuthenticationAuditEventTests
{
    [Fact]
    public void AuthenticationAuditEvent_Inherits_From_AuditEvent()
    {
        // Arrange & Act
        var auditEvent = new AuthenticationAuditEvent();
        
        // Assert
        Assert.IsAssignableFrom<AuditEvent>(auditEvent);
    }

    [Fact]
    public void AuthenticationAuditEvent_Successful_Login_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var tokenId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        
        var auditEvent = new AuthenticationAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "USER",
            ActorId = 123,
            CompanyId = 1,
            BranchId = 2,
            Action = "LOGIN",
            EntityType = "Authentication",
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp,
            Success = true,
            FailureReason = null,
            TokenId = tokenId,
            SessionDuration = null // Not applicable for login
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("USER", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Equal(2, auditEvent.BranchId);
        Assert.Equal("LOGIN", auditEvent.Action);
        Assert.Equal("Authentication", auditEvent.EntityType);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.True(auditEvent.Success);
        Assert.Null(auditEvent.FailureReason);
        Assert.Equal(tokenId, auditEvent.TokenId);
        Assert.Null(auditEvent.SessionDuration);
    }

    [Fact]
    public void AuthenticationAuditEvent_Failed_Login_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        
        var auditEvent = new AuthenticationAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "USER",
            ActorId = 0, // Unknown user for failed login
            CompanyId = null, // Unknown company for failed login
            BranchId = null, // Unknown branch for failed login
            Action = "LOGIN_FAILED",
            EntityType = "Authentication",
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp,
            Success = false,
            FailureReason = "Invalid username or password",
            TokenId = null, // No token for failed login
            SessionDuration = null // Not applicable for failed login
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("USER", auditEvent.ActorType);
        Assert.Equal(0, auditEvent.ActorId);
        Assert.Null(auditEvent.CompanyId);
        Assert.Null(auditEvent.BranchId);
        Assert.Equal("LOGIN_FAILED", auditEvent.Action);
        Assert.Equal("Authentication", auditEvent.EntityType);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.False(auditEvent.Success);
        Assert.Equal("Invalid username or password", auditEvent.FailureReason);
        Assert.Null(auditEvent.TokenId);
        Assert.Null(auditEvent.SessionDuration);
    }

    [Fact]
    public void AuthenticationAuditEvent_Logout_With_Session_Duration()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var tokenId = Guid.NewGuid().ToString();
        var sessionDuration = TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(30));
        var timestamp = DateTime.UtcNow;
        
        var auditEvent = new AuthenticationAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "USER",
            ActorId = 123,
            CompanyId = 1,
            BranchId = 2,
            Action = "LOGOUT",
            EntityType = "Authentication",
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp,
            Success = true,
            FailureReason = null,
            TokenId = tokenId,
            SessionDuration = sessionDuration
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("USER", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Equal(2, auditEvent.BranchId);
        Assert.Equal("LOGOUT", auditEvent.Action);
        Assert.Equal("Authentication", auditEvent.EntityType);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.True(auditEvent.Success);
        Assert.Null(auditEvent.FailureReason);
        Assert.Equal(tokenId, auditEvent.TokenId);
        Assert.Equal(sessionDuration, auditEvent.SessionDuration);
        Assert.Equal(2.5, auditEvent.SessionDuration.Value.TotalHours);
    }

    [Fact]
    public void AuthenticationAuditEvent_Token_Refresh_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var oldTokenId = Guid.NewGuid().ToString();
        var newTokenId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        
        var auditEvent = new AuthenticationAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "USER",
            ActorId = 123,
            CompanyId = 1,
            BranchId = 2,
            Action = "TOKEN_REFRESH",
            EntityType = "Authentication",
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp,
            Success = true,
            FailureReason = null,
            TokenId = newTokenId, // New token ID after refresh
            SessionDuration = null // Not applicable for token refresh
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("USER", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Equal(2, auditEvent.BranchId);
        Assert.Equal("TOKEN_REFRESH", auditEvent.Action);
        Assert.Equal("Authentication", auditEvent.EntityType);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.True(auditEvent.Success);
        Assert.Null(auditEvent.FailureReason);
        Assert.Equal(newTokenId, auditEvent.TokenId);
        Assert.Null(auditEvent.SessionDuration);
    }

    [Fact]
    public void AuthenticationAuditEvent_Failed_Token_Refresh()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var expiredTokenId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        
        var auditEvent = new AuthenticationAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "USER",
            ActorId = 123,
            CompanyId = 1,
            BranchId = 2,
            Action = "TOKEN_REFRESH_FAILED",
            EntityType = "Authentication",
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp,
            Success = false,
            FailureReason = "Refresh token expired",
            TokenId = expiredTokenId, // Expired token ID
            SessionDuration = null
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("USER", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Equal(2, auditEvent.BranchId);
        Assert.Equal("TOKEN_REFRESH_FAILED", auditEvent.Action);
        Assert.Equal("Authentication", auditEvent.EntityType);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
        Assert.False(auditEvent.Success);
        Assert.Equal("Refresh token expired", auditEvent.FailureReason);
        Assert.Equal(expiredTokenId, auditEvent.TokenId);
        Assert.Null(auditEvent.SessionDuration);
    }

    [Fact]
    public void AuthenticationAuditEvent_Can_Handle_Null_Optional_Properties()
    {
        // Arrange & Act
        var auditEvent = new AuthenticationAuditEvent
        {
            Success = true,
            FailureReason = null,
            TokenId = null,
            SessionDuration = null
        };

        // Assert
        Assert.True(auditEvent.Success);
        Assert.Null(auditEvent.FailureReason);
        Assert.Null(auditEvent.TokenId);
        Assert.Null(auditEvent.SessionDuration);
    }

    [Fact]
    public void AuthenticationAuditEvent_Can_Handle_Various_Failure_Reasons()
    {
        // Arrange
        var failureReasons = new[]
        {
            "Invalid username or password",
            "Account locked",
            "Account disabled",
            "Password expired",
            "Too many failed attempts",
            "Invalid two-factor authentication code",
            "Refresh token expired",
            "Token revoked",
            "Invalid token signature"
        };

        foreach (var failureReason in failureReasons)
        {
            // Act
            var auditEvent = new AuthenticationAuditEvent
            {
                Success = false,
                FailureReason = failureReason
            };

            // Assert
            Assert.False(auditEvent.Success);
            Assert.Equal(failureReason, auditEvent.FailureReason);
        }
    }

    [Fact]
    public void AuthenticationAuditEvent_Session_Duration_Can_Be_Various_Lengths()
    {
        // Arrange
        var sessionDurations = new[]
        {
            TimeSpan.FromMinutes(5),      // Short session
            TimeSpan.FromHours(1),        // Normal session
            TimeSpan.FromHours(8),        // Work day session
            TimeSpan.FromDays(1),         // Long session
            TimeSpan.FromMilliseconds(100) // Very short session
        };

        foreach (var duration in sessionDurations)
        {
            // Act
            var auditEvent = new AuthenticationAuditEvent
            {
                Action = "LOGOUT",
                Success = true,
                SessionDuration = duration
            };

            // Assert
            Assert.Equal("LOGOUT", auditEvent.Action);
            Assert.True(auditEvent.Success);
            Assert.Equal(duration, auditEvent.SessionDuration);
        }
    }

    [Fact]
    public void AuthenticationAuditEvent_Inherits_All_Base_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        
        var auditEvent = new AuthenticationAuditEvent
        {
            CorrelationId = correlationId,
            ActorType = "COMPANY_ADMIN",
            ActorId = 456,
            CompanyId = 2,
            BranchId = 3,
            Action = "LOGIN",
            EntityType = "Authentication",
            EntityId = null, // Not applicable for authentication events
            IpAddress = "10.0.0.1",
            UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
            Timestamp = timestamp
        };

        // Act & Assert
        Assert.Equal(correlationId, auditEvent.CorrelationId);
        Assert.Equal("COMPANY_ADMIN", auditEvent.ActorType);
        Assert.Equal(456, auditEvent.ActorId);
        Assert.Equal(2, auditEvent.CompanyId);
        Assert.Equal(3, auditEvent.BranchId);
        Assert.Equal("LOGIN", auditEvent.Action);
        Assert.Equal("Authentication", auditEvent.EntityType);
        Assert.Null(auditEvent.EntityId);
        Assert.Equal("10.0.0.1", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)", auditEvent.UserAgent);
        Assert.Equal(timestamp, auditEvent.Timestamp);
    }
}