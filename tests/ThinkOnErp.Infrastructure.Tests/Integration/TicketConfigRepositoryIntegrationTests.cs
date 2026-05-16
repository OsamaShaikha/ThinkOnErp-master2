using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;

namespace ThinkOnErp.Infrastructure.Tests.Integration;

/// <summary>
/// Integration tests for TicketConfigRepository using real Oracle database connection.
/// Tests EF Core implementation against actual Oracle database.
/// 
/// **Validates: Requirements REQ-12**
/// - Requirement 12: Testing Strategy - Integration tests that verify database operations
/// - Tests CRUD operations
/// - Tests configuration key lookups
/// - Tests configuration type filtering
/// - Verifies data persistence
/// </summary>
public class TicketConfigRepositoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ThinkOnErpDbContext _context;
    private readonly TicketConfigRepository _repository;
    private readonly List<long> _createdConfigIds = new();

    public TicketConfigRepositoryIntegrationTests()
    {
        // Setup configuration with Oracle connection string
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OracleDb"] = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=178.104.126.99)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XEPDB1)));User Id=THINKON_ERP;Password=THINKON_ERP;Pooling=true;Min Pool Size=5;Max Pool Size=100;Connection Timeout=15;"
            })
            .Build();

        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Add DbContext with Oracle provider
        services.AddDbContext<ThinkOnErpDbContext>(options =>
        {
            options.UseOracle(
                configuration.GetConnectionString("OracleDb"),
                oracleOptions =>
                {
                    oracleOptions.UseOracleSQLCompatibility("11");
                    oracleOptions.CommandTimeout(30);
                });
        });

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ThinkOnErpDbContext>();
        var logger = _serviceProvider.GetRequiredService<ILogger<TicketConfigRepository>>();
        _repository = new TicketConfigRepository(_context, logger);
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistTicketConfig()
    {
        // Arrange
        var uniqueKey = $"TEST_CONFIG_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10)}";
        var config = new SysTicketConfig
        {
            ConfigKey = uniqueKey,
            ConfigValue = "TestValue",
            ConfigType = "General",
            Description = "Integration test configuration",
            CreationUser = "IntegrationTest"
        };

        // Act
        var configId = await _repository.CreateAsync(config);
        _createdConfigIds.Add(configId);

        // Assert
        Assert.True(configId > 0);
        Assert.Equal(configId, config.RowId);
        Assert.NotNull(config.CreationDate);
        Assert.True(config.IsActive);

        // Verify persistence
        var savedConfig = await _repository.GetByKeyAsync(uniqueKey);
        Assert.NotNull(savedConfig);
        Assert.Equal(config.ConfigValue, savedConfig.ConfigValue);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllActiveConfigs()
    {
        // Arrange
        var uniqueKey = $"TEST_CONFIG_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10)}";
        var config = new SysTicketConfig
        {
            ConfigKey = uniqueKey,
            ConfigValue = "Value",
            ConfigType = "General",
            Description = "Test",
            CreationUser = "IntegrationTest"
        };
        var configId = await _repository.CreateAsync(config);
        _createdConfigIds.Add(configId);

        // Act
        var configs = await _repository.GetAllAsync();

        // Assert
        Assert.NotEmpty(configs);
        Assert.Contains(configs, c => c.RowId == configId);
        Assert.All(configs, c => Assert.True(c.IsActive));
    }

    [Fact]
    public async Task GetByKeyAsync_ShouldReturnConfigByKey()
    {
        // Arrange
        var uniqueKey = $"TEST_CONFIG_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10)}";
        var config = new SysTicketConfig
        {
            ConfigKey = uniqueKey,
            ConfigValue = "TestValue123",
            ConfigType = "SLA",
            Description = "Test configuration",
            CreationUser = "IntegrationTest"
        };
        var configId = await _repository.CreateAsync(config);
        _createdConfigIds.Add(configId);

        // Act
        var retrievedConfig = await _repository.GetByKeyAsync(uniqueKey);

        // Assert
        Assert.NotNull(retrievedConfig);
        Assert.Equal(config.ConfigKey, retrievedConfig.ConfigKey);
        Assert.Equal(config.ConfigValue, retrievedConfig.ConfigValue);
        Assert.Equal(config.ConfigType, retrievedConfig.ConfigType);
    }

    [Fact]
    public async Task GetByKeyAsync_ShouldReturnNull_WhenKeyNotExists()
    {
        // Act
        var result = await _repository.GetByKeyAsync("NON_EXISTENT_KEY_12345");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByTypeAsync_ShouldReturnConfigsOfSpecificType()
    {
        // Arrange
        var uniqueKey = $"TEST_CONFIG_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10)}";
        var config = new SysTicketConfig
        {
            ConfigKey = uniqueKey,
            ConfigValue = "Value",
            ConfigType = "Notification",
            Description = "Test notification config",
            CreationUser = "IntegrationTest"
        };
        var configId = await _repository.CreateAsync(config);
        _createdConfigIds.Add(configId);

        // Act
        var configs = await _repository.GetByTypeAsync("Notification");

        // Assert
        Assert.NotEmpty(configs);
        Assert.Contains(configs, c => c.RowId == configId);
        Assert.All(configs, c => Assert.Equal("Notification", c.ConfigType));
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateConfigProperties()
    {
        // Arrange
        var uniqueKey = $"TEST_CONFIG_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10)}";
        var config = new SysTicketConfig
        {
            ConfigKey = uniqueKey,
            ConfigValue = "OriginalValue",
            ConfigType = "General",
            Description = "Original description",
            CreationUser = "IntegrationTest"
        };
        var configId = await _repository.CreateAsync(config);
        _createdConfigIds.Add(configId);

        // Modify config
        config.ConfigValue = "UpdatedValue";
        config.Description = "Updated description";
        config.UpdateUser = "IntegrationTest";

        // Act
        var result = await _repository.UpdateAsync(config);

        // Assert
        Assert.Equal(configId, result);

        // Verify update
        var updatedConfig = await _repository.GetByKeyAsync(uniqueKey);
        Assert.NotNull(updatedConfig);
        Assert.Equal("UpdatedValue", updatedConfig.ConfigValue);
        Assert.Equal("Updated description", updatedConfig.Description);
        Assert.NotNull(updatedConfig.UpdateDate);
    }

    [Fact]
    public async Task UpdateByKeyAsync_ShouldUpdateConfigValue()
    {
        // Arrange
        var uniqueKey = $"TEST_CONFIG_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10)}";
        var config = new SysTicketConfig
        {
            ConfigKey = uniqueKey,
            ConfigValue = "InitialValue",
            ConfigType = "General",
            Description = "Test",
            CreationUser = "IntegrationTest"
        };
        var configId = await _repository.CreateAsync(config);
        _createdConfigIds.Add(configId);

        // Act
        var result = await _repository.UpdateByKeyAsync(uniqueKey, "NewValue", "IntegrationTest");

        // Assert
        Assert.True(result);

        // Verify update
        var updatedConfig = await _repository.GetByKeyAsync(uniqueKey);
        Assert.NotNull(updatedConfig);
        Assert.Equal("NewValue", updatedConfig.ConfigValue);
        Assert.Equal("IntegrationTest", updatedConfig.UpdateUser);
        Assert.NotNull(updatedConfig.UpdateDate);
    }

    [Fact]
    public async Task UpdateByKeyAsync_ShouldReturnFalse_WhenKeyNotExists()
    {
        // Act
        var result = await _repository.UpdateByKeyAsync("NON_EXISTENT_KEY", "Value", "User");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteConfig()
    {
        // Arrange
        var uniqueKey = $"TEST_CONFIG_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10)}";
        var config = new SysTicketConfig
        {
            ConfigKey = uniqueKey,
            ConfigValue = "ToDelete",
            ConfigType = "General",
            Description = "Config to delete",
            CreationUser = "IntegrationTest"
        };
        var configId = await _repository.CreateAsync(config);
        _createdConfigIds.Add(configId);

        // Act
        var result = await _repository.DeleteAsync(configId, "IntegrationTest");

        // Assert
        Assert.True(result);

        // Verify soft delete
        var deletedConfig = await _context.TicketConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.RowId == configId);
        Assert.NotNull(deletedConfig);
        Assert.False(deletedConfig.IsActive);
        Assert.Equal("IntegrationTest", deletedConfig.UpdateUser);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenConfigNotExists()
    {
        // Act
        var result = await _repository.DeleteAsync(999999999, "User");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ConfigTypes_ShouldSupportAllStandardTypes()
    {
        // Arrange
        var configTypes = new[] { "SLA", "FileAttachment", "Notification", "Workflow", "General" };
        var createdIds = new List<long>();

        foreach (var type in configTypes)
        {
            var uniqueKey = $"TEST_{type}_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 8)}";
            var config = new SysTicketConfig
            {
                ConfigKey = uniqueKey,
                ConfigValue = $"Value for {type}",
                ConfigType = type,
                Description = $"Test {type} config",
                CreationUser = "IntegrationTest"
            };
            var id = await _repository.CreateAsync(config);
            createdIds.Add(id);
            _createdConfigIds.Add(id);
        }

        // Act & Assert
        foreach (var type in configTypes)
        {
            var configs = await _repository.GetByTypeAsync(type);
            Assert.NotEmpty(configs);
            Assert.Contains(configs, c => createdIds.Contains(c.RowId));
        }
    }

    [Fact]
    public async Task TransactionRollback_ShouldNotPersistConfig()
    {
        // Arrange
        var uniqueKey = $"TEST_CONFIG_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10)}";
        var config = new SysTicketConfig
        {
            ConfigKey = uniqueKey,
            ConfigValue = "RollbackValue",
            ConfigType = "General",
            Description = "Rollback test",
            CreationUser = "IntegrationTest"
        };

        // Act - Create within a transaction and rollback
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.TicketConfigs.Add(config);
            await _context.SaveChangesAsync();
            
            var configId = config.RowId;
            Assert.True(configId > 0);

            // Rollback the transaction
            await transaction.RollbackAsync();

            // Assert - Verify the config was not persisted
            var retrievedConfig = await _repository.GetByKeyAsync(uniqueKey);
            Assert.Null(retrievedConfig);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [Fact]
    public async Task SequenceGeneration_ShouldGenerateUniqueIds()
    {
        // Arrange
        var config1 = new SysTicketConfig
        {
            ConfigKey = $"TEST_CONFIG_1_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 8)}",
            ConfigValue = "Value1",
            ConfigType = "General",
            Description = "Test 1",
            CreationUser = "IntegrationTest"
        };

        var config2 = new SysTicketConfig
        {
            ConfigKey = $"TEST_CONFIG_2_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 8)}",
            ConfigValue = "Value2",
            ConfigType = "General",
            Description = "Test 2",
            CreationUser = "IntegrationTest"
        };

        // Act
        var id1 = await _repository.CreateAsync(config1);
        var id2 = await _repository.CreateAsync(config2);
        _createdConfigIds.Add(id1);
        _createdConfigIds.Add(id2);

        // Assert
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task ConcurrentCreation_ShouldHandleMultipleInserts()
    {
        // Arrange
        var configs = new List<SysTicketConfig>();
        for (int i = 0; i < 3; i++)
        {
            configs.Add(new SysTicketConfig
            {
                ConfigKey = $"TEST_CONCURRENT_{i}_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 8)}",
                ConfigValue = $"ConcurrentValue{i}",
                ConfigType = "General",
                Description = $"Concurrent test {i}",
                CreationUser = "IntegrationTest"
            });
        }

        // Act
        var tasks = configs.Select(c => _repository.CreateAsync(c)).ToList();
        var ids = await Task.WhenAll(tasks);
        _createdConfigIds.AddRange(ids);

        // Assert
        Assert.Equal(3, ids.Length);
        Assert.All(ids, id => Assert.True(id > 0));
        Assert.Equal(ids.Distinct().Count(), ids.Length); // All IDs should be unique
    }

    [Fact]
    public async Task ConfigOrdering_ShouldOrderByTypeAndKey()
    {
        // Arrange
        var configs = new List<SysTicketConfig>
        {
            new SysTicketConfig
            {
                ConfigKey = $"B_KEY_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 8)}",
                ConfigValue = "Value",
                ConfigType = "General",
                Description = "Test",
                CreationUser = "IntegrationTest"
            },
            new SysTicketConfig
            {
                ConfigKey = $"A_KEY_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 8)}",
                ConfigValue = "Value",
                ConfigType = "General",
                Description = "Test",
                CreationUser = "IntegrationTest"
            }
        };

        foreach (var config in configs)
        {
            var id = await _repository.CreateAsync(config);
            _createdConfigIds.Add(id);
        }

        // Act
        var allConfigs = await _repository.GetAllAsync();

        // Assert
        Assert.NotEmpty(allConfigs);
        
        // Verify ordering by ConfigType then ConfigKey
        for (int i = 0; i < allConfigs.Count - 1; i++)
        {
            var comparison = string.Compare(allConfigs[i].ConfigType, allConfigs[i + 1].ConfigType, StringComparison.Ordinal);
            if (comparison == 0)
            {
                // Same type, check key ordering
                Assert.True(string.Compare(allConfigs[i].ConfigKey, allConfigs[i + 1].ConfigKey, StringComparison.Ordinal) <= 0);
            }
        }
    }

    public void Dispose()
    {
        // Cleanup - Delete all created test data
        foreach (var id in _createdConfigIds)
        {
            try
            {
                var config = _context.TicketConfigs
                    .IgnoreQueryFilters()
                    .FirstOrDefault(c => c.RowId == id);
                if (config != null)
                {
                    _context.TicketConfigs.Remove(config);
                    _context.SaveChanges();
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}
