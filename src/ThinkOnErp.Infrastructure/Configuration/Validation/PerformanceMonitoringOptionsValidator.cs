using Microsoft.Extensions.Options;

namespace ThinkOnErp.Infrastructure.Configuration.Validation;

/// <summary>
/// Validator for PerformanceMonitoringOptions that performs complex validation logic
/// beyond what data annotations can express.
/// </summary>
public class PerformanceMonitoringOptionsValidator : IValidateOptions<PerformanceMonitoringOptions>
{
    public ValidateOptionsResult Validate(string? name, PerformanceMonitoringOptions options)
    {
        var failures = new List<string>();

        // Validate slow query threshold is less than slow request threshold
        if (options.SlowQueryThresholdMs >= options.SlowRequestThresholdMs)
        {
            failures.Add($"SlowQueryThresholdMs ({options.SlowQueryThresholdMs}ms) should be less than SlowRequestThresholdMs ({options.SlowRequestThresholdMs}ms) since queries are part of requests.");
        }

        // Validate aggregation interval is reasonable relative to sliding window
        var slidingWindowSeconds = options.SlidingWindowDurationMinutes * 60;
        if (options.MetricsAggregationIntervalSeconds > slidingWindowSeconds)
        {
            failures.Add($"MetricsAggregationIntervalSeconds ({options.MetricsAggregationIntervalSeconds}s) should not exceed SlidingWindowDurationMinutes ({options.SlidingWindowDurationMinutes} minutes = {slidingWindowSeconds}s).");
        }

        // Validate threshold percentages are reasonable
        if (options.CpuThresholdPercent <= 50)
        {
            failures.Add("CpuThresholdPercent should be greater than 50% to avoid false alerts during normal operation.");
        }

        if (options.MemoryThresholdPercent <= 50)
        {
            failures.Add("MemoryThresholdPercent should be greater than 50% to avoid false alerts during normal operation.");
        }

        // Validate error rate threshold is reasonable
        if (options.ErrorRateThresholdPercent > 20)
        {
            failures.Add("ErrorRateThresholdPercent should not exceed 20% as higher error rates indicate severe system issues.");
        }

        // Validate retention limits are reasonable
        if (options.MaxSlowRequestsRetained < 100)
        {
            failures.Add("MaxSlowRequestsRetained should be at least 100 to provide meaningful performance analysis.");
        }

        if (options.MaxSlowQueriesRetained < 100)
        {
            failures.Add("MaxSlowQueriesRetained should be at least 100 to provide meaningful performance analysis.");
        }

        // Validate percentile calculations are enabled if persistence is enabled
        if ((options.PersistSlowRequests || options.PersistSlowQueries) && !options.EnablePercentileCalculations)
        {
            failures.Add("EnablePercentileCalculations should be true when PersistSlowRequests or PersistSlowQueries is enabled for meaningful metrics.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
