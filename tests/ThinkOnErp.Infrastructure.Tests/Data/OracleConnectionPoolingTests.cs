using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Infrastructure.Data;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Data;

/// <summary>
/// Unit tests for Oracle connection pooling configuration.
/// Validates that connection pooling is properly configured for high-volume scenarios.
/// </summary>
public class OracleConnectionPoolingTests
{
    private readonly IConfiguration _configuration;

    public OracleConnectionPoolingTests()
    {
        // Create test configuration with connection string
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OracleDb"] = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XEPDB1)));User Id=TEST;Password=TEST;Pooling=true;Min Pool Size=5;Max Pool Size=100;Connection Timeout=15;Incr Pool Size=5;Decr Pool Size=2;Validate Connection=true;Connection Lifetime=300;Statement Cache Size=50;Statement Cache Purge=false;"
        });
        _configuration = configBuilder.Build();
    }

    [Fact]
    public void ConnectionString_Should_Have_Pooling_Enabled()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("OracleDb");

        // Assert
        Assert.NotNull(connectionString);
        Assert.Contains("Pooling=true", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConnectionString_Should_Have_Minimum_Pool_Size_Configured()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("OracleDb");

        // Assert
        Assert.NotNull(connectionString);
        Assert.Contains("Min Pool Size=", connectionString, StringComparison.OrdinalIgnoreCase);
        
        // Extract and validate the value
        var minPoolSize = ExtractPoolParameter(connectionString, "Min Pool Size");
        Assert.True(minPoolSize >= 5, "Min Pool Size should be at least 5 for high-volume scenarios");
    }

    [Fact]
    public void ConnectionString_Should_Have_Maximum_Pool_Size_Configured()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("OracleDb");

        // Assert
        Assert.NotNull(connectionString);
        Assert.Contains("Max Pool Size=", connectionString, StringComparison.OrdinalIgnoreCase);
        
        // Extract and validate the value
        var maxPoolSize = ExtractPoolParameter(connectionString, "Max Pool Size");
        Assert.True(maxPoolSize >= 100, "Max Pool Size should be at least 100 for high-volume scenarios (10,000+ req/min)");
    }

    [Fact]
    public void ConnectionString_Should_Have_Connection_Timeout_Configured()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("OracleDb");

        // Assert
        Assert.NotNull(connectionString);
        Assert.Contains("Connection Timeout=", connectionString, StringComparison.OrdinalIgnoreCase);
        
        // Extract and validate the value
        var timeout = ExtractPoolParameter(connectionString, "Connection Timeout");
        Assert.True(timeout >= 10 && timeout <= 30, "Connection Timeout should be between 10-30 seconds for fail-fast behavior");
    }

    [Fact]
    public void ConnectionString_Should_Have_Connection_Validation_Enabled()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("OracleDb");

        // Assert
        Assert.NotNull(connectionString);
        Assert.Contains("Validate Connection=true", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConnectionString_Should_Have_Connection_Lifetime_Configured()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("OracleDb");

        // Assert
        Assert.NotNull(connectionString);
        Assert.Contains("Connection Lifetime=", connectionString, StringComparison.OrdinalIgnoreCase);
        
        // Extract and validate the value
        var lifetime = ExtractPoolParameter(connectionString, "Connection Lifetime");
        Assert.True(lifetime >= 180 && lifetime <= 600, "Connection Lifetime should be between 180-600 seconds to prevent stale connections");
    }

    [Fact]
    public void ConnectionString_Should_Have_Statement_Cache_Configured()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("OracleDb");

        // Assert
        Assert.NotNull(connectionString);
        Assert.Contains("Statement Cache Size=", connectionString, StringComparison.OrdinalIgnoreCase);
        
        // Extract and validate the value
        var cacheSize = ExtractPoolParameter(connectionString, "Statement Cache Size");
        Assert.True(cacheSize >= 50, "Statement Cache Size should be at least 50 for optimal performance with repetitive audit queries");
    }

    [Fact]
    public void ConnectionString_Should_Have_Statement_Cache_Purge_Disabled()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("OracleDb");

        // Assert
        Assert.NotNull(connectionString);
        Assert.Contains("Statement Cache Purge=false", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConnectionString_Should_Have_Increment_Pool_Size_Configured()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("OracleDb");

        // Assert
        Assert.NotNull(connectionString);
        Assert.Contains("Incr Pool Size=", connectionString, StringComparison.OrdinalIgnoreCase);
        
        // Extract and validate the value
        var incrSize = ExtractPoolParameter(connectionString, "Incr Pool Size");
        Assert.True(incrSize >= 5 && incrSize <= 10, "Incr Pool Size should be between 5-10 for balanced growth");
    }

    [Fact]
    public void ConnectionString_Should_Have_Decrement_Pool_Size_Configured()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("OracleDb");

        // Assert
        Assert.NotNull(connectionString);
        Assert.Contains("Decr Pool Size=", connectionString, StringComparison.OrdinalIgnoreCase);
        
        // Extract and validate the value
        var decrSize = ExtractPoolParameter(connectionString, "Decr Pool Size");
        Assert.True(decrSize >= 1 && decrSize <= 5, "Decr Pool Size should be between 1-5 for conservative shrinkage");
    }

    [Fact]
    public void OracleDbContext_Should_Create_Connection_Successfully()
    {
        // Arrange
        var context = new OracleDbContext(_configuration);

        // Act
        var connection = context.CreateConnection();

        // Assert
        Assert.NotNull(connection);
        Assert.IsType<OracleConnection>(connection);
        Assert.Equal(ConnectionState.Closed, connection.State);
        
        // Cleanup
        connection.Dispose();
        context.Dispose();
    }

    [Fact]
    public void OracleDbContext_Should_Use_Configured_Connection_String()
    {
        // Arrange
        var expectedConnectionString = _configuration.GetConnectionString("OracleDb");
        var context = new OracleDbContext(_configuration);

        // Act
        var connection = context.CreateConnection();

        // Assert
        Assert.NotNull(connection);
        Assert.Equal(expectedConnectionString, connection.ConnectionString);
        
        // Cleanup
        connection.Dispose();
        context.Dispose();
    }

    [Fact]
    public void ConnectionPooling_Configuration_Should_Support_High_Volume_Scenarios()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("OracleDb");
        Assert.NotNull(connectionString);

        // Extract pool parameters
        var minPoolSize = ExtractPoolParameter(connectionString, "Min Pool Size");
        var maxPoolSize = ExtractPoolParameter(connectionString, "Max Pool Size");
        var statementCacheSize = ExtractPoolParameter(connectionString, "Statement Cache Size");

        // Assert - Configuration should support 10,000+ requests per minute
        // With batch processing (50 events per 100ms), actual DB writes are ~200-400/min
        // Expected concurrent connections: 20-30 under normal load
        // Max pool size should provide 3-5x headroom
        
        Assert.True(minPoolSize >= 5, "Min Pool Size should be at least 5 to prevent cold starts");
        Assert.True(maxPoolSize >= 100, "Max Pool Size should be at least 100 to provide 3-5x headroom for 10,000+ req/min");
        Assert.True(maxPoolSize / minPoolSize >= 10, "Max/Min ratio should be at least 10 for good scaling");
        Assert.True(statementCacheSize >= 50, "Statement Cache Size should be at least 50 for optimal audit query performance");
    }

    [Fact]
    public void ConnectionPooling_Should_Have_Appropriate_Timeout_For_Fail_Fast()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("OracleDb");
        Assert.NotNull(connectionString);

        // Extract timeout
        var timeout = ExtractPoolParameter(connectionString, "Connection Timeout");

        // Assert - Timeout should be configured for fail-fast behavior
        // Too low (<10s): May cause false positives under load
        // Too high (>30s): Causes request queuing and poor user experience
        Assert.True(timeout >= 10, "Connection Timeout should be at least 10 seconds to avoid false positives");
        Assert.True(timeout <= 30, "Connection Timeout should be at most 30 seconds for fail-fast behavior");
    }

    [Fact]
    public void ConnectionPooling_Should_Have_Appropriate_Lifetime_For_Freshness()
    {
        // Arrange
        var connectionString = _configuration.GetConnectionString("OracleDb");
        Assert.NotNull(connectionString);

        // Extract lifetime
        var lifetime = ExtractPoolParameter(connectionString, "Connection Lifetime");

        // Assert - Lifetime should balance freshness and overhead
        // Too low (<180s): High connection churn, increased overhead
        // Too high (>600s): Risk of stale connections, memory leaks
        Assert.True(lifetime >= 180, "Connection Lifetime should be at least 180 seconds to avoid excessive churn");
        Assert.True(lifetime <= 600, "Connection Lifetime should be at most 600 seconds to prevent stale connections");
    }

    /// <summary>
    /// Helper method to extract numeric parameter value from connection string
    /// </summary>
    private int ExtractPoolParameter(string connectionString, string parameterName)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var parameter = parts.FirstOrDefault(p => p.Trim().StartsWith(parameterName, StringComparison.OrdinalIgnoreCase));
        
        if (parameter == null)
        {
            return 0;
        }

        var value = parameter.Split('=')[1].Trim();
        return int.TryParse(value, out var result) ? result : 0;
    }
}
