using ThinkOnErp.Application.Common;
using Xunit;

namespace ThinkOnErp.API.Tests.Common;

/// <summary>
/// Unit tests for ApiResponse wrapper
/// </summary>
public class ApiResponseUnitTests
{
    [Fact]
    public void CreateSuccess_FactoryMethod_CreatesResponseWithSuccessTrue()
    {
        // Arrange
        var data = "Test Data";
        var message = "Operation successful";

        // Act
        var response = ApiResponse<string>.CreateSuccess(data, message);

        // Assert
        Assert.True(response.Success);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(message, response.Message);
        Assert.Equal(data, response.Data);
    }

    [Fact]
    public void CreateFailure_FactoryMethod_CreatesResponseWithSuccessFalse()
    {
        // Arrange
        var message = "Operation failed";
        var errors = new List<string> { "Error 1", "Error 2" };

        // Act
        var response = ApiResponse<string>.CreateFailure(message, errors);

        // Assert
        Assert.False(response.Success);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(message, response.Message);
        Assert.Null(response.Data);
        Assert.Equal(errors, response.Errors);
    }

    [Fact]
    public void CreateSuccess_IncludesDataPayload()
    {
        // Arrange
        var data = new { Id = 1, Name = "Test" };
        var message = "Success";

        // Act
        var response = ApiResponse<object>.CreateSuccess(data, message);

        // Assert
        Assert.NotNull(response.Data);
        Assert.Equal(data, response.Data);
    }

    [Fact]
    public void CreateFailure_IncludesErrorsArray()
    {
        // Arrange
        var message = "Validation failed";
        var errors = new List<string> { "Field1 is required", "Field2 is invalid" };

        // Act
        var response = ApiResponse<object>.CreateFailure(message, errors);

        // Assert
        Assert.NotNull(response.Errors);
        Assert.Equal(2, response.Errors.Count);
        Assert.Contains("Field1 is required", response.Errors);
        Assert.Contains("Field2 is invalid", response.Errors);
    }

    [Fact]
    public void CreateSuccess_IncludesTimestamp()
    {
        // Arrange
        var data = "Test";
        var message = "Success";
        var beforeCreation = DateTime.UtcNow;

        // Act
        var response = ApiResponse<string>.CreateSuccess(data, message);
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(response.Timestamp >= beforeCreation);
        Assert.True(response.Timestamp <= afterCreation);
    }

    [Fact]
    public void CreateFailure_IncludesTimestamp()
    {
        // Arrange
        var message = "Failure";
        var beforeCreation = DateTime.UtcNow;

        // Act
        var response = ApiResponse<string>.CreateFailure(message);
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(response.Timestamp >= beforeCreation);
        Assert.True(response.Timestamp <= afterCreation);
    }

    [Fact]
    public void CreateSuccess_IncludesTraceId()
    {
        // Arrange
        var data = "Test";
        var message = "Success";

        // Act
        var response = ApiResponse<string>.CreateSuccess(data, message);

        // Assert
        Assert.NotNull(response.TraceId);
        Assert.NotEmpty(response.TraceId);
    }

    [Fact]
    public void CreateFailure_IncludesTraceId()
    {
        // Arrange
        var message = "Failure";

        // Act
        var response = ApiResponse<string>.CreateFailure(message);

        // Assert
        Assert.NotNull(response.TraceId);
        Assert.NotEmpty(response.TraceId);
    }

    [Fact]
    public void CreateSuccess_WithCustomStatusCode_UsesProvidedStatusCode()
    {
        // Arrange
        var data = "Test";
        var message = "Created";
        var statusCode = 201;

        // Act
        var response = ApiResponse<string>.CreateSuccess(data, message, statusCode);

        // Assert
        Assert.Equal(201, response.StatusCode);
    }

    [Fact]
    public void CreateFailure_WithCustomStatusCode_UsesProvidedStatusCode()
    {
        // Arrange
        var message = "Not Found";
        var statusCode = 404;

        // Act
        var response = ApiResponse<string>.CreateFailure(message, null, statusCode);

        // Assert
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public void MultipleResponses_HaveUniqueTraceIds()
    {
        // Arrange & Act
        var response1 = ApiResponse<string>.CreateSuccess("Data1", "Message1");
        var response2 = ApiResponse<string>.CreateSuccess("Data2", "Message2");
        var response3 = ApiResponse<string>.CreateFailure("Message3");

        // Assert
        Assert.NotEqual(response1.TraceId, response2.TraceId);
        Assert.NotEqual(response1.TraceId, response3.TraceId);
        Assert.NotEqual(response2.TraceId, response3.TraceId);
    }
}
