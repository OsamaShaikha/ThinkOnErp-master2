using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Standalone tests for CorrelationContext that don't depend on any other infrastructure.
/// These tests verify the core functionality of AsyncLocal-based correlation ID storage.
/// </summary>
public class CorrelationContextStandaloneTests
{
    [Fact]
    public void CorrelationContext_UsesAsyncLocal_ForThreadSafeStorage()
    {
        // Arrange
        CorrelationContext.Clear();

        // Act & Assert - Verify it's using AsyncLocal by checking isolation
        var mainThreadId = CorrelationContext.CreateNew();
        
        string? taskThreadId = null;
        var task = Task.Run(() =>
        {
            // This should be null because AsyncLocal doesn't flow to Task.Run
            taskThreadId = CorrelationContext.Current;
        });
        task.Wait();

        // Assert
        Assert.NotNull(mainThreadId);
        Assert.Null(taskThreadId); // AsyncLocal doesn't flow to Task.Run without await
    }

    [Fact]
    public void CorrelationContext_ProvidesStaticAccess()
    {
        // Arrange
        CorrelationContext.Clear();
        var testId = "static-access-test";

        // Act
        CorrelationContext.Current = testId;

        // Assert - Can access from anywhere without dependency injection
        Assert.Equal(testId, CorrelationContext.Current);
    }

    [Fact]
    public async Task CorrelationContext_PropagatesAcrossAwaitBoundaries()
    {
        // Arrange
        CorrelationContext.Clear();
        var testId = "await-propagation-test";
        CorrelationContext.Current = testId;

        // Act
        var beforeAwait = CorrelationContext.Current;
        await Task.Delay(1);
        var afterAwait = CorrelationContext.Current;
        await Task.Delay(1);
        var afterSecondAwait = CorrelationContext.Current;

        // Assert - AsyncLocal propagates through await
        Assert.Equal(testId, beforeAwait);
        Assert.Equal(testId, afterAwait);
        Assert.Equal(testId, afterSecondAwait);
    }

    [Fact]
    public void CorrelationContext_IsAccessibleFromAnywhereInApplication()
    {
        // Arrange
        CorrelationContext.Clear();
        var testId = "global-access-test";

        // Act - Set in one place
        CorrelationContext.Current = testId;

        // Assert - Access from different simulated layers
        Assert.Equal(testId, GetFromMiddleware());
        Assert.Equal(testId, GetFromService());
        Assert.Equal(testId, GetFromRepository());
    }

    // Simulate different application layers accessing CorrelationContext
    private static string? GetFromMiddleware() => CorrelationContext.Current;
    private static string? GetFromService() => CorrelationContext.Current;
    private static string? GetFromRepository() => CorrelationContext.Current;
}
