using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.API.Tests.Middleware;

/// <summary>
/// Property-based tests for correlation ID uniqueness in concurrent request scenarios.
/// 
/// **Validates: Requirements 4.1**
/// 
/// Property 8: Correlation ID Uniqueness
/// FOR ALL concurrent API requests, the generated correlation IDs SHALL be unique across all requests.
/// 
/// This property verifies that:
/// 1. Correlation IDs generated for concurrent requests are unique
/// 2. No duplicate correlation IDs are generated even under high concurrency
/// 3. The correlation ID generation mechanism is thread-safe
/// 4. Correlation IDs remain unique across multiple batches of concurrent requests
/// </summary>
public class CorrelationIdUniquenessPropertyTests
{
    private const int MinIterations = 100;

    /// <summary>
    /// **Validates: Requirements 4.1**
    /// 
    /// Property 8: Correlation ID Uniqueness
    /// 
    /// FOR ALL concurrent API requests, the generated correlation IDs SHALL be unique.
    /// 
    /// This property verifies that:
    /// 1. When multiple requests are processed concurrently, each gets a unique correlation ID
    /// 2. No two requests share the same correlation ID
    /// 3. The uniqueness holds across different concurrency levels
    /// 4. The correlation ID generation is thread-safe and collision-free
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllConcurrentRequests_CorrelationIdsAreUnique(ConcurrentRequestBatch batch)
    {
        // Arrange: Create multiple HTTP contexts simulating concurrent requests
        var correlationIds = new List<string>();
        var correlationIdLock = new object();
        var tasks = new List<Task>();

        // Create a mock middleware pipeline that captures correlation IDs
        var mockAuditLogger = new Mock<IAuditLogger>();
        var mockPerformanceMonitor = new Mock<IPerformanceMonitor>();
        var mockExceptionCategorization = new Mock<IExceptionCategorizationService>();
        var mockLogger = new Mock<ILogger<API.Middleware.RequestTracingMiddleware>>();
        
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<ISensitiveDataMasker>());
        var serviceProvider = services.BuildServiceProvider();
        var serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var options = Options.Create(new RequestTracingOptions
        {
            Enabled = true,
            CorrelationIdHeader = "X-Correlation-ID",
            LogPayloads = false,
            ExcludedPaths = []
        });

        // Act: Simulate concurrent requests
        for (int i = 0; i < batch.RequestCount; i++)
        {
            var requestIndex = i;
            var task = Task.Run(async () =>
            {
                // Create a new HTTP context for each request
                var context = new DefaultHttpContext();
                context.Request.Method = batch.Requests[requestIndex].Method;
                context.Request.Path = batch.Requests[requestIndex].Path;
                
                // If the request has a correlation ID header, add it
                if (!string.IsNullOrEmpty(batch.Requests[requestIndex].CorrelationIdHeader))
                {
                    context.Request.Headers["X-Correlation-ID"] = batch.Requests[requestIndex].CorrelationIdHeader;
                }

                // Create middleware instance
                var middleware = new API.Middleware.RequestTracingMiddleware(
                    next: async (ctx) => 
                    {
                        // Capture the correlation ID that was set by the middleware
                        var correlationId = CorrelationContext.Current;
                        if (!string.IsNullOrEmpty(correlationId))
                        {
                            lock (correlationIdLock)
                            {
                                correlationIds.Add(correlationId);
                            }
                        }
                        await Task.CompletedTask;
                    },
                    auditLogger: mockAuditLogger.Object,
                    performanceMonitor: mockPerformanceMonitor.Object,
                    serviceScopeFactory: serviceScopeFactory,
                    exceptionCategorization: mockExceptionCategorization.Object,
                    logger: mockLogger.Object,
                    options: options
                );

                // Invoke the middleware
                await middleware.InvokeAsync(context);
            });

            tasks.Add(task);
        }

        // Wait for all concurrent requests to complete
        Task.WaitAll([.. tasks]);

        // Property 1: All requests should have generated or captured a correlation ID
        var allRequestsHaveCorrelationId = correlationIds.Count == batch.RequestCount;

        // Property 2: All correlation IDs should be unique (no duplicates)
        var uniqueCorrelationIds = correlationIds.Distinct().ToList();
        var allCorrelationIdsAreUnique = uniqueCorrelationIds.Count == correlationIds.Count;

        // Property 3: Correlation IDs should be valid GUIDs (for generated IDs)
        var generatedIds = correlationIds
            .Where(id => !batch.Requests.Any(r => r.CorrelationIdHeader == id))
            .ToList();
        var allGeneratedIdsAreValidGuids = generatedIds.All(id => Guid.TryParse(id, out _));

        // Property 4: Provided correlation IDs should be preserved (not regenerated)
        var providedIds = batch.Requests
            .Where(r => !string.IsNullOrEmpty(r.CorrelationIdHeader))
            .Select(r => r.CorrelationIdHeader)
            .ToList();
        var allProvidedIdsArePreserved = providedIds.All(id => correlationIds.Contains(id));

        // Property 5: No correlation ID should appear more than once
        var duplicateIds = correlationIds
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToList();
        var noDuplicateIds = duplicateIds.Count == 0;

        // Combine all properties
        var result = allRequestsHaveCorrelationId
            && allCorrelationIdsAreUnique
            && allGeneratedIdsAreValidGuids
            && allProvidedIdsArePreserved
            && noDuplicateIds;

        return result
            .Label($"All requests have correlation ID: {allRequestsHaveCorrelationId} (expected: {batch.RequestCount}, actual: {correlationIds.Count})")
            .Label($"All correlation IDs are unique: {allCorrelationIdsAreUnique} (unique: {uniqueCorrelationIds.Count}, total: {correlationIds.Count})")
            .Label($"All generated IDs are valid GUIDs: {allGeneratedIdsAreValidGuids} (generated: {generatedIds.Count})")
            .Label($"All provided IDs are preserved: {allProvidedIdsArePreserved} (provided: {providedIds.Count})")
            .Label($"No duplicate IDs: {noDuplicateIds} (duplicates: {duplicateIds.Count})")
            .Label(duplicateIds.Any() 
                ? $"Duplicate IDs found: {string.Join(", ", duplicateIds.Select(d => $"{d.Id} ({d.Count}x)"))}"
                : "No duplicates found");
    }

    /// <summary>
    /// **Validates: Requirements 4.1**
    /// 
    /// Property 8: Correlation ID Uniqueness (Stress Test)
    /// 
    /// FOR ALL high-volume concurrent request scenarios, correlation IDs SHALL remain unique.
    /// 
    /// This property verifies uniqueness under stress conditions:
    /// 1. High concurrency (100+ concurrent requests)
    /// 2. Multiple batches of concurrent requests
    /// 3. Mix of requests with and without provided correlation IDs
    /// </summary>
    [Property(MaxTest = 50, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllHighVolumeConcurrentRequests_CorrelationIdsRemainUnique(HighVolumeBatch batch)
    {
        // Arrange: Create a large number of concurrent requests
        var allCorrelationIds = new List<string>();
        var correlationIdLock = new object();

        var mockAuditLogger = new Mock<IAuditLogger>();
        var mockPerformanceMonitor = new Mock<IPerformanceMonitor>();
        var mockExceptionCategorization = new Mock<IExceptionCategorizationService>();
        var mockLogger = new Mock<ILogger<API.Middleware.RequestTracingMiddleware>>();
        
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<ISensitiveDataMasker>());
        var serviceProvider = services.BuildServiceProvider();
        var serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var options = Options.Create(new RequestTracingOptions
        {
            Enabled = true,
            CorrelationIdHeader = "X-Correlation-ID",
            LogPayloads = false,
            ExcludedPaths = []
        });

        // Act: Process multiple batches of concurrent requests
        for (int batchIndex = 0; batchIndex < batch.BatchCount; batchIndex++)
        {
            var tasks = new List<Task>();

            for (int i = 0; i < batch.RequestsPerBatch; i++)
            {
                var task = Task.Run(async () =>
                {
                    var context = new DefaultHttpContext();
                    context.Request.Method = "GET";
                    context.Request.Path = $"/api/test/batch{batchIndex}/request{i}";

                    var middleware = new API.Middleware.RequestTracingMiddleware(
                        next: async (ctx) => 
                        {
                            var correlationId = CorrelationContext.Current;
                            if (!string.IsNullOrEmpty(correlationId))
                            {
                                lock (correlationIdLock)
                                {
                                    allCorrelationIds.Add(correlationId);
                                }
                            }
                            await Task.CompletedTask;
                        },
                        auditLogger: mockAuditLogger.Object,
                        performanceMonitor: mockPerformanceMonitor.Object,
                        serviceScopeFactory: serviceScopeFactory,
                        exceptionCategorization: mockExceptionCategorization.Object,
                        logger: mockLogger.Object,
                        options: options
                    );

                    await middleware.InvokeAsync(context);
                });

                tasks.Add(task);
            }

            // Wait for this batch to complete before starting the next
            Task.WaitAll([.. tasks]);
        }

        // Property 1: Total correlation IDs should match total requests
        var totalRequests = batch.BatchCount * batch.RequestsPerBatch;
        var allRequestsHaveCorrelationId = allCorrelationIds.Count == totalRequests;

        // Property 2: All correlation IDs should be unique across all batches
        var uniqueCorrelationIds = allCorrelationIds.Distinct().ToList();
        var allCorrelationIdsAreUnique = uniqueCorrelationIds.Count == allCorrelationIds.Count;

        // Property 3: All correlation IDs should be valid GUIDs
        var allAreValidGuids = allCorrelationIds.All(id => Guid.TryParse(id, out _));

        // Property 4: No correlation ID should appear in multiple batches
        var duplicateIds = allCorrelationIds
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToList();
        var noDuplicateIds = duplicateIds.Count == 0;

        // Combine all properties
        var result = allRequestsHaveCorrelationId
            && allCorrelationIdsAreUnique
            && allAreValidGuids
            && noDuplicateIds;

        return result
            .Label($"All requests have correlation ID: {allRequestsHaveCorrelationId} (expected: {totalRequests}, actual: {allCorrelationIds.Count})")
            .Label($"All correlation IDs are unique: {allCorrelationIdsAreUnique} (unique: {uniqueCorrelationIds.Count}, total: {allCorrelationIds.Count})")
            .Label($"All are valid GUIDs: {allAreValidGuids}")
            .Label($"No duplicate IDs: {noDuplicateIds} (duplicates: {duplicateIds.Count})")
            .Label($"Batches: {batch.BatchCount}, Requests per batch: {batch.RequestsPerBatch}")
            .Label(duplicateIds.Any() 
                ? $"Duplicate IDs found: {string.Join(", ", duplicateIds.Take(5).Select(d => $"{d.Id} ({d.Count}x)"))}"
                : "No duplicates found");
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary concurrent request batches for property testing.
        /// Covers various concurrency levels and request patterns.
        /// </summary>
        public static Arbitrary<ConcurrentRequestBatch> ConcurrentRequestBatch()
        {
            var batchGenerator =
                from requestCount in Gen.Choose(2, 50) // 2 to 50 concurrent requests
                from requests in Gen.ListOf(requestCount, RequestGenerator())
                select new ConcurrentRequestBatch
                {
                    RequestCount = requestCount,
                    Requests = requests.ToList()
                };

            return Arb.From(batchGenerator);
        }

        /// <summary>
        /// Generates arbitrary high-volume batches for stress testing.
        /// Tests with 100+ concurrent requests across multiple batches.
        /// </summary>
        public static Arbitrary<HighVolumeBatch> HighVolumeBatch()
        {
            var batchGenerator =
                from batchCount in Gen.Choose(2, 5) // 2 to 5 batches
                from requestsPerBatch in Gen.Choose(20, 50) // 20 to 50 requests per batch
                select new HighVolumeBatch
                {
                    BatchCount = batchCount,
                    RequestsPerBatch = requestsPerBatch
                };

            return Arb.From(batchGenerator);
        }

        /// <summary>
        /// Generates individual request configurations.
        /// Some requests have provided correlation IDs, others don't.
        /// </summary>
        private static Gen<RequestConfig> RequestGenerator()
        {
            return
                from method in Gen.Elements("GET", "POST", "PUT", "DELETE", "PATCH")
                from path in Gen.Elements(
                    "/api/users",
                    "/api/companies",
                    "/api/branches",
                    "/api/roles",
                    "/api/currencies",
                    "/api/invoices",
                    "/api/payments")
                from hasCorrelationId in Gen.Frequency(
                    Tuple.Create(8, Gen.Constant(false)), // 80% don't have correlation ID
                    Tuple.Create(2, Gen.Constant(true)))  // 20% have correlation ID
                from correlationId in hasCorrelationId 
                    ? Gen.Constant<string?>(Guid.NewGuid().ToString())
                    : Gen.Constant<string?>(null)
                select new RequestConfig
                {
                    Method = method,
                    Path = path,
                    CorrelationIdHeader = correlationId
                };
        }
    }
}

/// <summary>
/// Represents a batch of concurrent requests for property testing.
/// </summary>
public class ConcurrentRequestBatch
{
    public int RequestCount { get; set; }
    public List<RequestConfig> Requests { get; set; } = new();
}

/// <summary>
/// Represents a high-volume batch configuration for stress testing.
/// </summary>
public class HighVolumeBatch
{
    public int BatchCount { get; set; }
    public int RequestsPerBatch { get; set; }
}

/// <summary>
/// Represents configuration for a single request.
/// </summary>
public class RequestConfig
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public string? CorrelationIdHeader { get; set; }
}
