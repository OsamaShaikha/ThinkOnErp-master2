using System.Collections.Concurrent;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Comprehensive thread safety tests for CorrelationContext.
/// Tests verify that AsyncLocal provides proper isolation between async contexts,
/// propagation through async/await chains, and thread safety under high concurrency.
/// 
/// **Validates: Requirements 4 (Request Tracing with Correlation IDs)**
/// </summary>
public class CorrelationContextThreadSafetyTests
{
    #region Async Context Isolation Tests

    [Fact]
    public async Task CorrelationIds_AreIsolated_BetweenDifferentAsyncContexts()
    {
        // Arrange
        var context1Id = "context-1";
        var context2Id = "context-2";
        var context3Id = "context-3";

        // Act - Create three independent async contexts
        var task1 = Task.Run(async () =>
        {
            CorrelationContext.Current = context1Id;
            await Task.Delay(50);
            return CorrelationContext.Current;
        });

        var task2 = Task.Run(async () =>
        {
            CorrelationContext.Current = context2Id;
            await Task.Delay(50);
            return CorrelationContext.Current;
        });

        var task3 = Task.Run(async () =>
        {
            CorrelationContext.Current = context3Id;
            await Task.Delay(50);
            return CorrelationContext.Current;
        });

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert - Each context maintains its own correlation ID
        Assert.Equal(context1Id, results[0]);
        Assert.Equal(context2Id, results[1]);
        Assert.Equal(context3Id, results[2]);
    }

    [Fact]
    public async Task CorrelationIds_DoNotLeak_BetweenSimultaneousRequests()
    {
        // Arrange - Simulate 50 concurrent HTTP requests
        var requestCount = 50;
        var tasks = new List<Task<(string Expected, string? Actual)>>();

        // Act - Each "request" sets its own correlation ID
        for (int i = 0; i < requestCount; i++)
        {
            var requestId = $"request-{i:D3}";
            tasks.Add(Task.Run(async () =>
            {
                CorrelationContext.Current = requestId;
                
                // Simulate request processing with multiple async operations
                await Task.Delay(Random.Shared.Next(10, 100));
                var afterFirstDelay = CorrelationContext.Current;
                
                await Task.Delay(Random.Shared.Next(10, 100));
                var afterSecondDelay = CorrelationContext.Current;
                
                // Verify consistency throughout the request
                Assert.Equal(requestId, afterFirstDelay);
                Assert.Equal(requestId, afterSecondDelay);
                
                return (Expected: requestId, Actual: CorrelationContext.Current)!;
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - No correlation ID leakage between requests
        foreach (var (expected, actual) in results)
        {
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public async Task CorrelationIds_AreIsolated_InNestedTaskRun()
    {
        // Arrange
        CorrelationContext.Clear();
        var parentId = "parent-context";
        CorrelationContext.Current = parentId;

        // Act - Task.Run creates a new execution context
        var childId = await Task.Run(() =>
        {
            // Child context should not inherit parent's correlation ID
            var childCorrelationId = CorrelationContext.Current;
            
            // Set a new ID in child context
            CorrelationContext.Current = "child-context";
            
            return childCorrelationId;
        });

        var parentIdAfter = CorrelationContext.Current;

        // Assert
        Assert.Null(childId); // Task.Run doesn't flow AsyncLocal
        Assert.Equal(parentId, parentIdAfter); // Parent context unchanged
    }

    #endregion

    #region Async/Await Propagation Tests

    [Fact]
    public async Task CorrelationId_PropagatesCorrectly_ThroughAsyncAwaitChain()
    {
        // Arrange
        CorrelationContext.Clear();
        var expectedId = "chain-test-id";
        CorrelationContext.Current = expectedId;

        // Act - Call through a chain of async methods
        var result = await Level1Async();

        // Assert
        Assert.Equal(expectedId, result);
    }

    [Fact]
    public async Task CorrelationId_PropagatesCorrectly_ThroughDeepAsyncChain()
    {
        // Arrange
        CorrelationContext.Clear();
        var expectedId = "deep-chain-id";
        CorrelationContext.Current = expectedId;

        // Act - Call through a 10-level deep async chain
        var result = await DeepAsyncChain(10);

        // Assert
        Assert.Equal(expectedId, result);
    }

    [Fact]
    public async Task CorrelationId_PropagatesCorrectly_ThroughConfigureAwait()
    {
        // Arrange
        CorrelationContext.Clear();
        var expectedId = "configure-await-id";
        CorrelationContext.Current = expectedId;

        // Act - Test with ConfigureAwait(true) to maintain context
        var beforeConfigureAwait = CorrelationContext.Current;
        await Task.Delay(10).ConfigureAwait(true);
        var afterConfigureAwait = CorrelationContext.Current;

        // Assert - AsyncLocal propagates with ConfigureAwait(true)
        Assert.Equal(expectedId, beforeConfigureAwait);
        Assert.Equal(expectedId, afterConfigureAwait);
    }

    [Fact]
    public async Task CorrelationId_PropagatesCorrectly_ThroughMultipleAwaitPoints()
    {
        // Arrange
        CorrelationContext.Clear();
        var expectedId = "multiple-await-id";
        CorrelationContext.Current = expectedId;
        var checkpoints = new List<string?>();

        // Act - Multiple await points in sequence
        checkpoints.Add(CorrelationContext.Current);
        await Task.Delay(10);
        
        checkpoints.Add(CorrelationContext.Current);
        await Task.Delay(10);
        
        checkpoints.Add(CorrelationContext.Current);
        await Task.Delay(10);
        
        checkpoints.Add(CorrelationContext.Current);
        await Task.Delay(10);
        
        checkpoints.Add(CorrelationContext.Current);

        // Assert - Correlation ID maintained at all checkpoints
        Assert.All(checkpoints, id => Assert.Equal(expectedId, id));
    }

    [Fact]
    public async Task CorrelationId_PropagatesCorrectly_ThroughTaskWhenAll()
    {
        // Arrange
        CorrelationContext.Clear();
        var expectedId = "when-all-id";
        CorrelationContext.Current = expectedId;

        // Act - Multiple parallel operations that all inherit the same context
        var tasks = new[]
        {
            GetCorrelationIdAfterDelayAsync(10),
            GetCorrelationIdAfterDelayAsync(20),
            GetCorrelationIdAfterDelayAsync(30),
            GetCorrelationIdAfterDelayAsync(40),
            GetCorrelationIdAfterDelayAsync(50)
        };

        var results = await Task.WhenAll(tasks);

        // Assert - All tasks see the same correlation ID
        Assert.All(results, id => Assert.Equal(expectedId, id));
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void CorrelationId_IsThreadSafe_WithMultipleThreads()
    {
        // Arrange
        var threadCount = 50;
        var threads = new List<Thread>();
        var results = new ConcurrentBag<(string Expected, string? Actual)>();

        // Act - Create multiple threads, each with its own correlation ID
        for (int i = 0; i < threadCount; i++)
        {
            var threadId = $"thread-{i:D3}";
            var thread = new Thread(() =>
            {
                CorrelationContext.Current = threadId;
                Thread.Sleep(Random.Shared.Next(10, 100));
                results.Add((Expected: threadId, Actual: CorrelationContext.Current));
            });
            threads.Add(thread);
            thread.Start();
        }

        // Wait for all threads to complete
        foreach (var thread in threads)
        {
            thread.Join();
        }

        // Assert - Each thread maintained its own correlation ID
        Assert.Equal(threadCount, results.Count);
        foreach (var (expected, actual) in results)
        {
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public async Task CorrelationId_IsThreadSafe_UnderHighConcurrency()
    {
        // Arrange - Simulate very high concurrency (1000 concurrent operations)
        var operationCount = 1000;
        var tasks = new List<Task<bool>>();

        // Act
        for (int i = 0; i < operationCount; i++)
        {
            var operationId = $"operation-{i:D4}";
            tasks.Add(Task.Run(async () =>
            {
                CorrelationContext.Current = operationId;
                
                // Simulate work with random delays
                await Task.Delay(Random.Shared.Next(1, 10));
                var check1 = CorrelationContext.Current == operationId;
                
                await Task.Delay(Random.Shared.Next(1, 10));
                var check2 = CorrelationContext.Current == operationId;
                
                await Task.Delay(Random.Shared.Next(1, 10));
                var check3 = CorrelationContext.Current == operationId;
                
                return check1 && check2 && check3;
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All operations maintained their correlation IDs
        Assert.All(results, success => Assert.True(success));
    }

    [Fact]
    public void CorrelationId_IsThreadSafe_WithThreadPoolThreads()
    {
        // Arrange
        var operationCount = 100;
        var countdown = new CountdownEvent(operationCount);
        var results = new ConcurrentBag<(string Expected, string? Actual)>();

        // Act - Queue work items to ThreadPool
        for (int i = 0; i < operationCount; i++)
        {
            var workItemId = $"work-item-{i:D3}";
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    CorrelationContext.Current = workItemId;
                    Thread.Sleep(Random.Shared.Next(10, 50));
                    results.Add((Expected: workItemId, Actual: CorrelationContext.Current));
                }
                finally
                {
                    countdown.Signal();
                }
            });
        }

        // Wait for all work items to complete
        countdown.Wait(TimeSpan.FromSeconds(30));

        // Assert - Each work item maintained its own correlation ID
        Assert.Equal(operationCount, results.Count);
        foreach (var (expected, actual) in results)
        {
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public async Task CorrelationId_IsThreadSafe_WithMixedAsyncAndThreads()
    {
        // Arrange - Mix of async tasks and threads
        var asyncTasks = new List<Task<(string Expected, string? Actual)>>();
        var threads = new List<Thread>();
        var threadResults = new ConcurrentBag<(string Expected, string? Actual)>();

        // Act - Create async tasks
        for (int i = 0; i < 25; i++)
        {
            var taskId = $"async-{i:D2}";
            asyncTasks.Add(Task.Run(async () =>
            {
                CorrelationContext.Current = taskId;
                await Task.Delay(Random.Shared.Next(10, 50));
                return (Expected: taskId, Actual: CorrelationContext.Current)!;
            }));
        }

        // Create threads
        for (int i = 0; i < 25; i++)
        {
            var threadId = $"thread-{i:D2}";
            var thread = new Thread(() =>
            {
                CorrelationContext.Current = threadId;
                Thread.Sleep(Random.Shared.Next(10, 50));
                threadResults.Add((Expected: threadId, Actual: CorrelationContext.Current));
            });
            threads.Add(thread);
            thread.Start();
        }

        // Wait for completion
        var asyncResults = await Task.WhenAll(asyncTasks);
        foreach (var thread in threads)
        {
            thread.Join();
        }

        // Assert - Both async and thread contexts maintained isolation
        foreach (var (expected, actual) in asyncResults)
        {
            Assert.Equal(expected, actual);
        }
        foreach (var (expected, actual) in threadResults)
        {
            Assert.Equal(expected, actual);
        }
    }

    #endregion

    #region Task.Run and ThreadPool Behavior Tests

    [Fact]
    public async Task CorrelationId_DoesNotFlow_ToTaskRun()
    {
        // Arrange
        CorrelationContext.Clear();
        var parentId = "parent-id";
        CorrelationContext.Current = parentId;

        // Act - Task.Run creates a new execution context
        var childId = await Task.Run(() => CorrelationContext.Current);

        // Assert - AsyncLocal does not flow to Task.Run
        Assert.Null(childId);
        Assert.Equal(parentId, CorrelationContext.Current); // Parent unchanged
    }

    [Fact]
    public async Task CorrelationId_CanBeSet_InsideTaskRun()
    {
        // Arrange
        var taskRunId = "task-run-id";

        // Act - Set correlation ID inside Task.Run
        var result = await Task.Run(() =>
        {
            CorrelationContext.Current = taskRunId;
            return CorrelationContext.Current;
        });

        // Assert - Can set and retrieve within Task.Run context
        Assert.Equal(taskRunId, result);
    }

    [Fact]
    public async Task CorrelationId_IsIsolated_BetweenMultipleTaskRuns()
    {
        // Arrange & Act - Multiple Task.Run operations with different IDs
        var task1 = Task.Run(() =>
        {
            CorrelationContext.Current = "task-run-1";
            Thread.Sleep(50);
            return CorrelationContext.Current;
        });

        var task2 = Task.Run(() =>
        {
            CorrelationContext.Current = "task-run-2";
            Thread.Sleep(50);
            return CorrelationContext.Current;
        });

        var task3 = Task.Run(() =>
        {
            CorrelationContext.Current = "task-run-3";
            Thread.Sleep(50);
            return CorrelationContext.Current;
        });

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert - Each Task.Run maintained its own correlation ID
        Assert.Equal("task-run-1", results[0]);
        Assert.Equal("task-run-2", results[1]);
        Assert.Equal("task-run-3", results[2]);
    }

    [Fact]
    public void CorrelationId_DoesNotFlow_ToThreadPoolWorkItem()
    {
        // Arrange
        CorrelationContext.Clear();
        var parentId = "parent-id";
        CorrelationContext.Current = parentId;
        string? childId = "not-set";
        var resetEvent = new ManualResetEventSlim(false);

        // Act - Queue work to ThreadPool
        ThreadPool.QueueUserWorkItem(_ =>
        {
            childId = CorrelationContext.Current;
            resetEvent.Set();
        });

        resetEvent.Wait(TimeSpan.FromSeconds(5));

        // Assert - AsyncLocal does not flow to ThreadPool work items
        Assert.Null(childId);
        Assert.Equal(parentId, CorrelationContext.Current); // Parent unchanged
    }

    [Fact]
    public void CorrelationId_CanBeSet_InsideThreadPoolWorkItem()
    {
        // Arrange
        var workItemId = "work-item-id";
        string? result = null;
        var resetEvent = new ManualResetEventSlim(false);

        // Act - Set correlation ID inside ThreadPool work item
        ThreadPool.QueueUserWorkItem(_ =>
        {
            CorrelationContext.Current = workItemId;
            result = CorrelationContext.Current;
            resetEvent.Set();
        });

        resetEvent.Wait(TimeSpan.FromSeconds(5));

        // Assert - Can set and retrieve within ThreadPool work item
        Assert.Equal(workItemId, result);
    }

    #endregion

    #region Request Isolation Tests

    [Fact]
    public async Task CorrelationIds_DoNotLeak_BetweenSequentialRequests()
    {
        // Arrange & Act - Simulate sequential HTTP requests
        var request1Result = await SimulateHttpRequestAsync("request-1");
        var request2Result = await SimulateHttpRequestAsync("request-2");
        var request3Result = await SimulateHttpRequestAsync("request-3");

        // Assert - Each request maintained its own correlation ID
        Assert.Equal("request-1", request1Result);
        Assert.Equal("request-2", request2Result);
        Assert.Equal("request-3", request3Result);
    }

    [Fact]
    public async Task CorrelationIds_DoNotLeak_BetweenConcurrentRequests()
    {
        // Arrange - Simulate 100 concurrent HTTP requests
        var requestCount = 100;
        var tasks = new List<Task<string?>>();

        // Act
        for (int i = 0; i < requestCount; i++)
        {
            var requestId = $"concurrent-request-{i:D3}";
            tasks.Add(SimulateHttpRequestAsync(requestId));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Each request maintained its own correlation ID
        for (int i = 0; i < requestCount; i++)
        {
            var expectedId = $"concurrent-request-{i:D3}";
            Assert.Equal(expectedId, results[i]);
        }
    }

    [Fact]
    public async Task CorrelationId_IsCleared_BetweenRequestContexts()
    {
        // Arrange
        CorrelationContext.Clear();

        // Act - Simulate request 1
        await Task.Run(async () =>
        {
            CorrelationContext.Current = "request-1";
            await Task.Delay(10);
            Assert.Equal("request-1", CorrelationContext.Current);
        });

        // Simulate request 2 in a new context (Task.Run creates new context)
        var request2Id = await Task.Run(async () =>
        {
            // Should be null because Task.Run doesn't flow AsyncLocal
            var idAtStart = CorrelationContext.Current;
            
            CorrelationContext.Current = "request-2";
            await Task.Delay(10);
            
            return idAtStart;
        });

        // Assert - Request 2 started with null (no leakage from request 1)
        Assert.Null(request2Id);
    }

    #endregion

    #region Stress Tests

    [Fact]
    public async Task CorrelationId_RemainsStable_UnderSustainedLoad()
    {
        // Arrange - Simulate sustained load for 5 seconds
        var duration = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        var operationCount = 0;
        var failures = 0;

        // Act - Continuously create and verify correlation contexts
        while (DateTime.UtcNow - startTime < duration)
        {
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                var operationId = $"sustained-{operationCount++}";
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        CorrelationContext.Current = operationId;
                        await Task.Delay(Random.Shared.Next(1, 10));
                        
                        if (CorrelationContext.Current != operationId)
                        {
                            Interlocked.Increment(ref failures);
                        }
                    }
                    catch
                    {
                        Interlocked.Increment(ref failures);
                    }
                }));
            }
            await Task.WhenAll(tasks);
        }

        // Assert - No failures during sustained load
        Assert.Equal(0, failures);
        Assert.True(operationCount > 100, $"Should have processed many operations, got {operationCount}");
    }

    [Fact]
    public async Task CorrelationId_HandlesRapidContextSwitching()
    {
        // Arrange - Rapidly switch between contexts
        var switchCount = 1000;
        var results = new ConcurrentBag<bool>();

        // Act
        var tasks = Enumerable.Range(0, switchCount).Select(async i =>
        {
            var contextId = $"rapid-{i}";
            CorrelationContext.Current = contextId;
            
            // Rapid context switches with minimal delay
            await Task.Yield();
            var check1 = CorrelationContext.Current == contextId;
            
            await Task.Yield();
            var check2 = CorrelationContext.Current == contextId;
            
            await Task.Yield();
            var check3 = CorrelationContext.Current == contextId;
            
            results.Add(check1 && check2 && check3);
        });

        await Task.WhenAll(tasks);

        // Assert - All context switches maintained correct correlation IDs
        Assert.Equal(switchCount, results.Count);
        Assert.All(results, success => Assert.True(success));
    }

    #endregion

    #region Helper Methods

    private static async Task<string?> Level1Async()
    {
        await Task.Delay(5);
        return await Level2Async();
    }

    private static async Task<string?> Level2Async()
    {
        await Task.Delay(5);
        return await Level3Async();
    }

    private static async Task<string?> Level3Async()
    {
        await Task.Delay(5);
        return CorrelationContext.Current;
    }

    private static async Task<string?> DeepAsyncChain(int depth)
    {
        if (depth <= 0)
        {
            return CorrelationContext.Current;
        }

        await Task.Delay(1);
        return await DeepAsyncChain(depth - 1);
    }

    private static async Task<string?> GetCorrelationIdAfterDelayAsync(int delayMs)
    {
        await Task.Delay(delayMs);
        return CorrelationContext.Current;
    }

    private static async Task<string?> SimulateHttpRequestAsync(string requestId)
    {
        // Simulate a new request context (Task.Run creates new execution context)
        return await Task.Run(async () =>
        {
            // Set correlation ID at start of request
            CorrelationContext.Current = requestId;

            // Simulate request processing
            await Task.Delay(Random.Shared.Next(10, 50));
            var midRequest = CorrelationContext.Current;

            await Task.Delay(Random.Shared.Next(10, 50));
            var endRequest = CorrelationContext.Current;

            // Verify consistency throughout request
            Assert.Equal(requestId, midRequest);
            Assert.Equal(requestId, endRequest);

            return CorrelationContext.Current;
        });
    }

    #endregion
}
