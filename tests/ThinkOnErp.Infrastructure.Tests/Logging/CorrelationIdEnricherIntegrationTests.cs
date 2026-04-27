using Serilog;
using Serilog.Events;
using ThinkOnErp.Infrastructure.Logging;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Logging;

/// <summary>
/// Integration tests for CorrelationIdEnricher with actual Serilog logger.
/// </summary>
public class CorrelationIdEnricherIntegrationTests : IDisposable
{
    private readonly List<LogEvent> _logEvents = new();
    private readonly ILogger _logger;

    public CorrelationIdEnricherIntegrationTests()
    {
        // Create a logger with the CorrelationIdEnricher
        _logger = new LoggerConfiguration()
            .Enrich.With<CorrelationIdEnricher>()
            .WriteTo.Sink(new TestSink(_logEvents))
            .CreateLogger();
    }

    [Fact]
    public void Logger_WithCorrelationIdEnricher_AddsCorrelationIdToLogEvents()
    {
        // Arrange
        CorrelationContext.Clear();
        var expectedCorrelationId = "integration-test-id-123";
        CorrelationContext.Current = expectedCorrelationId;

        // Act
        _logger.Information("Test log message");

        // Assert
        Assert.Single(_logEvents);
        var logEvent = _logEvents[0];
        Assert.True(logEvent.Properties.ContainsKey("CorrelationId"));
        Assert.Equal($"\"{expectedCorrelationId}\"", logEvent.Properties["CorrelationId"].ToString());
    }

    [Fact]
    public void Logger_WithoutCorrelationId_DoesNotAddProperty()
    {
        // Arrange
        CorrelationContext.Clear();
        CorrelationContext.Current = null;

        // Act
        _logger.Information("Test log message without correlation ID");

        // Assert
        Assert.Single(_logEvents);
        var logEvent = _logEvents[0];
        Assert.False(logEvent.Properties.ContainsKey("CorrelationId"));
    }

    [Fact]
    public void Logger_WithMultipleMessages_UsesCurrentCorrelationId()
    {
        // Arrange
        CorrelationContext.Clear();

        // Act - First message with first correlation ID
        var firstId = "first-correlation-id";
        CorrelationContext.Current = firstId;
        _logger.Information("First message");

        // Act - Second message with second correlation ID
        var secondId = "second-correlation-id";
        CorrelationContext.Current = secondId;
        _logger.Information("Second message");

        // Assert
        Assert.Equal(2, _logEvents.Count);
        Assert.Equal($"\"{firstId}\"", _logEvents[0].Properties["CorrelationId"].ToString());
        Assert.Equal($"\"{secondId}\"", _logEvents[1].Properties["CorrelationId"].ToString());
    }

    public void Dispose()
    {
        (_logger as IDisposable)?.Dispose();
        _logEvents.Clear();
    }

    private class TestSink : Serilog.Core.ILogEventSink
    {
        private readonly List<LogEvent> _events;

        public TestSink(List<LogEvent> events)
        {
            _events = events;
        }

        public void Emit(LogEvent logEvent)
        {
            _events.Add(logEvent);
        }
    }
}
