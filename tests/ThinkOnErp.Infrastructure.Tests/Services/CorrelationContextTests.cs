using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for CorrelationContext to verify thread-safe correlation ID storage
/// and propagation across async/await boundaries.
/// </summary>
public class CorrelationContextTests
{
    [Fact]
    public void Current_WhenNotSet_ReturnsNull()
    {
        // Arrange
        CorrelationContext.Clear();

        // Act
        var result = CorrelationContext.Current;

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Current_WhenSet_ReturnsSetValue()
    {
        // Arrange
        CorrelationContext.Clear();
        var expectedId = "test-correlation-id-123";

        // Act
        CorrelationContext.Current = expectedId;
        var result = CorrelationContext.Current;

        // Assert
        Assert.Equal(expectedId, result);
    }

    [Fact]
    public void GetOrCreate_WhenNotSet_CreatesNewGuid()
    {
        // Arrange
        CorrelationContext.Clear();

        // Act
        var result = CorrelationContext.GetOrCreate();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.True(Guid.TryParse(result, out _), "Should be a valid GUID");
    }

    [Fact]
    public void GetOrCreate_WhenAlreadySet_ReturnsSameValue()
    {
        // Arrange
        CorrelationContext.Clear();
        var firstId = CorrelationContext.GetOrCreate();

        // Act
        var secondId = CorrelationContext.GetOrCreate();

        // Assert
        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public void CreateNew_AlwaysCreatesNewGuid()
    {
        // Arrange
        CorrelationContext.Clear();
        var firstId = CorrelationContext.CreateNew();

        // Act
        var secondId = CorrelationContext.CreateNew();

        // Assert
        Assert.NotEqual(firstId, secondId);
        Assert.True(Guid.TryParse(firstId, out _));
        Assert.True(Guid.TryParse(secondId, out _));
    }

    [Fact]
    public void Clear_RemovesCurrentValue()
    {
        // Arrange
        CorrelationContext.Current = "test-id";

        // Act
        CorrelationContext.Clear();
        var result = CorrelationContext.Current;

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CorrelationId_PropagatesAcrossAsyncAwait()
    {
        // Arrange
        CorrelationContext.Clear();
        var expectedId = "async-test-id";
        CorrelationContext.Current = expectedId;

        // Act
        var resultBeforeAwait = CorrelationContext.Current;
        await Task.Delay(10);
        var resultAfterAwait = CorrelationContext.Current;

        // Assert
        Assert.Equal(expectedId, resultBeforeAwait);
        Assert.Equal(expectedId, resultAfterAwait);
    }

    [Fact]
    public async Task CorrelationId_PropagatesAcrossMultipleAsyncCalls()
    {
        // Arrange
        CorrelationContext.Clear();
        var expectedId = "multi-async-test-id";
        CorrelationContext.Current = expectedId;

        // Act
        var result1 = await GetCorrelationIdAsync();
        var result2 = await GetCorrelationIdAsync();
        var result3 = CorrelationContext.Current;

        // Assert
        Assert.Equal(expectedId, result1);
        Assert.Equal(expectedId, result2);
        Assert.Equal(expectedId, result3);
    }

    [Fact]
    public async Task CorrelationId_IsIsolatedBetweenParallelTasks()
    {
        // Arrange & Act
        var task1 = Task.Run(async () =>
        {
            CorrelationContext.Current = "task-1-id";
            await Task.Delay(50);
            return CorrelationContext.Current;
        });

        var task2 = Task.Run(async () =>
        {
            CorrelationContext.Current = "task-2-id";
            await Task.Delay(50);
            return CorrelationContext.Current;
        });

        var task3 = Task.Run(async () =>
        {
            CorrelationContext.Current = "task-3-id";
            await Task.Delay(50);
            return CorrelationContext.Current;
        });

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert
        Assert.Equal("task-1-id", results[0]);
        Assert.Equal("task-2-id", results[1]);
        Assert.Equal("task-3-id", results[2]);
    }

    [Fact]
    public async Task CorrelationId_IsIsolatedBetweenConcurrentRequests()
    {
        // Arrange
        var tasks = new List<Task<(string SetId, string RetrievedId)>>();

        // Act - Simulate 10 concurrent requests
        for (int i = 0; i < 10; i++)
        {
            var requestId = $"request-{i}";
            tasks.Add(Task.Run(async () =>
            {
                CorrelationContext.Current = requestId;
                await Task.Delay(Random.Shared.Next(10, 50));
                return (SetId: requestId, RetrievedId: CorrelationContext.Current!);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Each request should maintain its own correlation ID
        foreach (var (setId, retrievedId) in results)
        {
            Assert.Equal(setId, retrievedId);
        }
    }

    [Fact]
    public async Task CorrelationId_PropagatesInNestedAsyncMethods()
    {
        // Arrange
        CorrelationContext.Clear();
        var expectedId = "nested-async-id";
        CorrelationContext.Current = expectedId;

        // Act
        var result = await Level1Async();

        // Assert
        Assert.Equal(expectedId, result);
    }

    [Fact]
    public void CorrelationId_IsThreadSafe()
    {
        // Arrange
        var threads = new List<Thread>();
        var results = new System.Collections.Concurrent.ConcurrentBag<(string SetId, string? RetrievedId)>();

        // Act - Create multiple threads that set and retrieve correlation IDs
        for (int i = 0; i < 20; i++)
        {
            var threadId = $"thread-{i}";
            var thread = new Thread(() =>
            {
                CorrelationContext.Current = threadId;
                Thread.Sleep(Random.Shared.Next(10, 50));
                results.Add((SetId: threadId, RetrievedId: CorrelationContext.Current));
            });
            threads.Add(thread);
            thread.Start();
        }

        // Wait for all threads to complete
        foreach (var thread in threads)
        {
            thread.Join();
        }

        // Assert - Each thread should maintain its own correlation ID
        foreach (var (setId, retrievedId) in results)
        {
            Assert.Equal(setId, retrievedId);
        }
    }

    [Fact]
    public async Task CorrelationId_IsAccessibleFromAnywhereInApplication()
    {
        // Arrange
        CorrelationContext.Clear();
        var expectedId = "global-access-id";
        CorrelationContext.Current = expectedId;

        // Act - Simulate accessing from different layers
        var fromService = await SimulateServiceLayerAsync();
        var fromRepository = SimulateRepositoryLayer();
        var fromMiddleware = SimulateMiddlewareLayer();

        // Assert
        Assert.Equal(expectedId, fromService);
        Assert.Equal(expectedId, fromRepository);
        Assert.Equal(expectedId, fromMiddleware);
    }

    [Fact]
    public void GetOrCreate_GeneratesUniqueIds()
    {
        // Arrange
        var ids = new HashSet<string>();

        // Act - Generate 100 correlation IDs
        for (int i = 0; i < 100; i++)
        {
            CorrelationContext.Clear();
            var id = CorrelationContext.GetOrCreate();
            ids.Add(id);
        }

        // Assert - All IDs should be unique
        Assert.Equal(100, ids.Count);
    }

    // Helper methods to simulate async call chains
    private static async Task<string?> GetCorrelationIdAsync()
    {
        await Task.Delay(10);
        return CorrelationContext.Current;
    }

    private static async Task<string?> Level1Async()
    {
        await Task.Delay(10);
        return await Level2Async();
    }

    private static async Task<string?> Level2Async()
    {
        await Task.Delay(10);
        return await Level3Async();
    }

    private static async Task<string?> Level3Async()
    {
        await Task.Delay(10);
        return CorrelationContext.Current;
    }

    // Helper methods to simulate different application layers
    private static async Task<string?> SimulateServiceLayerAsync()
    {
        await Task.Delay(5);
        return CorrelationContext.Current;
    }

    private static string? SimulateRepositoryLayer()
    {
        return CorrelationContext.Current;
    }

    private static string? SimulateMiddlewareLayer()
    {
        return CorrelationContext.Current;
    }
}
