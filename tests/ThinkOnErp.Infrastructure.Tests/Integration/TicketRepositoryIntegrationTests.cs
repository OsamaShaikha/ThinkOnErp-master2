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
/// Integration tests for TicketRepository using real Oracle database connection.
/// Tests EF Core implementation against actual Oracle database.
/// 
/// **Validates: Requirements REQ-12, REQ-27**
/// - Requirement 12: Testing Strategy - Integration tests that verify database operations
/// - Requirement 27: Multi-Tenancy Support - Tests company and branch filtering
/// - Tests CRUD operations
/// - Tests complex queries with joins
/// - Tests multi-tenancy filtering
/// - Verifies data persistence
/// </summary>
public class TicketRepositoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ThinkOnErpDbContext _context;
    private readonly TicketRepository _repository;
    private readonly List<long> _createdTicketIds = new();

    public TicketRepositoryIntegrationTests()
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
        var logger = _serviceProvider.GetRequiredService<ILogger<TicketRepository>>();
        _repository = new TicketRepository(_context, logger);
    }

    private async Task<(long companyId, long branchId, long userId, long typeId, long statusId, long priorityId, long categoryId)> GetTestDataIdsAsync()
    {
        var company = await _context.Companies.Where(c => c.IsActive).FirstOrDefaultAsync();
        var branch = await _context.Branches.Where(b => b.IsActive).FirstOrDefaultAsync();
        var user = await _context.Users.Where(u => u.IsActive).FirstOrDefaultAsync();
        var type = await _context.TicketTypes.Where(t => t.IsActive).FirstOrDefaultAsync();
        var status = await _context.TicketStatuses.Where(s => s.IsActive).FirstOrDefaultAsync();
        var priority = await _context.TicketPriorities.Where(p => p.IsActive).FirstOrDefaultAsync();
        var category = await _context.TicketCategories.Where(c => c.IsActive).FirstOrDefaultAsync();

        if (company == null || branch == null || user == null || type == null || status == null || priority == null || category == null)
        {
            throw new InvalidOperationException("Required test data not found in database");
        }

        return (company.RowId, branch.RowId, user.RowId, type.RowId, status.RowId, priority.RowId, category.RowId);
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistTicketWithNavigationProperties()
    {
        // Arrange
        var testData = await GetTestDataIdsAsync();

        var ticket = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TicketCategoryId = testData.categoryId,
            TitleAr = $"تذكرة اختبار {Guid.NewGuid()}",
            TitleEn = $"Integration Test Ticket {Guid.NewGuid()}",
            Description = "This is an integration test ticket",
            CreationUser = "IntegrationTest"
        };

        // Act
        var ticketId = await _repository.CreateAsync(ticket);
        _createdTicketIds.Add(ticketId);

        // Assert
        Assert.True(ticketId > 0);
        Assert.Equal(ticketId, ticket.RowId);
        Assert.NotNull(ticket.CreationDate);
        Assert.True(ticket.IsActive);

        // Verify persistence with navigation properties
        var savedTicket = await _repository.GetByIdAsync(ticketId);
        Assert.NotNull(savedTicket);
        Assert.Equal(ticket.TitleEn, savedTicket.TitleEn);
        Assert.NotNull(savedTicket.Company);
        Assert.NotNull(savedTicket.Branch);
        Assert.NotNull(savedTicket.Requester);
        Assert.NotNull(savedTicket.TicketType);
        Assert.NotNull(savedTicket.TicketStatus);
        Assert.NotNull(savedTicket.TicketPriority);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnTicketsWithPagination()
    {
        // Arrange
        var testData = await GetTestDataIdsAsync();

        // Create test tickets
        for (int i = 0; i < 3; i++)
        {
            var ticket = new SysRequestTicket
            {
                CompanyId = testData.companyId,
                BranchId = testData.branchId,
                RequesterId = testData.userId,
                TicketTypeId = testData.typeId,
                TicketStatusId = testData.statusId,
                TicketPriorityId = testData.priorityId,
                TitleEn = $"Pagination Test {i} {Guid.NewGuid()}",
                Description = "Test",
                CreationUser = "IntegrationTest"
            };
            var id = await _repository.CreateAsync(ticket);
            _createdTicketIds.Add(id);
        }

        // Act
        var (tickets, totalCount) = await _repository.GetAllAsync(
            companyId: testData.companyId,
            page: 1,
            pageSize: 10);

        // Assert
        Assert.NotEmpty(tickets);
        Assert.True(totalCount >= 3);
        Assert.All(tickets, t => Assert.NotNull(t.Company));
        Assert.All(tickets, t => Assert.NotNull(t.TicketType));
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByCompanyId()
    {
        // Arrange
        var testData = await GetTestDataIdsAsync();

        var ticket = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TitleEn = $"Company Filter Test {Guid.NewGuid()}",
            Description = "Test",
            CreationUser = "IntegrationTest"
        };
        var ticketId = await _repository.CreateAsync(ticket);
        _createdTicketIds.Add(ticketId);

        // Act
        var (tickets, totalCount) = await _repository.GetAllAsync(companyId: testData.companyId);

        // Assert
        Assert.NotEmpty(tickets);
        Assert.All(tickets, t => Assert.Equal(testData.companyId, t.CompanyId));
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByBranchId()
    {
        // Arrange
        var testData = await GetTestDataIdsAsync();

        var ticket = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TitleEn = $"Branch Filter Test {Guid.NewGuid()}",
            Description = "Test",
            CreationUser = "IntegrationTest"
        };
        var ticketId = await _repository.CreateAsync(ticket);
        _createdTicketIds.Add(ticketId);

        // Act
        var (tickets, totalCount) = await _repository.GetAllAsync(branchId: testData.branchId);

        // Assert
        Assert.NotEmpty(tickets);
        Assert.All(tickets, t => Assert.Equal(testData.branchId, t.BranchId));
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterBySearchTerm()
    {
        // Arrange
        var testData = await GetTestDataIdsAsync();
        var uniqueSearchTerm = $"SearchTest_{Guid.NewGuid().ToString().Substring(0, 8)}";

        var ticket = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TitleEn = $"Title with {uniqueSearchTerm}",
            Description = "Test",
            CreationUser = "IntegrationTest"
        };
        var ticketId = await _repository.CreateAsync(ticket);
        _createdTicketIds.Add(ticketId);

        // Act
        var (tickets, totalCount) = await _repository.GetAllAsync(searchTerm: uniqueSearchTerm);

        // Assert
        Assert.NotEmpty(tickets);
        Assert.Contains(tickets, t => t.TitleEn.Contains(uniqueSearchTerm));
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateTicketProperties()
    {
        // Arrange
        var testData = await GetTestDataIdsAsync();

        var ticket = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TitleEn = "Original Title",
            Description = "Original Description",
            CreationUser = "IntegrationTest"
        };
        var ticketId = await _repository.CreateAsync(ticket);
        _createdTicketIds.Add(ticketId);

        // Modify ticket
        ticket.TitleEn = "Updated Title";
        ticket.Description = "Updated Description";
        ticket.UpdateUser = "IntegrationTest";

        // Act
        var result = await _repository.UpdateAsync(ticket);

        // Assert
        Assert.Equal(1, result);

        // Verify update
        var updatedTicket = await _repository.GetByIdAsync(ticketId);
        Assert.NotNull(updatedTicket);
        Assert.Equal("Updated Title", updatedTicket.TitleEn);
        Assert.Equal("Updated Description", updatedTicket.Description);
        Assert.NotNull(updatedTicket.UpdateDate);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteTicket()
    {
        // Arrange
        var testData = await GetTestDataIdsAsync();

        var ticket = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TitleEn = "To Delete",
            Description = "Test",
            CreationUser = "IntegrationTest"
        };
        var ticketId = await _repository.CreateAsync(ticket);
        _createdTicketIds.Add(ticketId);

        // Act
        var result = await _repository.DeleteAsync(ticketId, "IntegrationTest");

        // Assert
        Assert.Equal(1, result);

        // Verify soft delete
        var deletedTicket = await _context.Tickets
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.RowId == ticketId);
        Assert.NotNull(deletedTicket);
        Assert.False(deletedTicket.IsActive);
    }

    [Fact]
    public async Task AssignTicketAsync_ShouldAssignTicketToUser()
    {
        // Arrange
        var testData = await GetTestDataIdsAsync();

        var ticket = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TitleEn = "Assignment Test",
            Description = "Test",
            CreationUser = "IntegrationTest"
        };
        var ticketId = await _repository.CreateAsync(ticket);
        _createdTicketIds.Add(ticketId);

        // Act
        var result = await _repository.AssignTicketAsync(ticketId, testData.userId, "IntegrationTest");

        // Assert
        Assert.Equal(1, result);

        // Verify assignment
        var assignedTicket = await _repository.GetByIdAsync(ticketId);
        Assert.NotNull(assignedTicket);
        Assert.Equal(testData.userId, assignedTicket.AssigneeId);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldUpdateTicketStatus()
    {
        // Arrange
        var testData = await GetTestDataIdsAsync();

        var ticket = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TitleEn = "Status Update Test",
            Description = "Test",
            CreationUser = "IntegrationTest"
        };
        var ticketId = await _repository.CreateAsync(ticket);
        _createdTicketIds.Add(ticketId);

        // Get another status
        var newStatus = await _context.TicketStatuses
            .Where(s => s.IsActive && s.RowId != testData.statusId)
            .FirstOrDefaultAsync();

        if (newStatus != null)
        {
            // Act
            var result = await _repository.UpdateStatusAsync(ticketId, newStatus.RowId, "Status changed", "IntegrationTest");

            // Assert
            Assert.Equal(1, result);

            // Verify status update
            var updatedTicket = await _repository.GetByIdAsync(ticketId);
            Assert.NotNull(updatedTicket);
            Assert.Equal(newStatus.RowId, updatedTicket.TicketStatusId);
        }
    }

    [Fact]
    public async Task GetByCompanyIdAsync_ShouldReturnCompanyTickets()
    {
        // Arrange
        var testData = await GetTestDataIdsAsync();

        var ticket = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TitleEn = $"Company Test {Guid.NewGuid()}",
            Description = "Test",
            CreationUser = "IntegrationTest"
        };
        var ticketId = await _repository.CreateAsync(ticket);
        _createdTicketIds.Add(ticketId);

        // Act
        var tickets = await _repository.GetByCompanyIdAsync(testData.companyId);

        // Assert
        Assert.NotEmpty(tickets);
        Assert.All(tickets, t => Assert.Equal(testData.companyId, t.CompanyId));
        Assert.Contains(tickets, t => t.RowId == ticketId);
    }

    [Fact]
    public async Task GetByBranchIdAsync_ShouldReturnBranchTickets()
    {
        // Arrange
        var testData = await GetTestDataIdsAsync();

        var ticket = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TitleEn = $"Branch Test {Guid.NewGuid()}",
            Description = "Test",
            CreationUser = "IntegrationTest"
        };
        var ticketId = await _repository.CreateAsync(ticket);
        _createdTicketIds.Add(ticketId);

        // Act
        var tickets = await _repository.GetByBranchIdAsync(testData.branchId);

        // Assert
        Assert.NotEmpty(tickets);
        Assert.All(tickets, t => Assert.Equal(testData.branchId, t.BranchId));
        Assert.Contains(tickets, t => t.RowId == ticketId);
    }

    [Fact]
    public async Task GetByStatusAsync_ShouldReturnTicketsWithStatus()
    {
        // Arrange
        var testData = await GetTestDataIdsAsync();

        var ticket = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TitleEn = $"Status Test {Guid.NewGuid()}",
            Description = "Test",
            CreationUser = "IntegrationTest"
        };
        var ticketId = await _repository.CreateAsync(ticket);
        _createdTicketIds.Add(ticketId);

        // Act
        var tickets = await _repository.GetByStatusAsync(testData.statusId);

        // Assert
        Assert.NotEmpty(tickets);
        Assert.All(tickets, t => Assert.Equal(testData.statusId, t.TicketStatusId));
        Assert.Contains(tickets, t => t.RowId == ticketId);
    }

    [Fact]
    public async Task GetOverdueTicketsAsync_ShouldReturnOverdueTickets()
    {
        // Arrange
        var testData = await GetTestDataIdsAsync();

        var overdueTicket = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TitleEn = $"Overdue Test {Guid.NewGuid()}",
            Description = "Test",
            ExpectedResolutionDate = DateTime.Now.AddDays(-1),
            CreationUser = "IntegrationTest"
        };
        var ticketId = await _repository.CreateAsync(overdueTicket);
        _createdTicketIds.Add(ticketId);

        // Act
        var tickets = await _repository.GetOverdueTicketsAsync(DateTime.Now);

        // Assert
        Assert.NotEmpty(tickets);
        Assert.Contains(tickets, t => t.RowId == ticketId);
        Assert.All(tickets, t => Assert.True(t.ExpectedResolutionDate < DateTime.Now));
    }

    [Fact]
    public async Task TransactionRollback_ShouldNotPersistTicket()
    {
        // Arrange
        var testData = await GetTestDataIdsAsync();

        var ticket = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TitleEn = "Rollback Test",
            Description = "Test",
            CreationUser = "IntegrationTest"
        };

        // Act - Create within a transaction and rollback
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();
            
            var ticketId = ticket.RowId;
            Assert.True(ticketId > 0);

            // Rollback the transaction
            await transaction.RollbackAsync();

            // Assert - Verify the ticket was not persisted
            var retrievedTicket = await _repository.GetByIdAsync(ticketId);
            Assert.Null(retrievedTicket);
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
        var testData = await GetTestDataIdsAsync();

        var ticket1 = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TitleEn = $"Sequence Test 1 {Guid.NewGuid()}",
            Description = "Test",
            CreationUser = "IntegrationTest"
        };

        var ticket2 = new SysRequestTicket
        {
            CompanyId = testData.companyId,
            BranchId = testData.branchId,
            RequesterId = testData.userId,
            TicketTypeId = testData.typeId,
            TicketStatusId = testData.statusId,
            TicketPriorityId = testData.priorityId,
            TitleEn = $"Sequence Test 2 {Guid.NewGuid()}",
            Description = "Test",
            CreationUser = "IntegrationTest"
        };

        // Act
        var id1 = await _repository.CreateAsync(ticket1);
        var id2 = await _repository.CreateAsync(ticket2);
        _createdTicketIds.Add(id1);
        _createdTicketIds.Add(id2);

        // Assert
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.NotEqual(id1, id2);
    }

    public void Dispose()
    {
        // Cleanup - Delete all created test data
        foreach (var id in _createdTicketIds)
        {
            try
            {
                var ticket = _context.Tickets
                    .IgnoreQueryFilters()
                    .FirstOrDefault(t => t.RowId == id);
                if (ticket != null)
                {
                    _context.Tickets.Remove(ticket);
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
