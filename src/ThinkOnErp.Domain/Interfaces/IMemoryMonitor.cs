using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for memory usage monitoring and optimization.
/// Provides detailed memory metrics, pressure detection, and optimization recommendations.
/// </summary>
public interface IMemoryMonitor
{
    /// <summary>
    /// Get current detailed memory metrics including heap sizes and GC statistics.
    /// </summary>
    Task<MemoryMetrics> GetMemoryMetricsAsync();
    
    /// <summary>
    /// Detect current memory pressure level and get recommendations.
    /// </summary>
    Task<MemoryPressureInfo> DetectMemoryPressureAsync();
    
    /// <summary>
    /// Force a garbage collection if memory pressure is high.
    /// Should be used sparingly as it can impact performance.
    /// </summary>
    /// <param name="generation">GC generation to collect (0, 1, or 2)</param>
    /// <param name="blocking">Whether to wait for GC to complete</param>
    /// <param name="compacting">Whether to compact the heap</param>
    void ForceGarbageCollection(int generation = 2, bool blocking = true, bool compacting = true);
    
    /// <summary>
    /// Get memory allocation rate over a specified period.
    /// </summary>
    /// <param name="period">Time period to measure allocation rate</param>
    Task<long> GetAllocationRateAsync(TimeSpan period);
    
    /// <summary>
    /// Get optimization recommendations based on current memory usage patterns.
    /// </summary>
    Task<List<string>> GetOptimizationRecommendationsAsync();
    
    /// <summary>
    /// Check if the system should apply backpressure due to memory constraints.
    /// </summary>
    bool ShouldApplyBackpressure();
    
    /// <summary>
    /// Get the current audit queue depth for memory monitoring.
    /// </summary>
    int GetAuditQueueDepth();
    
    /// <summary>
    /// Trigger memory optimization strategies (e.g., compact heap, trim working set).
    /// </summary>
    Task OptimizeMemoryAsync();
}
