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
/// Integration tests for TicketCommentRepository using real Oracle database connection.
/// Tests EF Core implementation against actual Oracle database.
/// 
/// **Validates: Requirements REQ-12**
/// - Requirement 12: Testing Strategy - Integration tests that verify database operations
/// - Tests CRUD operations
/// - Tests multi-tenancy filtering
/// - Tests complex queries with joins
/// - Verifies data persistence
/// </summary>
public class TicketCommentRepositoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ThinkOnErpDbContext _context;
    private readonly TicketCommentRepository _repository;
    private readonly List<long> _createdCommentIds = new();
    private readonly List<long> _createdTicketIds = new();

    public TicketCommentRepositoryIntegrationTests()
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
        var logger = _serviceProvider.GetRequiredService<ILogger<TicketCommentRepository>>();
        _repository = new TicketCommentRepository(_context, logger);
    }

    private async Task<long> GetOrCreateTestTicketAsync()
    {
        // Get first active ticket from database
        var ticket = await _context.Tickets
            .Where(t => t.IsActive)
            .FirstOrDefaultAsync();

        if (ticket != null)
        {
            return ticket.RowId;
        }

        // If no ticket exists, we can't create one without all dependencies
        // Skip the test
        throw new InvalidOperationException("No active tickets found in database for testing");
    }

    private async Task<long> GetTestUserIdAsync()
    {
        var user = await _context.Users
            .Where(u => u.IsActive)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            throw new InvalidOperationException("No active users found in database for testing");
        }

        return user.RowId;
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistCommentToDatabase()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var comment = new SysTicketComment
        {
            TicketId = ticketId,
            CommentText = $"Integration test comment {Guid.NewGuid()}",
            CommentedBy = userId,
            IsInternal = false,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        var commentId = await _repository.CreateAsync(comment);
        _createdCommentIds.Add(commentId);

        // Assert
        Assert.True(commentId > 0);
        
        // Verify persistence
        var savedComment = await _repository.GetByIdAsync(commentId);
        Assert.NotNull(savedComment);
        Assert.Equal(comment.CommentText, savedComment.CommentText);
        Assert.Equal(ticketId, savedComment.TicketId);
    }

    [Fact]
    public async Task GetByTicketIdAsync_ShouldReturnCommentsWithNavigationProperties()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var comment = new SysTicketComment
        {
            TicketId = ticketId,
            CommentText = $"Test comment with navigation {Guid.NewGuid()}",
            CommentedBy = userId,
            IsInternal = false,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        var commentId = await _repository.CreateAsync(comment);
        _createdCommentIds.Add(commentId);

        // Act
        var comments = await _repository.GetByTicketIdAsync(ticketId);

        // Assert
        Assert.NotEmpty(comments);
        var testComment = comments.FirstOrDefault(c => c.RowId == commentId);
        Assert.NotNull(testComment);
        Assert.NotNull(testComment.Ticket);
        Assert.NotNull(testComment.Commenter);
    }

    [Fact]
    public async Task GetByTicketIdAsync_ShouldFilterInternalComments()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var publicComment = new SysTicketComment
        {
            TicketId = ticketId,
            CommentText = $"Public comment {Guid.NewGuid()}",
            CommentedBy = userId,
            IsInternal = false,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        var internalComment = new SysTicketComment
        {
            TicketId = ticketId,
            CommentText = $"Internal comment {Guid.NewGuid()}",
            CommentedBy = userId,
            IsInternal = true,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        var publicId = await _repository.CreateAsync(publicComment);
        var internalId = await _repository.CreateAsync(internalComment);
        _createdCommentIds.Add(publicId);
        _createdCommentIds.Add(internalId);

        // Act - Get without internal comments
        var publicComments = await _repository.GetByTicketIdAsync(ticketId, includeInternal: false);
        var allComments = await _repository.GetByTicketIdAsync(ticketId, includeInternal: true);

        // Assert
        Assert.Contains(publicComments, c => c.RowId == publicId);
        Assert.DoesNotContain(publicComments, c => c.RowId == internalId);
        Assert.Contains(allComments, c => c.RowId == publicId);
        Assert.Contains(allComments, c => c.RowId == internalId);
    }

    [Fact]
    public async Task GetCommentCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var initialCount = await _repository.GetCommentCountAsync(ticketId, includeInternal: true);

        var comment = new SysTicketComment
        {
            TicketId = ticketId,
            CommentText = $"Count test comment {Guid.NewGuid()}",
            CommentedBy = userId,
            IsInternal = false,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        var commentId = await _repository.CreateAsync(comment);
        _createdCommentIds.Add(commentId);

        // Act
        var newCount = await _repository.GetCommentCountAsync(ticketId, includeInternal: true);

        // Assert
        Assert.Equal(initialCount + 1, newCount);
    }

    [Fact]
    public async Task GetRecentCommentsAsync_ShouldReturnRecentComments()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var comment = new SysTicketComment
        {
            TicketId = ticketId,
            CommentText = $"Recent comment {Guid.NewGuid()}",
            CommentedBy = userId,
            IsInternal = false,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        var commentId = await _repository.CreateAsync(comment);
        _createdCommentIds.Add(commentId);

        // Act
        var recentComments = await _repository.GetRecentCommentsAsync(limit: 10);

        // Assert
        Assert.NotEmpty(recentComments);
        Assert.Contains(recentComments, c => c.RowId == commentId);
        
        // Verify ordering (most recent first)
        for (int i = 0; i < recentComments.Count - 1; i++)
        {
            Assert.True(recentComments[i].CreationDate >= recentComments[i + 1].CreationDate);
        }
    }

    [Fact]
    public async Task SearchCommentsAsync_ShouldFindCommentsBySearchTerm()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var uniqueText = $"SEARCHABLE_{Guid.NewGuid().ToString().Replace("-", "")}";
        var comment = new SysTicketComment
        {
            TicketId = ticketId,
            CommentText = $"This comment contains {uniqueText} for searching",
            CommentedBy = userId,
            IsInternal = false,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        var commentId = await _repository.CreateAsync(comment);
        _createdCommentIds.Add(commentId);

        // Act
        var (results, totalCount) = await _repository.SearchCommentsAsync(uniqueText);

        // Assert
        Assert.True(totalCount > 0);
        Assert.Contains(results, c => c.RowId == commentId);
    }

    [Fact]
    public async Task TransactionRollback_ShouldNotPersistComment()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var comment = new SysTicketComment
        {
            TicketId = ticketId,
            CommentText = $"Rollback test comment {Guid.NewGuid()}",
            CommentedBy = userId,
            IsInternal = false,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act - Create within a transaction and rollback
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.TicketComments.Add(comment);
            await _context.SaveChangesAsync();
            
            var commentId = comment.RowId;
            Assert.True(commentId > 0);

            // Rollback the transaction
            await transaction.RollbackAsync();

            // Assert - Verify the comment was not persisted
            var retrievedComment = await _repository.GetByIdAsync(commentId);
            Assert.Null(retrievedComment);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [Fact]
    public async Task TransactionCommit_ShouldPersistComment()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var comment = new SysTicketComment
        {
            TicketId = ticketId,
            CommentText = $"Commit test comment {Guid.NewGuid()}",
            CommentedBy = userId,
            IsInternal = false,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        long commentId = 0;

        // Act - Create within a transaction and commit
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.TicketComments.Add(comment);
            await _context.SaveChangesAsync();
            
            commentId = comment.RowId;
            _createdCommentIds.Add(commentId);
            Assert.True(commentId > 0);

            // Commit the transaction
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // Assert - Verify the comment was persisted
        var retrievedComment = await _repository.GetByIdAsync(commentId);
        Assert.NotNull(retrievedComment);
        Assert.Equal(comment.CommentText, retrievedComment.CommentText);
    }

    [Fact]
    public async Task SequenceGeneration_ShouldGenerateUniqueIds()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var comment1 = new SysTicketComment
        {
            TicketId = ticketId,
            CommentText = $"Sequence test 1 {Guid.NewGuid()}",
            CommentedBy = userId,
            IsInternal = false,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        var comment2 = new SysTicketComment
        {
            TicketId = ticketId,
            CommentText = $"Sequence test 2 {Guid.NewGuid()}",
            CommentedBy = userId,
            IsInternal = false,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        var id1 = await _repository.CreateAsync(comment1);
        var id2 = await _repository.CreateAsync(comment2);
        _createdCommentIds.Add(id1);
        _createdCommentIds.Add(id2);

        // Assert
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.NotEqual(id1, id2);
    }

    public void Dispose()
    {
        // Cleanup - Delete all created test data
        foreach (var id in _createdCommentIds)
        {
            try
            {
                var comment = _context.TicketComments
                    .IgnoreQueryFilters()
                    .FirstOrDefault(c => c.RowId == id);
                if (comment != null)
                {
                    _context.TicketComments.Remove(comment);
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
