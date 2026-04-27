using ThinkOnErp.Domain.Entities.Audit;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Domain;

/// <summary>
/// Unit tests for RequestContext class
/// Tests property initialization, validation, and edge cases
/// </summary>
public class RequestContextTests
{
    [Fact]
    public void RequestContext_Default_Constructor_Initializes_Properties()
    {
        // Arrange & Act
        var context = new RequestContext();

        // Assert
        Assert.NotNull(context.CorrelationId);
        Assert.Equal(string.Empty, context.CorrelationId);
        Assert.NotNull(context.HttpMethod);
        Assert.Equal(string.Empty, context.HttpMethod);
        Assert.NotNull(context.Path);
        Assert.Equal(string.Empty, context.Path);
        Assert.NotNull(context.Headers);
        Assert.Empty(context.Headers);
        Assert.NotEqual(default(DateTime), context.StartTime);
    }

    [Fact]
    public void RequestContext_Can_Store_All_HTTP_Methods()
    {
        // Arrange
        var httpMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS", "HEAD" };

        foreach (var method in httpMethods)
        {
            // Act
            var context = new RequestContext
            {
                HttpMethod = method
            };

            // Assert
            Assert.Equal(method, context.HttpMethod);
        }
    }

    [Fact]
    public void RequestContext_Can_Store_Complex_Paths()
    {
        // Arrange
        var paths = new[]
        {
            "/api/users",
            "/api/users/123",
            "/api/companies/456/branches/789",
            "/api/audit-logs/query",
            "/api/compliance/gdpr/access-report/123"
        };

        foreach (var path in paths)
        {
            // Act
            var context = new RequestContext
            {
                Path = path
            };

            // Assert
            Assert.Equal(path, context.Path);
        }
    }

    [Fact]
    public void RequestContext_Can_Store_Query_String()
    {
        // Arrange
        var context = new RequestContext
        {
            QueryString = "page=1&pageSize=50&sortBy=name&sortOrder=asc"
        };

        // Act & Assert
        Assert.NotNull(context.QueryString);
        Assert.Contains("page=1", context.QueryString);
        Assert.Contains("pageSize=50", context.QueryString);
    }

    [Fact]
    public void RequestContext_QueryString_Can_Be_Null()
    {
        // Arrange & Act
        var context = new RequestContext
        {
            QueryString = null
        };

        // Assert
        Assert.Null(context.QueryString);
    }

    [Fact]
    public void RequestContext_Can_Store_Multiple_Headers()
    {
        // Arrange
        var context = new RequestContext
        {
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "Authorization", "Bearer token123" },
                { "X-Correlation-ID", "abc-123-def" },
                { "User-Agent", "Mozilla/5.0" },
                { "Accept", "application/json" }
            }
        };

        // Act & Assert
        Assert.Equal(5, context.Headers.Count);
        Assert.Equal("application/json", context.Headers["Content-Type"]);
        Assert.Equal("Bearer token123", context.Headers["Authorization"]);
        Assert.Equal("abc-123-def", context.Headers["X-Correlation-ID"]);
    }

    [Fact]
    public void RequestContext_Can_Store_Request_Body()
    {
        // Arrange
        var requestBody = "{\"name\":\"John Doe\",\"email\":\"john@example.com\"}";
        var context = new RequestContext
        {
            RequestBody = requestBody
        };

        // Act & Assert
        Assert.NotNull(context.RequestBody);
        Assert.Equal(requestBody, context.RequestBody);
    }

    [Fact]
    public void RequestContext_RequestBody_Can_Be_Null()
    {
        // Arrange & Act
        var context = new RequestContext
        {
            RequestBody = null
        };

        // Assert
        Assert.Null(context.RequestBody);
    }

    [Fact]
    public void RequestContext_Can_Store_User_Context()
    {
        // Arrange
        var context = new RequestContext
        {
            UserId = 123,
            CompanyId = 456
        };

        // Act & Assert
        Assert.Equal(123, context.UserId);
        Assert.Equal(456, context.CompanyId);
    }

    [Fact]
    public void RequestContext_User_Context_Can_Be_Null()
    {
        // Arrange & Act
        var context = new RequestContext
        {
            UserId = null,
            CompanyId = null
        };

        // Assert
        Assert.Null(context.UserId);
        Assert.Null(context.CompanyId);
    }

    [Fact]
    public void RequestContext_Can_Store_IP_Address()
    {
        // Arrange
        var ipAddresses = new[]
        {
            "192.168.1.100",
            "10.0.0.1",
            "172.16.0.1",
            "2001:0db8:85a3:0000:0000:8a2e:0370:7334", // IPv6
            "::1" // IPv6 localhost
        };

        foreach (var ip in ipAddresses)
        {
            // Act
            var context = new RequestContext
            {
                IpAddress = ip
            };

            // Assert
            Assert.Equal(ip, context.IpAddress);
        }
    }

    [Fact]
    public void RequestContext_IpAddress_Can_Be_Null()
    {
        // Arrange & Act
        var context = new RequestContext
        {
            IpAddress = null
        };

        // Assert
        Assert.Null(context.IpAddress);
    }

    [Fact]
    public void RequestContext_Can_Store_User_Agent()
    {
        // Arrange
        var userAgents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
            "Mozilla/5.0 (X11; Linux x86_64)",
            "PostmanRuntime/7.29.2",
            "curl/7.68.0"
        };

        foreach (var userAgent in userAgents)
        {
            // Act
            var context = new RequestContext
            {
                UserAgent = userAgent
            };

            // Assert
            Assert.Equal(userAgent, context.UserAgent);
        }
    }

    [Fact]
    public void RequestContext_UserAgent_Can_Be_Null()
    {
        // Arrange & Act
        var context = new RequestContext
        {
            UserAgent = null
        };

        // Assert
        Assert.Null(context.UserAgent);
    }

    [Fact]
    public void RequestContext_StartTime_Defaults_To_UtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var context = new RequestContext();
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(context.StartTime, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void RequestContext_Can_Set_Custom_StartTime()
    {
        // Arrange
        var customTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var context = new RequestContext
        {
            StartTime = customTime
        };

        // Assert
        Assert.Equal(customTime, context.StartTime);
    }

    [Fact]
    public void RequestContext_Complete_Example_With_All_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;

        var context = new RequestContext
        {
            CorrelationId = correlationId,
            HttpMethod = "POST",
            Path = "/api/users",
            QueryString = "includeInactive=false",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "Authorization", "Bearer token123" }
            },
            RequestBody = "{\"name\":\"John Doe\"}",
            UserId = 123,
            CompanyId = 456,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0",
            StartTime = startTime
        };

        // Act & Assert
        Assert.Equal(correlationId, context.CorrelationId);
        Assert.Equal("POST", context.HttpMethod);
        Assert.Equal("/api/users", context.Path);
        Assert.Equal("includeInactive=false", context.QueryString);
        Assert.Equal(2, context.Headers.Count);
        Assert.Equal("{\"name\":\"John Doe\"}", context.RequestBody);
        Assert.Equal(123, context.UserId);
        Assert.Equal(456, context.CompanyId);
        Assert.Equal("192.168.1.100", context.IpAddress);
        Assert.Equal("Mozilla/5.0", context.UserAgent);
        Assert.Equal(startTime, context.StartTime);
    }

    [Fact]
    public void RequestContext_Can_Handle_Empty_Headers_Dictionary()
    {
        // Arrange & Act
        var context = new RequestContext
        {
            Headers = new Dictionary<string, string>()
        };

        // Assert
        Assert.NotNull(context.Headers);
        Assert.Empty(context.Headers);
    }

    [Fact]
    public void RequestContext_Can_Handle_Large_Request_Body()
    {
        // Arrange
        var largeBody = new string('x', 10000); // 10KB of data

        // Act
        var context = new RequestContext
        {
            RequestBody = largeBody
        };

        // Assert
        Assert.NotNull(context.RequestBody);
        Assert.Equal(10000, context.RequestBody.Length);
    }

    [Fact]
    public void RequestContext_Can_Handle_Special_Characters_In_Path()
    {
        // Arrange
        var paths = new[]
        {
            "/api/users/search?name=John%20Doe",
            "/api/files/document%2Epdf",
            "/api/data/filter?value=%3E100"
        };

        foreach (var path in paths)
        {
            // Act
            var context = new RequestContext
            {
                Path = path
            };

            // Assert
            Assert.Equal(path, context.Path);
        }
    }

    [Fact]
    public void RequestContext_CorrelationId_Can_Be_GUID()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        // Act
        var context = new RequestContext
        {
            CorrelationId = guid
        };

        // Assert
        Assert.Equal(guid, context.CorrelationId);
        Assert.True(Guid.TryParse(context.CorrelationId, out _));
    }

    [Fact]
    public void RequestContext_Can_Handle_Unauthenticated_Requests()
    {
        // Arrange & Act
        var context = new RequestContext
        {
            HttpMethod = "GET",
            Path = "/api/public/health",
            UserId = null,
            CompanyId = null
        };

        // Assert
        Assert.Null(context.UserId);
        Assert.Null(context.CompanyId);
        Assert.Equal("GET", context.HttpMethod);
        Assert.Equal("/api/public/health", context.Path);
    }
}
