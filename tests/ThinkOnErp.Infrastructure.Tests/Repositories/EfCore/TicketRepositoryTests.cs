using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for TicketRepository using EF Core InMemory provider.
/// Tests all CRUD operations, complex queries with joins, multi-tenancy filtering, and exception scenarios.
/// </summary>
public class TicketRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly TicketRepository _repository;
    private readonly Mock<ILogger<TicketRepository>> _loggerMock;

    public TicketRepositoryTests()
    {
        // Create InMemory database with unique name for each test instance
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_Ticket_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<TicketRepository>>();
        _repository = new TicketRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Test Data Setup Helpers

    private async Task<(SysCompany company, SysBranch branch, SysUser user, SysTicketType type, SysTicketStatus status, SysTicketPriority priority, SysTicketCategory category)> SetupTestDataAsync()
    {
        var company = new SysCompany
        {
            RowId = 1,
            RowDesc = "Test Company",
            RowDescE = "Test Company",
            CompanyCode = "TEST",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Companies.AddAsync(company);

        var branch = new SysBranch
        {
            RowId = 1,
            ParRowId = company.RowId,
            RowDesc = "Test Branch",
            RowDescE = "Test Branch",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Branches.AddAsync(branch);

        var user = new SysUser
        {
            RowId = 1,
            CompanyId = company.RowId,
            BranchId = branch.RowId,
            UserName = "testuser",
            PasswordHash = "hash",
            FullName = "Test User",
            Email = "test@test.com",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Users.AddAsync(user);

        var priority = new SysTicketPriority
        {
            RowId = 1,
            PriorityNameAr = "عادي",
            PriorityNameEn = "Normal",
            PriorityLevel = 3,
            SlaHours = 24,
            DisplayOrder = 3,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketPriorities.AddAsync(priority);

        var type = new SysTicketType
        {
            RowId = 1,
            TypeNameAr = "طلب دعم",
            TypeNameEn = "Support Request",
            DefaultPriorityId = priority.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketTypes.AddAsync(type);

        var status = new SysTicketStatus
        {
            RowId = 1,
            StatusNameAr = "مفتوح",
            StatusNameEn = "Open",
            StatusCode = "OPEN",
            IsFinalStatus = false,
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(status);

        var category = new SysTicketCategory
        {
            RowId = 1,
            CategoryNameAr = "تقني",
            CategoryNameEn = "Technical",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketCategories.AddAsync(category);

        await _context.SaveChangesAsync();

        return (company, branch, user, type, status, priority, category);
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllTickets_WithPagination()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var tickets = new List<SysRequestTicket>
        {
            new SysRequestTicket
            {
                CompanyId = testData.company.RowId,
                BranchId = testData.branch.RowId,
                RequesterId = testData.user.RowId,
                TicketTypeId = testData.type.RowId,
                TicketStatusId = testData.status.RowId,
                TicketPriorityId = testData.priority.RowId,
                TicketCategoryId = testData.category.RowId,
                TitleAr = "تذكرة 1",
                TitleEn = "Ticket 1",
                Description = "Description 1",
                IsActive = true,
                CreationUser = "test",
                CreationDate = DateTime.Now.AddDays(-2)
            },
            new SysRequestTicket
            {
                CompanyId = testData.company.RowId,
                BranchId = testData.branch.RowId,
                RequesterId = testData.user.RowId,
                TicketTypeId = testData.type.RowId,
                TicketStatusId = testData.status.RowId,
                TicketPriorityId = testData.priority.RowId,
                TicketCategoryId = testData.category.RowId,
                TitleAr = "تذكرة 2",
                TitleEn = "Ticket 2",
                Description = "Description 2",
                IsActive = true,
                CreationUser = "test",
                CreationDate = DateTime.Now.AddDays(-1)
            }
        };
        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        // Act
        var (result, totalCount) = await _repository.GetAllAsync(page: 1, pageSize: 10);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, totalCount);
        Assert.Equal(2, result.Count);
        Assert.Equal("Ticket 2", result[0].TitleEn); // Ordered by CreationDate DESC
    }

    [Fact]
    public async Task GetAllAsync_FiltersBy CompanyId()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var company2 = new SysCompany
        {
            RowId = 2,
            RowDesc = "Company 2",
            RowDescE = "Company 2",
            CompanyCode = "COMP2",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Companies.AddAsync(company2);
        await _context.SaveChangesAsync();

        var ticket1 = new SysRequestTicket
        {
            CompanyId = testData.company.RowId,
            BranchId = testData.branch.RowId,
            RequesterId = testData.user.RowId,
            TicketTypeId = testData.type.RowId,
            TicketStatusId = testData.status.RowId,
            TicketPriorityId = testData.priority.RowId,
            TitleEn = "Ticket Company 1",
            Description = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        var ticket2 = new SysRequestTicket
        {
            CompanyId = company2.RowId,
            BranchId = testData.branch.RowId,
            RequesterId = testData.user.RowId,
            TicketTypeId = testData.type.RowId,
            TicketStatusId = testData.status.RowId,
            TicketPriorityId = testData.priority.RowId,
            TitleEn = "Ticket Company 2",
            Description = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Tickets.AddRangeAsync(ticket1, ticket2);
        await _context.SaveChangesAsync();

        // Act
        var (result, totalCount) = await _repository.GetAllAsync(companyId: testData.company.RowId);

        // Assert
        Assert.Single(result);
        Assert.Equal(1, totalCount);
        Assert.Equal("Ticket Company 1", result[0].TitleEn);
    }

    [Fact]
    public async Task GetAllAsync_FiltersBy BranchId()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var branch2 = new SysBranch
        {
            RowId = 2,
            ParRowId = testData.company.RowId,
            RowDesc = "Branch 2",
            RowDescE = "Branch 2",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Branches.AddAsync(branch2);
        await _context.SaveChangesAsync();

        var ticket1 = new SysRequestTicket
        {
            CompanyId = testData.company.RowId,
            BranchId = testData.branch.RowId,
            RequesterId = testData.user.RowId,
            TicketTypeId = testData.type.RowId,
            TicketStatusId = testData.status.RowId,
            TicketPriorityId = testData.priority.RowId,
            TitleEn = "Ticket Branch 1",
            Description = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        var ticket2 = new SysRequestTicket
        {
            CompanyId = testData.company.RowId,
            BranchId = branch2.RowId,
            RequesterId = testData.user.RowId,
            TicketTypeId = testData.type.RowId,
            TicketStatusId = testData.status.RowId,
            TicketPriorityId = testData.priority.RowId,
            TitleEn = "Ticket Branch 2",
            Description = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Tickets.AddRangeAsync(ticket1, ticket2);
        await _context.SaveChangesAsync();

        // Act
        var (result, totalCount) = await _repository.GetAllAsync(branchId: testData.branch.RowId);

        // Assert
        Assert.Single(result);
        Assert.Equal(1, totalCount);
        Assert.Equal("Ticket Branch 1", result[0].TitleEn);
    }

    [Fact]
    public async Task GetAllAsync_FiltersBy SearchTerm()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var tickets = new List<SysRequestTicket>
        {
            new SysRequestTicket
            {
                CompanyId = testData.company.RowId,
                BranchId = testData.branch.RowId,
                RequesterId = testData.user.RowId,
                TicketTypeId = testData.type.RowId,
                TicketStatusId = testData.status.RowId,
                TicketPriorityId = testData.priority.RowId,
                TitleEn = "Login Issue",
                Description = "Cannot login",
                IsActive = true,
                CreationUser = "test"
            },
            new SysRequestTicket
            {
                CompanyId = testData.company.RowId,
                BranchId = testData.branch.RowId,
                RequesterId = testData.user.RowId,
                TicketTypeId = testData.type.RowId,
                TicketStatusId = testData.status.RowId,
                TicketPriorityId = testData.priority.RowId,
                TitleEn = "Report Problem",
                Description = "Report not generating",
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        // Act
        var (result, totalCount) = await _repository.GetAllAsync(searchTerm: "Login");

        // Assert
        Assert.Single(result);
        Assert.Equal(1, totalCount);
        Assert.Equal("Login Issue", result[0].TitleEn);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsTicket_WithNavigationProperties()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var ticket = new SysRequestTicket
        {
            CompanyId = testData.company.RowId,
            BranchId = testData.branch.RowId,
            RequesterId = testData.user.RowId,
            TicketTypeId = testData.type.RowId,
            TicketStatusId = testData.status.RowId,
            TicketPriorityId = testData.priority.RowId,
            TicketCategoryId = testData.category.RowId,
            TitleEn = "Test Ticket",
            Description = "Test Description",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(ticket.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Ticket", result.TitleEn);
        Assert.NotNull(result.Company);
        Assert.NotNull(result.Branch);
        Assert.NotNull(result.Requester);
        Assert.NotNull(result.TicketType);
        Assert.NotNull(result.TicketStatus);
        Assert.NotNull(result.TicketPriority);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CreatesNewTicket_ReturnsGeneratedId()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var ticket = new SysRequestTicket
        {
            CompanyId = testData.company.RowId,
            BranchId = testData.branch.RowId,
            RequesterId = testData.user.RowId,
            TicketTypeId = testData.type.RowId,
            TicketStatusId = testData.status.RowId,
            TicketPriorityId = testData.priority.RowId,
            TitleAr = "تذكرة جديدة",
            TitleEn = "New Ticket",
            Description = "Test Description",
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(ticket);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, ticket.RowId);
        Assert.NotNull(ticket.CreationDate);
        Assert.True(ticket.IsActive);

        // Verify in database
        var savedTicket = await _context.Tickets.FindAsync(result);
        Assert.NotNull(savedTicket);
        Assert.Equal("New Ticket", savedTicket.TitleEn);
    }

    [Fact]
    public async Task CreateAsync_SetsCreationDate_WhenNotProvided()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var ticket = new SysRequestTicket
        {
            CompanyId = testData.company.RowId,
            BranchId = testData.branch.RowId,
            RequesterId = testData.user.RowId,
            TicketTypeId = testData.type.RowId,
            TicketStatusId = testData.status.RowId,
            TicketPriorityId = testData.priority.RowId,
            TitleEn = "Test",
            Description = "Test",
            CreationUser = "test"
        };

        // Act
        await _repository.CreateAsync(ticket);

        // Assert
        Assert.NotNull(ticket.CreationDate);
        Assert.True(ticket.CreationDate.Value <= DateTime.Now);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesExistingTicket_ReturnsRowsAffected()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var ticket = new SysRequestTicket
        {
            CompanyId = testData.company.RowId,
            BranchId = testData.branch.RowId,
            RequesterId = testData.user.RowId,
            TicketTypeId = testData.type.RowId,
            TicketStatusId = testData.status.RowId,
            TicketPriorityId = testData.priority.RowId,
            TitleEn = "Original Title",
            Description = "Original Description",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        // Modify the ticket
        ticket.TitleEn = "Updated Title";
        ticket.Description = "Updated Description";
        ticket.UpdateUser = "update_user";

        // Act
        var result = await _repository.UpdateAsync(ticket);

        // Assert
        Assert.Equal(1, result);
        Assert.NotNull(ticket.UpdateDate);

        // Verify in database
        var updatedTicket = await _context.Tickets.FindAsync(ticket.RowId);
        Assert.NotNull(updatedTicket);
        Assert.Equal("Updated Title", updatedTicket.TitleEn);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesTicket_ReturnsRowsAffected()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var ticket = new SysRequestTicket
        {
            CompanyId = testData.company.RowId,
            BranchId = testData.branch.RowId,
            RequesterId = testData.user.RowId,
            TicketTypeId = testData.type.RowId,
            TicketStatusId = testData.status.RowId,
            TicketPriorityId = testData.priority.RowId,
            TitleEn = "To Delete",
            Description = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();
        var ticketId = ticket.RowId;

        // Act
        var result = await _repository.DeleteAsync(ticketId, "delete_user");

        // Assert
        Assert.Equal(1, result);

        // Verify ticket is soft deleted
        var deletedTicket = await _context.Tickets
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.RowId == ticketId);
        Assert.NotNull(deletedTicket);
        Assert.False(deletedTicket.IsActive);
        Assert.Equal("delete_user", deletedTicket.UpdateUser);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ReturnsZero()
    {
        // Act
        var result = await _repository.DeleteAsync(999, "user");

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region AssignTicketAsync Tests

    [Fact]
    public async Task AssignTicketAsync_AssignsTicketToUser_ReturnsRowsAffected()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var assignee = new SysUser
        {
            RowId = 2,
            CompanyId = testData.company.RowId,
            BranchId = testData.branch.RowId,
            UserName = "assignee",
            PasswordHash = "hash",
            FullName = "Assignee User",
            Email = "assignee@test.com",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Users.AddAsync(assignee);

        var ticket = new SysRequestTicket
        {
            CompanyId = testData.company.RowId,
            BranchId = testData.branch.RowId,
            RequesterId = testData.user.RowId,
            TicketTypeId = testData.type.RowId,
            TicketStatusId = testData.status.RowId,
            TicketPriorityId = testData.priority.RowId,
            TitleEn = "Test Ticket",
            Description = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.AssignTicketAsync(ticket.RowId, assignee.RowId, "admin");

        // Assert
        Assert.Equal(1, result);

        // Verify assignment
        var updatedTicket = await _context.Tickets.FindAsync(ticket.RowId);
        Assert.NotNull(updatedTicket);
        Assert.Equal(assignee.RowId, updatedTicket.AssigneeId);
    }

    #endregion

    #region UpdateStatusAsync Tests

    [Fact]
    public async Task UpdateStatusAsync_UpdatesTicketStatus_ReturnsRowsAffected()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var closedStatus = new SysTicketStatus
        {
            RowId = 2,
            StatusNameEn = "Closed",
            StatusCode = "CLOSED",
            IsFinalStatus = true,
            DisplayOrder = 5,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(closedStatus);

        var ticket = new SysRequestTicket
        {
            CompanyId = testData.company.RowId,
            BranchId = testData.branch.RowId,
            RequesterId = testData.user.RowId,
            TicketTypeId = testData.type.RowId,
            TicketStatusId = testData.status.RowId,
            TicketPriorityId = testData.priority.RowId,
            TitleEn = "Test Ticket",
            Description = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.UpdateStatusAsync(ticket.RowId, closedStatus.RowId, "Resolved", "admin");

        // Assert
        Assert.Equal(1, result);

        // Verify status update
        var updatedTicket = await _context.Tickets.FindAsync(ticket.RowId);
        Assert.NotNull(updatedTicket);
        Assert.Equal(closedStatus.RowId, updatedTicket.TicketStatusId);
        Assert.NotNull(updatedTicket.ActualResolutionDate);
    }

    #endregion

    #region GetByCompanyIdAsync Tests

    [Fact]
    public async Task GetByCompanyIdAsync_ReturnsTicketsForCompany()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var ticket = new SysRequestTicket
        {
            CompanyId = testData.company.RowId,
            BranchId = testData.branch.RowId,
            RequesterId = testData.user.RowId,
            TicketTypeId = testData.type.RowId,
            TicketStatusId = testData.status.RowId,
            TicketPriorityId = testData.priority.RowId,
            TitleEn = "Company Ticket",
            Description = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByCompanyIdAsync(testData.company.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Company Ticket", result[0].TitleEn);
    }

    #endregion

    #region GetOverdueTicketsAsync Tests

    [Fact]
    public async Task GetOverdueTicketsAsync_ReturnsOverdueTickets()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var overdueTicket = new SysRequestTicket
        {
            CompanyId = testData.company.RowId,
            BranchId = testData.branch.RowId,
            RequesterId = testData.user.RowId,
            TicketTypeId = testData.type.RowId,
            TicketStatusId = testData.status.RowId,
            TicketPriorityId = testData.priority.RowId,
            TitleEn = "Overdue Ticket",
            Description = "Test",
            ExpectedResolutionDate = DateTime.Now.AddDays(-1),
            IsActive = true,
            CreationUser = "test"
        };
        var onTimeTicket = new SysRequestTicket
        {
            CompanyId = testData.company.RowId,
            BranchId = testData.branch.RowId,
            RequesterId = testData.user.RowId,
            TicketTypeId = testData.type.RowId,
            TicketStatusId = testData.status.RowId,
            TicketPriorityId = testData.priority.RowId,
            TitleEn = "On Time Ticket",
            Description = "Test",
            ExpectedResolutionDate = DateTime.Now.AddDays(1),
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Tickets.AddRangeAsync(overdueTicket, onTimeTicket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetOverdueTicketsAsync(DateTime.Now);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Overdue Ticket", result[0].TitleEn);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketRepository(_context, null!));
    }

    #endregion
}
