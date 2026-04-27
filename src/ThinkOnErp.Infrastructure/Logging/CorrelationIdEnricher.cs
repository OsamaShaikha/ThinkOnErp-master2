using Serilog.Core;
using Serilog.Events;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.Infrastructure.Logging;

/// <summary>
/// Serilog enricher that adds correlation ID to all log entries.
/// Integrates with CorrelationContext to get the current correlation ID.
/// </summary>
public class CorrelationIdEnricher : ILogEventEnricher
{
    /// <summary>
    /// Enriches the log event with the current correlation ID from CorrelationContext.
    /// </summary>
    /// <param name="logEvent">The log event to enrich</param>
    /// <param name="propertyFactory">Factory for creating log event properties</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = CorrelationContext.Current;
        
        if (!string.IsNullOrEmpty(correlationId))
        {
            var property = propertyFactory.CreateProperty("CorrelationId", correlationId);
            logEvent.AddPropertyIfAbsent(property);
        }
    }
}
