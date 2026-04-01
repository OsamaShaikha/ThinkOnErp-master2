using System.Net;
using System.Text.Json;
using FluentValidation;
using ThinkOnErp.Application.Common;

namespace ThinkOnErp.API.Middleware;

/// <summary>
/// Global exception handling middleware that catches all unhandled exceptions,
/// logs them using Serilog, and returns formatted ApiResponse with appropriate status codes.
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
        // Log the exception with full details
        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        // Set content type to application/json
        context.Response.ContentType = "application/json";

        ApiResponse<object> response;

        // Handle ValidationException specifically
        if (exception is ValidationException validationException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

            var errors = validationException.Errors
                .Select(e => e.ErrorMessage)
                .ToList();

            response = ApiResponse<object>.CreateFailure(
                "One or more validation errors occurred",
                errors,
                (int)HttpStatusCode.BadRequest);
        }
        else
        {
            // Handle all other exceptions as 500 Internal Server Error
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            response = ApiResponse<object>.CreateFailure(
                "An unexpected error occurred. Please try again later",
                null,
                (int)HttpStatusCode.InternalServerError);
        }

        // Serialize and write the response
        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}
