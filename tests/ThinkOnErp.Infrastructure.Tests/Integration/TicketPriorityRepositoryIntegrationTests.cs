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
/// Integration tests for TicketPriorityRepository using real Oracle database connection.
/// Tests EF Core implementation against actual Oracle database with stored procedures.
/// 
/// **Validates: Requirements REQ-12**
/// - Requirement 12: Testing Strategy - Integration tests that verify database operations
/// - Tests stored procedure execution (if applicable)
/// - Tests output parameter retrieval
/// - Tests transaction rollback
/// - Verifies data persistence
/// </summary>
public class TicketPriorityRepositoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ThinkOnErpDbContext _context;
    private readonly ITicketPriorityRepository _repository;
    private readonly ILogger<TicketPriorityRepository> _logger;
    private readonly List<long> _createdIds = new();

    public TicketPriorityRepositoryIntegrationTests()
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
        services.AddScoped<ITicketPriorityRepository, TicketPriorityRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ThinkOnErpDbContext>();
        _repository = _serviceProvider.GetRequiredService<ITicketPriorityRepository>();
        _logger = _serviceProvider.GetRequiredService<ILogger<TicketPriorityRepository>>();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnPrioritiesOrderedByLevel()
    {
        // Act
        var priorities = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(priorities);
        Assert.NotEmpty(priorities);
        
        // Verify ordering by PriorityLevel
        for (int i = 0; i < priorities.Count - 1; i++)
        {
            Assert.True(priorities[i].PriorityLevel <= priorities[i + 1].PriorityLevel,
                $"Priorities should be ordered by PriorityLevel. Found {priorities[i].PriorityLevel} before {priorities[i + 1].PriorityLevel}");
        }

        Assert.All(priorities, p =>
        {
            Assert.True(p.RowId > 0);
            Assert.True(p.PriorityLevel > 0);
            Assert.False(string.IsNullOrWhiteSpace(p.PriorityNameEn));
        });
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnPriority()
    {
        // Arrange - Get first priority from database
        var priorities = await _repository.GetAllAsync();
        Assert.NotEmpty(priorities);
        var existingId = priorities.First().RowId;

        // Act
        var priority = await _repository.GetByIdAsync(existingId);

        // Assert
        Assert.NotNull(priority);
        Assert.Equal(existingId, priority.RowId);
        Assert.True(priority.PriorityLevel > 0);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var invalidId = -999999L;

        // Act
        var priority = await _repository.GetByIdAsync(invalidId);

        // Assert
        Assert.Null(priority);
    }

    [Fact]
    public async Task GetByLevelAsync_WithValidLevel_ShouldReturnPriority()
    {
        // Arrange - Get first priority from database
        var priorities = await _repository.GetAllAsync();
        Assert.NotEmpty(priorities);
        var existingLevel = priorities.First().PriorityLevel;

        // Act
        var priority = await _repository.GetByLevelAsync(existingLevel);

        // Assert
        Assert.NotNull(priority);
        Assert.Equal(existingLevel, priority.PriorityLevel);
    }

    [Fact]
    public async Task GetByLevelAsync_WithInvalidLevel_ShouldReturnNull()
    {
        // Arrange
        var invalidLevel = 999;

        // Act
        var priority = await _repository.GetByLevelAsync(invalidLevel);

        // Assert
        Assert.Null(priority);
    }

    [Fact]
    public async Task GetDefaultPriorityAsync_ShouldReturnMediumPriority()
    {
        // Act
        var defaultPriority = await _repository.GetDefaultPriorityAsync();

        // Assert
        Assert.NotNull(defaultPriority);
        Assert.Equal(3, defaultPriority.PriorityLevel); // Medium priority is level 3
    }

    [Fact]
    public async Task GetHighPrioritiesAsync_ShouldReturnOnlyCriticalAndHigh()
    {
        // Act
        var highPriorities = await _repository.GetHighPrioritiesAsync();

        // Assert
        Assert.NotNull(highPriorities);
        Assert.NotEmpty(highPriorities);
        Assert.All(highPriorities, p => Assert.True(p.PriorityLevel <= 2));
    }

    [Fact]
    public async Task CalculateSlaDeadlineAsync_ShouldAddSlaHoursToCreationDate()
    {
        // Arrange
        var priorities = await _repository.GetAllAsync();
        Assert.NotEmpty(priorities);
        var priority = priorities.First();
        var creationDate = DateTime.Now;

        // Act
        var deadline = await _repository.CalculateSlaDeadlineAsync(
            priority.RowId, 
            creationDate);

        // Assert
        var expectedDeadline = creationDate.AddHours((double)priority.SlaTargetHours);
        Assert.Equal(expectedDeadline, deadline, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CalculateSlaDeadlineAsync_WithInvalidPriorityId_ShouldThrowException()
    {
        // Arrange
        var invalidId = -999999L;
        var creationDate = DateTime.Now;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _repository.CalculateSlaDeadlineAsync(invalidId, creationDate));
    }

    [Fact]
    public async Task GetUsageStatisticsAsync_ShouldReturnStatisticsForAllPriorities()
    {
        // Act
        var statistics = await _repository.GetUsageStatisticsAsync();

        // Assert
        Assert.NotNull(statistics);
        Assert.NotEmpty(statistics);
        Assert.All(statistics, stat =>
        {
            Assert.NotNull(stat.Priority);
            Assert.True(stat.TicketCount >= 0);
            Assert.True(stat.SlaComplianceRate >= 0 && stat.SlaComplianceRate <= 100);
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
        Assert.NotEmpty(statistics);
    }

    [Fact]
    public async Task GetEscalationCandidatesAsync_ShouldReturnTicketsNeedingEscalation()
    {
        // Act
        var candidates = await _repository.GetEscalationCandidatesAsync();

        // Assert
        Assert.NotNull(candidates);
        // May be empty if no tickets need escalation
        Assert.All(candidates, ticket =>
        {
            Assert.NotNull(ticket.TicketPriority);
            Assert.NotNull(ticket.TicketStatus);
            Assert.True(ticket.TicketStatus.StatusCode != "CLOSED");
            Assert.True(ticket.TicketStatus.StatusCode != "RESOLVED");
        });
    }

    [Fact]
    public async Task TransactionRollback_ShouldNotPersistChanges()
    {
        // Arrange
        var newPriority = new SysTicketPriority
        {
            PriorityNameAr = $"أولوية اختبار {Guid.NewGuid()}",
            PriorityNameEn = $"Test Priority {Guid.NewGuid()}",
            PriorityLevel = 99,
            SlaTargetHours = 48,
            EscalationThresholdHours = 36,
            IsActive = true,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act - Create within a transaction and rollback
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.TicketPriorities.Add(newPriority);
            await _context.SaveChangesAsync();
            
            var generatedId = newPriority.RowId;
            Assert.True(generatedId > 0);

            // Rollback the transaction
            await transaction.RollbackAsync();

            // Assert - Verify the priority was not persisted
            var retrievedPriority = await _repository.GetByIdAsync(generatedId);
            Assert.Null(retrievedPriority);
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
        var newPriority = new SysTicketPriority
        {
            PriorityNameAr = $"أولوية اختبار {Guid.NewGuid()}",
            PriorityNameEn = $"Test Priority {Guid.NewGuid()}",
            PriorityLevel = 98,
            SlaTargetHours = 48,
            EscalationThresholdHours = 36,
            IsActive = true,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        long generatedId = 0;

        // Act - Create within a transaction and commit
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.TicketPriorities.Add(newPriority);
            await _context.SaveChangesAsync();
            
            generatedId = newPriority.RowId;
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

        // Assert - Verify the priority was persisted
        var retrievedPriority = await _repository.GetByIdAsync(generatedId);
        Assert.NotNull(retrievedPriority);
        Assert.Equal(newPriority.PriorityLevel, retrievedPriority.PriorityLevel);
    }

    [Fact]
    public async Task SequenceGeneration_ShouldGenerateUniqueIds()
    {
        // Arrange
        var priority1 = new SysTicketPriority
        {
            PriorityNameAr = $"أولوية اختبار 1 {Guid.NewGuid()}",
            PriorityNameEn = $"Test Priority 1 {Guid.NewGuid()}",
            PriorityLevel = 97,
            SlaTargetHours = 48,
            EscalationThresholdHours = 36,
            IsActive = true,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        var priority2 = new SysTicketPriority
        {
            PriorityNameAr = $"أولوية اختبار 2 {Guid.NewGuid()}",
            PriorityNameEn = $"Test Priority 2 {Guid.NewGuid()}",
            PriorityLevel = 96,
            SlaTargetHours = 48,
            EscalationThresholdHours = 36,
            IsActive = true,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        _context.TicketPriorities.Add(priority1);
        await _context.SaveChangesAsync();
        var id1 = priority1.RowId;
        _createdIds.Add(id1);

        _context.TicketPriorities.Add(priority2);
        await _context.SaveChangesAsync();
        var id2 = priority2.RowId;
        _createdIds.Add(id2);

        // Assert
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task SlaCalculation_ShouldBeConsistentAcrossPriorities()
    {
        // Arrange
        var priorities = await _repository.GetAllAsync();
        var creationDate = new DateTime(2024, 1, 1, 9, 0, 0);

        // Act & Assert
        foreach (var priority in priorities)
        {
            var deadline = await _repository.CalculateSlaDeadlineAsync(
                priority.RowId, 
                creationDate);

            var expectedDeadline = creationDate.AddHours((double)priority.SlaTargetHours);
            Assert.Equal(expectedDeadline, deadline, TimeSpan.FromSeconds(1));
        }
    }

    public void Dispose()
    {
        // Cleanup - Delete all created test data
        foreach (var id in _createdIds)
        {
            try
            {
                var priority = _context.TicketPriorities.Find(id);
                if (priority != null)
                {
                    _context.TicketPriorities.Remove(priority);
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
