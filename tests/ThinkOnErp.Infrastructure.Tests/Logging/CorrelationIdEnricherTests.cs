using Serilog.Events;
using ThinkOnErp.Infrastructure.Logging;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Logging;

/// <summary>
/// Unit tests for CorrelationIdEnricher to verify it correctly adds correlation IDs to log events.
/// </summary>
public class CorrelationIdEnricherTests
{
    [Fact]
    public void Enrich_WhenCorrelationIdExists_AddsCorrelationIdProperty()
    {
        // Arrange
        CorrelationContext.Clear();
        var expectedCorrelationId = "test-correlation-id-123";
        CorrelationContext.Current = expectedCorrelationId;

        var enricher = new CorrelationIdEnricher();
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestLogEventPropertyFactory());

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("CorrelationId"));
        var correlationIdProperty = logEvent.Properties["CorrelationId"];
        Assert.Equal($"\"{expectedCorrelationId}\"", correlationIdProperty.ToString());
    }

    [Fact]
    public void Enrich_WhenCorrelationIdIsNull_DoesNotAddProperty()
    {
        // Arrange
        CorrelationContext.Clear();
        CorrelationContext.Current = null;

        var enricher = new CorrelationIdEnricher();
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestLogEventPropertyFactory());

        // Assert
        Assert.False(logEvent.Properties.ContainsKey("CorrelationId"));
    }

    [Fact]
    public void Enrich_WhenCorrelationIdIsEmpty_DoesNotAddProperty()
    {
        // Arrange
        CorrelationContext.Clear();
        CorrelationContext.Current = string.Empty;

        var enricher = new CorrelationIdEnricher();
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestLogEventPropertyFactory());

        // Assert
        Assert.False(logEvent.Properties.ContainsKey("CorrelationId"));
    }

    [Fact]
    public void Enrich_WhenPropertyAlreadyExists_DoesNotOverwrite()
    {
        // Arrange
        CorrelationContext.Clear();
        CorrelationContext.Current = "new-correlation-id";

        var enricher = new CorrelationIdEnricher();
        var logEvent = CreateLogEvent();
        
        // Add existing property
        var existingValue = "existing-correlation-id";
        logEvent.AddPropertyIfAbsent(new LogEventProperty("CorrelationId", 
            new ScalarValue(existingValue)));

        // Act
        enricher.Enrich(logEvent, new TestLogEventPropertyFactory());

        // Assert
        Assert.True(logEvent.Properties.ContainsKey("CorrelationId"));
        var correlationIdProperty = logEvent.Properties["CorrelationId"];
        Assert.Equal($"\"{existingValue}\"", correlationIdProperty.ToString());
    }

    [Fact]
    public void Enrich_WithMultipleLogEvents_UsesCurrentCorrelationId()
    {
        // Arrange
        CorrelationContext.Clear();
        var enricher = new CorrelationIdEnricher();

        // First log event with first correlation ID
        var firstCorrelationId = "first-id";
        CorrelationContext.Current = firstCorrelationId;
        var firstLogEvent = CreateLogEvent();
        enricher.Enrich(firstLogEvent, new TestLogEventPropertyFactory());

        // Second log event with second correlation ID
        var secondCorrelationId = "second-id";
        CorrelationContext.Current = secondCorrelationId;
        var secondLogEvent = CreateLogEvent();
        enricher.Enrich(secondLogEvent, new TestLogEventPropertyFactory());

        // Assert
        Assert.Equal($"\"{firstCorrelationId}\"", 
            firstLogEvent.Properties["CorrelationId"].ToString());
        Assert.Equal($"\"{secondCorrelationId}\"", 
            secondLogEvent.Properties["CorrelationId"].ToString());
    }

    private static LogEvent CreateLogEvent()
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            MessageTemplate.Empty,
            Array.Empty<LogEventProperty>());
    }

    private class TestLogEventPropertyFactory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }
}
