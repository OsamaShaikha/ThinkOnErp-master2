namespace ThinkOnErp.Domain.Entities;

public class SysPerformanceMetric
{
    public long Id { get; set; }
    public string EndpointPath { get; set; } = string.Empty;
    public DateTime HourTimestamp { get; set; }
    public int RequestCount { get; set; }
    public decimal AvgExecutionTimeMs { get; set; }
    public decimal MinExecutionTimeMs { get; set; }
    public decimal MaxExecutionTimeMs { get; set; }
    public decimal P50ExecutionTimeMs { get; set; }
    public decimal P95ExecutionTimeMs { get; set; }
    public decimal P99ExecutionTimeMs { get; set; }
    public decimal AvgDatabaseTimeMs { get; set; }
    public decimal AvgQueryCount { get; set; }
    public decimal ErrorCount { get; set; }
    public DateTime CreationDate { get; set; }
}
