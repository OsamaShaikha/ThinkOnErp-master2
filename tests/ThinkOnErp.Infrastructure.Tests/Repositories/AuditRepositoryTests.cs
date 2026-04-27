using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories;

/// <summary>
/// Unit tests for AuditRepository.
/// Tests database operations for audit logging.
/// </summary>
public class AuditRepositoryTests
{
    private readonly Mock<ILogger<AuditRepository>> _mockLogger;

    public AuditRepositoryTests()
    {
        _mockLogger = new Mock<ILogger<AuditRepository>>();
    }

    [Fact]
    public void AuditRepository_Should_Initialize_Successfully()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string> {
            {"ConnectionStrings:OracleDb", "Data Source=test;User Id=test;Password=test;"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var dbContext = new OracleDbContext(configuration);

        // Act & Assert - Should not throw exception
        var repository = new AuditRepository(dbContext, _mockLogger.Object);
        Assert.NotNull(repository);
    }

    [Fact]
    public void DataChangeAuditEvent_Should_Have_Required_Properties()
    {
        // Arrange & Act
        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = "test-correlation-id",
            ActorType = "USER",
            ActorId = 123,
            CompanyId = 1,
            BranchId = 2,
            Action = "UPDATE",
            EntityType = "User",
            EntityId = 456,
            OldValue = "{\"name\":\"old\"}",
            NewValue = "{\"name\":\"new\"}",
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0",
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.Equal("test-correlation-id", auditEvent.CorrelationId);
        Assert.Equal("USER", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal(1, auditEvent.CompanyId);
        Assert.Equal(2, auditEvent.BranchId);
        Assert.Equal("UPDATE", auditEvent.Action);
        Assert.Equal("User", auditEvent.EntityType);
        Assert.Equal(456, auditEvent.EntityId);
        Assert.Equal("{\"name\":\"old\"}", auditEvent.OldValue);
        Assert.Equal("{\"name\":\"new\"}", auditEvent.NewValue);
        Assert.Equal("192.168.1.100", auditEvent.IpAddress);
        Assert.Equal("Mozilla/5.0", auditEvent.UserAgent);
    }

    [Fact]
    public void AuthenticationAuditEvent_Should_Have_Required_Properties()
    {
        // Arrange & Act
        var auditEvent = new AuthenticationAuditEvent
        {
            CorrelationId = "test-correlation-id",
            ActorType = "USER",
            ActorId = 123,
            Action = "LOGIN",
            EntityType = "User",
            Success = true,
            TokenId = "token-123",
            SessionDuration = TimeSpan.FromMinutes(30)
        };

        // Assert
        Assert.Equal("test-correlation-id", auditEvent.CorrelationId);
        Assert.Equal("USER", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal("LOGIN", auditEvent.Action);
        Assert.Equal("User", auditEvent.EntityType);
        Assert.True(auditEvent.Success);
        Assert.Equal("token-123", auditEvent.TokenId);
        Assert.Equal(TimeSpan.FromMinutes(30), auditEvent.SessionDuration);
    }

    [Fact]
    public void ExceptionAuditEvent_Should_Have_Required_Properties()
    {
        // Arrange & Act
        var auditEvent = new ExceptionAuditEvent
        {
            CorrelationId = "test-correlation-id",
            ActorType = "USER",
            ActorId = 123,
            Action = "EXCEPTION",
            EntityType = "System",
            ExceptionType = "ValidationException",
            ExceptionMessage = "Test exception message",
            StackTrace = "Stack trace here",
            InnerException = "Inner exception details",
            Severity = "Error"
        };

        // Assert
        Assert.Equal("test-correlation-id", auditEvent.CorrelationId);
        Assert.Equal("USER", auditEvent.ActorType);
        Assert.Equal(123, auditEvent.ActorId);
        Assert.Equal("EXCEPTION", auditEvent.Action);
        Assert.Equal("System", auditEvent.EntityType);
        Assert.Equal("ValidationException", auditEvent.ExceptionType);
        Assert.Equal("Test exception message", auditEvent.ExceptionMessage);
        Assert.Equal("Stack trace here", auditEvent.StackTrace);
        Assert.Equal("Inner exception details", auditEvent.InnerException);
        Assert.Equal("Error", auditEvent.Severity);
    }
}