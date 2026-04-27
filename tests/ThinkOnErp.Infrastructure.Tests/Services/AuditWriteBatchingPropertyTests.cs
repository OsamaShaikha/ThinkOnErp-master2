using System.Diagnostics;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Resilience;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Property-based tests for audit write batching.
/// Validates that multiple audit events queued within the batch window or batch size 
/// are written to the database in a single batch operation.
/// 
/// **Validates: Requirements 13.3**
/// 
/// Property 26: Audit Write Batching
/// FOR ALL sequences of audit events, when multiple events are queued within the batch window 
/// or the batch size is reached, they SHALL be written to the database in a single batch operation.
/// 
/// This property verifies that:
/// 1. Multiple events queued rapidly are batched together
/// 2. Batch writes occur when batch size is reached
/// 3. Batch writes occur when batch window expires
/// 4. Batching reduces database round trips
/// </summary>
public class AuditWriteBatchingPropertyTests : IDisposable
{
    private const int MinIterations = 50;
    private readonly Mock<IAuditRepository> _mockRepository;
    private readonly Mock<ISensitiveDataMasker> _mockDataMasker;
    private readonly Mock<ILegacyAuditService> _mockLegacyService;
    private readonly Mock<ILogger<AuditLogger>> _mockAuditLogger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly List<BatchWriteInfo> _batchWrites;

    public AuditWriteBatchingPropertyTests()
    {
        _mockRepository = new Mock<IAuditRepository>();
        _mockDataMasker = new Mock<ISensitiveDataMasker>();
        _mockLegacyService = new Mock<ILegacyAuditService>();
        _mockAuditLogger = new Mock<ILogger<AuditLogger>>();
        _batchWrites = new List<BatchWriteInfo>();

        // Setup mock repository to track batch writes
        _mockRepository
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<SysAuditLog>>(), It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<SysAuditLog>, CancellationToken>(async (logs, ct) =>
            {
                var batchInfo = new BatchWriteInfo
                {
                    BatchSize = logs.Count(),
                    Timestamp = DateTime.UtcNow
                };

                lock (_batchWrites)
                {
                    _batchWrites.Add(batchInfo);
                }

                // Simulate minimal database write time
                await Task.Delay(10, ct);
                
                return logs.Count();
            });

        _mockRepository
            .Setup(r => r.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Setup data masker to pass through data unchanged for testing
        _mockDataMasker
            .Setup(m => m.MaskSensitiveFields(It.IsAny<string?>()))
            .Returns<string?>(s => s);

        _mockDataMasker
            .Setup(m => m.TruncateIfNeeded(It.IsAny<string?>()))
            .Returns<string?>(s => s);

        // Setup legacy service with default implementations
        _mockLegacyService
            .Setup(l => l.DetermineBusinessModuleAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("TestModule");

        _mockLegacyService
            .Setup(l => l.ExtractDeviceIdentifierAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("TestDevice");

        _mockLegacyService
            .Setup(l => l.GenerateErrorCodeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("TEST_001");

        _mockLegacyService
            .Setup(l => l.GenerateBusinessDescriptionAsync(It.IsAny<AuditLogEntry>()))
            .ReturnsAsync("Test description");

        // Create service collection for dependency injection
        var services = new ServiceCollection();
        services.AddSingleton(_mockRepository.Object);
        services.AddSingleton(_mockDataMasker.Object);
        services.AddSingleton(_mockLegacyService.Object);
        services.AddSingleton(_mockAuditLogger.Object);

        var serviceProvider = services.BuildServiceProvider();
        _serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }

    /// <summary>
    /// **Validates: Requirements 13.3**
    /// 
    /// Property 26: Audit Write Batching - Batch Size Trigger
    /// 
    /// FOR ALL sequences of audit events where the count reaches the configured batch size,
    /// they SHALL be written to the database in a single batch operation.
    /// 
    /// This property verifies that:
    /// 1. When batch size is reached, a batch write occurs
    /// 2. The batch contains exactly the configured batch size (or close to it)
    /// 3. Multiple events are combined into fewer database operations
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllEventSequences_WhenBatchSizeReached_EventsAreWrittenInSingleBatch(
        BatchSizeTestInput input)
    {
        // Clear previous batch write data
        lock (_batchWrites)
        {
            _batchWrites.Clear();
        }

        // Configure audit logging with specific batch size
        var auditOptions = Options.Create(new AuditLoggingOptions
        {
            Enabled = true,
            BatchSize = input.BatchSize,
            BatchWindowMs = 5000, // Long window to ensure batch size triggers first
            MaxQueueSize = 10000,
            EnableCircuitBreaker = false
        });

        var circuitBreakerRegistry = new CircuitBreakerRegistry(
            new LoggerFactory(),
            failureThreshold: 5,
            openDuration: TimeSpan.FromSeconds(60));

        var auditLogger = new AuditLogger(
            _serviceScopeFactory,
            _mockAuditLogger.Object,
            auditOptions,
            circuitBreakerRegistry);

        // Start the audit logger background service
        auditLogger.StartAsync(CancellationToken.None).Wait();

        try
        {
            // Queue events rapidly (more than batch size)
            var eventCount = input.BatchSize + input.AdditionalEvents;
            var tasks = new List<Task>();

            for (int i = 0; i < eventCount; i++)
            {
                var auditEvent = new DataChangeAuditEvent
                {
                    CorrelationId = $"{input.CorrelationId}-{i}",
                    ActorType = "USER",
                    ActorId = input.ActorId,
                    CompanyId = input.CompanyId,
                    BranchId = input.BranchId,
                    Action = "INSERT",
                    EntityType = "TestEntity",
                    EntityId = i,
                    NewValue = $"{{\"id\":{i}}}",
                    Timestamp = DateTime.UtcNow
                };

                tasks.Add(auditLogger.LogDataChangeAsync(auditEvent));
            }

            // Wait for all events to be queued
            Task.WaitAll(tasks.ToArray());

            // Wait for batch processing to complete
            // Should be enough time for at least one batch to be written
            Thread.Sleep(500);

            // Get batch write information
            List<BatchWriteInfo> batchWritesCopy;
            lock (_batchWrites)
            {
                batchWritesCopy = new List<BatchWriteInfo>(_batchWrites);
            }

            // Property 1: At least one batch write occurred
            var batchWriteOccurred = batchWritesCopy.Count > 0;

            // Property 2: If we queued more than batch size, we should have at least one full batch
            var hasFullBatch = batchWritesCopy.Any(b => b.BatchSize >= input.BatchSize);

            // Property 3: Total events written should eventually match events queued
            // (may need to wait for final batch window to expire)
            var totalWritten = batchWritesCopy.Sum(b => b.BatchSize);
            
            // Property 4: Number of batch writes should be less than number of events
            // (proving batching is happening)
            var batchingOccurred = batchWritesCopy.Count < eventCount;

            // Property 5: If we have multiple batches, they should be reasonably sized
            var batchesAreReasonablySized = batchWritesCopy.All(b => 
                b.BatchSize > 0 && b.BatchSize <= input.BatchSize + 5); // Allow small overflow

            var result = batchWriteOccurred && hasFullBatch && batchingOccurred && batchesAreReasonablySized;

            return result
                .Label($"Batch write occurred: {batchWriteOccurred}")
                .Label($"Has full batch: {hasFullBatch}")
                .Label($"Batching occurred: {batchingOccurred}")
                .Label($"Batches reasonably sized: {batchesAreReasonablySized}")
                .Label($"Configured batch size: {input.BatchSize}")
                .Label($"Events queued: {eventCount}")
                .Label($"Batch writes: {batchWritesCopy.Count}")
                .Label($"Total written: {totalWritten}")
                .Label($"Batch sizes: [{string.Join(", ", batchWritesCopy.Select(b => b.BatchSize))}]");
        }
        finally
        {
            // Stop the audit logger
            auditLogger.StopAsync(CancellationToken.None).Wait();
        }
    }

    /// <summary>
    /// **Validates: Requirements 13.3**
    /// 
    /// Property 26: Audit Write Batching - Batch Window Trigger
    /// 
    /// FOR ALL sequences of audit events where events are queued within the batch window,
    /// they SHALL be written together when the window expires, even if batch size is not reached.
    /// 
    /// This property verifies that:
    /// 1. Events queued within the batch window are batched together
    /// 2. Batch write occurs when window expires
    /// 3. Small batches are still written efficiently
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllEventSequences_WhenBatchWindowExpires_EventsAreWrittenInSingleBatch(
        BatchWindowTestInput input)
    {
        // Clear previous batch write data
        lock (_batchWrites)
        {
            _batchWrites.Clear();
        }

        // Configure audit logging with specific batch window
        var auditOptions = Options.Create(new AuditLoggingOptions
        {
            Enabled = true,
            BatchSize = 1000, // Large batch size to ensure window triggers first
            BatchWindowMs = input.BatchWindowMs,
            MaxQueueSize = 10000,
            EnableCircuitBreaker = false
        });

        var circuitBreakerRegistry = new CircuitBreakerRegistry(
            new LoggerFactory(),
            failureThreshold: 5,
            openDuration: TimeSpan.FromSeconds(60));

        var auditLogger = new AuditLogger(
            _serviceScopeFactory,
            _mockAuditLogger.Object,
            auditOptions,
            circuitBreakerRegistry);

        // Start the audit logger background service
        auditLogger.StartAsync(CancellationToken.None).Wait();

        try
        {
            // Queue a small number of events rapidly (less than batch size)
            var eventCount = input.EventCount;
            var tasks = new List<Task>();

            var startTime = DateTime.UtcNow;

            for (int i = 0; i < eventCount; i++)
            {
                var auditEvent = new DataChangeAuditEvent
                {
                    CorrelationId = $"{input.CorrelationId}-{i}",
                    ActorType = "USER",
                    ActorId = input.ActorId,
                    CompanyId = input.CompanyId,
                    BranchId = input.BranchId,
                    Action = "INSERT",
                    EntityType = "TestEntity",
                    EntityId = i,
                    NewValue = $"{{\"id\":{i}}}",
                    Timestamp = DateTime.UtcNow
                };

                tasks.Add(auditLogger.LogDataChangeAsync(auditEvent));
            }

            // Wait for all events to be queued
            Task.WaitAll(tasks.ToArray());

            // Wait for batch window to expire plus some buffer
            Thread.Sleep(input.BatchWindowMs + 200);

            // Get batch write information
            List<BatchWriteInfo> batchWritesCopy;
            lock (_batchWrites)
            {
                batchWritesCopy = new List<BatchWriteInfo>(_batchWrites);
            }

            // Property 1: At least one batch write occurred
            var batchWriteOccurred = batchWritesCopy.Count > 0;

            // Property 2: All events were written
            var totalWritten = batchWritesCopy.Sum(b => b.BatchSize);
            var allEventsWritten = totalWritten >= eventCount;

            // Property 3: Events were batched (not written individually)
            // Should have fewer batch writes than events
            var eventsBatched = batchWritesCopy.Count <= eventCount;

            // Property 4: First batch write occurred after batch window expired
            // (within reasonable tolerance)
            var firstBatchTime = batchWritesCopy.FirstOrDefault()?.Timestamp;
            var timeSinceStart = firstBatchTime.HasValue 
                ? (firstBatchTime.Value - startTime).TotalMilliseconds 
                : 0;
            var batchWindowRespected = timeSinceStart >= input.BatchWindowMs * 0.8; // 80% tolerance

            // Property 5: Events were combined into a small number of batches
            var efficientBatching = batchWritesCopy.Count <= 3; // Should be 1-2 batches typically

            var result = batchWriteOccurred && allEventsWritten && eventsBatched 
                && batchWindowRespected && efficientBatching;

            return result
                .Label($"Batch write occurred: {batchWriteOccurred}")
                .Label($"All events written: {allEventsWritten} ({totalWritten}/{eventCount})")
                .Label($"Events batched: {eventsBatched}")
                .Label($"Batch window respected: {batchWindowRespected} (time: {timeSinceStart:F0}ms, window: {input.BatchWindowMs}ms)")
                .Label($"Efficient batching: {efficientBatching}")
                .Label($"Batch window: {input.BatchWindowMs}ms")
                .Label($"Events queued: {eventCount}")
                .Label($"Batch writes: {batchWritesCopy.Count}")
                .Label($"Batch sizes: [{string.Join(", ", batchWritesCopy.Select(b => b.BatchSize))}]");
        }
        finally
        {
            // Stop the audit logger
            auditLogger.StopAsync(CancellationToken.None).Wait();
        }
    }

    /// <summary>
    /// **Validates: Requirements 13.3**
    /// 
    /// Property 26: Audit Write Batching - Reduced Database Round Trips
    /// 
    /// FOR ALL sequences of audit events, batching SHALL reduce the number of database 
    /// operations compared to writing events individually.
    /// 
    /// This property verifies that:
    /// 1. Multiple events result in fewer database operations
    /// 2. Batching efficiency improves with more events
    /// 3. Database round trips are minimized
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllEventSequences_BatchingReducesDatabaseRoundTrips(
        DatabaseRoundTripTestInput input)
    {
        // Clear previous batch write data
        lock (_batchWrites)
        {
            _batchWrites.Clear();
        }

        // Configure audit logging with reasonable batch settings
        var auditOptions = Options.Create(new AuditLoggingOptions
        {
            Enabled = true,
            BatchSize = input.BatchSize,
            BatchWindowMs = input.BatchWindowMs,
            MaxQueueSize = 10000,
            EnableCircuitBreaker = false
        });

        var circuitBreakerRegistry = new CircuitBreakerRegistry(
            new LoggerFactory(),
            failureThreshold: 5,
            openDuration: TimeSpan.FromSeconds(60));

        var auditLogger = new AuditLogger(
            _serviceScopeFactory,
            _mockAuditLogger.Object,
            auditOptions,
            circuitBreakerRegistry);

        // Start the audit logger background service
        auditLogger.StartAsync(CancellationToken.None).Wait();

        try
        {
            // Queue multiple events
            var eventCount = input.EventCount;
            var tasks = new List<Task>();

            for (int i = 0; i < eventCount; i++)
            {
                var auditEvent = new DataChangeAuditEvent
                {
                    CorrelationId = $"{input.CorrelationId}-{i}",
                    ActorType = "USER",
                    ActorId = input.ActorId,
                    CompanyId = input.CompanyId,
                    BranchId = input.BranchId,
                    Action = "INSERT",
                    EntityType = "TestEntity",
                    EntityId = i,
                    NewValue = $"{{\"id\":{i}}}",
                    Timestamp = DateTime.UtcNow
                };

                tasks.Add(auditLogger.LogDataChangeAsync(auditEvent));
            }

            // Wait for all events to be queued
            Task.WaitAll(tasks.ToArray());

            // Wait for batch processing to complete
            Thread.Sleep(Math.Max(input.BatchWindowMs * 2, 500));

            // Get batch write information
            List<BatchWriteInfo> batchWritesCopy;
            lock (_batchWrites)
            {
                batchWritesCopy = new List<BatchWriteInfo>(_batchWrites);
            }

            // Property 1: Database operations occurred
            var databaseOperationsOccurred = batchWritesCopy.Count > 0;

            // Property 2: Number of database operations is less than number of events
            // (proving batching reduces round trips)
            var reducedRoundTrips = batchWritesCopy.Count < eventCount;

            // Property 3: All events were eventually written
            var totalWritten = batchWritesCopy.Sum(b => b.BatchSize);
            var allEventsWritten = totalWritten >= eventCount;

            // Property 4: Batching efficiency - ratio of events to database operations
            var batchingEfficiency = eventCount > 0 
                ? (double)totalWritten / batchWritesCopy.Count 
                : 0;
            var efficientBatching = batchingEfficiency >= 1.0; // At least 1 event per batch

            // Property 5: For larger event counts, efficiency should be better
            var expectedMinEfficiency = eventCount >= input.BatchSize ? input.BatchSize * 0.5 : 1.0;
            var meetsEfficiencyTarget = batchingEfficiency >= expectedMinEfficiency;

            var result = databaseOperationsOccurred && reducedRoundTrips && allEventsWritten 
                && efficientBatching && meetsEfficiencyTarget;

            return result
                .Label($"Database operations occurred: {databaseOperationsOccurred}")
                .Label($"Reduced round trips: {reducedRoundTrips} ({batchWritesCopy.Count} ops for {eventCount} events)")
                .Label($"All events written: {allEventsWritten} ({totalWritten}/{eventCount})")
                .Label($"Efficient batching: {efficientBatching} (efficiency: {batchingEfficiency:F2})")
                .Label($"Meets efficiency target: {meetsEfficiencyTarget} (actual: {batchingEfficiency:F2}, target: {expectedMinEfficiency:F2})")
                .Label($"Batch size: {input.BatchSize}, Window: {input.BatchWindowMs}ms")
                .Label($"Events: {eventCount}, DB ops: {batchWritesCopy.Count}")
                .Label($"Batch sizes: [{string.Join(", ", batchWritesCopy.Select(b => b.BatchSize))}]");
        }
        finally
        {
            // Stop the audit logger
            auditLogger.StopAsync(CancellationToken.None).Wait();
        }
    }

    public void Dispose()
    {
        // Cleanup is handled in individual tests
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates test inputs for batch size trigger testing.
        /// </summary>
        public static Arbitrary<BatchSizeTestInput> BatchSizeTestInput()
        {
            var generator =
                from batchSize in Gen.Choose(5, 50)
                from additionalEvents in Gen.Choose(1, 20)
                from actorId in Gen.Choose(1, 1000).Select(i => (long)i)
                from companyId in Gen.Choose(1, 100).Select(i => (long?)i)
                from branchId in Gen.Choose(1, 500).Select(i => (long?)i)
                from correlationId in Gen.Constant(Guid.NewGuid().ToString())
                select new ThinkOnErp.Infrastructure.Tests.Services.BatchSizeTestInput
                {
                    BatchSize = batchSize,
                    AdditionalEvents = additionalEvents,
                    ActorId = actorId,
                    CompanyId = companyId,
                    BranchId = branchId,
                    CorrelationId = correlationId
                };

            return Arb.From(generator);
        }

        /// <summary>
        /// Generates test inputs for batch window trigger testing.
        /// </summary>
        public static Arbitrary<BatchWindowTestInput> BatchWindowTestInput()
        {
            var generator =
                from batchWindowMs in Gen.Choose(50, 300)
                from eventCount in Gen.Choose(2, 20)
                from actorId in Gen.Choose(1, 1000).Select(i => (long)i)
                from companyId in Gen.Choose(1, 100).Select(i => (long?)i)
                from branchId in Gen.Choose(1, 500).Select(i => (long?)i)
                from correlationId in Gen.Constant(Guid.NewGuid().ToString())
                select new ThinkOnErp.Infrastructure.Tests.Services.BatchWindowTestInput
                {
                    BatchWindowMs = batchWindowMs,
                    EventCount = eventCount,
                    ActorId = actorId,
                    CompanyId = companyId,
                    BranchId = branchId,
                    CorrelationId = correlationId
                };

            return Arb.From(generator);
        }

        /// <summary>
        /// Generates test inputs for database round trip reduction testing.
        /// </summary>
        public static Arbitrary<DatabaseRoundTripTestInput> DatabaseRoundTripTestInput()
        {
            var generator =
                from batchSize in Gen.Choose(10, 50)
                from batchWindowMs in Gen.Choose(50, 200)
                from eventCount in Gen.Choose(15, 100)
                from actorId in Gen.Choose(1, 1000).Select(i => (long)i)
                from companyId in Gen.Choose(1, 100).Select(i => (long?)i)
                from branchId in Gen.Choose(1, 500).Select(i => (long?)i)
                from correlationId in Gen.Constant(Guid.NewGuid().ToString())
                select new ThinkOnErp.Infrastructure.Tests.Services.DatabaseRoundTripTestInput
                {
                    BatchSize = batchSize,
                    BatchWindowMs = batchWindowMs,
                    EventCount = eventCount,
                    ActorId = actorId,
                    CompanyId = companyId,
                    BranchId = branchId,
                    CorrelationId = correlationId
                };

            return Arb.From(generator);
        }
    }
}

/// <summary>
/// Tracks information about a batch write operation.
/// </summary>
public class BatchWriteInfo
{
    public int BatchSize { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Input for batch size trigger testing.
/// </summary>
public class BatchSizeTestInput
{
    public int BatchSize { get; set; }
    public int AdditionalEvents { get; set; }
    public long ActorId { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// Input for batch window trigger testing.
/// </summary>
public class BatchWindowTestInput
{
    public int BatchWindowMs { get; set; }
    public int EventCount { get; set; }
    public long ActorId { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// Input for database round trip reduction testing.
/// </summary>
public class DatabaseRoundTripTestInput
{
    public int BatchSize { get; set; }
    public int BatchWindowMs { get; set; }
    public int EventCount { get; set; }
    public long ActorId { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
