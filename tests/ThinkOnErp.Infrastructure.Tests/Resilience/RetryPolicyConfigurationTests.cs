using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Resilience;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Resilience;

/// <summary>
/// Tests for RetryPolicy configuration and behavior.
/// Validates Task 16.2: Retry policy for transient database failures.
/// </summary>
public class RetryPolicyConfigurationTests
{
    private readonly Mock<ILogger<RetryPolicy>> _mockLogger;

    public RetryPolicyConfigurationTests()
    {
        _mockLogger = new Mock<ILogger<RetryPolicy>>();
    }

    [Fact]
    public void RetryPolicy_DefaultConstructor_UsesDefaultValues()
    {
        // Arrange & Act
        var retryPolicy = new RetryPolicy(_mockLogger.Object);

        // Assert - verify it was created successfully
        Assert.NotNull(retryPolicy);
    }

    [Fact]
    public void RetryPolicy_CustomConstructor_AcceptsCustomValues()
    {
        // Arrange & Act
        var retryPolicy = new RetryPolicy(
            _mockLogger.Object,
            maxRetries: 5,
            initialDelay: TimeSpan.FromMilliseconds(200),
            maxDelay: TimeSpan.FromMilliseconds(10000),
            useJitter: false);

        // Assert - verify it was created successfully
        Assert.NotNull(retryPolicy);
    }

    [Fact]
    public void RetryPolicy_FromOptions_CreatesInstanceWithConfiguredValues()
    {
        // Arrange
        var options = new AuditLoggingOptions
        {
            EnableRetryPolicy = true,
            MaxRetryAttempts = 5,
            InitialRetryDelayMs = 200,
            MaxRetryDelayMs = 10000,
            UseRetryJitter = false
        };

        // Act
        var retryPolicy = RetryPolicy.FromOptions(_mockLogger.Object, options);

        // Assert
        Assert.NotNull(retryPolicy);
    }

    [Fact]
    public async Task RetryPolicy_ExecuteAsync_SucceedsOnFirstAttempt()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(_mockLogger.Object, maxRetries: 3);
        var executionCount = 0;

        // Act
        var result = await retryPolicy.ExecuteAsync(
            async () =>
            {
                executionCount++;
                await Task.CompletedTask;
                return "success";
            },
            "TestOperation");

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task RetryPolicy_ExecuteAsync_RetriesOnTransientFailure()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(
            _mockLogger.Object, 
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(10), // Fast for testing
            useJitter: false); // No jitter for predictable timing
        
        var executionCount = 0;

        // Act
        var result = await retryPolicy.ExecuteAsync(
            async () =>
            {
                executionCount++;
                await Task.CompletedTask;
                
                // Fail first 2 attempts, succeed on 3rd
                if (executionCount < 3)
                {
                    throw new TimeoutException("Transient timeout");
                }
                
                return "success";
            },
            "TestOperation",
            ex => ex is TimeoutException); // Custom transient check

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(3, executionCount); // Should have retried twice
    }

    [Fact]
    public async Task RetryPolicy_ExecuteAsync_ThrowsAfterMaxRetries()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(
            _mockLogger.Object, 
            maxRetries: 2,
            initialDelay: TimeSpan.FromMilliseconds(10),
            useJitter: false);
        
        var executionCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await retryPolicy.ExecuteAsync(
                async () =>
                {
                    executionCount++;
                    await Task.CompletedTask;
                    throw new TimeoutException("Always fails");
                },
                "TestOperation",
                ex => ex is TimeoutException);
        });

        Assert.Equal(2, executionCount); // Should have attempted maxRetries times
    }

    [Fact]
    public async Task RetryPolicy_ExecuteAsync_DoesNotRetryNonTransientFailure()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(
            _mockLogger.Object, 
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(10));
        
        var executionCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await retryPolicy.ExecuteAsync(
                async () =>
                {
                    executionCount++;
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Non-transient error");
                },
                "TestOperation",
                ex => ex is TimeoutException); // Only TimeoutException is transient
        });

        Assert.Equal(1, executionCount); // Should not have retried
    }

    [Theory]
    [InlineData(1, 10, 5000, true)]  // Min values with jitter
    [InlineData(10, 5000, 30000, false)] // Max values without jitter
    [InlineData(3, 100, 5000, true)]  // Default values
    [InlineData(5, 200, 10000, false)] // Custom values
    public void AuditLoggingOptions_RetryPolicyConfiguration_AcceptsValidValues(
        int maxRetries, 
        int initialDelayMs, 
        int maxDelayMs, 
        bool useJitter)
    {
        // Arrange & Act
        var options = new AuditLoggingOptions
        {
            EnableRetryPolicy = true,
            MaxRetryAttempts = maxRetries,
            InitialRetryDelayMs = initialDelayMs,
            MaxRetryDelayMs = maxDelayMs,
            UseRetryJitter = useJitter
        };

        // Assert
        Assert.True(options.EnableRetryPolicy);
        Assert.Equal(maxRetries, options.MaxRetryAttempts);
        Assert.Equal(initialDelayMs, options.InitialRetryDelayMs);
        Assert.Equal(maxDelayMs, options.MaxRetryDelayMs);
        Assert.Equal(useJitter, options.UseRetryJitter);
    }

    [Fact]
    public void AuditLoggingOptions_RetryPolicyDefaults_AreCorrect()
    {
        // Arrange & Act
        var options = new AuditLoggingOptions();

        // Assert - verify default values from Task 16.2
        Assert.True(options.EnableRetryPolicy);
        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.Equal(100, options.InitialRetryDelayMs);
        Assert.Equal(5000, options.MaxRetryDelayMs);
        Assert.True(options.UseRetryJitter);
    }

    [Fact]
    public async Task RetryPolicy_ExponentialBackoff_IncreasesDelayBetweenRetries()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(
            _mockLogger.Object, 
            maxRetries: 4,
            initialDelay: TimeSpan.FromMilliseconds(100),
            maxDelay: TimeSpan.FromMilliseconds(5000),
            useJitter: false); // No jitter for predictable timing
        
        var executionCount = 0;
        var executionTimes = new List<DateTime>();

        // Act
        try
        {
            await retryPolicy.ExecuteAsync(
                async () =>
                {
                    executionCount++;
                    executionTimes.Add(DateTime.UtcNow);
                    await Task.CompletedTask;
                    throw new TimeoutException("Always fails");
                },
                "TestOperation",
                ex => ex is TimeoutException);
        }
        catch (TimeoutException)
        {
            // Expected
        }

        // Assert
        Assert.Equal(4, executionCount);
        Assert.Equal(4, executionTimes.Count);

        // Verify delays increase exponentially (with some tolerance for timing)
        // Attempt 1 -> 2: ~100ms delay
        // Attempt 2 -> 3: ~200ms delay
        // Attempt 3 -> 4: ~400ms delay
        var delay1 = (executionTimes[1] - executionTimes[0]).TotalMilliseconds;
        var delay2 = (executionTimes[2] - executionTimes[1]).TotalMilliseconds;
        var delay3 = (executionTimes[3] - executionTimes[2]).TotalMilliseconds;

        Assert.True(delay1 >= 80 && delay1 <= 120, $"First delay should be ~100ms, was {delay1}ms");
        Assert.True(delay2 >= 180 && delay2 <= 220, $"Second delay should be ~200ms, was {delay2}ms");
        Assert.True(delay3 >= 380 && delay3 <= 420, $"Third delay should be ~400ms, was {delay3}ms");
    }

    [Fact]
    public async Task RetryPolicy_MaxDelay_CapsExponentialBackoff()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(
            _mockLogger.Object, 
            maxRetries: 10,
            initialDelay: TimeSpan.FromMilliseconds(100),
            maxDelay: TimeSpan.FromMilliseconds(500), // Cap at 500ms
            useJitter: false);
        
        var executionCount = 0;
        var executionTimes = new List<DateTime>();

        // Act
        try
        {
            await retryPolicy.ExecuteAsync(
                async () =>
                {
                    executionCount++;
                    executionTimes.Add(DateTime.UtcNow);
                    await Task.CompletedTask;
                    throw new TimeoutException("Always fails");
                },
                "TestOperation",
                ex => ex is TimeoutException);
        }
        catch (TimeoutException)
        {
            // Expected
        }

        // Assert
        Assert.Equal(10, executionCount);

        // Verify later delays are capped at maxDelay (500ms)
        // After attempt 3, exponential backoff would exceed 500ms, so it should be capped
        for (int i = 3; i < executionTimes.Count - 1; i++)
        {
            var delay = (executionTimes[i + 1] - executionTimes[i]).TotalMilliseconds;
            Assert.True(delay <= 550, $"Delay at attempt {i + 1} should be capped at ~500ms, was {delay}ms");
        }
    }
}
