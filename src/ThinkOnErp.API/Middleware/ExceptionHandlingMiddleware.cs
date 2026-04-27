using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.API.Middleware;

/// <summary>
/// Global exception handling middleware that catches all unhandled exceptions,
/// logs them using Serilog, and returns formatted ApiResponse with appropriate status codes.
/// Supports custom domain exceptions with specific error codes and context.
/// Integrates with audit logging system to capture exceptions with full context.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IAuditLogger _auditLogger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IAuditLogger auditLogger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _next = next;
        _logger = logger;
        _auditLogger = auditLogger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Log exception to audit system before handling
        await LogExceptionToAuditSystemAsync(context, exception);

        // Set content type to application/json
        context.Response.ContentType = "application/json";

        ApiResponse<object> response;
        int statusCode;

        // Handle different exception types
        switch (exception)
        {
            case ValidationException validationException:
                statusCode = (int)HttpStatusCode.BadRequest;
                var errors = validationException.Errors
                    .Select(e => e.ErrorMessage)
                    .ToList();

                _logger.LogWarning(validationException, "Validation failed: {Errors}", string.Join(", ", errors));

                response = ApiResponse<object>.CreateFailure(
                    "One or more validation errors occurred",
                    errors,
                    statusCode);
                break;

            case TicketNotFoundException notFoundException:
                statusCode = (int)HttpStatusCode.NotFound;
                _logger.LogWarning(notFoundException, "Ticket not found: {TicketId}", notFoundException.TicketId);

                response = ApiResponse<object>.CreateFailure(
                    notFoundException.Message,
                    new List<string> { notFoundException.ErrorCode },
                    statusCode);
                break;

            case UnauthorizedTicketAccessException unauthorizedException:
                statusCode = (int)HttpStatusCode.Forbidden;
                _logger.LogWarning(unauthorizedException, "Unauthorized ticket access: User {UserId} attempted to access Ticket {TicketId}", 
                    unauthorizedException.UserId, unauthorizedException.TicketId);

                response = ApiResponse<object>.CreateFailure(
                    unauthorizedException.Message,
                    new List<string> { unauthorizedException.ErrorCode },
                    statusCode);
                break;

            case InvalidStatusTransitionException statusException:
                statusCode = (int)HttpStatusCode.BadRequest;
                _logger.LogWarning(statusException, "Invalid status transition: {CurrentStatus} -> {NewStatus}", 
                    statusException.CurrentStatusId, statusException.NewStatusId);

                response = ApiResponse<object>.CreateFailure(
                    statusException.Message,
                    new List<string> { statusException.ErrorCode },
                    statusCode);
                break;

            case AttachmentSizeExceededException sizeException:
                statusCode = (int)HttpStatusCode.BadRequest;
                _logger.LogWarning(sizeException, "Attachment size exceeded: {FileSize} > {MaxSize}", 
                    sizeException.FileSize, sizeException.MaxSize);

                response = ApiResponse<object>.CreateFailure(
                    sizeException.Message,
                    new List<string> { sizeException.ErrorCode },
                    statusCode);
                break;

            case InvalidFileTypeException fileTypeException:
                statusCode = (int)HttpStatusCode.BadRequest;
                _logger.LogWarning(fileTypeException, "Invalid file type: {FileName} ({MimeType})", 
                    fileTypeException.FileName, fileTypeException.MimeType);

                response = ApiResponse<object>.CreateFailure(
                    fileTypeException.Message,
                    new List<string> { fileTypeException.ErrorCode },
                    statusCode);
                break;

            case DatabaseConnectionException dbException:
                statusCode = (int)HttpStatusCode.ServiceUnavailable;
                _logger.LogError(dbException, "Database connection error during operation: {Operation}", dbException.Operation);

                response = ApiResponse<object>.CreateFailure(
                    "A database error occurred. Please try again later.",
                    new List<string> { dbException.ErrorCode },
                    statusCode);
                break;

            case ExternalServiceException serviceException:
                statusCode = (int)HttpStatusCode.ServiceUnavailable;
                _logger.LogError(serviceException, "External service error: {ServiceName}", serviceException.ServiceName);

                response = ApiResponse<object>.CreateFailure(
                    "An external service is temporarily unavailable. Please try again later.",
                    new List<string> { serviceException.ErrorCode },
                    statusCode);
                break;

            case ConcurrentModificationException concurrencyException:
                statusCode = (int)HttpStatusCode.Conflict;
                _logger.LogWarning(concurrencyException, "Concurrent modification detected: {EntityType} {EntityId}", 
                    concurrencyException.EntityType, concurrencyException.EntityId);

                response = ApiResponse<object>.CreateFailure(
                    concurrencyException.Message,
                    new List<string> { concurrencyException.ErrorCode },
                    statusCode);
                break;

            case DomainException domainException:
                // Generic domain exception handler
                statusCode = (int)HttpStatusCode.BadRequest;
                _logger.LogWarning(domainException, "Domain exception: {ErrorCode} - {Message}", 
                    domainException.ErrorCode, domainException.Message);

                response = ApiResponse<object>.CreateFailure(
                    domainException.Message,
                    new List<string> { domainException.ErrorCode },
                    statusCode);
                break;

            default:
                // Handle all other exceptions as 500 Internal Server Error
                statusCode = (int)HttpStatusCode.InternalServerError;
                _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

                response = ApiResponse<object>.CreateFailure(
                    "An unexpected error occurred. Please try again later",
                    null,
                    statusCode);
                break;
        }

        context.Response.StatusCode = statusCode;

        // Serialize and write the response
        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }

    /// <summary>
    /// Logs exception to the audit system with full context
    /// </summary>
    private async Task LogExceptionToAuditSystemAsync(HttpContext context, Exception exception)
    {
        try
        {
            // Extract user information from claims
            var userId = GetUserIdFromClaims(context.User);
            var companyId = GetCompanyIdFromClaims(context.User);
            var actorType = GetActorTypeFromClaims(context.User);

            // Get correlation ID from context
            var correlationId = CorrelationContext.Current ?? Guid.NewGuid().ToString();

            // Extract request context
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            var endpoint = $"{context.Request.Method} {context.Request.Path}";

            // Determine entity type and action from exception
            var (entityType, entityId) = ExtractEntityInfoFromException(exception);
            var action = DetermineActionFromException(exception);

            // Resolve scoped service to determine severity
            string severity;
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var exceptionCategorization = scope.ServiceProvider.GetRequiredService<IExceptionCategorizationService>();
                severity = exceptionCategorization.DetermineSeverity(exception);
            }

            // Create exception audit event
            var auditEvent = new ExceptionAuditEvent
            {
                CorrelationId = correlationId,
                ActorType = actorType,
                ActorId = userId,
                CompanyId = companyId,
                BranchId = null, // Branch ID not available in claims, could be added if needed
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Timestamp = DateTime.UtcNow,
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                ExceptionMessage = exception.Message,
                StackTrace = exception.StackTrace ?? string.Empty,
                InnerException = exception.InnerException?.ToString(),
                Severity = severity
            };

            // Log to audit system (fire and forget - don't block exception handling)
            await _auditLogger.LogExceptionAsync(auditEvent);
        }
        catch (Exception ex)
        {
            // If audit logging fails, log the failure but don't throw
            _logger.LogError(ex, "Failed to log exception to audit system");
        }
    }

    /// <summary>
    /// Extracts user ID from JWT claims
    /// </summary>
    private long GetUserIdFromClaims(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? user.FindFirst("sub")?.Value
                       ?? user.FindFirst("userId")?.Value;

        return long.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    /// <summary>
    /// Extracts company ID from JWT claims
    /// </summary>
    private long? GetCompanyIdFromClaims(ClaimsPrincipal user)
    {
        var companyIdClaim = user.FindFirst("companyId")?.Value;
        return long.TryParse(companyIdClaim, out var companyId) ? companyId : null;
    }

    /// <summary>
    /// Extracts actor type from JWT claims
    /// </summary>
    private string GetActorTypeFromClaims(ClaimsPrincipal user)
    {
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            return "ANONYMOUS";
        }

        var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value
                     ?? user.FindFirst("role")?.Value;

        return roleClaim?.ToUpperInvariant() switch
        {
            "SUPERADMIN" => "SUPER_ADMIN",
            "COMPANYADMIN" => "COMPANY_ADMIN",
            "USER" => "USER",
            _ => "USER"
        };
    }

    /// <summary>
    /// Extracts entity information from exception if available
    /// </summary>
    private (string entityType, long? entityId) ExtractEntityInfoFromException(Exception exception)
    {
        return exception switch
        {
            TicketNotFoundException ticketEx => ("Ticket", ticketEx.TicketId),
            UnauthorizedTicketAccessException ticketAccessEx => ("Ticket", ticketAccessEx.TicketId),
            ConcurrentModificationException concurrencyEx => (concurrencyEx.EntityType, concurrencyEx.EntityId),
            _ => ("Unknown", null)
        };
    }

    /// <summary>
    /// Determines action type from exception
    /// </summary>
    private string DetermineActionFromException(Exception exception)
    {
        return exception switch
        {
            ValidationException => "VALIDATION_ERROR",
            TicketNotFoundException => "NOT_FOUND",
            UnauthorizedTicketAccessException => "AUTHORIZATION_ERROR",
            InvalidStatusTransitionException => "INVALID_STATUS_TRANSITION",
            AttachmentSizeExceededException => "ATTACHMENT_SIZE_EXCEEDED",
            InvalidFileTypeException => "INVALID_FILE_TYPE",
            DatabaseConnectionException => "DATABASE_ERROR",
            ExternalServiceException => "EXTERNAL_SERVICE_ERROR",
            ConcurrentModificationException => "CONCURRENT_MODIFICATION",
            DomainException => "DOMAIN_ERROR",
            _ => "UNHANDLED_EXCEPTION"
        };
    }
}
