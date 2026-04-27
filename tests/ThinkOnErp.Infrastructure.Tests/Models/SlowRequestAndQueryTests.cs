using ThinkOnErp.Domain.Models;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Models;

/// <summary>
/// Unit tests for SlowRequest and SlowQuery classes
/// Tests property initialization, validation, and edge cases
/// </summary>
public class SlowRequestTests
{
    [Fact]
    public void SlowRequest_Can_Store_All_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;

        // Act
        var slowRequest = new SlowRequest
        {
            CorrelationId = correlationId,
            Endpoint = "/api/reports/complex",
            HttpMethod = "POST",
            ExecutionTimeMs = 2500,
            DatabaseTimeMs = 2000,
            QueryCount = 50,
            StatusCode = 200,
            UserId = 123,
            CompanyId = 456,
            Timestamp = timestamp,
            ExceptionMessage = null
        };

        // Assert
        Assert.Equal(correlationId, slowRequest.CorrelationId);
        Assert.Equal("/api/reports/complex", slowRequest.Endpoint);
        Assert.Equal("POST", slowRequest.HttpMethod);
        Assert.Equal(2500, slowRequest.ExecutionTimeMs);
        Assert.Equal(2000, slowRequest.DatabaseTimeMs);
        Assert.Equal(50, slowRequest.QueryCount);
        Assert.Equal(200, slowRequest.StatusCode);
        Assert.Equal(123, slowRequest.UserId);
        Assert.Equal(456, slowRequest.CompanyId);
        Assert.Equal(timestamp, slowRequest.Timestamp);
        Assert.Null(slowRequest.ExceptionMessage);
    }

    [Fact]
    public void SlowRequest_Can_Identify_Database_Bottleneck()
    {
        // Arrange
        var slowRequest = new SlowRequest
        {
            ExecutionTimeMs = 2000,
            DatabaseTimeMs = 1800,
            QueryCount = 100
        };

        // Act
        var databasePercentage = (double)slowRequest.DatabaseTimeMs / slowRequest.ExecutionTimeMs * 100;

        // Assert
        Assert.True(databasePercentage > 80); // Database bottleneck
        Assert.True(slowRequest.QueryCount > 10); // Potential N+1 problem
    }

    [Fact]
    public void SlowRequest_Can_Handle_Failed_Slow_Request()
    {
        // Arrange & Act
        var slowRequest = new SlowRequest
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Endpoint = "/api/users/999",
            HttpMethod = "GET",
            ExecutionTimeMs = 1500,
            DatabaseTimeMs = 1400,
            QueryCount = 5,
            StatusCode = 500,
            ExceptionMessage = "Database connection timeout"
        };

        // Assert
        Assert.Equal(500, slowRequest.StatusCode);
        Assert.NotNull(slowRequest.ExceptionMessage);
        Assert.Contains("timeout", slowRequest.ExceptionMessage);
    }

    [Fact]
    public void SlowRequest_Can_Handle_Successful_Slow_Request()
    {
        // Arrange & Act
        var slowRequest = new SlowRequest
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Endpoint = "/api/reports/annual",
            HttpMethod = "GET",
            ExecutionTimeMs = 3000,
            DatabaseTimeMs = 2500,
            QueryCount = 20,
            StatusCode = 200,
            ExceptionMessage = null
        };

        // Assert
        Assert.Equal(200, slowRequest.StatusCode);
        Assert.Null(slowRequest.ExceptionMessage);
    }

    [Fact]
    public void SlowRequest_Can_Handle_Various_HTTP_Methods()
    {
        // Arrange
        var methods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH" };

        foreach (var method in methods)
        {
            // Act
            var slowRequest = new SlowRequest
            {
                HttpMethod = method,
                ExecutionTimeMs = 1500
            };

            // Assert
            Assert.Equal(method, slowRequest.HttpMethod);
        }
    }

    [Fact]
    public void SlowRequest_UserId_Can_Be_Null_For_Unauthenticated()
    {
        // Arrange & Act
        var slowRequest = new SlowRequest
        {
            Endpoint = "/api/public/report",
            ExecutionTimeMs = 2000,
            UserId = null,
            CompanyId = null
        };

        // Assert
        Assert.Null(slowRequest.UserId);
        Assert.Null(slowRequest.CompanyId);
    }

    [Fact]
    public void SlowRequest_ExceptionMessage_Can_Be_Null()
    {
        // Arrange & Act
        var slowRequest = new SlowRequest
        {
            ExecutionTimeMs = 1500,
            StatusCode = 200,
            ExceptionMessage = null
        };

        // Assert
        Assert.Null(slowRequest.ExceptionMessage);
    }

    [Fact]
    public void SlowRequest_Complete_Example()
    {
        // Arrange & Act
        var slowRequest = new SlowRequest
        {
            CorrelationId = Guid.NewGuid().ToString(),
            Endpoint = "/api/audit-logs/query",
            HttpMethod = "POST",
            ExecutionTimeMs = 2500,
            DatabaseTimeMs = 2200,
            QueryCount = 15,
            StatusCode = 200,
            UserId = 123,
            CompanyId = 456,
            Timestamp = DateTime.UtcNow,
            ExceptionMessage = null
        };

        // Assert
        Assert.True(slowRequest.ExecutionTimeMs >= 1000); // Slow request threshold
        Assert.NotNull(slowRequest.CorrelationId);
        Assert.NotNull(slowRequest.Endpoint);
    }
}

/// <summary>
/// Unit tests for SlowQuery class
/// </summary>
public class SlowQueryTests
{
    [Fact]
    public void SlowQuery_Can_Store_All_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        var sqlStatement = "SELECT * FROM SYS_AUDIT_LOG WHERE CREATION_DATE > :p0";

        // Act
        var slowQuery = new SlowQuery
        {
            CorrelationId = correlationId,
            SqlStatement = sqlStatement,
            ExecutionTimeMs = 1500,
            RowsAffected = 10000,
            EndpointPath = "/api/audit-logs/query",
            UserId = 123,
            CompanyId = 456,
            Timestamp = timestamp,
            ErrorMessage = null
        };

        // Assert
        Assert.Equal(correlationId, slowQuery.CorrelationId);
        Assert.Equal(sqlStatement, slowQuery.SqlStatement);
        Assert.Equal(1500, slowQuery.ExecutionTimeMs);
        Assert.Equal(10000, slowQuery.RowsAffected);
        Assert.Equal("/api/audit-logs/query", slowQuery.EndpointPath);
        Assert.Equal(123, slowQuery.UserId);
        Assert.Equal(456, slowQuery.CompanyId);
        Assert.Equal(timestamp, slowQuery.Timestamp);
        Assert.Null(slowQuery.ErrorMessage);
    }

    [Fact]
    public void SlowQuery_Can_Handle_Failed_Query()
    {
        // Arrange & Act
        var slowQuery = new SlowQuery
        {
            CorrelationId = Guid.NewGuid().ToString(),
            SqlStatement = "SELECT * FROM NON_EXISTENT_TABLE",
            ExecutionTimeMs = 1000,
            RowsAffected = 0,
            ErrorMessage = "ORA-00942: table or view does not exist"
        };

        // Assert
        Assert.NotNull(slowQuery.ErrorMessage);
        Assert.Contains("ORA-00942", slowQuery.ErrorMessage);
        Assert.Equal(0, slowQuery.RowsAffected);
    }

    [Fact]
    public void SlowQuery_Can_Handle_Successful_Query()
    {
        // Arrange & Act
        var slowQuery = new SlowQuery
        {
            CorrelationId = Guid.NewGuid().ToString(),
            SqlStatement = "SELECT * FROM SYS_USERS WHERE IS_ACTIVE = 1",
            ExecutionTimeMs = 750,
            RowsAffected = 5000,
            ErrorMessage = null
        };

        // Assert
        Assert.Null(slowQuery.ErrorMessage);
        Assert.True(slowQuery.RowsAffected > 0);
    }

    [Fact]
    public void SlowQuery_Can_Identify_Large_Result_Set()
    {
        // Arrange & Act
        var slowQuery = new SlowQuery
        {
            SqlStatement = "SELECT * FROM SYS_AUDIT_LOG",
            ExecutionTimeMs = 2000,
            RowsAffected = 100000
        };

        // Assert
        Assert.True(slowQuery.RowsAffected > 10000); // Large result set
        Assert.True(slowQuery.ExecutionTimeMs > 1000); // Slow query
    }

    [Fact]
    public void SlowQuery_Can_Handle_Queries_With_Masked_Parameters()
    {
        // Arrange & Act
        var slowQuery = new SlowQuery
        {
            SqlStatement = "SELECT * FROM SYS_USERS WHERE EMAIL = :p0 AND PASSWORD_HASH = :p1",
            ExecutionTimeMs = 800,
            RowsAffected = 1
        };

        // Assert
        Assert.Contains(":p0", slowQuery.SqlStatement);
        Assert.Contains(":p1", slowQuery.SqlStatement);
        Assert.DoesNotContain("password", slowQuery.SqlStatement.ToLower().Replace("password_hash", ""));
    }

    [Fact]
    public void SlowQuery_EndpointPath_Can_Be_Null()
    {
        // Arrange & Act
        var slowQuery = new SlowQuery
        {
            SqlStatement = "SELECT * FROM SYS_USERS",
            ExecutionTimeMs = 600,
            EndpointPath = null
        };

        // Assert
        Assert.Null(slowQuery.EndpointPath);
    }

    [Fact]
    public void SlowQuery_UserId_Can_Be_Null()
    {
        // Arrange & Act
        var slowQuery = new SlowQuery
        {
            SqlStatement = "SELECT * FROM SYS_USERS",
            ExecutionTimeMs = 600,
            UserId = null,
            CompanyId = null
        };

        // Assert
        Assert.Null(slowQuery.UserId);
        Assert.Null(slowQuery.CompanyId);
    }

    [Fact]
    public void SlowQuery_ErrorMessage_Can_Be_Null()
    {
        // Arrange & Act
        var slowQuery = new SlowQuery
        {
            SqlStatement = "SELECT * FROM SYS_USERS",
            ExecutionTimeMs = 600,
            RowsAffected = 100,
            ErrorMessage = null
        };

        // Assert
        Assert.Null(slowQuery.ErrorMessage);
    }

    [Fact]
    public void SlowQuery_Can_Handle_Various_Query_Types()
    {
        // Arrange
        var queries = new[]
        {
            "SELECT * FROM SYS_USERS WHERE IS_ACTIVE = 1",
            "UPDATE SYS_USERS SET LAST_LOGIN_DATE = SYSDATE",
            "DELETE FROM SYS_AUDIT_LOG WHERE CREATION_DATE < :p0",
            "INSERT INTO SYS_USERS (NAME_AR, EMAIL) VALUES (:p0, :p1)"
        };

        foreach (var query in queries)
        {
            // Act
            var slowQuery = new SlowQuery
            {
                SqlStatement = query,
                ExecutionTimeMs = 750
            };

            // Assert
            Assert.Equal(query, slowQuery.SqlStatement);
            Assert.True(slowQuery.ExecutionTimeMs >= 500); // Slow query threshold
        }
    }

    [Fact]
    public void SlowQuery_Can_Handle_Complex_Queries()
    {
        // Arrange & Act
        var slowQuery = new SlowQuery
        {
            SqlStatement = @"SELECT u.*, c.NAME_AR, b.NAME_AR 
                            FROM SYS_USERS u 
                            JOIN SYS_COMPANY c ON u.COMPANY_ID = c.ROW_ID 
                            JOIN SYS_BRANCH b ON u.BRANCH_ID = b.ROW_ID 
                            WHERE u.IS_ACTIVE = 1 AND c.IS_ACTIVE = 1",
            ExecutionTimeMs = 1200,
            RowsAffected = 500
        };

        // Assert
        Assert.Contains("JOIN", slowQuery.SqlStatement);
        Assert.True(slowQuery.ExecutionTimeMs >= 500);
    }

    [Fact]
    public void SlowQuery_Complete_Example()
    {
        // Arrange & Act
        var slowQuery = new SlowQuery
        {
            CorrelationId = Guid.NewGuid().ToString(),
            SqlStatement = "SELECT * FROM SYS_AUDIT_LOG WHERE COMPANY_ID = :p0 AND CREATION_DATE > :p1",
            ExecutionTimeMs = 1500,
            RowsAffected = 15000,
            EndpointPath = "/api/audit-logs/query",
            UserId = 123,
            CompanyId = 456,
            Timestamp = DateTime.UtcNow,
            ErrorMessage = null
        };

        // Assert
        Assert.True(slowQuery.ExecutionTimeMs >= 500); // Slow query threshold
        Assert.NotNull(slowQuery.CorrelationId);
        Assert.NotNull(slowQuery.SqlStatement);
        Assert.True(slowQuery.RowsAffected > 0);
    }

    [Fact]
    public void SlowQuery_Can_Handle_Timeout_Errors()
    {
        // Arrange & Act
        var slowQuery = new SlowQuery
        {
            SqlStatement = "SELECT * FROM LARGE_TABLE",
            ExecutionTimeMs = 30000, // 30 seconds
            RowsAffected = 0,
            ErrorMessage = "ORA-01013: user requested cancel of current operation"
        };

        // Assert
        Assert.Contains("ORA-01013", slowQuery.ErrorMessage);
        Assert.True(slowQuery.ExecutionTimeMs >= 30000);
    }

    [Fact]
    public void SlowQuery_Can_Link_To_Request_Via_CorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var slowQuery = new SlowQuery
        {
            CorrelationId = correlationId,
            SqlStatement = "SELECT * FROM SYS_USERS",
            ExecutionTimeMs = 600
        };

        // Assert
        Assert.Equal(correlationId, slowQuery.CorrelationId);
        Assert.True(Guid.TryParse(slowQuery.CorrelationId, out _));
    }
}
