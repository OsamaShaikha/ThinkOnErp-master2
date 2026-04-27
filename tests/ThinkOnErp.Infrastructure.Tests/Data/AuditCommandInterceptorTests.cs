using Xunit;
using Moq;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Entities.Audit;
using Microsoft.Extensions.Logging;

namespace ThinkOnErp.Infrastructure.Tests.Data;

/// <summary>
/// Unit tests for AuditCommandInterceptor.
/// Validates automatic detection of INSERT, UPDATE, DELETE operations and audit logging.
/// </summary>
public class AuditCommandInterceptorTests
{
    private readonly Mock<IAuditLogger> _mockAuditLogger;
    private readonly Mock<IAuditContextProvider> _mockContextProvider;
    private readonly Mock<ILogger<AuditCommandInterceptor>> _mockLogger;
    private readonly AuditCommandInterceptor _interceptor;

    public AuditCommandInterceptorTests()
    {
        _mockAuditLogger = new Mock<IAuditLogger>();
        _mockContextProvider = new Mock<IAuditContextProvider>();
        _mockLogger = new Mock<ILogger<AuditCommandInterceptor>>();

        // Setup default context provider values
        _mockContextProvider.Setup(x => x.GetCorrelationId()).Returns("test-correlation-id");
        _mockContextProvider.Setup(x => x.GetActorType()).Returns("USER");
        _mockContextProvider.Setup(x => x.GetActorId()).Returns(123L);
        _mockContextProvider.Setup(x => x.GetCompanyId()).Returns(1L);
        _mockContextProvider.Setup(x => x.GetBranchId()).Returns(10L);
        _mockContextProvider.Setup(x => x.GetIpAddress()).Returns("192.168.1.1");
        _mockContextProvider.Setup(x => x.GetUserAgent()).Returns("Test User Agent");

        _interceptor = new AuditCommandInterceptor(
            _mockAuditLogger.Object,
            _mockContextProvider.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task OnCommandExecutedAsync_WithInsertCommand_LogsAuditEvent()
    {
        // Arrange
        var command = new OracleCommand("INSERT INTO SYS_USERS (USER_NAME, EMAIL) VALUES ('test', 'test@example.com')");
        DataChangeAuditEvent? capturedEvent = null;
        
        _mockAuditLogger
            .Setup(x => x.LogDataChangeAsync(It.IsAny<DataChangeAuditEvent>(), It.IsAny<CancellationToken>()))
            .Callback<DataChangeAuditEvent, CancellationToken>((evt, ct) => capturedEvent = evt)
            .Returns(Task.CompletedTask);

        // Act
        await _interceptor.OnCommandExecutedAsync(command, 1);

        // Assert
        _mockAuditLogger.Verify(
            x => x.LogDataChangeAsync(It.IsAny<DataChangeAuditEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(capturedEvent);
        Assert.Equal("INSERT", capturedEvent.Action);
        Assert.Equal("SYS_USERS", capturedEvent.EntityType);
        Assert.Equal("test-correlation-id", capturedEvent.CorrelationId);
        Assert.Equal("USER", capturedEvent.ActorType);
        Assert.Equal(123L, capturedEvent.ActorId);
        Assert.Equal(1L, capturedEvent.CompanyId);
        Assert.Equal(10L, capturedEvent.BranchId);
    }

    [Fact]
    public async Task OnCommandExecutedAsync_WithUpdateCommand_LogsAuditEvent()
    {
        // Arrange
        var command = new OracleCommand("UPDATE SYS_USERS SET EMAIL = 'new@example.com' WHERE ROW_ID = 1");
        DataChangeAuditEvent? capturedEvent = null;
        
        _mockAuditLogger
            .Setup(x => x.LogDataChangeAsync(It.IsAny<DataChangeAuditEvent>(), It.IsAny<CancellationToken>()))
            .Callback<DataChangeAuditEvent, CancellationToken>((evt, ct) => capturedEvent = evt)
            .Returns(Task.CompletedTask);

        // Act
        await _interceptor.OnCommandExecutedAsync(command, 1);

        // Assert
        _mockAuditLogger.Verify(
            x => x.LogDataChangeAsync(It.IsAny<DataChangeAuditEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(capturedEvent);
        Assert.Equal("UPDATE", capturedEvent.Action);
        Assert.Equal("SYS_USERS", capturedEvent.EntityType);
    }

    [Fact]
    public async Task OnCommandExecutedAsync_WithDeleteCommand_LogsAuditEvent()
    {
        // Arrange
        var command = new OracleCommand("DELETE FROM SYS_USERS WHERE ROW_ID = 1");
        DataChangeAuditEvent? capturedEvent = null;
        
        _mockAuditLogger
            .Setup(x => x.LogDataChangeAsync(It.IsAny<DataChangeAuditEvent>(), It.IsAny<CancellationToken>()))
            .Callback<DataChangeAuditEvent, CancellationToken>((evt, ct) => capturedEvent = evt)
            .Returns(Task.CompletedTask);

        // Act
        await _interceptor.OnCommandExecutedAsync(command, 1);

        // Assert
        _mockAuditLogger.Verify(
            x => x.LogDataChangeAsync(It.IsAny<DataChangeAuditEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(capturedEvent);
        Assert.Equal("DELETE", capturedEvent.Action);
        Assert.Equal("SYS_USERS", capturedEvent.EntityType);
    }

    [Fact]
    public async Task OnCommandExecutedAsync_WithSelectCommand_DoesNotLogAuditEvent()
    {
        // Arrange
        var command = new OracleCommand("SELECT * FROM SYS_USERS WHERE ROW_ID = 1");

        // Act
        await _interceptor.OnCommandExecutedAsync(command, 0);

        // Assert
        _mockAuditLogger.Verify(
            x => x.LogDataChangeAsync(It.IsAny<DataChangeAuditEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnCommandExecutedAsync_WithAuditLogTable_DoesNotLogAuditEvent()
    {
        // Arrange - prevent infinite recursion
        var command = new OracleCommand("INSERT INTO SYS_AUDIT_LOG (ACTOR_ID, ACTION) VALUES (1, 'TEST')");

        // Act
        await _interceptor.OnCommandExecutedAsync(command, 1);

        // Assert
        _mockAuditLogger.Verify(
            x => x.LogDataChangeAsync(It.IsAny<DataChangeAuditEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnCommandExecutedAsync_WithAuditArchiveTable_DoesNotLogAuditEvent()
    {
        // Arrange - prevent infinite recursion
        var command = new OracleCommand("INSERT INTO SYS_AUDIT_LOG_ARCHIVE (ACTOR_ID, ACTION) VALUES (1, 'TEST')");

        // Act
        await _interceptor.OnCommandExecutedAsync(command, 1);

        // Assert
        _mockAuditLogger.Verify(
            x => x.LogDataChangeAsync(It.IsAny<DataChangeAuditEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnCommandExecutedAsync_WithStatusTrackingTable_DoesNotLogAuditEvent()
    {
        // Arrange - prevent infinite recursion
        var command = new OracleCommand("INSERT INTO SYS_AUDIT_STATUS_TRACKING (AUDIT_LOG_ID, STATUS) VALUES (1, 'Resolved')");

        // Act
        await _interceptor.OnCommandExecutedAsync(command, 1);

        // Assert
        _mockAuditLogger.Verify(
            x => x.LogDataChangeAsync(It.IsAny<DataChangeAuditEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("  INSERT INTO SYS_COMPANY (NAME) VALUES ('Test')", "INSERT", "SYS_COMPANY")]
    [InlineData("insert into sys_branch (name) values ('Test')", "INSERT", "sys_branch")]
    [InlineData("  UPDATE SYS_ROLE SET NAME = 'Admin'", "UPDATE", "SYS_ROLE")]
    [InlineData("update sys_currency set code = 'USD'", "UPDATE", "sys_currency")]
    [InlineData("  DELETE FROM SYS_COMPANY WHERE ROW_ID = 1", "DELETE", "SYS_COMPANY")]
    [InlineData("delete from sys_branch where row_id = 1", "DELETE", "sys_branch")]
    public async Task OnCommandExecutedAsync_WithVariousFormats_ExtractsCorrectActionAndTable(
        string commandText, string expectedAction, string expectedTable)
    {
        // Arrange
        var command = new OracleCommand(commandText);
        DataChangeAuditEvent? capturedEvent = null;
        
        _mockAuditLogger
            .Setup(x => x.LogDataChangeAsync(It.IsAny<DataChangeAuditEvent>(), It.IsAny<CancellationToken>()))
            .Callback<DataChangeAuditEvent, CancellationToken>((evt, ct) => capturedEvent = evt)
            .Returns(Task.CompletedTask);

        // Act
        await _interceptor.OnCommandExecutedAsync(command, 1);

        // Assert
        Assert.NotNull(capturedEvent);
        Assert.Equal(expectedAction, capturedEvent.Action);
        Assert.Equal(expectedTable, capturedEvent.EntityType);
    }

    [Fact]
    public async Task OnCommandExecutedAsync_WhenAuditLoggerThrows_DoesNotPropagateException()
    {
        // Arrange
        var command = new OracleCommand("INSERT INTO SYS_USERS (USER_NAME) VALUES ('test')");
        
        _mockAuditLogger
            .Setup(x => x.LogDataChangeAsync(It.IsAny<DataChangeAuditEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Audit logging failed"));

        // Act & Assert - should not throw
        await _interceptor.OnCommandExecutedAsync(command, 1);

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}
