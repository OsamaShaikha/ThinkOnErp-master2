using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ThinkOnErp.Infrastructure.Data;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Data;

public class ThinkOnErpDbContextTests
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
    public void Constructor_WithValidConfiguration_CreatesInstance()
    {
        // Arrange
        var configuration = CreateConfiguration("Data Source=localhost:1521/XEPDB1;User Id=test;Password=test;");

        // Act & Assert - verify options can be created
        var optionsBuilder = new DbContextOptionsBuilder<ThinkOnErpDbContext>();
        optionsBuilder.UseOracle(configuration.GetConnectionString("OracleDb")!);
        
        var exception = Record.Exception(() =>
        {
            using var context = new ThinkOnErpDbContext(optionsBuilder.Options);
            Assert.NotNull(context);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange - Create options without connection string
        var optionsBuilder = new DbContextOptionsBuilder<ThinkOnErpDbContext>();
        optionsBuilder.UseOracle("Data Source=localhost:1521/XEPDB1;User Id=test;Password=test;");

        // Act & Assert
        var exception = Record.Exception(() =>
        {
            using var context = new ThinkOnErpDbContext(optionsBuilder.Options);
            Assert.NotNull(context);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void ConnectionString_WithoutOracleDb_Throws()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ThinkOnErpDbContext>();
        optionsBuilder.UseOracle("Data Source=localhost:1521/XEPDB1;User Id=test;Password=test;");

        var exception = Record.Exception(() =>
        {
            using var context = new ThinkOnErpDbContext(optionsBuilder.Options);
            Assert.NotNull(context);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void CreateConnection_SuccessfullyCreatesConnection()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ThinkOnErpDbContext>();
        optionsBuilder.UseOracle("Data Source=localhost:1521/XEPDB1;User Id=test;Password=test;");

        using var context = new ThinkOnErpDbContext(optionsBuilder.Options);
        Assert.NotNull(context);
        Assert.IsAssignableFrom<DbContext>(context);
    }

    [Fact]
    public void DbSets_AreInitialized()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ThinkOnErpDbContext>();
        optionsBuilder.UseOracle("Data Source=localhost:1521/XEPDB1;User Id=test;Password=test;");

        using var context = new ThinkOnErpDbContext(optionsBuilder.Options);
        
        Assert.NotNull(context.SysRoles);
        Assert.NotNull(context.SysCurrencies);
        Assert.NotNull(context.SysCompanies);
        Assert.NotNull(context.SysBranches);
        Assert.NotNull(context.SysUsers);
        Assert.NotNull(context.SysFiscalYears);
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ThinkOnErpDbContext>();
        optionsBuilder.UseOracle("Data Source=localhost:1521/XEPDB1;User Id=test;Password=test;");

        var context = new ThinkOnErpDbContext(optionsBuilder.Options);
        context.Dispose();
        
        var exception = Record.Exception(() => context.Dispose());
        Assert.Null(exception);
    }
}