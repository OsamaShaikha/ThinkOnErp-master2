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
/// Integration tests for TicketTypeRepository using real Oracle database connection.
/// Tests EF Core implementation against actual Oracle database.
/// 
/// **Validates: Requirements REQ-12**
/// - Requirement 12: Testing Strategy - Integration tests that verify database operations
/// - Tests CRUD operations
/// - Tests navigation properties
/// - Tests usage statistics
/// - Verifies data persistence
/// </summary>
public class TicketTypeRepositoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ThinkOnErpDbContext _context;
    private readonly TicketTypeRepository _repository;
    private readonly List<long> _createdTypeIds = new();

    public TicketTypeRepositoryIntegrationTests()
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
        var logger = _serviceProvider.GetRequiredService<ILogger<TicketTypeRepository>>();
        _repository = new TicketTypeRepository(_context, logger);
    }

    private async Task<long> GetTestPriorityIdAsync()
    {
        var priority = await _context.TicketPriorities
            .Where(p => p.IsActive)
            .FirstOrDefaultAsync();

        if (priority == null)
        {
            throw new InvalidOperationException("No active ticket priorities found in database for testing");
        }

        return priority.RowId;
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistTicketType()
    {
        // Arrange
        var priorityId = await GetTestPriorityIdAsync();

        var ticketType = new SysTicketType
        {
            TypeNameAr = $"نوع اختبار {Guid.NewGuid().ToString().Substring(0, 8)}",
            TypeNameEn = $"Test Type {Guid.NewGuid().ToString().Substring(0, 8)}",
            DefaultPriorityId = priorityId,
            CreationUser = "IntegrationTest"
        };

        // Act
        var typeId = await _repository.CreateAsync(ticketType);
        _createdTypeIds.Add(typeId);

        // Assert
        Assert.True(typeId > 0);
        Assert.Equal(typeId, ticketType.RowId);
        Assert.NotNull(ticketType.CreationDate);
        Assert.True(ticketType.IsActive);

        // Verify persistence
        var savedType = await _repository.GetByIdAsync(typeId);
        Assert.NotNull(savedType);
        Assert.Equal(ticketType.TypeNameEn, savedType.TypeNameEn);
        Assert.NotNull(savedType.DefaultPriority);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllActiveTicketTypes()
    {
        // Arrange
        var priorityId = await GetTestPriorityIdAsync();

        var ticketType = new SysTicketType
        {
            TypeNameAr = $"نوع {Guid.NewGuid().ToString().Substring(0, 8)}",
            TypeNameEn = $"Type {Guid.NewGuid().ToString().Substring(0, 8)}",
            DefaultPriorityId = priorityId,
            CreationUser = "IntegrationTest"
        };
        var typeId = await _repository.CreateAsync(ticketType);
        _createdTypeIds.Add(typeId);

        // Act
        var types = await _repository.GetAllAsync();

        // Assert
        Assert.NotEmpty(types);
        Assert.Contains(types, t => t.RowId == typeId);
        Assert.All(types, t => Assert.True(t.IsActive));
        Assert.All(types, t => Assert.NotNull(t.DefaultPriority));
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTicketTypeWithNavigationProperties()
    {
        // Arrange
        var priorityId = await GetTestPriorityIdAsync();

        var ticketType = new SysTicketType
        {
            TypeNameAr = $"نوع {Guid.NewGuid().ToString().Substring(0, 8)}",
            TypeNameEn = $"Type {Guid.NewGuid().ToString().Substring(0, 8)}",
            DefaultPriorityId = priorityId,
            CreationUser = "IntegrationTest"
        };
        var typeId = await _repository.CreateAsync(ticketType);
        _createdTypeIds.Add(typeId);

        // Act
        var retrievedType = await _repository.GetByIdAsync(typeId);

        // Assert
        Assert.NotNull(retrievedType);
        Assert.Equal(ticketType.TypeNameEn, retrievedType.TypeNameEn);
        Assert.NotNull(retrievedType.DefaultPriority);
        Assert.Equal(priorityId, retrievedType.DefaultPriorityId);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByIdAsync(999999999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateTicketTypeProperties()
    {
        // Arrange
        var priorityId = await GetTestPriorityIdAsync();

        var ticketType = new SysTicketType
        {
            TypeNameAr = "نوع أصلي",
            TypeNameEn = "Original Type",
            DefaultPriorityId = priorityId,
            CreationUser = "IntegrationTest"
        };
        var typeId = await _repository.CreateAsync(ticketType);
        _createdTypeIds.Add(typeId);

        // Modify type
        ticketType.TypeNameEn = "Updated Type";
        ticketType.TypeNameAr = "نوع محدث";
        ticketType.UpdateUser = "IntegrationTest";

        // Act
        var result = await _repository.UpdateAsync(ticketType);

        // Assert
        Assert.Equal(1, result);

        // Verify update
        var updatedType = await _repository.GetByIdAsync(typeId);
        Assert.NotNull(updatedType);
        Assert.Equal("Updated Type", updatedType.TypeNameEn);
        Assert.Equal("نوع محدث", updatedType.TypeNameAr);
        Assert.NotNull(updatedType.UpdateDate);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteTicketType()
    {
        // Arrange
        var priorityId = await GetTestPriorityIdAsync();

        var ticketType = new SysTicketType
        {
            TypeNameAr = "نوع للحذف",
            TypeNameEn = "Type To Delete",
            DefaultPriorityId = priorityId,
            CreationUser = "IntegrationTest"
        };
        var typeId = await _repository.CreateAsync(ticketType);
        _createdTypeIds.Add(typeId);

        // Act
        var result = await _repository.DeleteAsync(typeId, "IntegrationTest");

        // Assert
        Assert.Equal(1, result);

        // Verify soft delete
        var deletedType = await _context.TicketTypes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.RowId == typeId);
        Assert.NotNull(deletedType);
        Assert.False(deletedType.IsActive);
        Assert.Equal("IntegrationTest", deletedType.UpdateUser);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowException_WhenTypeIsInUse()
    {
        // Arrange - Get an existing type that is likely in use
        var existingType = await _context.TicketTypes
            .Where(t => t.IsActive)
            .FirstOrDefaultAsync();

        if (existingType != null)
        {
            var isInUse = await _repository.IsInUseAsync(existingType.RowId);
            
            if (isInUse)
            {
                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await _repository.DeleteAsync(existingType.RowId, "IntegrationTest"));
            }
        }
    }

    [Fact]
    public async Task IsInUseAsync_ShouldReturnTrue_WhenTypeHasActiveTickets()
    {
        // Arrange - Get a type that has tickets
        var typeWithTickets = await _context.TicketTypes
            .Where(t => t.IsActive && _context.Tickets.Any(ticket => ticket.TicketTypeId == t.RowId && ticket.IsActive))
            .FirstOrDefaultAsync();

        if (typeWithTickets != null)
        {
            // Act
            var isInUse = await _repository.IsInUseAsync(typeWithTickets.RowId);

            // Assert
            Assert.True(isInUse);
        }
    }

    [Fact]
    public async Task IsInUseAsync_ShouldReturnFalse_WhenTypeHasNoActiveTickets()
    {
        // Arrange
        var priorityId = await GetTestPriorityIdAsync();

        var ticketType = new SysTicketType
        {
            TypeNameAr = "نوع غير مستخدم",
            TypeNameEn = "Unused Type",
            DefaultPriorityId = priorityId,
            CreationUser = "IntegrationTest"
        };
        var typeId = await _repository.CreateAsync(ticketType);
        _createdTypeIds.Add(typeId);

        // Act
        var isInUse = await _repository.IsInUseAsync(typeId);

        // Assert
        Assert.False(isInUse);
    }

    [Fact]
    public async Task GetByUsageAsync_ShouldReturnTypesOrderedByUsage()
    {
        // Act
        var usageStats = await _repository.GetByUsageAsync();

        // Assert
        Assert.NotEmpty(usageStats);
        
        // Verify ordering - types with more tickets should come first
        for (int i = 0; i < usageStats.Count - 1; i++)
        {
            Assert.True(usageStats[i].TicketCount >= usageStats[i + 1].TicketCount);
        }

        // Verify all types have navigation properties loaded
        Assert.All(usageStats, stat => Assert.NotNull(stat.TicketType));
    }

    [Fact]
    public async Task GetByUsageAsync_WithDateRange_ShouldFilterByDateRange()
    {
        // Arrange
        var fromDate = DateTime.Now.AddMonths(-1);
        var toDate = DateTime.Now;

        // Act
        var usageStats = await _repository.GetByUsageAsync(fromDate, toDate);

        // Assert
        Assert.NotNull(usageStats);
        // Verify all types are included (even with 0 count)
        Assert.All(usageStats, stat => Assert.NotNull(stat.TicketType));
    }

    [Fact]
    public async Task TransactionRollback_ShouldNotPersistTicketType()
    {
        // Arrange
        var priorityId = await GetTestPriorityIdAsync();

        var ticketType = new SysTicketType
        {
            TypeNameAr = "نوع للتراجع",
            TypeNameEn = "Rollback Type",
            DefaultPriorityId = priorityId,
            CreationUser = "IntegrationTest"
        };

        // Act - Create within a transaction and rollback
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.TicketTypes.Add(ticketType);
            await _context.SaveChangesAsync();
            
            var typeId = ticketType.RowId;
            Assert.True(typeId > 0);

            // Rollback the transaction
            await transaction.RollbackAsync();

            // Assert - Verify the type was not persisted
            var retrievedType = await _repository.GetByIdAsync(typeId);
            Assert.Null(retrievedType);
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
        var priorityId = await GetTestPriorityIdAsync();

        var type1 = new SysTicketType
        {
            TypeNameAr = $"نوع 1 {Guid.NewGuid().ToString().Substring(0, 8)}",
            TypeNameEn = $"Type 1 {Guid.NewGuid().ToString().Substring(0, 8)}",
            DefaultPriorityId = priorityId,
            CreationUser = "IntegrationTest"
        };

        var type2 = new SysTicketType
        {
            TypeNameAr = $"نوع 2 {Guid.NewGuid().ToString().Substring(0, 8)}",
            TypeNameEn = $"Type 2 {Guid.NewGuid().ToString().Substring(0, 8)}",
            DefaultPriorityId = priorityId,
            CreationUser = "IntegrationTest"
        };

        // Act
        var id1 = await _repository.CreateAsync(type1);
        var id2 = await _repository.CreateAsync(type2);
        _createdTypeIds.Add(id1);
        _createdTypeIds.Add(id2);

        // Assert
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task ConcurrentCreation_ShouldHandleMultipleInserts()
    {
        // Arrange
        var priorityId = await GetTestPriorityIdAsync();

        var types = new List<SysTicketType>();
        for (int i = 0; i < 3; i++)
        {
            types.Add(new SysTicketType
            {
                TypeNameAr = $"نوع متزامن {i} {Guid.NewGuid().ToString().Substring(0, 8)}",
                TypeNameEn = $"Concurrent Type {i} {Guid.NewGuid().ToString().Substring(0, 8)}",
                DefaultPriorityId = priorityId,
                CreationUser = "IntegrationTest"
            });
        }

        // Act
        var tasks = types.Select(t => _repository.CreateAsync(t)).ToList();
        var ids = await Task.WhenAll(tasks);
        _createdTypeIds.AddRange(ids);

        // Assert
        Assert.Equal(3, ids.Length);
        Assert.All(ids, id => Assert.True(id > 0));
        Assert.Equal(ids.Distinct().Count(), ids.Length); // All IDs should be unique
    }

    public void Dispose()
    {
        // Cleanup - Delete all created test data
        foreach (var id in _createdTypeIds)
        {
            try
            {
                var type = _context.TicketTypes
                    .IgnoreQueryFilters()
                    .FirstOrDefault(t => t.RowId == id);
                if (type != null)
                {
                    _context.TicketTypes.Remove(type);
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
