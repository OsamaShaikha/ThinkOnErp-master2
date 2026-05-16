using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for TicketCommentRepository using EF Core InMemory provider.
/// Tests all CRUD operations, comment filtering, search functionality, and exception scenarios.
/// </summary>
public class TicketCommentRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly TicketCommentRepository _repository;
    private readonly Mock<ILogger<TicketCommentRepository>> _loggerMock;

    public TicketCommentRepositoryTests()
    {
        // Create InMemory database with unique name for each test instance
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_TicketComment_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<TicketCommentRepository>>();
        _repository = new TicketCommentRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Test Data Setup Helpers

    private async Task<(SysCompany company, SysBranch branch, SysUser user, SysRequestTicket ticket)> SetupTestDataAsync()
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

        var ticket = new SysRequestTicket
        {
            RowId = 1,
            CompanyId = company.RowId,
            BranchId = branch.RowId,
            RequesterId = user.RowId,
            TicketTypeId = type.RowId,
            TicketStatusId = status.RowId,
            TicketPriorityId = priority.RowId,
            TitleEn = "Test Ticket",
            Description = "Test Description",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Tickets.AddAsync(ticket);

        await _context.SaveChangesAsync();

        return (company, branch, user, ticket);
    }

    #endregion

    #region GetByTicketIdAsync Tests

    [Fact]
    public async Task GetByTicketIdAsync_ReturnsCommentsForTicket_OrderedByCreationDate()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var comments = new List<SysTicketComment>
        {
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "First comment",
                CommentedBy = testData.user.RowId,
                IsInternal = false,
                IsActive = true,
                CreationUser = "test",
                CreationDate = DateTime.Now.AddHours(-2)
            },
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "Second comment",
                CommentedBy = testData.user.RowId,
                IsInternal = false,
                IsActive = true,
                CreationUser = "test",
                CreationDate = DateTime.Now.AddHours(-1)
            }
        };
        await _context.TicketComments.AddRangeAsync(comments);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTicketIdAsync(testData.ticket.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("First comment", result[0].CommentText); // Ordered by CreationDate ASC
        Assert.Equal("Second comment", result[1].CommentText);
    }

    [Fact]
    public async Task GetByTicketIdAsync_ExcludesInternalComments_WhenIncludeInternalIsFalse()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var comments = new List<SysTicketComment>
        {
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "Public comment",
                CommentedBy = testData.user.RowId,
                IsInternal = false,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "Internal comment",
                CommentedBy = testData.user.RowId,
                IsInternal = true,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.TicketComments.AddRangeAsync(comments);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTicketIdAsync(testData.ticket.RowId, includeInternal: false);

        // Assert
        Assert.Single(result);
        Assert.Equal("Public comment", result[0].CommentText);
    }

    [Fact]
    public async Task GetByTicketIdAsync_IncludesInternalComments_WhenIncludeInternalIsTrue()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var comments = new List<SysTicketComment>
        {
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "Public comment",
                CommentedBy = testData.user.RowId,
                IsInternal = false,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "Internal comment",
                CommentedBy = testData.user.RowId,
                IsInternal = true,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.TicketComments.AddRangeAsync(comments);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTicketIdAsync(testData.ticket.RowId, includeInternal: true);

        // Assert
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsComment_WithNavigationProperties()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var comment = new SysTicketComment
        {
            TicketId = testData.ticket.RowId,
            CommentText = "Test comment",
            CommentedBy = testData.user.RowId,
            IsInternal = false,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketComments.AddAsync(comment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(comment.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test comment", result.CommentText);
        Assert.NotNull(result.Ticket);
        Assert.NotNull(result.Commenter);
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
    public async Task CreateAsync_CreatesNewComment_ReturnsGeneratedId()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var comment = new SysTicketComment
        {
            TicketId = testData.ticket.RowId,
            CommentText = "New comment",
            CommentedBy = testData.user.RowId,
            IsInternal = false,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(comment);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, comment.RowId);
        Assert.NotNull(comment.CreationDate);
        Assert.True(comment.IsActive);

        // Verify in database
        var savedComment = await _context.TicketComments.FindAsync(result);
        Assert.NotNull(savedComment);
        Assert.Equal("New comment", savedComment.CommentText);
    }

    [Fact]
    public async Task CreateAsync_SetsCreationDate_WhenNotProvided()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var comment = new SysTicketComment
        {
            TicketId = testData.ticket.RowId,
            CommentText = "Test",
            CommentedBy = testData.user.RowId,
            IsInternal = false,
            CreationUser = "test"
        };

        // Act
        await _repository.CreateAsync(comment);

        // Assert
        Assert.NotNull(comment.CreationDate);
        Assert.True(comment.CreationDate.Value <= DateTime.Now);
    }

    #endregion

    #region GetCommentCountAsync Tests

    [Fact]
    public async Task GetCommentCountAsync_ReturnsCorrectCount_ExcludingInternal()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var comments = new List<SysTicketComment>
        {
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "Public 1",
                CommentedBy = testData.user.RowId,
                IsInternal = false,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "Public 2",
                CommentedBy = testData.user.RowId,
                IsInternal = false,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "Internal",
                CommentedBy = testData.user.RowId,
                IsInternal = true,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.TicketComments.AddRangeAsync(comments);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetCommentCountAsync(testData.ticket.RowId, includeInternal: false);

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task GetCommentCountAsync_ReturnsCorrectCount_IncludingInternal()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var comments = new List<SysTicketComment>
        {
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "Public",
                CommentedBy = testData.user.RowId,
                IsInternal = false,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "Internal",
                CommentedBy = testData.user.RowId,
                IsInternal = true,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.TicketComments.AddRangeAsync(comments);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetCommentCountAsync(testData.ticket.RowId, includeInternal: true);

        // Assert
        Assert.Equal(2, result);
    }

    #endregion

    #region GetRecentCommentsAsync Tests

    [Fact]
    public async Task GetRecentCommentsAsync_ReturnsRecentComments_WithLimit()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        for (int i = 0; i < 5; i++)
        {
            await _context.TicketComments.AddAsync(new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = $"Comment {i}",
                CommentedBy = testData.user.RowId,
                IsInternal = false,
                IsActive = true,
                CreationUser = "test",
                CreationDate = DateTime.Now.AddHours(-i)
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRecentCommentsAsync(limit: 3);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Comment 0", result[0].CommentText); // Most recent first
    }

    [Fact]
    public async Task GetRecentCommentsAsync_FiltersBy CompanyId()
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

        var comment1 = new SysTicketComment
        {
            TicketId = testData.ticket.RowId,
            CommentText = "Company 1 Comment",
            CommentedBy = testData.user.RowId,
            IsInternal = false,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketComments.AddAsync(comment1);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRecentCommentsAsync(companyId: testData.company.RowId);

        // Assert
        Assert.Single(result);
        Assert.Equal("Company 1 Comment", result[0].CommentText);
    }

    #endregion

    #region GetByUserAsync Tests

    [Fact]
    public async Task GetByUserAsync_ReturnsCommentsBy User()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var user2 = new SysUser
        {
            RowId = 2,
            CompanyId = testData.company.RowId,
            BranchId = testData.branch.RowId,
            UserName = "user2",
            PasswordHash = "hash",
            FullName = "User 2",
            Email = "user2@test.com",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Users.AddAsync(user2);

        var comments = new List<SysTicketComment>
        {
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "User 1 Comment",
                CommentedBy = testData.user.RowId,
                IsInternal = false,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "User 2 Comment",
                CommentedBy = user2.RowId,
                IsInternal = false,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.TicketComments.AddRangeAsync(comments);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserAsync(testData.user.UserName);

        // Assert
        Assert.Single(result);
        Assert.Equal("User 1 Comment", result[0].CommentText);
    }

    #endregion

    #region SearchCommentsAsync Tests

    [Fact]
    public async Task SearchCommentsAsync_FindsCommentsBySearchTerm()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var comments = new List<SysTicketComment>
        {
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "This is about login issue",
                CommentedBy = testData.user.RowId,
                IsInternal = false,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = "This is about report generation",
                CommentedBy = testData.user.RowId,
                IsInternal = false,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.TicketComments.AddRangeAsync(comments);
        await _context.SaveChangesAsync();

        // Act
        var (result, totalCount) = await _repository.SearchCommentsAsync("login");

        // Assert
        Assert.Single(result);
        Assert.Equal(1, totalCount);
        Assert.Contains("login", result[0].CommentText);
    }

    [Fact]
    public async Task SearchCommentsAsync_SupportsPagination()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        for (int i = 0; i < 5; i++)
        {
            await _context.TicketComments.AddAsync(new SysTicketComment
            {
                TicketId = testData.ticket.RowId,
                CommentText = $"Test comment {i}",
                CommentedBy = testData.user.RowId,
                IsInternal = false,
                IsActive = true,
                CreationUser = "test"
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var (result, totalCount) = await _repository.SearchCommentsAsync("Test", page: 1, pageSize: 2);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(5, totalCount);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketCommentRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketCommentRepository(_context, null!));
    }

    #endregion
}
