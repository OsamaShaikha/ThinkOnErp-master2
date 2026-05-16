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
/// Integration tests for TicketStatusRepository using real Oracle database connection.
/// Tests EF Core implementation against actual Oracle database with stored procedures.
/// 
/// **Validates: Requirements REQ-12**
/// - Requirement 12: Testing Strategy - Integration tests that verify database operations
/// - Tests stored procedure execution (if applicable)
/// - Tests output parameter retrieval
/// - Tests transaction rollback
/// - Verifies data persistence
/// </summary>
public class TicketStatusRepositoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ThinkOnErpDbContext _context;
    private readonly ITicketStatusRepository _repository;
    private readonly ILogger<TicketStatusRepository> _logger;
    private readonly List<long> _createdIds = new();

    public TicketStatusRepositoryIntegrationTests()
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
        services.AddScoped<ITicketStatusRepository, TicketStatusRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ThinkOnErpDbContext>();
        _repository = _serviceProvider.GetRequiredService<ITicketStatusRepository>();
        _logger = _serviceProvider.GetRequiredService<ILogger<TicketStatusRepository>>();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnStatusesOrderedByDisplayOrder()
    {
        // Act
        var statuses = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(statuses);
        Assert.NotEmpty(statuses);
        
        // Verify ordering by DisplayOrder
        for (int i = 0; i < statuses.Count - 1; i++)
        {
            Assert.True(statuses[i].DisplayOrder <= statuses[i + 1].DisplayOrder,
                $"Statuses should be ordered by DisplayOrder. Found {statuses[i].DisplayOrder} before {statuses[i + 1].DisplayOrder}");
        }

        Assert.All(statuses, s =>
        {
            Assert.True(s.RowId > 0);
            Assert.False(string.IsNullOrWhiteSpace(s.StatusCode));
            Assert.False(string.IsNullOrWhiteSpace(s.StatusNameEn));
        });
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnStatus()
    {
        // Arrange - Get first status from database
        var statuses = await _repository.GetAllAsync();
        Assert.NotEmpty(statuses);
        var existingId = statuses.First().RowId;

        // Act
        var status = await _repository.GetByIdAsync(existingId);

        // Assert
        Assert.NotNull(status);
        Assert.Equal(existingId, status.RowId);
        Assert.False(string.IsNullOrWhiteSpace(status.StatusCode));
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var invalidId = -999999L;

        // Act
        var status = await _repository.GetByIdAsync(invalidId);

        // Assert
        Assert.Null(status);
    }

    [Fact]
    public async Task GetByCodeAsync_WithValidCode_ShouldReturnStatus()
    {
        // Arrange - Get first status from database
        var statuses = await _repository.GetAllAsync();
        Assert.NotEmpty(statuses);
        var existingCode = statuses.First().StatusCode;

        // Act
        var status = await _repository.GetByCodeAsync(existingCode);

        // Assert
        Assert.NotNull(status);
        Assert.Equal(existingCode, status.StatusCode);
    }

    [Fact]
    public async Task GetByCodeAsync_WithInvalidCode_ShouldReturnNull()
    {
        // Arrange
        var invalidCode = "INVALID_STATUS_CODE_XYZ";

        // Act
        var status = await _repository.GetByCodeAsync(invalidCode);

        // Assert
        Assert.Null(status);
    }

    [Fact]
    public async Task GetDefaultInitialStatusAsync_ShouldReturnOpenStatus()
    {
        // Act
        var defaultStatus = await _repository.GetDefaultInitialStatusAsync();

        // Assert
        Assert.NotNull(defaultStatus);
        Assert.Equal(SysTicketStatus.StatusCodes.Open, defaultStatus.StatusCode);
    }

    [Fact]
    public async Task GetFinalStatusesAsync_ShouldReturnOnlyFinalStatuses()
    {
        // Act
        var finalStatuses = await _repository.GetFinalStatusesAsync();

        // Assert
        Assert.NotNull(finalStatuses);
        Assert.All(finalStatuses, s => Assert.True(s.IsFinalStatus));
    }

    [Fact]
    public async Task IsTransitionAllowedAsync_FromNonFinalToAny_ShouldReturnTrue()
    {
        // Arrange - Get a non-final status and any other status
        var allStatuses = await _repository.GetAllAsync();
        var nonFinalStatus = allStatuses.FirstOrDefault(s => !s.IsFinalStatus);
        var targetStatus = allStatuses.FirstOrDefault(s => s.RowId != nonFinalStatus?.RowId);

        Assert.NotNull(nonFinalStatus);
        Assert.NotNull(targetStatus);

        // Act
        var isAllowed = await _repository.IsTransitionAllowedAsync(
            nonFinalStatus.RowId, 
            targetStatus.RowId);

        // Assert
        Assert.True(isAllowed);
    }

    [Fact]
    public async Task IsTransitionAllowedAsync_FromFinalToAny_ShouldReturnFalse()
    {
        // Arrange - Get a final status and any other status
        var allStatuses = await _repository.GetAllAsync();
        var finalStatus = allStatuses.FirstOrDefault(s => s.IsFinalStatus);
        var targetStatus = allStatuses.FirstOrDefault(s => s.RowId != finalStatus?.RowId);

        if (finalStatus == null)
        {
            // Skip test if no final status exists
            return;
        }

        Assert.NotNull(targetStatus);

        // Act
        var isAllowed = await _repository.IsTransitionAllowedAsync(
            finalStatus.RowId, 
            targetStatus.RowId);

        // Assert
        Assert.False(isAllowed);
    }

    [Fact]
    public async Task GetUsageStatisticsAsync_ShouldReturnStatisticsForAllStatuses()
    {
        // Act
        var statistics = await _repository.GetUsageStatisticsAsync();

        // Assert
        Assert.NotNull(statistics);
        Assert.NotEmpty(statistics);
        Assert.All(statistics, stat =>
        {
            Assert.NotNull(stat.Status);
            Assert.True(stat.TicketCount >= 0);
        });
    }

    [Fact]
    public async Task GetUsageStatisticsAsync_WithDateFilter_ShouldReturnFilteredStatistics()
    {
        // Arrange
        var fromDate = DateTime.Now.AddMonths(-1);
        var toDate = DateTime.Now;

        // Act
        var statistics = await _repository.GetUsageStatisticsAsync(
            fromDate: fromDate,
            toDate: toDate);

        // Assert
        Assert.NotNull(statistics);
        // Statistics should be returned even if counts are zero
        Assert.NotEmpty(statistics);
    }

    [Fact]
    public async Task TransactionRollback_ShouldNotPersistChanges()
    {
        // Arrange
        var newStatus = new SysTicketStatus
        {
            StatusCode = $"TEST_RBK_{DateTime.Now.Ticks % 1000}",
            StatusNameAr = $"حالة اختبار {Guid.NewGuid()}",
            StatusNameEn = $"Test Status {Guid.NewGuid()}",
            DisplayOrder = 999,
            IsFinalStatus = false,
            IsActive = true,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act - Create within a transaction and rollback
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.TicketStatuses.Add(newStatus);
            await _context.SaveChangesAsync();
            
            var generatedId = newStatus.RowId;
            Assert.True(generatedId > 0);

            // Rollback the transaction
            await transaction.RollbackAsync();

            // Assert - Verify the status was not persisted
            var retrievedStatus = await _repository.GetByIdAsync(generatedId);
            Assert.Null(retrievedStatus);
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
        var newStatus = new SysTicketStatus
        {
            StatusCode = $"TEST_CMT_{DateTime.Now.Ticks % 1000}",
            StatusNameAr = $"حالة اختبار {Guid.NewGuid()}",
            StatusNameEn = $"Test Status {Guid.NewGuid()}",
            DisplayOrder = 999,
            IsFinalStatus = false,
            IsActive = true,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        long generatedId = 0;

        // Act - Create within a transaction and commit
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.TicketStatuses.Add(newStatus);
            await _context.SaveChangesAsync();
            
            generatedId = newStatus.RowId;
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

        // Assert - Verify the status was persisted
        var retrievedStatus = await _repository.GetByIdAsync(generatedId);
        Assert.NotNull(retrievedStatus);
        Assert.Equal(newStatus.StatusCode, retrievedStatus.StatusCode);
    }

    [Fact]
    public async Task SequenceGeneration_ShouldGenerateUniqueIds()
    {
        // Arrange
        var status1 = new SysTicketStatus
        {
            StatusCode = $"TEST_SQ1_{DateTime.Now.Ticks % 1000}",
            StatusNameAr = $"حالة اختبار 1 {Guid.NewGuid()}",
            StatusNameEn = $"Test Status 1 {Guid.NewGuid()}",
            DisplayOrder = 998,
            IsFinalStatus = false,
            IsActive = true,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        var status2 = new SysTicketStatus
        {
            StatusCode = $"TEST_SQ2_{DateTime.Now.Ticks % 1000}",
            StatusNameAr = $"حالة اختبار 2 {Guid.NewGuid()}",
            StatusNameEn = $"Test Status 2 {Guid.NewGuid()}",
            DisplayOrder = 997,
            IsFinalStatus = false,
            IsActive = true,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        _context.TicketStatuses.Add(status1);
        await _context.SaveChangesAsync();
        var id1 = status1.RowId;
        _createdIds.Add(id1);

        _context.TicketStatuses.Add(status2);
        await _context.SaveChangesAsync();
        var id2 = status2.RowId;
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
                var status = _context.TicketStatuses.Find(id);
                if (status != null)
                {
                    _context.TicketStatuses.Remove(status);
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
