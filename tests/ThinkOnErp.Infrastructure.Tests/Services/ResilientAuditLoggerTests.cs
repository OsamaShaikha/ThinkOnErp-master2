using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Resilience;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for ResilientAuditLogger.
/// Tests circuit breaker pattern, retry logic, and fallback mechanisms.
/// Implements Task 16.1: ResilientAuditLogger with circuit breaker pattern
/// </summary>
public class ResilientAuditLoggerTests
{
    private readonly Mock<IAuditLogger> _mockInnerLogger;
    private readonly Mock<ILogger<CircuitBreaker>> _mockCircuitBreakerLogger;
    private readonly Mock<ILogger<RetryPolicy>> _mockRetryPolicyLogger;
    private readonly Mock<ILogger<ResilientAuditLogger>> _mockLogger;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly RetryPolicy _retryPolicy;

    public ResilientAuditLoggerTests()
    {
        _mockInnerLogger = new Mock<IAuditLogger>();
        _mockCircuitBreakerLogger = new Mock<ILogger<CircuitBreaker>>();
        _mockRetryPolicyLogger = new Mock<ILogger<RetryPolicy>>();
        _mockLogger = new Mock<ILogger<ResilientAuditLogger>>();
        
        _circuitBreaker = new CircuitBreaker(
            _mockCircuitBreakerLogger.Object,
            failureThreshold: 3,
            openDuration: TimeSpan.FromSeconds(5),
            halfOpenTimeout: TimeSpan.FromSeconds(2));
        
        _retryPolicy = new RetryPolicy(
            _mockRetryPolicyLogger.Object,
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public async Task LogDataChangeAsync_Success_CallsInnerLogger()
    {
        // Arrange
        var options = new ResilientAuditLoggerOptions
        {
            EnableCircuitBreaker = false,
            EnableRetryPolicy = false
        };
        var resilientLogger = new ResilientAuditLogger(
            _mockInnerLogger.Object,
            _circuitBreaker,
            _retryPolicy,
            _mockLogger.Object,
            options);

        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = "test-123",
            ActorType = "USER",
            ActorId = 1,
            Action = "UPDATE",
            EntityType = "SysUser",
            EntityId = 1
        };

        // Act
        await resilientLogger.LogDataChangeAsync(auditEvent);

        // Assert
        _mockInnerLogger.Verify(
            x => x.LogDataChangeAsync(auditEvent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogDataChangeAsync_TransientFailure_RetriesAndSucceeds()
    {
        // Arrange
        var options = new ResilientAuditLoggerOptions
        {
            EnableCircuitBreaker = false,
            EnableRetryPolicy = true
        };
        var resilientLogger = new ResilientAuditLogger(
            _mockInnerLogger.Object,
            _circuitBreaker,
            _retryPolicy,
            _mockLogger.Object,
            options);

        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = "test-123",
            ActorType = "USER",
            ActorId = 1,
            Action = "UPDATE",
            EntityType = "SysUser",
            EntityId = 1
        };

        var callCount = 0;
        _mockInnerLogger
            .Setup(x => x.LogDataChangeAsync(auditEvent, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount < 3)
                {
                    throw new TimeoutException("Database timeout");
                }
                return Task.CompletedTask;
            });

        // Act
        await resilientLogger.LogDataChangeAsync(auditEvent);

        // Assert
        _mockInnerLogger.Verify(
            x => x.LogDataChangeAsync(auditEvent, It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        
        var metrics = resilientLogger.GetMetrics();
        Assert.Equal(1, metrics.TotalRequests);
        Assert.Equal(1, metrics.SuccessfulRequests);
        Assert.Equal(0, metrics.FailedRequests);
    }

    [Fact]
    public async Task LogDataChangeAsync_PermanentFailure_DoesNotRetry()
    {
        // Arrange
        var options = new ResilientAuditLoggerOptions
        {
            EnableCircuitBreaker = false,
            EnableRetryPolicy = true
        };
        var resilientLogger = new ResilientAuditLogger(
            _mockInnerLogger.Object,
            _circuitBreaker,
            _retryPolicy,
            _mockLogger.Object,
            options);

        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = "test-123",
            ActorType = "USER",
            ActorId = 1,
            Action = "UPDATE",
            EntityType = "SysUser",
            EntityId = 1
        };

        _mockInnerLogger
            .Setup(x => x.LogDataChangeAsync(auditEvent, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Permanent error"));

        // Act
        await resilientLogger.LogDataChangeAsync(auditEvent);

        // Assert - should only try once for non-transient errors
        _mockInnerLogger.Verify(
            x => x.LogDataChangeAsync(auditEvent, It.IsAny<CancellationToken>()),
            Times.Once);
        
        var metrics = resilientLogger.GetMetrics();
        Assert.Equal(1, metrics.TotalRequests);
        Assert.Equal(0, metrics.SuccessfulRequests);
        Assert.Equal(1, metrics.FailedRequests);
    }

    [Fact]
    public async Task LogDataChangeAsync_CircuitBreakerOpens_AfterThresholdFailures()
    {
        // Arrange
        var options = new ResilientAuditLoggerOptions
        {
            EnableCircuitBreaker = true,
            EnableRetryPolicy = false,
            FallbackStrategy = FallbackStrategy.Silent
        };
        var resilientLogger = new ResilientAuditLogger(
            _mockInnerLogger.Object,
            _circuitBreaker,
            _retryPolicy,
            _mockLogger.Object,
            options);

        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = "test-123",
            ActorType = "USER",
            ActorId = 1,
            Action = "UPDATE",
            EntityType = "SysUser",
            EntityId = 1
        };

        _mockInnerLogger
            .Setup(x => x.LogDataChangeAsync(auditEvent, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Database timeout"));

        // Act - trigger failures to open circuit breaker (threshold is 3)
        await resilientLogger.LogDataChangeAsync(auditEvent);
        await resilientLogger.LogDataChangeAsync(auditEvent);
        await resilientLogger.LogDataChangeAsync(auditEvent);

        // Assert - circuit breaker should be open
        Assert.Equal(CircuitState.Open, _circuitBreaker.State);
        
        var metrics = resilientLogger.GetMetrics();
        Assert.Equal(3, metrics.TotalRequests);
        Assert.Equal(0, metrics.SuccessfulRequests);
        Assert.Equal(3, metrics.FailedRequests);
    }

    [Fact]
    public async Task LogDataChangeAsync_CircuitBreakerOpen_RejectsRequests()
    {
        // Arrange
        var options = new ResilientAuditLoggerOptions
        {
            EnableCircuitBreaker = true,
            EnableRetryPolicy = false,
            FallbackStrategy = FallbackStrategy.Silent
        };
        var resilientLogger = new ResilientAuditLogger(
            _mockInnerLogger.Object,
            _circuitBreaker,
            _retryPolicy,
            _mockLogger.Object,
            options);

        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = "test-123",
            ActorType = "USER",
            ActorId = 1,
            Action = "UPDATE",
            EntityType = "SysUser",
            EntityId = 1
        };

        _mockInnerLogger
            .Setup(x => x.LogDataChangeAsync(auditEvent, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Database timeout"));

        // Act - open circuit breaker
        await resilientLogger.LogDataChangeAsync(auditEvent);
        await resilientLogger.LogDataChangeAsync(auditEvent);
        await resilientLogger.LogDataChangeAsync(auditEvent);

        // Reset mock to track subsequent calls
        _mockInnerLogger.Invocations.Clear();

        // Try to log when circuit is open
        await resilientLogger.LogDataChangeAsync(auditEvent);

        // Assert - inner logger should not be called when circuit is open
        _mockInnerLogger.Verify(
            x => x.LogDataChangeAsync(It.IsAny<DataChangeAuditEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        
        var metrics = resilientLogger.GetMetrics();
        Assert.Equal(4, metrics.TotalRequests);
        Assert.Equal(1, metrics.CircuitBreakerRejections);
    }

    [Fact]
    public async Task LogDataChangeAsync_CircuitBreakerHalfOpen_AllowsTestRequest()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(
            _mockCircuitBreakerLogger.Object,
            failureThreshold: 3,
            openDuration: TimeSpan.FromMilliseconds(100), // Short duration for test
            halfOpenTimeout: TimeSpan.FromSeconds(2));

        var options = new ResilientAuditLoggerOptions
        {
            EnableCircuitBreaker = true,
            EnableRetryPolicy = false,
            FallbackStrategy = FallbackStrategy.Silent
        };
        var resilientLogger = new ResilientAuditLogger(
            _mockInnerLogger.Object,
            circuitBreaker,
            _retryPolicy,
            _mockLogger.Object,
            options);

        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = "test-123",
            ActorType = "USER",
            ActorId = 1,
            Action = "UPDATE",
            EntityType = "SysUser",
            EntityId = 1
        };

        var callCount = 0;
        _mockInnerLogger
            .Setup(x => x.LogDataChangeAsync(auditEvent, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount <= 3)
                {
                    throw new TimeoutException("Database timeout");
                }
                return Task.CompletedTask;
            });

        // Act - open circuit breaker
        await resilientLogger.LogDataChangeAsync(auditEvent);
        await resilientLogger.LogDataChangeAsync(auditEvent);
        await resilientLogger.LogDataChangeAsync(auditEvent);

        Assert.Equal(CircuitState.Open, circuitBreaker.State);

        // Wait for circuit to transition to half-open
        await Task.Delay(150);

        // Try request in half-open state - should succeed and close circuit
        await resilientLogger.LogDataChangeAsync(auditEvent);

        // Assert - circuit should be closed after successful request
        Assert.Equal(CircuitState.Closed, circuitBreaker.State);
        
        var metrics = resilientLogger.GetMetrics();
        Assert.Equal(4, metrics.TotalRequests);
        Assert.Equal(1, metrics.SuccessfulRequests);
    }

    [Fact]
    public async Task LogAuthenticationAsync_Success_CallsInnerLogger()
    {
        // Arrange
        var options = new ResilientAuditLoggerOptions
        {
            EnableCircuitBreaker = false,
            EnableRetryPolicy = false
        };
        var resilientLogger = new ResilientAuditLogger(
            _mockInnerLogger.Object,
            _circuitBreaker,
            _retryPolicy,
            _mockLogger.Object,
            options);

        var auditEvent = new AuthenticationAuditEvent
        {
            CorrelationId = "test-123",
            ActorType = "USER",
            ActorId = 1,
            Action = "LOGIN",
            EntityType = "Authentication",
            Success = true
        };

        // Act
        await resilientLogger.LogAuthenticationAsync(auditEvent);

        // Assert
        _mockInnerLogger.Verify(
            x => x.LogAuthenticationAsync(auditEvent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogBatchAsync_Success_CallsInnerLogger()
    {
        // Arrange
        var options = new ResilientAuditLoggerOptions
        {
            EnableCircuitBreaker = false,
            EnableRetryPolicy = false
        };
        var resilientLogger = new ResilientAuditLogger(
            _mockInnerLogger.Object,
            _circuitBreaker,
            _retryPolicy,
            _mockLogger.Object,
            options);

        var auditEvents = new List<AuditEvent>
        {
            new DataChangeAuditEvent
            {
                CorrelationId = "test-1",
                ActorType = "USER",
                ActorId = 1,
                Action = "UPDATE",
                EntityType = "SysUser",
                EntityId = 1
            },
            new DataChangeAuditEvent
            {
                CorrelationId = "test-2",
                ActorType = "USER",
                ActorId = 1,
                Action = "UPDATE",
                EntityType = "SysUser",
                EntityId = 2
            }
        };

        // Act
        await resilientLogger.LogBatchAsync(auditEvents);

        // Assert
        _mockInnerLogger.Verify(
            x => x.LogBatchAsync(auditEvents, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IsHealthyAsync_InnerLoggerHealthy_ReturnsTrue()
    {
        // Arrange
        var options = new ResilientAuditLoggerOptions();
        var resilientLogger = new ResilientAuditLogger(
            _mockInnerLogger.Object,
            _circuitBreaker,
            _retryPolicy,
            _mockLogger.Object,
            options);

        _mockInnerLogger
            .Setup(x => x.IsHealthyAsync())
            .ReturnsAsync(true);

        // Act
        var isHealthy = await resilientLogger.IsHealthyAsync();

        // Assert
        Assert.True(isHealthy);
    }

    [Fact]
    public async Task IsHealthyAsync_CircuitBreakerOpen_ReturnsFalse()
    {
        // Arrange
        var options = new ResilientAuditLoggerOptions
        {
            EnableCircuitBreaker = true,
            FallbackStrategy = FallbackStrategy.Silent
        };
        var resilientLogger = new ResilientAuditLogger(
            _mockInnerLogger.Object,
            _circuitBreaker,
            _retryPolicy,
            _mockLogger.Object,
            options);

        _mockInnerLogger
            .Setup(x => x.IsHealthyAsync())
            .ReturnsAsync(true);

        _mockInnerLogger
            .Setup(x => x.LogDataChangeAsync(It.IsAny<DataChangeAuditEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Database timeout"));

        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = "test-123",
            ActorType = "USER",
            ActorId = 1,
            Action = "UPDATE",
            EntityType = "SysUser",
            EntityId = 1
        };

        // Open circuit breaker
        await resilientLogger.LogDataChangeAsync(auditEvent);
        await resilientLogger.LogDataChangeAsync(auditEvent);
        await resilientLogger.LogDataChangeAsync(auditEvent);

        // Act
        var isHealthy = await resilientLogger.IsHealthyAsync();

        // Assert
        Assert.False(isHealthy);
    }

    [Fact]
    public void GetMetrics_ReturnsCorrectMetrics()
    {
        // Arrange
        var options = new ResilientAuditLoggerOptions
        {
            EnableCircuitBreaker = false,
            EnableRetryPolicy = false
        };
        var resilientLogger = new ResilientAuditLogger(
            _mockInnerLogger.Object,
            _circuitBreaker,
            _retryPolicy,
            _mockLogger.Object,
            options);

        // Act
        var metrics = resilientLogger.GetMetrics();

        // Assert
        Assert.Equal(0, metrics.TotalRequests);
        Assert.Equal(0, metrics.SuccessfulRequests);
        Assert.Equal(0, metrics.FailedRequests);
        Assert.Equal(0, metrics.CircuitBreakerRejections);
        Assert.Equal(CircuitState.Closed, metrics.CircuitState);
        Assert.Equal(100.0, metrics.SuccessRate);
        Assert.Equal(0.0, metrics.FailureRate);
    }

    [Fact]
    public void ResetMetrics_ClearsAllMetrics()
    {
        // Arrange
        var options = new ResilientAuditLoggerOptions
        {
            EnableCircuitBreaker = false,
            EnableRetryPolicy = false
        };
        var resilientLogger = new ResilientAuditLogger(
            _mockInnerLogger.Object,
            _circuitBreaker,
            _retryPolicy,
            _mockLogger.Object,
            options);

        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = "test-123",
            ActorType = "USER",
            ActorId = 1,
            Action = "UPDATE",
            EntityType = "SysUser",
            EntityId = 1
        };

        // Log some events to generate metrics
        resilientLogger.LogDataChangeAsync(auditEvent).Wait();

        // Act
        resilientLogger.ResetMetrics();
        var metrics = resilientLogger.GetMetrics();

        // Assert
        Assert.Equal(0, metrics.TotalRequests);
        Assert.Equal(0, metrics.SuccessfulRequests);
        Assert.Equal(0, metrics.FailedRequests);
    }

    [Fact]
    public async Task LogDataChangeAsync_WithRetryAndCircuitBreaker_WorksTogether()
    {
        // Arrange
        var options = new ResilientAuditLoggerOptions
        {
            EnableCircuitBreaker = true,
            EnableRetryPolicy = true,
            FallbackStrategy = FallbackStrategy.Silent
        };
        var resilientLogger = new ResilientAuditLogger(
            _mockInnerLogger.Object,
            _circuitBreaker,
            _retryPolicy,
            _mockLogger.Object,
            options);

        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = "test-123",
            ActorType = "USER",
            ActorId = 1,
            Action = "UPDATE",
            EntityType = "SysUser",
            EntityId = 1
        };

        var callCount = 0;
        _mockInnerLogger
            .Setup(x => x.LogDataChangeAsync(auditEvent, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount < 2)
                {
                    throw new TimeoutException("Database timeout");
                }
                return Task.CompletedTask;
            });

        // Act
        await resilientLogger.LogDataChangeAsync(auditEvent);

        // Assert - should retry and succeed
        _mockInnerLogger.Verify(
            x => x.LogDataChangeAsync(auditEvent, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        
        var metrics = resilientLogger.GetMetrics();
        Assert.Equal(1, metrics.TotalRequests);
        Assert.Equal(1, metrics.SuccessfulRequests);
        Assert.Equal(0, metrics.FailedRequests);
        Assert.Equal(CircuitState.Closed, metrics.CircuitState);
    }
}
