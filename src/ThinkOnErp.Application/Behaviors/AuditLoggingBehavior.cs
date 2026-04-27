using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Behaviors;

/// <summary>
/// Pipeline behavior that automatically captures audit events for all commands executed through MediatR.
/// Captures request state before command execution, response state after execution, and integrates with IAuditLogger.
/// Handles exceptions gracefully without breaking the pipeline.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAuditLogger _auditLogger;
    private readonly IAuditContextProvider _auditContextProvider;
    private readonly IExceptionCategorizationService _exceptionCategorization;
    private readonly ILogger<AuditLoggingBehavior<TRequest, TResponse>> _logger;

    public AuditLoggingBehavior(
        IAuditLogger auditLogger,
        IAuditContextProvider auditContextProvider,
        IExceptionCategorizationService exceptionCategorization,
        ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger)
    {
        _auditLogger = auditLogger;
        _auditContextProvider = auditContextProvider;
        _exceptionCategorization = exceptionCategorization;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only audit commands (not queries)
        var requestName = typeof(TRequest).Name;
        if (!IsCommand(requestName))
        {
            return await next();
        }

        // Capture request state before execution
        var requestState = CaptureRequestState(request);
        var correlationId = _auditContextProvider.GetCorrelationId();

        TResponse? response = default;
        Exception? exception = null;

        try
        {
            // Execute the command
            response = await next();

            // Capture response state after execution
            var responseState = CaptureResponseState(response);

            // Log audit event asynchronously (fire-and-forget)
            _ = LogAuditEventAsync(requestName, requestState, responseState, correlationId, null, cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            exception = ex;

            // Log audit event with exception (fire-and-forget)
            _ = LogAuditEventAsync(requestName, requestState, null, correlationId, ex, cancellationToken);

            throw; // Re-throw to maintain exception flow
        }
    }

    /// <summary>
    /// Determines if the request is a command (should be audited) or a query (should not be audited).
    /// Commands typically have names ending with "Command".
    /// </summary>
    private bool IsCommand(string requestName)
    {
        return requestName.EndsWith("Command", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Captures the request state before command execution.
    /// Serializes the request object to JSON for audit logging.
    /// </summary>
    private string CaptureRequestState(TRequest request)
    {
        try
        {
            return JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize request state for {RequestType}", typeof(TRequest).Name);
            return "{}";
        }
    }

    /// <summary>
    /// Captures the response state after command execution.
    /// Serializes the response object to JSON for audit logging.
    /// </summary>
    private string? CaptureResponseState(TResponse? response)
    {
        if (response == null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize response state for {ResponseType}", typeof(TResponse).Name);
            return null;
        }
    }

    /// <summary>
    /// Logs the audit event asynchronously without blocking the pipeline.
    /// Extracts entity ID from response, determines action type from command name,
    /// and integrates with IAuditLogger service.
    /// </summary>
    private async Task LogAuditEventAsync(
        string requestName,
        string requestState,
        string? responseState,
        string correlationId,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract actor information from audit context provider
            var actorId = _auditContextProvider.GetActorId();
            var actorType = _auditContextProvider.GetActorType();
            var companyId = _auditContextProvider.GetCompanyId();
            var branchId = _auditContextProvider.GetBranchId();
            var ipAddress = _auditContextProvider.GetIpAddress();
            var userAgent = _auditContextProvider.GetUserAgent();

            // Extract entity information
            var entityType = ExtractEntityType(requestName);
            var entityId = ExtractEntityId(responseState);
            var action = DetermineAction(requestName);

            // Create appropriate audit event based on whether there was an exception
            if (exception != null)
            {
                var exceptionEvent = new ExceptionAuditEvent
                {
                    CorrelationId = correlationId,
                    ActorType = actorType,
                    ActorId = actorId,
                    CompanyId = companyId,
                    BranchId = branchId,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Timestamp = DateTime.UtcNow,
                    ExceptionType = exception.GetType().Name,
                    ExceptionMessage = exception.Message,
                    StackTrace = exception.StackTrace,
                    InnerException = exception.InnerException?.Message,
                    Severity = _exceptionCategorization.DetermineSeverity(exception)
                };

                await _auditLogger.LogExceptionAsync(exceptionEvent, cancellationToken);
            }
            else
            {
                var dataChangeEvent = new DataChangeAuditEvent
                {
                    CorrelationId = correlationId,
                    ActorType = actorType,
                    ActorId = actorId,
                    CompanyId = companyId,
                    BranchId = branchId,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Timestamp = DateTime.UtcNow,
                    OldValue = action == "UPDATE" ? requestState : null,
                    NewValue = action == "DELETE" ? null : responseState
                };

                await _auditLogger.LogDataChangeAsync(dataChangeEvent, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - audit logging should never break the pipeline
            _logger.LogError(ex, "Failed to log audit event for {RequestName}", requestName);
        }
    }

    /// <summary>
    /// Extracts the entity type from the command name.
    /// Example: "CreateUserCommand" -> "User"
    /// </summary>
    private string ExtractEntityType(string requestName)
    {
        // Remove "Command" suffix
        var withoutCommand = requestName.Replace("Command", "", StringComparison.OrdinalIgnoreCase);

        // Remove action prefix (Create, Update, Delete, etc.)
        var actionPrefixes = new[] { "Create", "Update", "Delete", "Add", "Remove", "Assign", "Unassign", "Reset", "Change", "Force", "Upload", "Download" };
        foreach (var prefix in actionPrefixes)
        {
            if (withoutCommand.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return withoutCommand.Substring(prefix.Length);
            }
        }

        return withoutCommand;
    }

    /// <summary>
    /// Extracts the entity ID from the command response.
    /// Handles various response types (long, int, custom objects with Id property).
    /// </summary>
    private long? ExtractEntityId(string? responseState)
    {
        if (string.IsNullOrEmpty(responseState))
        {
            return null;
        }

        try
        {
            // Try to parse as a simple numeric ID
            if (long.TryParse(responseState, out var id))
            {
                return id;
            }

            // Try to deserialize as JSON and extract Id property
            using var doc = JsonDocument.Parse(responseState);
            if (doc.RootElement.TryGetProperty("id", out var idProperty) ||
                doc.RootElement.TryGetProperty("Id", out idProperty))
            {
                if (idProperty.TryGetInt64(out var entityId))
                {
                    return entityId;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Determines the action type from the command name.
    /// Example: "CreateUserCommand" -> "INSERT", "UpdateUserCommand" -> "UPDATE"
    /// </summary>
    private string DetermineAction(string requestName)
    {
        if (requestName.StartsWith("Create", StringComparison.OrdinalIgnoreCase) ||
            requestName.StartsWith("Add", StringComparison.OrdinalIgnoreCase))
        {
            return "INSERT";
        }

        if (requestName.StartsWith("Update", StringComparison.OrdinalIgnoreCase) ||
            requestName.StartsWith("Change", StringComparison.OrdinalIgnoreCase) ||
            requestName.StartsWith("Assign", StringComparison.OrdinalIgnoreCase) ||
            requestName.StartsWith("Reset", StringComparison.OrdinalIgnoreCase))
        {
            return "UPDATE";
        }

        if (requestName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ||
            requestName.StartsWith("Remove", StringComparison.OrdinalIgnoreCase))
        {
            return "DELETE";
        }

        // Default to UPDATE for other commands
        return "UPDATE";
    }
}
