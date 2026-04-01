using Microsoft.Extensions.Configuration;
using ThinkOnErp.Infrastructure.Data;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Data;

public class OracleDbContextTests
{
    private static IConfiguration CreateConfiguration(string? connectionString)
    {
        var configData = new Dictionary<string, string?>();
        if (connectionString != null)
        {
            configData["ConnectionStrings:OracleDb"] = connectionString;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [Fact]
    public void Constructor_WithValidConfiguration_ShouldReadConnectionString()
    {
        // Arrange
        var connectionString = "Data Source=localhost:1521/ORCL;User Id=testuser;Password=testpass;";
        var configuration = CreateConfiguration(connectionString);

        // Act
        var context = new OracleDbContext(configuration);

        // Assert
        Assert.NotNull(context);
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new OracleDbContext(null!));
        Assert.Equal("configuration", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithMissingConnectionString_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var configuration = CreateConfiguration(null);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => new OracleDbContext(configuration));
        Assert.Contains("OracleDb", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void CreateConnection_ShouldReturnOracleConnection()
    {
        // Arrange
        var connectionString = "Data Source=localhost:1521/ORCL;User Id=testuser;Password=testpass;";
        var configuration = CreateConfiguration(connectionString);
        var context = new OracleDbContext(configuration);

        // Act
        var connection = context.CreateConnection();

        // Assert
        Assert.NotNull(connection);
        Assert.Equal(connectionString, connection.ConnectionString);
    }

    [Fact]
    public void CreateConnection_ShouldReturnNewInstanceEachTime()
    {
        // Arrange
        var connectionString = "Data Source=localhost:1521/ORCL;User Id=testuser;Password=testpass;";
        var configuration = CreateConfiguration(connectionString);
        var context = new OracleDbContext(configuration);

        // Act
        var connection1 = context.CreateConnection();
        var connection2 = context.CreateConnection();

        // Assert
        Assert.NotNull(connection1);
        Assert.NotNull(connection2);
        Assert.NotSame(connection1, connection2);

        // Cleanup
        connection1.Dispose();
        connection2.Dispose();
    }

    [Fact]
    public void Dispose_ShouldNotThrowException()
    {
        // Arrange
        var connectionString = "Data Source=localhost:1521/ORCL;User Id=testuser;Password=testpass;";
        var configuration = CreateConfiguration(connectionString);
        var context = new OracleDbContext(configuration);

        // Act & Assert
        context.Dispose();
        // Should not throw any exception
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrowException()
    {
        // Arrange
        var connectionString = "Data Source=localhost:1521/ORCL;User Id=testuser;Password=testpass;";
        var configuration = CreateConfiguration(connectionString);
        var context = new OracleDbContext(configuration);

        // Act & Assert
        context.Dispose();
        context.Dispose(); // Second call should not throw
        // Should not throw any exception
    }
}
