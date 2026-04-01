using System.Net;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.API.Middleware;
using ThinkOnErp.Application.Common;
using Xunit;

namespace ThinkOnErp.API.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock;
    private readonly ExceptionHandlingMiddleware _middleware;

    public ExceptionHandlingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
    }

    [Fact]
    public async Task InvokeAsync_WhenNoException_ShouldCallNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        RequestDelegate next = (HttpContext ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationException_ShouldReturn400WithErrors()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("Field1", "Field1 is required"),
            new ValidationFailure("Field2", "Field2 must be greater than 0")
        };
        var validationException = new ValidationException(validationFailures);

        RequestDelegate next = (HttpContext ctx) => throw validationException;

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<ApiResponse<object>>(responseBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("One or more validation errors occurred", response.Message);
        Assert.NotNull(response.Errors);
        Assert.Equal(2, response.Errors.Count);
        Assert.Contains("Field1 is required", response.Errors);
        Assert.Contains("Field2 must be greater than 0", response.Errors);
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationException_ShouldLogError()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("Field1", "Field1 is required")
        };
        var validationException = new ValidationException(validationFailures);

        RequestDelegate next = (HttpContext ctx) => throw validationException;

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenGenericException_ShouldReturn500WithGenericMessage()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var exception = new Exception("Some internal error");

        RequestDelegate next = (HttpContext ctx) => throw exception;

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<ApiResponse<object>>(responseBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Equal(500, response.StatusCode);
        Assert.Equal("An unexpected error occurred. Please try again later", response.Message);
        Assert.Null(response.Errors);
    }

    [Fact]
    public async Task InvokeAsync_WhenGenericException_ShouldNotExposeStackTrace()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var exception = new Exception("Some internal error with stack trace");

        RequestDelegate next = (HttpContext ctx) => throw exception;

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();

        // Verify that the response does not contain stack trace information
        Assert.DoesNotContain("at ", responseBody); // Stack traces typically contain "at" for method calls
        Assert.DoesNotContain("StackTrace", responseBody);
        Assert.DoesNotContain("Some internal error with stack trace", responseBody); // Original exception message should not be exposed
    }

    [Fact]
    public async Task InvokeAsync_WhenGenericException_ShouldLogError()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var exception = new Exception("Some internal error");

        RequestDelegate next = (HttpContext ctx) => throw exception;

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenDivideByZeroException_ShouldReturn500()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        RequestDelegate next = (HttpContext ctx) => throw new DivideByZeroException("Division by zero");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_WhenNullReferenceException_ShouldReturn500()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        RequestDelegate next = (HttpContext ctx) => throw new NullReferenceException("Null reference");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_ResponseShouldHaveTimestamp()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        RequestDelegate next = (HttpContext ctx) => throw new Exception("Test exception");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        var beforeTime = DateTime.UtcNow;
        await middleware.InvokeAsync(context);
        var afterTime = DateTime.UtcNow;

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<ApiResponse<object>>(responseBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(response);
        Assert.True(response.Timestamp >= beforeTime && response.Timestamp <= afterTime);
    }

    [Fact]
    public async Task InvokeAsync_ResponseShouldHaveTraceId()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        RequestDelegate next = (HttpContext ctx) => throw new Exception("Test exception");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<ApiResponse<object>>(responseBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(response);
        Assert.False(string.IsNullOrEmpty(response.TraceId));
        Assert.True(Guid.TryParse(response.TraceId, out _)); // TraceId should be a valid GUID
    }
}
