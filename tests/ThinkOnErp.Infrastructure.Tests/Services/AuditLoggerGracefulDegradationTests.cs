using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Resilience;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Tests for graceful degradation behavior of the audit logging system.
/// Verifies that audit logging failures do not break the application.
/// </summary>
public class AuditLoggerGracefulDegradationTests
{
    private readonly Mock<IAuditRepository> _mockRepository;
    private readonly Mock<ISensitiveDataMasker> _mockDataMasker;
    private readonly Mock<ILegacyAuditService> _mockLegacyService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AuditLogger> _logger;
    private readonly AuditLoggingOptions _options;
    private readonly CircuitBreakerRegistry _circuitBreakerRegistry;

    public AuditLoggerGracefulDegradationTests()
    {
        _mockRepository = new Mock<IAuditRepository>();
        _mockDataMasker = new Mock<ISensitiveDataMasker>();
        _mockLegacyService = new Mock<ILegacyAuditService>();

        // Setup service scope factory
        var services = new ServiceCollection();
        services.AddSingleton(_mockRepository.Object);
        services.AddSingleton(_mockDataMasker.Object);
        services.AddSingleton(_mockLegacyService.Object);
        var serviceProvider = services.BuildServiceProvider();

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(s => s.ServiceProvider).Returns(serviceProvider);
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
        _serviceScopeFactory = mockScopeFactory.Object;

        _logger = new LoggerFactory().CreateLogger<AuditLogger>();
        _options = new AuditLoggingOptions
        {
            Enabled = true,
            BatchSize = 10,
            BatchWindowMs = 100,
            MaxQueueSize = 100,
            EnableCircuitBreaker = false // Disable for basic tests
        };
        _circuitBreakerRegistry = new CircuitBreakerRegistry(
            new LoggerFactory().CreateLogger<CircuitBreakerRegistry>());

        // Setup default mock behaviors
        _mockDataMasker.Setup(m => m.MaskSensitiveFields(It.IsAny<string>()))
            .Returns<string>(s => s);
        _mockDataMasker.Setup(m => m.TruncateIfNeeded(It.IsAny<string>()))
            .Returns<string>(s => s);
        _mockLegacyService.Setup(s => s.DetermineBusinessModuleAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("TestModule");
        _mockLegacyService.Setup(s => s.ExtractDeviceIdentifierAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("TestDevice");
        _mockLegacyService.Setup(s => s.GenerateErrorCodeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("TEST_001");
        _mockLegacyService.Setup(s => s.GenerateBusinessDescriptionAsync(It.IsAny<AuditLogEntry>()))
            .ReturnsAsync("Test description");
    }

    [Fact]
    public async Task LogDataChangeAsync_WhenRepositoryThrowsException_DoesNotThrowException()
    {
        // Arrange
        _mockRepository.Setup(r => r.InsertBatchAsync(It.IsAny<List<Domain.Entities.SysAuditLog>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var auditLogger = new AuditLogger(
            _serviceScopeFactory,
            _logger,
            Options.Create(_options),
            _circuitBreakerRegistry);

        await auditLogger.StartAsync(CancellationToken.None);

        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = "test-correlation-id",
            ActorType = "USER",
            ActorId = 1,
            CompanyId = 1,
            Action = "INSERT",
            EntityType = "User",
            EntityId = 123,
            NewValue = "{\"name\":\"Test User\"}",
            Timestamp = DateTime.UtcNow
        };

        // Act - should not throw exception
        await auditLogger.LogDataChangeAsync(auditEvent);

        // Wait for background processing
        await Task.Delay(500);

        // Assert - no exception thrown, test passes
        Assert.True(true);

        await auditLogger.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task LogAuthenticationAsync_WhenRepositoryThrowsException_DoesNotThrowException()
    {
        // Arrange
        _mockRepository.Setup(r => r.InsertBatchAsync(It.IsAny<List<Domain.Entities.SysAuditLog>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var auditLogger = new AuditLogger(
            _serviceScopeFactory,
            _logger,
            Options.Create(_options),
            _circuitBreakerRegistry);

        await auditLogger.StartAsync(CancellationToken.None);

        var auditEvent = new AuthenticationAuditEvent
        {
            CorrelationId = "test-correlation-id",
            ActorType = "USER",
            ActorId = 1,
            CompanyId = 1,
            Action = "LOGIN",
            EntityType = "Authentication",
            Success = true,
            Timestamp = DateTime.UtcNow
        };

        // Act - should not throw exception
        await auditLogger.LogAuthenticationAsync(auditEvent);

        // Wait for background processing
        await Task.Delay(500);

        // Assert - no exception thrown, test passes
        Assert.True(true);

        await auditLogger.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task LogExceptionAsync_WhenRepositoryThrowsException_DoesNotThrowException()
    {
        // Arrange
        _mockRepository.Setup(r => r.InsertBatchAsync(It.IsAny<List<Domain.Entities.SysAuditLog>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var auditLogger = new AuditLogger(
            _serviceScopeFactory,
            _logger,
            Options.Create(_options),
            _circuitBreakerRegistry);

        await auditLogger.StartAsync(CancellationToken.None);

        var auditEvent = new ExceptionAuditEvent
        {
            CorrelationId = "test-correlation-id",
            ActorType = "USER",
            ActorId = 1,
            CompanyId = 1,
            Action = "EXCEPTION",
            EntityType = "System",
            ExceptionType = "NullReferenceException",
            ExceptionMessage = "Object reference not set",
            StackTrace = "at System.Test()",
            Severity = "Error",
            Timestamp = DateTime.UtcNow
        };

        // Act - should not throw exception
        await auditLogger.LogExceptionAsync(auditEvent);

        // Wait for background processing
        await Task.Delay(500);

        // Assert - no exception thrown, test passes
        Assert.True(true);

        await auditLogger.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task IsHealthyAsync_WhenRepositoryIsUnhealthy_ReturnsFalse()
    {
        // Arrange
        _mockRepository.Setup(r => r.IsHealthyAsync())
            .ReturnsAsync(false);

        var auditLogger = new AuditLogger(
            _serviceScopeFactory,
            _logger,
            Options.Create(_options),
            _circuitBreakerRegistry);

        await auditLogger.StartAsync(CancellationToken.None);

        // Act
        var isHealthy = await auditLogger.IsHealthyAsync();

        // Assert
        Assert.False(isHealthy);

        await auditLogger.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task IsHealthyAsync_WhenQueueIsFull_ReturnsFalse()
    {
        // Arrange
        var smallQueueOptions = new AuditLoggingOptions
        {
            Enabled = true,
            BatchSize = 10,
            BatchWindowMs = 100,
            MaxQueueSize = 10, // Small queue
            EnableCircuitBreaker = false
        };

        // Make repository slow to fill up queue
        _mockRepository.Setup(r => r.InsertBatchAsync(It.IsAny<List<Domain.Entities.SysAuditLog>>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(1000); // Slow processing
                return 1;
            });

        var auditLogger = new AuditLogger(
            _serviceScopeFactory,
            _logger,
            Options.Create(smallQueueOptions),
            _circuitBreakerRegistry);

        await auditLogger.StartAsync(CancellationToken.None);

        // Fill up the queue
        for (int i = 0; i < 15; i++)
        {
            var auditEvent = new DataChangeAuditEvent
            {
                CorrelationId = $"test-{i}",
                ActorType = "USER",
                ActorId = 1,
                Action = "INSERT",
                EntityType = "Test",
                Timestamp = DateTime.UtcNow
            };

            // Use fire-and-forget to avoid blocking
            _ = auditLogger.LogDataChangeAsync(auditEvent);
        }

        // Wait for queue to fill
        await Task.Delay(200);

        // Act
        var isHealthy = await auditLogger.IsHealthyAsync();

        // Assert
        Assert.False(isHealthy);

        await auditLogger.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GetQueueDepth_ReturnsCurrentQueueSize()
    {
        // Arrange
        // Make repository slow to accumulate events in queue
        _mockRepository.Setup(r => r.InsertBatchAsync(It.IsAny<List<Domain.Entities.SysAuditLog>>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(500); // Slow processing
                return 1;
            });

        var auditLogger = new AuditLogger(
            _serviceScopeFactory,
            _logger,
            Options.Create(_options),
            _circuitBreakerRegistry);

        await auditLogger.StartAsync(CancellationToken.None);

        // Add events to queue
        for (int i = 0; i < 5; i++)
        {
            var auditEvent = new DataChangeAuditEvent
            {
                CorrelationId = $"test-{i}",
                ActorType = "USER",
                ActorId = 1,
                Action = "INSERT",
                EntityType = "Test",
                Timestamp = DateTime.UtcNow
            };

            await auditLogger.LogDataChangeAsync(auditEvent);
        }

        // Wait a bit for events to be queued
        await Task.Delay(100);

        // Act
        var queueDepth = auditLogger.GetQueueDepth();

        // Assert
        Assert.True(queueDepth > 0, "Queue should have pending events");

        await auditLogger.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task LogDataChangeAsync_WhenAuditingDisabled_DoesNotQueueEvents()
    {
        // Arrange
        var disabledOptions = new AuditLoggingOptions
        {
            Enabled = false, // Disabled
            BatchSize = 10,
            BatchWindowMs = 100,
            MaxQueueSize = 100
        };

        var auditLogger = new AuditLogger(
            _serviceScopeFactory,
            _logger,
            Options.Create(disabledOptions),
            _circuitBreakerRegistry);

        await auditLogger.StartAsync(CancellationToken.None);

        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = "test-correlation-id",
            ActorType = "USER",
            ActorId = 1,
            Action = "INSERT",
            EntityType = "Test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        await auditLogger.LogDataChangeAsync(auditEvent);

        // Wait for potential processing
        await Task.Delay(200);

        // Assert
        var queueDepth = auditLogger.GetQueueDepth();
        Assert.Equal(0, queueDepth);

        // Verify repository was never called
        _mockRepository.Verify(
            r => r.InsertBatchAsync(It.IsAny<List<Domain.Entities.SysAuditLog>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await auditLogger.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_FlushesRemainingEvents()
    {
        // Arrange
        var insertedEvents = new List<Domain.Entities.SysAuditLog>();
        _mockRepository.Setup(r => r.InsertBatchAsync(It.IsAny<List<Domain.Entities.SysAuditLog>>(), It.IsAny<CancellationToken>()))
            .Callback<List<Domain.Entities.SysAuditLog>, CancellationToken>((events, ct) =>
            {
                insertedEvents.AddRange(events);
            })
            .ReturnsAsync((List<Domain.Entities.SysAuditLog> events, CancellationToken ct) => events.Count);

        var auditLogger = new AuditLogger(
            _serviceScopeFactory,
            _logger,
            Options.Create(_options),
            _circuitBreakerRegistry);

        await auditLogger.StartAsync(CancellationToken.None);

        // Add events
        for (int i = 0; i < 5; i++)
        {
            var auditEvent = new DataChangeAuditEvent
            {
                CorrelationId = $"test-{i}",
                ActorType = "USER",
                ActorId = 1,
                Action = "INSERT",
                EntityType = "Test",
                Timestamp = DateTime.UtcNow
            };

            await auditLogger.LogDataChangeAsync(auditEvent);
        }

        // Act - stop should flush remaining events
        await auditLogger.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(5, insertedEvents.Count);
    }

    [Fact]
    public async Task LogBatchAsync_WhenRepositoryThrowsException_DoesNotThrowException()
    {
        // Arrange
        _mockRepository.Setup(r => r.InsertBatchAsync(It.IsAny<List<Domain.Entities.SysAuditLog>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var auditLogger = new AuditLogger(
            _serviceScopeFactory,
            _logger,
            Options.Create(_options),
            _circuitBreakerRegistry);

        await auditLogger.StartAsync(CancellationToken.None);

        var auditEvents = new List<AuditEvent>
        {
            new DataChangeAuditEvent
            {
                CorrelationId = "test-1",
                ActorType = "USER",
                ActorId = 1,
                Action = "INSERT",
                EntityType = "Test",
                Timestamp = DateTime.UtcNow
            },
            new DataChangeAuditEvent
            {
                CorrelationId = "test-2",
                ActorType = "USER",
                ActorId = 1,
                Action = "UPDATE",
                EntityType = "Test",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act - should not throw exception
        await auditLogger.LogBatchAsync(auditEvents);

        // Wait for background processing
        await Task.Delay(500);

        // Assert - no exception thrown, test passes
        Assert.True(true);

        await auditLogger.StopAsync(CancellationToken.None);
    }
}
