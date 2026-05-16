using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oracle.EntityFrameworkCore;
using Xunit;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;

namespace ThinkOnErp.Infrastructure.Tests.Integration;

/// <summary>
/// Integration tests for CurrencyRepository using real Oracle database connection.
/// Tests EF Core implementation against actual Oracle database with stored procedures.
/// 
/// **Validates: Requirements REQ-12**
/// - Requirement 12: Testing Strategy - Integration tests that verify database operations
/// - Tests stored procedure execution (if applicable)
/// - Tests output parameter retrieval
/// - Tests transaction rollback
/// - Verifies data persistence
/// </summary>
public class CurrencyRepositoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ThinkOnErpDbContext _context;
    private readonly ICurrencyRepository _repository;
    private readonly ILogger<CurrencyRepository> _logger;
    private readonly List<long> _createdIds = new();

    public CurrencyRepositoryIntegrationTests()
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

        // Add repository
        services.AddScoped<ICurrencyRepository, CurrencyRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ThinkOnErpDbContext>();
        _repository = _serviceProvider.GetRequiredService<ICurrencyRepository>();
        _logger = _serviceProvider.GetRequiredService<ILogger<CurrencyRepository>>();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnCurrenciesFromDatabase()
    {
        // Act
        var currencies = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(currencies);
        Assert.NotEmpty(currencies);
        Assert.All(currencies, c =>
        {
            Assert.True(c.RowId > 0);
            Assert.False(string.IsNullOrWhiteSpace(c.RowDesc));
            Assert.False(string.IsNullOrWhiteSpace(c.RowDescE));
        });
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnCurrency()
    {
        // Arrange - Get first currency from database
        var currencies = await _repository.GetAllAsync();
        Assert.NotEmpty(currencies);
        var existingId = currencies.First().RowId;

        // Act
        var currency = await _repository.GetByIdAsync(existingId);

        // Assert
        Assert.NotNull(currency);
        Assert.Equal(existingId, currency.RowId);
        Assert.False(string.IsNullOrWhiteSpace(currency.RowDesc));
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var invalidId = -999999L;

        // Act
        var currency = await _repository.GetByIdAsync(invalidId);

        // Assert
        Assert.Null(currency);
    }

    [Fact]
    public async Task CreateAsync_ShouldInsertCurrencyAndReturnGeneratedId()
    {
        // Arrange
        var newCurrency = new SysCurrency
        {
            RowDesc = $"Test Currency {Guid.NewGuid()}",
            RowDescE = $"Test Currency EN {Guid.NewGuid()}",
            CurrencyCode = $"TST{DateTime.Now.Ticks % 1000}",
            CurrencySymbol = "T$",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        var generatedId = await _repository.CreateAsync(newCurrency);
        _createdIds.Add(generatedId);

        // Assert
        Assert.True(generatedId > 0, "Generated ID should be positive");
        Assert.Equal(generatedId, newCurrency.RowId);

        // Verify persistence
        var retrievedCurrency = await _repository.GetByIdAsync(generatedId);
        Assert.NotNull(retrievedCurrency);
        Assert.Equal(newCurrency.RowDesc, retrievedCurrency.RowDesc);
        Assert.Equal(newCurrency.RowDescE, retrievedCurrency.RowDescE);
        Assert.Equal(newCurrency.CurrencyCode, retrievedCurrency.CurrencyCode);
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyExistingCurrency()
    {
        // Arrange - Create a currency first
        var newCurrency = new SysCurrency
        {
            RowDesc = $"Update Test {Guid.NewGuid()}",
            RowDescE = $"Update Test EN {Guid.NewGuid()}",
            CurrencyCode = $"UPD{DateTime.Now.Ticks % 1000}",
            CurrencySymbol = "U$",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newCurrency);
        _createdIds.Add(createdId);

        // Act - Update the currency
        var currencyToUpdate = await _repository.GetByIdAsync(createdId);
        Assert.NotNull(currencyToUpdate);
        
        var updatedDesc = $"Updated {Guid.NewGuid()}";
        currencyToUpdate.RowDesc = updatedDesc;
        currencyToUpdate.UpdateUser = "IntegrationTest";
        currencyToUpdate.UpdateDate = DateTime.Now;

        var rowsAffected = await _repository.UpdateAsync(currencyToUpdate);

        // Assert
        Assert.Equal(1, rowsAffected);

        // Verify the update persisted
        var retrievedCurrency = await _repository.GetByIdAsync(createdId);
        Assert.NotNull(retrievedCurrency);
        Assert.Equal(updatedDesc, retrievedCurrency.RowDesc);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveCurrency()
    {
        // Arrange - Create a currency first
        var newCurrency = new SysCurrency
        {
            RowDesc = $"Delete Test {Guid.NewGuid()}",
            RowDescE = $"Delete Test EN {Guid.NewGuid()}",
            CurrencyCode = $"DEL{DateTime.Now.Ticks % 1000}",
            CurrencySymbol = "D$",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newCurrency);

        // Act - Delete the currency
        var rowsAffected = await _repository.DeleteAsync(createdId);

        // Assert
        Assert.Equal(1, rowsAffected);

        // Verify the currency is deleted
        var retrievedCurrency = await _repository.GetByIdAsync(createdId);
        Assert.Null(retrievedCurrency);
    }

    [Fact]
    public async Task TransactionRollback_ShouldNotPersistChanges()
    {
        // Arrange
        var newCurrency = new SysCurrency
        {
            RowDesc = $"Rollback Test {Guid.NewGuid()}",
            RowDescE = $"Rollback Test EN {Guid.NewGuid()}",
            CurrencyCode = $"RBK{DateTime.Now.Ticks % 1000}",
            CurrencySymbol = "R$",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act - Create within a transaction and rollback
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Currencies.Add(newCurrency);
            await _context.SaveChangesAsync();
            
            var generatedId = newCurrency.RowId;
            Assert.True(generatedId > 0);

            // Rollback the transaction
            await transaction.RollbackAsync();

            // Assert - Verify the currency was not persisted
            var retrievedCurrency = await _repository.GetByIdAsync(generatedId);
            Assert.Null(retrievedCurrency);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [Fact]
    public async Task TransactionCommit_ShouldPersistChanges()
    {
        // Arrange
        var newCurrency = new SysCurrency
        {
            RowDesc = $"Commit Test {Guid.NewGuid()}",
            RowDescE = $"Commit Test EN {Guid.NewGuid()}",
            CurrencyCode = $"CMT{DateTime.Now.Ticks % 1000}",
            CurrencySymbol = "C$",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        long generatedId = 0;

        // Act - Create within a transaction and commit
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Currencies.Add(newCurrency);
            await _context.SaveChangesAsync();
            
            generatedId = newCurrency.RowId;
            _createdIds.Add(generatedId);
            Assert.True(generatedId > 0);

            // Commit the transaction
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // Assert - Verify the currency was persisted
        var retrievedCurrency = await _repository.GetByIdAsync(generatedId);
        Assert.NotNull(retrievedCurrency);
        Assert.Equal(newCurrency.RowDesc, retrievedCurrency.RowDesc);
    }

    [Fact]
    public async Task SequenceGeneration_ShouldGenerateUniqueIds()
    {
        // Arrange
        var currency1 = new SysCurrency
        {
            RowDesc = $"Sequence Test 1 {Guid.NewGuid()}",
            RowDescE = $"Sequence Test 1 EN {Guid.NewGuid()}",
            CurrencyCode = $"SQ1{DateTime.Now.Ticks % 1000}",
            CurrencySymbol = "S1",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        var currency2 = new SysCurrency
        {
            RowDesc = $"Sequence Test 2 {Guid.NewGuid()}",
            RowDescE = $"Sequence Test 2 EN {Guid.NewGuid()}",
            CurrencyCode = $"SQ2{DateTime.Now.Ticks % 1000}",
            CurrencySymbol = "S2",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        var id1 = await _repository.CreateAsync(currency1);
        var id2 = await _repository.CreateAsync(currency2);
        _createdIds.Add(id1);
        _createdIds.Add(id2);

        // Assert
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.NotEqual(id1, id2);
    }

    public void Dispose()
    {
        // Cleanup - Delete all created test data
        foreach (var id in _createdIds)
        {
            try
            {
                _repository.DeleteAsync(id).GetAwaiter().GetResult();
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
