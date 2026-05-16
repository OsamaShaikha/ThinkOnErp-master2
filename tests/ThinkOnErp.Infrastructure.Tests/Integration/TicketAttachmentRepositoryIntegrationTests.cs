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
/// Integration tests for TicketAttachmentRepository using real Oracle database connection.
/// Tests EF Core implementation against actual Oracle database with BLOB handling.
/// 
/// **Validates: Requirements REQ-12, REQ-23**
/// - Requirement 12: Testing Strategy - Integration tests that verify database operations
/// - Requirement 23: BLOB and Large Object Handling
/// - Tests CRUD operations with BLOB data
/// - Tests file size limits
/// - Verifies data persistence
/// </summary>
public class TicketAttachmentRepositoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ThinkOnErpDbContext _context;
    private readonly TicketAttachmentRepository _repository;
    private readonly List<long> _createdAttachmentIds = new();

    public TicketAttachmentRepositoryIntegrationTests()
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
        var logger = _serviceProvider.GetRequiredService<ILogger<TicketAttachmentRepository>>();
        _repository = new TicketAttachmentRepository(_context, logger);
    }

    private async Task<long> GetOrCreateTestTicketAsync()
    {
        var ticket = await _context.Tickets
            .Where(t => t.IsActive)
            .FirstOrDefaultAsync();

        if (ticket != null)
        {
            return ticket.RowId;
        }

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
    public async Task CreateAsync_ShouldPersistAttachmentWithBlobData()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var fileContent = new byte[1024]; // 1KB file
        new Random().NextBytes(fileContent);

        var attachment = new SysTicketAttachment
        {
            TicketId = ticketId,
            FileName = $"test_file_{Guid.NewGuid()}.pdf",
            FileSize = fileContent.Length,
            MimeType = "application/pdf",
            FileContent = fileContent,
            UploadedBy = userId,
            CreationUser = "IntegrationTest",
            UploadDate = DateTime.Now
        };

        // Act
        var attachmentId = await _repository.CreateAsync(attachment);
        _createdAttachmentIds.Add(attachmentId);

        // Assert
        Assert.True(attachmentId > 0);
        
        // Verify persistence
        var savedAttachment = await _repository.GetByIdAsync(attachmentId);
        Assert.NotNull(savedAttachment);
        Assert.Equal(attachment.FileName, savedAttachment.FileName);
        Assert.NotNull(savedAttachment.FileContent);
        Assert.Equal(fileContent.Length, savedAttachment.FileContent.Length);
    }

    [Fact]
    public async Task CreateAsync_ShouldHandleLargeBlobData()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        // Create 2MB file
        var largeFileContent = new byte[2 * 1024 * 1024];
        new Random().NextBytes(largeFileContent);

        var attachment = new SysTicketAttachment
        {
            TicketId = ticketId,
            FileName = $"large_file_{Guid.NewGuid()}.bin",
            FileSize = largeFileContent.Length,
            MimeType = "application/octet-stream",
            FileContent = largeFileContent,
            UploadedBy = userId,
            CreationUser = "IntegrationTest",
            UploadDate = DateTime.Now
        };

        // Act
        var attachmentId = await _repository.CreateAsync(attachment);
        _createdAttachmentIds.Add(attachmentId);

        // Assert
        Assert.True(attachmentId > 0);
        
        // Verify large BLOB was stored correctly
        var savedAttachment = await _repository.GetByIdAsync(attachmentId);
        Assert.NotNull(savedAttachment);
        Assert.NotNull(savedAttachment.FileContent);
        Assert.Equal(2 * 1024 * 1024, savedAttachment.FileContent.Length);
    }

    [Fact]
    public async Task GetByTicketIdAsync_ShouldReturnAttachmentsWithNavigationProperties()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var fileContent = new byte[512];
        new Random().NextBytes(fileContent);

        var attachment = new SysTicketAttachment
        {
            TicketId = ticketId,
            FileName = $"nav_test_{Guid.NewGuid()}.jpg",
            FileSize = fileContent.Length,
            MimeType = "image/jpeg",
            FileContent = fileContent,
            UploadedBy = userId,
            CreationUser = "IntegrationTest",
            UploadDate = DateTime.Now
        };

        var attachmentId = await _repository.CreateAsync(attachment);
        _createdAttachmentIds.Add(attachmentId);

        // Act
        var attachments = await _repository.GetByTicketIdAsync(ticketId);

        // Assert
        Assert.NotEmpty(attachments);
        var testAttachment = attachments.FirstOrDefault(a => a.RowId == attachmentId);
        Assert.NotNull(testAttachment);
        Assert.NotNull(testAttachment.Ticket);
        Assert.NotNull(testAttachment.Uploader);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteAttachment()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var fileContent = new byte[256];
        new Random().NextBytes(fileContent);

        var attachment = new SysTicketAttachment
        {
            TicketId = ticketId,
            FileName = $"delete_test_{Guid.NewGuid()}.txt",
            FileSize = fileContent.Length,
            MimeType = "text/plain",
            FileContent = fileContent,
            UploadedBy = userId,
            CreationUser = "IntegrationTest",
            UploadDate = DateTime.Now
        };

        var attachmentId = await _repository.CreateAsync(attachment);
        _createdAttachmentIds.Add(attachmentId);

        // Act
        var result = await _repository.DeleteAsync(attachmentId, "IntegrationTest");

        // Assert
        Assert.Equal(1, result);
        
        // Verify soft delete
        var deletedAttachment = await _context.TicketAttachments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.RowId == attachmentId);
        Assert.NotNull(deletedAttachment);
        Assert.False(deletedAttachment.IsActive);
    }

    [Fact]
    public async Task GetAttachmentCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var initialCount = await _repository.GetAttachmentCountAsync(ticketId);

        var fileContent = new byte[128];
        new Random().NextBytes(fileContent);

        var attachment = new SysTicketAttachment
        {
            TicketId = ticketId,
            FileName = $"count_test_{Guid.NewGuid()}.doc",
            FileSize = fileContent.Length,
            MimeType = "application/msword",
            FileContent = fileContent,
            UploadedBy = userId,
            CreationUser = "IntegrationTest",
            UploadDate = DateTime.Now
        };

        var attachmentId = await _repository.CreateAsync(attachment);
        _createdAttachmentIds.Add(attachmentId);

        // Act
        var newCount = await _repository.GetAttachmentCountAsync(ticketId);

        // Assert
        Assert.Equal(initialCount + 1, newCount);
    }

    [Fact]
    public async Task GetTotalAttachmentSizeAsync_ShouldReturnCorrectTotalSize()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var initialSize = await _repository.GetTotalAttachmentSizeAsync(ticketId);

        var fileContent = new byte[2048];
        new Random().NextBytes(fileContent);

        var attachment = new SysTicketAttachment
        {
            TicketId = ticketId,
            FileName = $"size_test_{Guid.NewGuid()}.bin",
            FileSize = fileContent.Length,
            MimeType = "application/octet-stream",
            FileContent = fileContent,
            UploadedBy = userId,
            CreationUser = "IntegrationTest",
            UploadDate = DateTime.Now
        };

        var attachmentId = await _repository.CreateAsync(attachment);
        _createdAttachmentIds.Add(attachmentId);

        // Act
        var newSize = await _repository.GetTotalAttachmentSizeAsync(ticketId);

        // Assert
        Assert.Equal(initialSize + 2048, newSize);
    }

    [Fact]
    public async Task GetAttachmentMetadataAsync_ShouldReturnMetadataWithoutFileContent()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var fileContent = new byte[1024];
        new Random().NextBytes(fileContent);

        var attachment = new SysTicketAttachment
        {
            TicketId = ticketId,
            FileName = $"metadata_test_{Guid.NewGuid()}.pdf",
            FileSize = fileContent.Length,
            MimeType = "application/pdf",
            FileContent = fileContent,
            UploadedBy = userId,
            CreationUser = "IntegrationTest",
            UploadDate = DateTime.Now
        };

        var attachmentId = await _repository.CreateAsync(attachment);
        _createdAttachmentIds.Add(attachmentId);

        // Act
        var metadata = await _repository.GetAttachmentMetadataAsync(ticketId);

        // Assert
        var testMetadata = metadata.FirstOrDefault(m => m.RowId == attachmentId);
        Assert.NotNull(testMetadata);
        Assert.Equal(attachment.FileName, testMetadata.FileName);
        Assert.Equal(1024, testMetadata.FileSize);
        Assert.Null(testMetadata.FileContent); // Should not include file content
    }

    [Fact]
    public async Task GetFileContentAsync_ShouldReturnOnlyFileContent()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var fileContent = new byte[512];
        new Random().NextBytes(fileContent);

        var attachment = new SysTicketAttachment
        {
            TicketId = ticketId,
            FileName = $"content_test_{Guid.NewGuid()}.bin",
            FileSize = fileContent.Length,
            MimeType = "application/octet-stream",
            FileContent = fileContent,
            UploadedBy = userId,
            CreationUser = "IntegrationTest",
            UploadDate = DateTime.Now
        };

        var attachmentId = await _repository.CreateAsync(attachment);
        _createdAttachmentIds.Add(attachmentId);

        // Act
        var retrievedContent = await _repository.GetFileContentAsync(attachmentId);

        // Assert
        Assert.NotNull(retrievedContent);
        Assert.Equal(512, retrievedContent.Length);
        Assert.Equal(fileContent, retrievedContent);
    }

    [Fact]
    public async Task TransactionRollback_ShouldNotPersistAttachment()
    {
        // Arrange
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var fileContent = new byte[256];
        new Random().NextBytes(fileContent);

        var attachment = new SysTicketAttachment
        {
            TicketId = ticketId,
            FileName = $"rollback_test_{Guid.NewGuid()}.txt",
            FileSize = fileContent.Length,
            MimeType = "text/plain",
            FileContent = fileContent,
            UploadedBy = userId,
            CreationUser = "IntegrationTest",
            UploadDate = DateTime.Now
        };

        // Act - Create within a transaction and rollback
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.TicketAttachments.Add(attachment);
            await _context.SaveChangesAsync();
            
            var attachmentId = attachment.RowId;
            Assert.True(attachmentId > 0);

            // Rollback the transaction
            await transaction.RollbackAsync();

            // Assert - Verify the attachment was not persisted
            var retrievedAttachment = await _repository.GetByIdAsync(attachmentId);
            Assert.Null(retrievedAttachment);
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
        var ticketId = await GetOrCreateTestTicketAsync();
        var userId = await GetTestUserIdAsync();

        var fileContent1 = new byte[128];
        var fileContent2 = new byte[256];
        new Random().NextBytes(fileContent1);
        new Random().NextBytes(fileContent2);

        var attachment1 = new SysTicketAttachment
        {
            TicketId = ticketId,
            FileName = $"seq_test_1_{Guid.NewGuid()}.txt",
            FileSize = fileContent1.Length,
            MimeType = "text/plain",
            FileContent = fileContent1,
            UploadedBy = userId,
            CreationUser = "IntegrationTest",
            UploadDate = DateTime.Now
        };

        var attachment2 = new SysTicketAttachment
        {
            TicketId = ticketId,
            FileName = $"seq_test_2_{Guid.NewGuid()}.txt",
            FileSize = fileContent2.Length,
            MimeType = "text/plain",
            FileContent = fileContent2,
            UploadedBy = userId,
            CreationUser = "IntegrationTest",
            UploadDate = DateTime.Now
        };

        // Act
        var id1 = await _repository.CreateAsync(attachment1);
        var id2 = await _repository.CreateAsync(attachment2);
        _createdAttachmentIds.Add(id1);
        _createdAttachmentIds.Add(id2);

        // Assert
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.NotEqual(id1, id2);
    }

    public void Dispose()
    {
        // Cleanup - Delete all created test data
        foreach (var id in _createdAttachmentIds)
        {
            try
            {
                var attachment = _context.TicketAttachments
                    .IgnoreQueryFilters()
                    .FirstOrDefault(a => a.RowId == id);
                if (attachment != null)
                {
                    _context.TicketAttachments.Remove(attachment);
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
