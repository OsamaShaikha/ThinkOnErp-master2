using System.Diagnostics;
using System.Runtime;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Memory monitoring service that tracks heap usage, GC statistics, and memory pressure.
/// Provides optimization recommendations and memory pressure detection.
/// Integrates with audit logger to monitor queue depth and prevent memory exhaustion.
/// </summary>
public class MemoryMonitor : IMemoryMonitor
{
    private readonly ILogger<MemoryMonitor> _logger;
    private readonly IAuditLogger? _auditLogger;
    private readonly Process _currentProcess;
    
    // Tracking for allocation rate calculation
    private long _lastTotalMemory;
    private DateTime _lastMeasurementTime;
    private readonly object _measurementLock = new();
    
    // Memory pressure thresholds (configurable)
    private const double LowPressureThreshold = 70.0;      // 70% memory usage
    private const double ModeratePressureThreshold = 80.0; // 80% memory usage
    private const double HighPressureThreshold = 90.0;     // 90% memory usage
    private const double CriticalPressureThreshold = 95.0; // 95% memory usage
    
    // GC frequency thresholds (collections per minute)
    private const double HighGcFrequencyThreshold = 60.0;  // More than 1 per second
    private const double CriticalGcFrequencyThreshold = 120.0; // More than 2 per second
    
    public MemoryMonitor(
        ILogger<MemoryMonitor> logger,
        IAuditLogger? auditLogger = null)
    {
        _logger = logger;
        _auditLogger = auditLogger;
        _currentProcess = Process.GetCurrentProcess();
        _lastTotalMemory = GC.GetTotalMemory(false);
        _lastMeasurementTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Get comprehensive memory metrics including heap sizes, GC statistics, and pressure indicators.
    /// </summary>
    public Task<MemoryMetrics> GetMemoryMetricsAsync()
    {
        try
        {
            _currentProcess.Refresh();
            
            // Get GC memory info
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            var totalMemory = GC.GetTotalMemory(false);
            
            // Get generation sizes
            var gen0Size = GC.GetGeneration(new object()) >= 0 ? GetGenerationSize(0) : 0;
            var gen1Size = GetGenerationSize(1);
            var gen2Size = GetGenerationSize(2);
            
            // Calculate allocation rate
            var allocationRate = CalculateAllocationRate(totalMemory);
            
            // Detect memory pressure
            var pressureInfo = DetectMemoryPressureSync();
            
            // Get GC time percentage
            var gcTimePercent = CalculateGcTimePercent();
            
            var metrics = new MemoryMetrics
            {
                TotalAllocatedBytes = totalMemory,
                TotalAvailableBytes = gcMemoryInfo.TotalAvailableMemoryBytes,
                Gen0HeapSizeBytes = gen0Size,
                Gen1HeapSizeBytes = gen1Size,
                Gen2HeapSizeBytes = gen2Size,
                LargeObjectHeapSizeBytes = gcMemoryInfo.GenerationInfo.Length > 3 
                    ? gcMemoryInfo.GenerationInfo[3].SizeAfterBytes 
                    : 0,
                Gen0CollectionCount = GC.CollectionCount(0),
                Gen1CollectionCount = GC.CollectionCount(1),
                Gen2CollectionCount = GC.CollectionCount(2),
                GcFrequencyPerMinute = 0, // Will be calculated by PerformanceMonitor
                AllocationRateBytesPerSecond = allocationRate,
                IsUnderMemoryPressure = pressureInfo.Severity >= MemoryPressureSeverity.Moderate,
                MemoryPressureLevel = pressureInfo.PressureLevel,
                TotalGcTimeMs = 0, // Calculated separately
                GcTimePercent = gcTimePercent,
                PinnedObjectCount = gcMemoryInfo.PinnedObjectsCount,
                FragmentedBytes = gcMemoryInfo.FragmentedBytes,
                Timestamp = DateTime.UtcNow,
                OptimizationRecommendations = pressureInfo.Recommendations
            };
            
            return Task.FromResult(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory metrics");
            return Task.FromResult(new MemoryMetrics { Timestamp = DateTime.UtcNow });
        }
    }

    /// <summary>
    /// Detect memory pressure and provide recommendations.
    /// </summary>
    public Task<MemoryPressureInfo> DetectMemoryPressureAsync()
    {
        var pressureInfo = DetectMemoryPressureSync();
        return Task.FromResult(pressureInfo);
    }

    /// <summary>
    /// Synchronous memory pressure detection for internal use.
    /// </summary>
    private MemoryPressureInfo DetectMemoryPressureSync()
    {
        try
        {
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            var totalMemory = GC.GetTotalMemory(false);
            var availableMemory = gcMemoryInfo.TotalAvailableMemoryBytes;
            
            var memoryUsagePercent = availableMemory > 0 
                ? (double)totalMemory / availableMemory * 100 
                : 0;
            
            var pressureInfo = new MemoryPressureInfo
            {
                PressureLevel = (int)Math.Min(memoryUsagePercent, 100)
            };
            
            // Determine severity based on memory usage
            if (memoryUsagePercent >= CriticalPressureThreshold)
            {
                pressureInfo.Severity = MemoryPressureSeverity.Critical;
                pressureInfo.Description = $"Critical memory pressure: {memoryUsagePercent:F1}% memory usage";
                pressureInfo.RequiresImmediateAction = true;
                pressureInfo.Recommendations.Add("Immediate action required: System is at risk of out-of-memory errors");
                pressureInfo.Recommendations.Add("Force garbage collection to reclaim memory");
                pressureInfo.Recommendations.Add("Apply backpressure to audit queue to prevent new allocations");
                pressureInfo.Recommendations.Add("Consider restarting the application if memory cannot be reclaimed");
            }
            else if (memoryUsagePercent >= HighPressureThreshold)
            {
                pressureInfo.Severity = MemoryPressureSeverity.High;
                pressureInfo.Description = $"High memory pressure: {memoryUsagePercent:F1}% memory usage";
                pressureInfo.RequiresImmediateAction = true;
                pressureInfo.Recommendations.Add("High memory usage detected - optimization recommended");
                pressureInfo.Recommendations.Add("Trigger garbage collection to reclaim memory");
                pressureInfo.Recommendations.Add("Apply backpressure to audit queue");
                pressureInfo.Recommendations.Add("Review recent memory allocation patterns");
            }
            else if (memoryUsagePercent >= ModeratePressureThreshold)
            {
                pressureInfo.Severity = MemoryPressureSeverity.Moderate;
                pressureInfo.Description = $"Moderate memory pressure: {memoryUsagePercent:F1}% memory usage";
                pressureInfo.RequiresImmediateAction = false;
                pressureInfo.Recommendations.Add("Moderate memory usage - monitoring recommended");
                pressureInfo.Recommendations.Add("Consider triggering garbage collection during low-traffic periods");
                pressureInfo.Recommendations.Add("Monitor audit queue depth");
            }
            else if (memoryUsagePercent >= LowPressureThreshold)
            {
                pressureInfo.Severity = MemoryPressureSeverity.Low;
                pressureInfo.Description = $"Low memory pressure: {memoryUsagePercent:F1}% memory usage";
                pressureInfo.RequiresImmediateAction = false;
                pressureInfo.Recommendations.Add("Memory usage is elevated but within acceptable range");
                pressureInfo.Recommendations.Add("Continue monitoring memory trends");
            }
            else
            {
                pressureInfo.Severity = MemoryPressureSeverity.None;
                pressureInfo.Description = $"No memory pressure: {memoryUsagePercent:F1}% memory usage";
                pressureInfo.RequiresImmediateAction = false;
                pressureInfo.Recommendations.Add("Memory usage is healthy");
            }
            
            // Check GC frequency
            var gcFrequency = CalculateGcFrequency();
            if (gcFrequency >= CriticalGcFrequencyThreshold)
            {
                pressureInfo.Recommendations.Add($"Critical GC frequency: {gcFrequency:F1} collections/min - investigate memory leaks");
                if (pressureInfo.Severity < MemoryPressureSeverity.High)
                {
                    pressureInfo.Severity = MemoryPressureSeverity.High;
                    pressureInfo.RequiresImmediateAction = true;
                }
            }
            else if (gcFrequency >= HighGcFrequencyThreshold)
            {
                pressureInfo.Recommendations.Add($"High GC frequency: {gcFrequency:F1} collections/min - review object allocation patterns");
            }
            
            // Check audit queue depth
            var queueDepth = GetAuditQueueDepth();
            var maxQueueSize = 10000; // Should match AuditLoggingOptions.MaxQueueSize
            var queueUtilization = maxQueueSize > 0 ? (double)queueDepth / maxQueueSize * 100 : 0;
            
            if (queueUtilization >= 90)
            {
                pressureInfo.Recommendations.Add($"Audit queue is {queueUtilization:F1}% full - backpressure is being applied");
                if (pressureInfo.Severity < MemoryPressureSeverity.Moderate)
                {
                    pressureInfo.Severity = MemoryPressureSeverity.Moderate;
                }
            }
            else if (queueUtilization >= 70)
            {
                pressureInfo.Recommendations.Add($"Audit queue is {queueUtilization:F1}% full - monitor for potential backlog");
            }
            
            return pressureInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect memory pressure");
            return new MemoryPressureInfo
            {
                Severity = MemoryPressureSeverity.None,
                Description = "Unable to determine memory pressure",
                PressureLevel = 0
            };
        }
    }

    /// <summary>
    /// Force garbage collection to reclaim memory.
    /// Should be used sparingly as it can impact performance.
    /// </summary>
    public void ForceGarbageCollection(int generation = 2, bool blocking = true, bool compacting = true)
    {
        try
        {
            _logger.LogInformation(
                "Forcing garbage collection: Generation={Generation}, Blocking={Blocking}, Compacting={Compacting}",
                generation, blocking, compacting);
            
            var beforeMemory = GC.GetTotalMemory(false);
            var stopwatch = Stopwatch.StartNew();
            
            if (compacting)
            {
                // Compact the heap to reduce fragmentation
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            }
            
            if (blocking)
            {
                // Blocking collection - wait for GC to complete
                GC.Collect(generation, GCCollectionMode.Forced, blocking: true, compacting: compacting);
                GC.WaitForPendingFinalizers();
                GC.Collect(generation, GCCollectionMode.Forced, blocking: true, compacting: false);
            }
            else
            {
                // Non-blocking collection
                GC.Collect(generation, GCCollectionMode.Optimized, blocking: false);
            }
            
            stopwatch.Stop();
            var afterMemory = GC.GetTotalMemory(false);
            var reclaimedBytes = beforeMemory - afterMemory;
            
            _logger.LogInformation(
                "Garbage collection completed: Duration={DurationMs}ms, ReclaimedMemory={ReclaimedMB}MB, BeforeMemory={BeforeMB}MB, AfterMemory={AfterMB}MB",
                stopwatch.ElapsedMilliseconds,
                reclaimedBytes / 1024 / 1024,
                beforeMemory / 1024 / 1024,
                afterMemory / 1024 / 1024);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to force garbage collection");
        }
    }

    /// <summary>
    /// Get memory allocation rate over a specified period.
    /// </summary>
    public async Task<long> GetAllocationRateAsync(TimeSpan period)
    {
        try
        {
            var startMemory = GC.GetTotalMemory(false);
            var startTime = DateTime.UtcNow;
            
            await Task.Delay(period);
            
            var endMemory = GC.GetTotalMemory(false);
            var endTime = DateTime.UtcNow;
            
            var allocatedBytes = endMemory - startMemory;
            var elapsedSeconds = (endTime - startTime).TotalSeconds;
            
            var allocationRate = elapsedSeconds > 0 
                ? (long)(allocatedBytes / elapsedSeconds) 
                : 0;
            
            return allocationRate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate allocation rate");
            return 0;
        }
    }

    /// <summary>
    /// Get optimization recommendations based on current memory usage patterns.
    /// </summary>
    public async Task<List<string>> GetOptimizationRecommendationsAsync()
    {
        var recommendations = new List<string>();
        
        try
        {
            var metrics = await GetMemoryMetricsAsync();
            var pressureInfo = await DetectMemoryPressureAsync();
            
            // Add pressure-based recommendations
            recommendations.AddRange(pressureInfo.Recommendations);
            
            // Check for high fragmentation
            if (metrics.FragmentedBytes > metrics.TotalAllocatedBytes * 0.2)
            {
                recommendations.Add($"High heap fragmentation detected: {metrics.FragmentedBytes / 1024 / 1024}MB fragmented");
                recommendations.Add("Consider forcing a compacting GC during low-traffic periods");
            }
            
            // Check for high pinned object count
            if (metrics.PinnedObjectCount > 1000)
            {
                recommendations.Add($"High number of pinned objects: {metrics.PinnedObjectCount}");
                recommendations.Add("Review code for excessive pinning (e.g., fixed buffers, GCHandle)");
            }
            
            // Check allocation rate
            if (metrics.AllocationRateBytesPerSecond > 10 * 1024 * 1024) // 10 MB/s
            {
                recommendations.Add($"High allocation rate: {metrics.AllocationRateBytesPerSecond / 1024 / 1024}MB/s");
                recommendations.Add("Consider object pooling for frequently allocated objects");
                recommendations.Add("Review code for unnecessary allocations");
            }
            
            // Check LOH size
            if (metrics.LargeObjectHeapSizeBytes > 100 * 1024 * 1024) // 100 MB
            {
                recommendations.Add($"Large Object Heap is {metrics.LargeObjectHeapSizeBytes / 1024 / 1024}MB");
                recommendations.Add("Review large object allocations (>85KB)");
                recommendations.Add("Consider using ArrayPool for large buffers");
            }
            
            // Check Gen2 size
            if (metrics.Gen2HeapSizeBytes > metrics.TotalAllocatedBytes * 0.7)
            {
                recommendations.Add("Most objects are in Gen2 - indicates long-lived objects");
                recommendations.Add("Review object lifetimes and consider implementing IDisposable");
            }
            
            // General recommendations
            if (recommendations.Count == 0)
            {
                recommendations.Add("Memory usage is optimal");
                recommendations.Add("Continue monitoring for trends");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get optimization recommendations");
            recommendations.Add("Unable to generate recommendations due to error");
        }
        
        return recommendations;
    }

    /// <summary>
    /// Check if the system should apply backpressure due to memory constraints.
    /// </summary>
    public bool ShouldApplyBackpressure()
    {
        try
        {
            var pressureInfo = DetectMemoryPressureSync();
            
            // Apply backpressure if memory pressure is high or critical
            return pressureInfo.Severity >= MemoryPressureSeverity.High;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check backpressure status");
            return false;
        }
    }

    /// <summary>
    /// Get the current audit queue depth for memory monitoring.
    /// </summary>
    public int GetAuditQueueDepth()
    {
        try
        {
            return _auditLogger?.GetQueueDepth() ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit queue depth");
            return 0;
        }
    }

    /// <summary>
    /// Trigger memory optimization strategies.
    /// </summary>
    public async Task OptimizeMemoryAsync()
    {
        try
        {
            _logger.LogInformation("Starting memory optimization");
            
            var beforeMetrics = await GetMemoryMetricsAsync();
            var stopwatch = Stopwatch.StartNew();
            
            // Strategy 1: Force garbage collection with compaction
            ForceGarbageCollection(generation: 2, blocking: true, compacting: true);
            
            // Strategy 2: Trim working set (Windows only)
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    // This tells the OS to trim the working set
                    _currentProcess.Refresh();
                    var beforeWorkingSet = _currentProcess.WorkingSet64;
                    
                    // EmptyWorkingSet is Windows-specific
                    System.Runtime.InteropServices.NativeLibrary.TryLoad("psapi.dll", out var psapiHandle);
                    if (psapiHandle != IntPtr.Zero)
                    {
                        // Note: This requires P/Invoke which we'll skip for now
                        _logger.LogDebug("Working set trim not implemented");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to trim working set");
                }
            }
            
            // Strategy 3: Compact LOH
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            
            stopwatch.Stop();
            var afterMetrics = await GetMemoryMetricsAsync();
            
            var memoryReclaimed = beforeMetrics.TotalAllocatedBytes - afterMetrics.TotalAllocatedBytes;
            
            _logger.LogInformation(
                "Memory optimization completed: Duration={DurationMs}ms, MemoryReclaimed={ReclaimedMB}MB, Before={BeforeMB}MB, After={AfterMB}MB",
                stopwatch.ElapsedMilliseconds,
                memoryReclaimed / 1024 / 1024,
                beforeMetrics.TotalAllocatedBytes / 1024 / 1024,
                afterMetrics.TotalAllocatedBytes / 1024 / 1024);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize memory");
        }
    }

    /// <summary>
    /// Calculate memory allocation rate based on recent measurements.
    /// </summary>
    private long CalculateAllocationRate(long currentMemory)
    {
        lock (_measurementLock)
        {
            var now = DateTime.UtcNow;
            var elapsedSeconds = (now - _lastMeasurementTime).TotalSeconds;
            
            if (elapsedSeconds < 1.0)
            {
                // Not enough time has passed for accurate measurement
                return 0;
            }
            
            var allocatedBytes = currentMemory - _lastTotalMemory;
            var allocationRate = (long)(allocatedBytes / elapsedSeconds);
            
            // Update last measurement
            _lastTotalMemory = currentMemory;
            _lastMeasurementTime = now;
            
            return Math.Max(0, allocationRate); // Ensure non-negative
        }
    }

    /// <summary>
    /// Get the size of a specific generation heap.
    /// </summary>
    private long GetGenerationSize(int generation)
    {
        try
        {
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            if (generation < gcMemoryInfo.GenerationInfo.Length)
            {
                return gcMemoryInfo.GenerationInfo[generation].SizeAfterBytes;
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Calculate GC frequency (collections per minute).
    /// </summary>
    private double CalculateGcFrequency()
    {
        try
        {
            // This is a simplified calculation
            // For accurate frequency, we'd need to track collection counts over time
            // For now, return 0 and let PerformanceMonitor handle this
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Calculate percentage of time spent in GC.
    /// </summary>
    private double CalculateGcTimePercent()
    {
        try
        {
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            // GC time percentage is not directly available in .NET
            // Would need to use performance counters or ETW events
            // For now, return 0
            return 0;
        }
        catch
        {
            return 0;
        }
    }
}
