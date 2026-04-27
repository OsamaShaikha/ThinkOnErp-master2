namespace ThinkOnErp.Domain.Models;

/// <summary>
/// Detailed memory usage metrics for monitoring and optimization.
/// Tracks heap sizes, GC statistics, and memory pressure indicators.
/// </summary>
public class MemoryMetrics
{
    /// <summary>
    /// Total memory allocated by the application (bytes)
    /// </summary>
    public long TotalAllocatedBytes { get; set; }
    
    /// <summary>
    /// Total available memory for the application (bytes)
    /// </summary>
    public long TotalAvailableBytes { get; set; }
    
    /// <summary>
    /// Memory usage percentage (0-100)
    /// </summary>
    public double MemoryUsagePercent => TotalAvailableBytes > 0 
        ? (double)TotalAllocatedBytes / TotalAvailableBytes * 100 
        : 0;
    
    /// <summary>
    /// Heap size for Generation 0 (bytes)
    /// </summary>
    public long Gen0HeapSizeBytes { get; set; }
    
    /// <summary>
    /// Heap size for Generation 1 (bytes)
    /// </summary>
    public long Gen1HeapSizeBytes { get; set; }
    
    /// <summary>
    /// Heap size for Generation 2 (bytes)
    /// </summary>
    public long Gen2HeapSizeBytes { get; set; }
    
    /// <summary>
    /// Large Object Heap size (bytes)
    /// </summary>
    public long LargeObjectHeapSizeBytes { get; set; }
    
    /// <summary>
    /// Total number of Gen0 collections since start
    /// </summary>
    public int Gen0CollectionCount { get; set; }
    
    /// <summary>
    /// Total number of Gen1 collections since start
    /// </summary>
    public int Gen1CollectionCount { get; set; }
    
    /// <summary>
    /// Total number of Gen2 collections since start
    /// </summary>
    public int Gen2CollectionCount { get; set; }
    
    /// <summary>
    /// GC collections per minute across all generations
    /// </summary>
    public double GcFrequencyPerMinute { get; set; }
    
    /// <summary>
    /// Memory allocation rate (bytes per second)
    /// </summary>
    public long AllocationRateBytesPerSecond { get; set; }
    
    /// <summary>
    /// Indicates if the system is under memory pressure
    /// </summary>
    public bool IsUnderMemoryPressure { get; set; }
    
    /// <summary>
    /// Memory pressure level (0-100, where 100 is critical)
    /// </summary>
    public int MemoryPressureLevel { get; set; }
    
    /// <summary>
    /// Total time spent in GC (milliseconds)
    /// </summary>
    public long TotalGcTimeMs { get; set; }
    
    /// <summary>
    /// Percentage of time spent in GC (0-100)
    /// </summary>
    public double GcTimePercent { get; set; }
    
    /// <summary>
    /// Number of pinned objects (objects that cannot be moved by GC)
    /// </summary>
    public long PinnedObjectCount { get; set; }
    
    /// <summary>
    /// Fragmented bytes in the heap
    /// </summary>
    public long FragmentedBytes { get; set; }
    
    /// <summary>
    /// Timestamp when metrics were collected
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Recommended actions to optimize memory usage
    /// </summary>
    public List<string> OptimizationRecommendations { get; set; } = new();
}

/// <summary>
/// Memory pressure detection result with severity and recommendations.
/// </summary>
public class MemoryPressureInfo
{
    /// <summary>
    /// Severity level of memory pressure
    /// </summary>
    public MemoryPressureSeverity Severity { get; set; }
    
    /// <summary>
    /// Pressure level (0-100)
    /// </summary>
    public int PressureLevel { get; set; }
    
    /// <summary>
    /// Description of the memory pressure situation
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Recommended actions to reduce memory pressure
    /// </summary>
    public List<string> Recommendations { get; set; } = new();
    
    /// <summary>
    /// Indicates if immediate action is required
    /// </summary>
    public bool RequiresImmediateAction { get; set; }
}

/// <summary>
/// Memory pressure severity levels
/// </summary>
public enum MemoryPressureSeverity
{
    /// <summary>
    /// No memory pressure, system operating normally
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Low memory pressure, monitoring recommended
    /// </summary>
    Low = 1,
    
    /// <summary>
    /// Moderate memory pressure, optimization recommended
    /// </summary>
    Moderate = 2,
    
    /// <summary>
    /// High memory pressure, immediate action recommended
    /// </summary>
    High = 3,
    
    /// <summary>
    /// Critical memory pressure, system at risk
    /// </summary>
    Critical = 4
}
