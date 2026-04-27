using System.Net;
using System.Text.Json;
using FluentValidation;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Exceptions;

namespace ThinkOnErp.API.Middleware;

/// <summary>
/// Global exception handling middleware that catches all unhandled exceptions,
/// logs them using Serilog, and returns formatted ApiResponse with appropriate status codes.
/// Supports custom domain exceptions with specific error codes and context.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
}
