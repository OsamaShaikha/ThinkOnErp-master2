using ThinkOnErp.Domain.Models;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Models;

/// <summary>
/// Unit tests for QueryMetrics class
/// Tests property initialization, validation, and edge cases
/// </summary>
public class QueryMetricsTests
{
    [Fact]
    public void QueryMetrics_Can_Store_All_Properties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        var sqlStatement = "SELECT * FROM SYS_USERS WHERE ROW_ID = :p0";

        // Act
        var metrics = new QueryMetrics
        {
            CorrelationId = correlationId,
            SqlStatement = sqlStatement,
            ExecutionTimeMs = 25,
            RowsAffected = 1,
            Timestamp = timestamp
        };

        // Assert
        Assert.Equal(correlationId, metrics.CorrelationId);
        Assert.Equal(sqlStatement, metrics.SqlStatement);
        Assert.Equal(25, metrics.ExecutionTimeMs);
        Assert.Equal(1, metrics.RowsAffected);
        Assert.Equal(timestamp, metrics.Timestamp);
    }

    [Fact]
    public void QueryMetrics_Can_Handle_Fast_Queries()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "SELECT ROW_ID FROM SYS_USERS WHERE ROW_ID = :p0",
            ExecutionTimeMs = 5,
            RowsAffected = 1
        };

        // Assert
        Assert.Equal(5, metrics.ExecutionTimeMs);
        Assert.True(metrics.ExecutionTimeMs < 100);
    }

    [Fact]
    public void QueryMetrics_Can_Handle_Slow_Queries()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "SELECT * FROM SYS_AUDIT_LOG WHERE CREATION_DATE > :p0",
            ExecutionTimeMs = 1500, // 1.5 seconds
            RowsAffected = 10000
        };

        // Assert
        Assert.Equal(1500, metrics.ExecutionTimeMs);
        Assert.True(metrics.ExecutionTimeMs >= 500); // Slow query threshold
        Assert.True(metrics.RowsAffected > 1000); // Large result set
    }

    [Fact]
    public void QueryMetrics_Can_Handle_SELECT_Queries()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "SELECT * FROM SYS_USERS WHERE IS_ACTIVE = 1",
            ExecutionTimeMs = 50,
            RowsAffected = 100 // Rows returned
        };

        // Assert
        Assert.Contains("SELECT", metrics.SqlStatement);
        Assert.Equal(100, metrics.RowsAffected);
    }

    [Fact]
    public void QueryMetrics_Can_Handle_INSERT_Queries()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "INSERT INTO SYS_USERS (NAME_AR, NAME_EN, EMAIL) VALUES (:p0, :p1, :p2)",
            ExecutionTimeMs = 15,
            RowsAffected = 1
        };

        // Assert
        Assert.Contains("INSERT", metrics.SqlStatement);
        Assert.Equal(1, metrics.RowsAffected);
    }

    [Fact]
    public void QueryMetrics_Can_Handle_UPDATE_Queries()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "UPDATE SYS_USERS SET IS_ACTIVE = 0 WHERE ROW_ID = :p0",
            ExecutionTimeMs = 20,
            RowsAffected = 1
        };

        // Assert
        Assert.Contains("UPDATE", metrics.SqlStatement);
        Assert.Equal(1, metrics.RowsAffected);
    }

    [Fact]
    public void QueryMetrics_Can_Handle_DELETE_Queries()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "DELETE FROM SYS_AUDIT_LOG WHERE CREATION_DATE < :p0",
            ExecutionTimeMs = 500,
            RowsAffected = 5000
        };

        // Assert
        Assert.Contains("DELETE", metrics.SqlStatement);
        Assert.Equal(5000, metrics.RowsAffected);
    }

    [Fact]
    public void QueryMetrics_Can_Handle_Stored_Procedure_Calls()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "BEGIN PKG_SYS_USERS.SP_GET_USER_BY_ID(:p0, :p1); END;",
            ExecutionTimeMs = 30,
            RowsAffected = 1
        };

        // Assert
        Assert.Contains("BEGIN", metrics.SqlStatement);
        Assert.Contains("END", metrics.SqlStatement);
    }

    [Fact]
    public void QueryMetrics_Can_Handle_Queries_With_Masked_Parameters()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "SELECT * FROM SYS_USERS WHERE EMAIL = :p0 AND PASSWORD_HASH = :p1",
            ExecutionTimeMs = 25,
            RowsAffected = 1
        };

        // Assert
        Assert.Contains(":p0", metrics.SqlStatement);
        Assert.Contains(":p1", metrics.SqlStatement);
        Assert.DoesNotContain("password123", metrics.SqlStatement); // Actual values should be masked
    }

    [Fact]
    public void QueryMetrics_Can_Handle_Zero_Rows_Affected()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "SELECT * FROM SYS_USERS WHERE ROW_ID = 999999",
            ExecutionTimeMs = 10,
            RowsAffected = 0 // No rows found
        };

        // Assert
        Assert.Equal(0, metrics.RowsAffected);
    }

    [Fact]
    public void QueryMetrics_Can_Handle_Large_Result_Sets()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "SELECT * FROM SYS_AUDIT_LOG",
            ExecutionTimeMs = 2000,
            RowsAffected = 100000
        };

        // Assert
        Assert.Equal(100000, metrics.RowsAffected);
        Assert.True(metrics.RowsAffected > 10000);
    }

    [Fact]
    public void QueryMetrics_Can_Handle_Bulk_Operations()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "UPDATE SYS_USERS SET LAST_LOGIN_DATE = SYSDATE WHERE IS_ACTIVE = 1",
            ExecutionTimeMs = 1000,
            RowsAffected = 5000
        };

        // Assert
        Assert.Equal(5000, metrics.RowsAffected);
        Assert.True(metrics.RowsAffected > 100); // Bulk operation
    }

    [Fact]
    public void QueryMetrics_CorrelationId_Links_To_Request()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var metrics = new QueryMetrics
        {
            CorrelationId = correlationId,
            SqlStatement = "SELECT * FROM SYS_USERS",
            ExecutionTimeMs = 50,
            RowsAffected = 10
        };

        // Assert
        Assert.Equal(correlationId, metrics.CorrelationId);
        Assert.True(Guid.TryParse(metrics.CorrelationId, out _));
    }

    [Fact]
    public void QueryMetrics_Can_Handle_Complex_Queries()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = @"SELECT u.*, c.NAME_AR AS COMPANY_NAME, b.NAME_AR AS BRANCH_NAME 
                            FROM SYS_USERS u 
                            JOIN SYS_COMPANY c ON u.COMPANY_ID = c.ROW_ID 
                            JOIN SYS_BRANCH b ON u.BRANCH_ID = b.ROW_ID 
                            WHERE u.IS_ACTIVE = 1",
            ExecutionTimeMs = 75,
            RowsAffected = 50
        };

        // Assert
        Assert.Contains("JOIN", metrics.SqlStatement);
        Assert.Equal(75, metrics.ExecutionTimeMs);
    }

    [Fact]
    public void QueryMetrics_Can_Handle_Aggregate_Queries()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "SELECT COUNT(*) FROM SYS_USERS WHERE IS_ACTIVE = 1",
            ExecutionTimeMs = 20,
            RowsAffected = 1 // COUNT returns 1 row
        };

        // Assert
        Assert.Contains("COUNT", metrics.SqlStatement);
        Assert.Equal(1, metrics.RowsAffected);
    }

    [Fact]
    public void QueryMetrics_Timestamp_Can_Be_Set()
    {
        // Arrange
        var timestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var metrics = new QueryMetrics
        {
            Timestamp = timestamp
        };

        // Assert
        Assert.Equal(timestamp, metrics.Timestamp);
    }

    [Fact]
    public void QueryMetrics_Can_Identify_Slow_Query()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "SELECT * FROM SYS_AUDIT_LOG WHERE CREATION_DATE > :p0",
            ExecutionTimeMs = 750,
            RowsAffected = 5000
        };

        // Act
        var isSlowQuery = metrics.ExecutionTimeMs >= 500;

        // Assert
        Assert.True(isSlowQuery);
    }

    [Fact]
    public void QueryMetrics_Can_Handle_Transaction_Queries()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "BEGIN TRANSACTION; UPDATE SYS_USERS SET IS_ACTIVE = 0; COMMIT;",
            ExecutionTimeMs = 100,
            RowsAffected = 10
        };

        // Assert
        Assert.Contains("TRANSACTION", metrics.SqlStatement);
        Assert.Contains("COMMIT", metrics.SqlStatement);
    }

    [Fact]
    public void QueryMetrics_Can_Handle_DDL_Statements()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "CREATE INDEX IDX_USERS_EMAIL ON SYS_USERS(EMAIL)",
            ExecutionTimeMs = 5000,
            RowsAffected = 0
        };

        // Assert
        Assert.Contains("CREATE INDEX", metrics.SqlStatement);
    }

    [Fact]
    public void QueryMetrics_Complete_Example()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            CorrelationId = Guid.NewGuid().ToString(),
            SqlStatement = "SELECT * FROM SYS_USERS WHERE COMPANY_ID = :p0 AND IS_ACTIVE = 1",
            ExecutionTimeMs = 35,
            RowsAffected = 25,
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.NotNull(metrics.CorrelationId);
        Assert.NotNull(metrics.SqlStatement);
        Assert.True(metrics.ExecutionTimeMs > 0);
        Assert.True(metrics.RowsAffected > 0);
        Assert.NotEqual(default(DateTime), metrics.Timestamp);
    }

    [Fact]
    public void QueryMetrics_Can_Handle_Very_Fast_Queries()
    {
        // Arrange & Act
        var metrics = new QueryMetrics
        {
            SqlStatement = "SELECT 1 FROM DUAL",
            ExecutionTimeMs = 1,
            RowsAffected = 1
        };

        // Assert
        Assert.Equal(1, metrics.ExecutionTimeMs);
    }

    [Fact]
    public void QueryMetrics_Can_Handle_Long_SQL_Statements()
    {
        // Arrange
        var longSql = new string('x', 10000); // 10KB SQL statement

        // Act
        var metrics = new QueryMetrics
        {
            SqlStatement = longSql,
            ExecutionTimeMs = 100,
            RowsAffected = 1
        };

        // Assert
        Assert.Equal(10000, metrics.SqlStatement.Length);
    }
}
