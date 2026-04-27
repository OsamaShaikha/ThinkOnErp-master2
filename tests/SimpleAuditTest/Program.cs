using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities.Audit;

// Simple test to verify System.Threading.Channels functionality with audit events
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var logger = loggerFactory.CreateLogger<Program>();

logger.LogInformation("Starting simple audit channel test...");

// Create a bounded channel
var channelOptions = new BoundedChannelOptions(10)
{
    FullMode = BoundedChannelFullMode.Wait,
    SingleReader = true,
    SingleWriter = false
};

var channel = Channel.CreateBounded<AuditEvent>(channelOptions);

// Start a background task to process events
var processingTask = Task.Run(async () =>
{
    var eventCount = 0;
    await foreach (var auditEvent in channel.Reader.ReadAllAsync())
    {
        eventCount++;
        logger.LogInformation("Processed audit event #{Count}: {EventType} - {Action} - {EntityType} (CorrelationId: {CorrelationId})", 
            eventCount, auditEvent.GetType().Name, auditEvent.Action, auditEvent.EntityType, auditEvent.CorrelationId);
        
        // Simulate processing time
        await Task.Delay(100);
    }
    logger.LogInformation("Finished processing {Count} audit events", eventCount);
});

// Create and queue some test audit events
var events = new List<AuditEvent>
{
    new DataChangeAuditEvent
    {
        CorrelationId = "test-1",
        ActorType = "USER",
        ActorId = 123,
        Action = "UPDATE",
        EntityType = "User",
        EntityId = 456,
        OldValue = "{\"name\":\"John\"}",
        NewValue = "{\"name\":\"John Doe\"}"
    },
    new AuthenticationAuditEvent
    {
        CorrelationId = "test-2",
        ActorType = "USER",
        ActorId = 123,
        Action = "LOGIN",
        EntityType = "User",
        Success = true,
        TokenId = "token-123"
    },
    new ExceptionAuditEvent
    {
        CorrelationId = "test-3",
        ActorType = "SYSTEM",
        ActorId = 0,
        Action = "EXCEPTION",
        EntityType = "System",
        ExceptionType = "ValidationException",
        ExceptionMessage = "Test validation error",
        StackTrace = "Stack trace here...",
        Severity = "Error"
    }
};

// Queue the events
logger.LogInformation("Queuing {Count} audit events...", events.Count);
foreach (var auditEvent in events)
{
    await channel.Writer.WriteAsync(auditEvent);
    logger.LogDebug("Queued: {EventType} - {CorrelationId}", auditEvent.GetType().Name, auditEvent.CorrelationId);
}

// Signal completion and wait for processing to finish
logger.LogInformation("Signaling completion...");
channel.Writer.Complete();

await processingTask;

logger.LogInformation("Simple audit channel test completed successfully!");

// Test the channel health check pattern
logger.LogInformation("Testing health check pattern...");

var healthChannel = Channel.CreateBounded<string>(1);
var healthCheckResult = healthChannel.Writer.TryWrite("health-check");
logger.LogInformation("Health check write result: {Result}", healthCheckResult);

if (healthChannel.Reader.TryRead(out var healthMessage))
{
    logger.LogInformation("Health check message: {Message}", healthMessage);
}

logger.LogInformation("All tests completed successfully!");