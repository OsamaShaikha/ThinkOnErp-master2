using ThinkOnErp.Domain.Entities.Audit;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Domain;

/// <summary>
/// Unit tests for ResponseContext class
/// Tests property initialization, validation, and edge cases
/// </summary>
public class ResponseContextTests
{
    [Fact]
    public void ResponseContext_Default_Constructor_Initializes_Properties()
    {
        // Arrange & Act
        var context = new ResponseContext();

        // Assert
        Assert.Equal(0, context.StatusCode);
        Assert.Equal(0, context.ResponseSize);
        Assert.Equal(0, context.ExecutionTimeMs);
        Assert.NotEqual(default(DateTime), context.EndTime);
    }

    [Fact]
    public void ResponseContext_Can_Store_Success_Status_Codes()
    {
        // Arrange
        var successCodes = new[] { 200, 201, 202, 204, 206 };

        foreach (var code in successCodes)
        {
            // Act
            var context = new ResponseContext
            {
                StatusCode = code
            };

            // Assert
            Assert.Equal(code, context.StatusCode);
            Assert.True(context.StatusCode >= 200 && context.StatusCode < 300);
        }
    }

    [Fact]
    public void ResponseContext_Can_Store_Client_Error_Status_Codes()
    {
        // Arrange
        var clientErrorCodes = new[] { 400, 401, 403, 404, 409, 422, 429 };

        foreach (var code in clientErrorCodes)
        {
            // Act
            var context = new ResponseContext
            {
                StatusCode = code
            };

            // Assert
            Assert.Equal(code, context.StatusCode);
            Assert.True(context.StatusCode >= 400 && context.StatusCode < 500);
        }
    }

    [Fact]
    public void ResponseContext_Can_Store_Server_Error_Status_Codes()
    {
        // Arrange
        var serverErrorCodes = new[] { 500, 501, 502, 503, 504 };

        foreach (var code in serverErrorCodes)
        {
            // Act
            var context = new ResponseContext
            {
                StatusCode = code
            };

            // Assert
            Assert.Equal(code, context.StatusCode);
            Assert.True(context.StatusCode >= 500 && context.StatusCode < 600);
        }
    }

    [Fact]
    public void ResponseContext_Can_Store_Response_Size()
    {
        // Arrange
        var sizes = new long[] { 0, 100, 1024, 10240, 1048576, 10485760 }; // 0B to 10MB

        foreach (var size in sizes)
        {
            // Act
            var context = new ResponseContext
            {
                ResponseSize = size
            };

            // Assert
            Assert.Equal(size, context.ResponseSize);
        }
    }

    [Fact]
    public void ResponseContext_Can_Store_Response_Body()
    {
        // Arrange
        var responseBody = "{\"id\":123,\"name\":\"John Doe\",\"email\":\"john@example.com\"}";
        var context = new ResponseContext
        {
            ResponseBody = responseBody
        };

        // Act & Assert
        Assert.NotNull(context.ResponseBody);
        Assert.Equal(responseBody, context.ResponseBody);
    }

    [Fact]
    public void ResponseContext_ResponseBody_Can_Be_Null()
    {
        // Arrange & Act
        var context = new ResponseContext
        {
            ResponseBody = null
        };

        // Assert
        Assert.Null(context.ResponseBody);
    }

    [Fact]
    public void ResponseContext_Can_Store_Execution_Time()
    {
        // Arrange
        var executionTimes = new long[] { 1, 10, 50, 100, 500, 1000, 5000, 10000 }; // 1ms to 10s

        foreach (var time in executionTimes)
        {
            // Act
            var context = new ResponseContext
            {
                ExecutionTimeMs = time
            };

            // Assert
            Assert.Equal(time, context.ExecutionTimeMs);
        }
    }

    [Fact]
    public void ResponseContext_EndTime_Defaults_To_UtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var context = new ResponseContext();
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(context.EndTime, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void ResponseContext_Can_Set_Custom_EndTime()
    {
        // Arrange
        var customTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var context = new ResponseContext
        {
            EndTime = customTime
        };

        // Assert
        Assert.Equal(customTime, context.EndTime);
    }

    [Fact]
    public void ResponseContext_Complete_Success_Response_Example()
    {
        // Arrange
        var endTime = DateTime.UtcNow;
        var responseBody = "{\"success\":true,\"data\":{\"id\":123}}";

        var context = new ResponseContext
        {
            StatusCode = 200,
            ResponseSize = responseBody.Length,
            ResponseBody = responseBody,
            ExecutionTimeMs = 45,
            EndTime = endTime
        };

        // Act & Assert
        Assert.Equal(200, context.StatusCode);
        Assert.Equal(responseBody.Length, context.ResponseSize);
        Assert.Equal(responseBody, context.ResponseBody);
        Assert.Equal(45, context.ExecutionTimeMs);
        Assert.Equal(endTime, context.EndTime);
    }

    [Fact]
    public void ResponseContext_Complete_Error_Response_Example()
    {
        // Arrange
        var endTime = DateTime.UtcNow;
        var errorBody = "{\"error\":\"Not Found\",\"message\":\"User with ID 999 not found\"}";

        var context = new ResponseContext
        {
            StatusCode = 404,
            ResponseSize = errorBody.Length,
            ResponseBody = errorBody,
            ExecutionTimeMs = 12,
            EndTime = endTime
        };

        // Act & Assert
        Assert.Equal(404, context.StatusCode);
        Assert.Equal(errorBody.Length, context.ResponseSize);
        Assert.Equal(errorBody, context.ResponseBody);
        Assert.Equal(12, context.ExecutionTimeMs);
        Assert.Equal(endTime, context.EndTime);
    }

    [Fact]
    public void ResponseContext_Can_Handle_Empty_Response_Body()
    {
        // Arrange & Act
        var context = new ResponseContext
        {
            StatusCode = 204, // No Content
            ResponseSize = 0,
            ResponseBody = null,
            ExecutionTimeMs = 5
        };

        // Assert
        Assert.Equal(204, context.StatusCode);
        Assert.Equal(0, context.ResponseSize);
        Assert.Null(context.ResponseBody);
    }

    [Fact]
    public void ResponseContext_Can_Handle_Large_Response_Body()
    {
        // Arrange
        var largeBody = new string('x', 100000); // 100KB of data

        // Act
        var context = new ResponseContext
        {
            ResponseBody = largeBody,
            ResponseSize = largeBody.Length
        };

        // Assert
        Assert.NotNull(context.ResponseBody);
        Assert.Equal(100000, context.ResponseBody.Length);
        Assert.Equal(100000, context.ResponseSize);
    }

    [Fact]
    public void ResponseContext_Can_Handle_Very_Fast_Responses()
    {
        // Arrange & Act
        var context = new ResponseContext
        {
            StatusCode = 200,
            ExecutionTimeMs = 1 // 1 millisecond
        };

        // Assert
        Assert.Equal(1, context.ExecutionTimeMs);
    }

    [Fact]
    public void ResponseContext_Can_Handle_Very_Slow_Responses()
    {
        // Arrange & Act
        var context = new ResponseContext
        {
            StatusCode = 200,
            ExecutionTimeMs = 30000 // 30 seconds
        };

        // Assert
        Assert.Equal(30000, context.ExecutionTimeMs);
    }

    [Fact]
    public void ResponseContext_Can_Handle_Timeout_Responses()
    {
        // Arrange & Act
        var context = new ResponseContext
        {
            StatusCode = 504, // Gateway Timeout
            ExecutionTimeMs = 60000, // 60 seconds
            ResponseBody = "{\"error\":\"Request timeout\"}"
        };

        // Assert
        Assert.Equal(504, context.StatusCode);
        Assert.Equal(60000, context.ExecutionTimeMs);
        Assert.Contains("timeout", context.ResponseBody);
    }

    [Fact]
    public void ResponseContext_Can_Handle_Redirect_Status_Codes()
    {
        // Arrange
        var redirectCodes = new[] { 301, 302, 303, 307, 308 };

        foreach (var code in redirectCodes)
        {
            // Act
            var context = new ResponseContext
            {
                StatusCode = code
            };

            // Assert
            Assert.Equal(code, context.StatusCode);
            Assert.True(context.StatusCode >= 300 && context.StatusCode < 400);
        }
    }

    [Fact]
    public void ResponseContext_Can_Store_JSON_Response()
    {
        // Arrange
        var jsonResponse = @"{
            ""id"": 123,
            ""name"": ""John Doe"",
            ""email"": ""john@example.com"",
            ""roles"": [""admin"", ""user""]
        }";

        // Act
        var context = new ResponseContext
        {
            StatusCode = 200,
            ResponseBody = jsonResponse,
            ResponseSize = jsonResponse.Length
        };

        // Assert
        Assert.Equal(200, context.StatusCode);
        Assert.Contains("John Doe", context.ResponseBody);
        Assert.Equal(jsonResponse.Length, context.ResponseSize);
    }

    [Fact]
    public void ResponseContext_Can_Store_XML_Response()
    {
        // Arrange
        var xmlResponse = "<?xml version=\"1.0\"?><user><id>123</id><name>John Doe</name></user>";

        // Act
        var context = new ResponseContext
        {
            StatusCode = 200,
            ResponseBody = xmlResponse,
            ResponseSize = xmlResponse.Length
        };

        // Assert
        Assert.Equal(200, context.StatusCode);
        Assert.Contains("<user>", context.ResponseBody);
        Assert.Equal(xmlResponse.Length, context.ResponseSize);
    }

    [Fact]
    public void ResponseContext_Can_Store_Plain_Text_Response()
    {
        // Arrange
        var textResponse = "Operation completed successfully";

        // Act
        var context = new ResponseContext
        {
            StatusCode = 200,
            ResponseBody = textResponse,
            ResponseSize = textResponse.Length
        };

        // Assert
        Assert.Equal(200, context.StatusCode);
        Assert.Equal(textResponse, context.ResponseBody);
        Assert.Equal(textResponse.Length, context.ResponseSize);
    }

    [Fact]
    public void ResponseContext_ResponseSize_Can_Be_Zero()
    {
        // Arrange & Act
        var context = new ResponseContext
        {
            StatusCode = 204,
            ResponseSize = 0
        };

        // Assert
        Assert.Equal(0, context.ResponseSize);
    }

    [Fact]
    public void ResponseContext_ExecutionTime_Can_Be_Zero()
    {
        // Arrange & Act
        var context = new ResponseContext
        {
            StatusCode = 200,
            ExecutionTimeMs = 0
        };

        // Assert
        Assert.Equal(0, context.ExecutionTimeMs);
    }

    [Fact]
    public void ResponseContext_Can_Handle_Validation_Error_Response()
    {
        // Arrange
        var validationError = @"{
            ""errors"": {
                ""email"": [""Email is required""],
                ""password"": [""Password must be at least 8 characters""]
            }
        }";

        // Act
        var context = new ResponseContext
        {
            StatusCode = 422, // Unprocessable Entity
            ResponseBody = validationError,
            ResponseSize = validationError.Length,
            ExecutionTimeMs = 8
        };

        // Assert
        Assert.Equal(422, context.StatusCode);
        Assert.Contains("errors", context.ResponseBody);
        Assert.Equal(validationError.Length, context.ResponseSize);
    }

    [Fact]
    public void ResponseContext_Can_Handle_Server_Error_With_Stack_Trace()
    {
        // Arrange
        var errorResponse = @"{
            ""error"": ""Internal Server Error"",
            ""message"": ""An unexpected error occurred"",
            ""stackTrace"": ""at System.Something...""
        }";

        // Act
        var context = new ResponseContext
        {
            StatusCode = 500,
            ResponseBody = errorResponse,
            ResponseSize = errorResponse.Length,
            ExecutionTimeMs = 150
        };

        // Assert
        Assert.Equal(500, context.StatusCode);
        Assert.Contains("Internal Server Error", context.ResponseBody);
        Assert.Contains("stackTrace", context.ResponseBody);
    }

    [Fact]
    public void ResponseContext_Can_Handle_Paginated_Response()
    {
        // Arrange
        var paginatedResponse = @"{
            ""items"": [{""id"": 1}, {""id"": 2}],
            ""totalCount"": 100,
            ""pageNumber"": 1,
            ""pageSize"": 50
        }";

        // Act
        var context = new ResponseContext
        {
            StatusCode = 200,
            ResponseBody = paginatedResponse,
            ResponseSize = paginatedResponse.Length,
            ExecutionTimeMs = 75
        };

        // Assert
        Assert.Equal(200, context.StatusCode);
        Assert.Contains("totalCount", context.ResponseBody);
        Assert.Contains("pageNumber", context.ResponseBody);
    }
}
