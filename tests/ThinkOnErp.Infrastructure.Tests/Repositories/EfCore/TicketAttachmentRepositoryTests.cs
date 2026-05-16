using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for TicketAttachmentRepository using EF Core InMemory provider.
/// Tests all CRUD operations, BLOB handling, file size limits, and exception scenarios.
/// </summary>
public class TicketAttachmentRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly TicketAttachmentRepository _repository;
    private readonly Mock<ILogger<TicketAttachmentRepository>> _loggerMock;

    public TicketAttachmentRepositoryTests()
    {
        // Create InMemory database with unique name for each test instance
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_TicketAttachment_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<TicketAttachmentRepository>>();
        _repository = new TicketAttachmentRepository(_context, _loggerMock.Object);
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
    public async Task GetByTicketIdAsync_ReturnsAttachmentsForTicket()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var attachments = new List<SysTicketAttachment>
        {
            new SysTicketAttachment
            {
                TicketId = testData.ticket.RowId,
                FileName = "file1.pdf",
                FileSize = 1024,
                MimeType = "application/pdf",
                FileContent = new byte[] { 1, 2, 3 },
                UploadedBy = testData.user.RowId,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketAttachment
            {
                TicketId = testData.ticket.RowId,
                FileName = "file2.jpg",
                FileSize = 2048,
                MimeType = "image/jpeg",
                FileContent = new byte[] { 4, 5, 6 },
                UploadedBy = testData.user.RowId,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.TicketAttachments.AddRangeAsync(attachments);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTicketIdAsync(testData.ticket.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("file1.pdf", result[0].FileName);
        Assert.Equal("file2.jpg", result[1].FileName);
    }

    [Fact]
    public async Task GetByTicketIdAsync_ReturnsEmptyList_WhenNoAttachments()
    {
        // Arrange
        var testData = await SetupTestDataAsync();

        // Act
        var result = await _repository.GetByTicketIdAsync(testData.ticket.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsAttachment_WithNavigationProperties()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var attachment = new SysTicketAttachment
        {
            TicketId = testData.ticket.RowId,
            FileName = "test.pdf",
            FileSize = 1024,
            MimeType = "application/pdf",
            FileContent = new byte[] { 1, 2, 3, 4, 5 },
            UploadedBy = testData.user.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketAttachments.AddAsync(attachment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(attachment.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test.pdf", result.FileName);
        Assert.NotNull(result.Ticket);
        Assert.NotNull(result.Uploader);
        Assert.NotNull(result.FileContent);
        Assert.Equal(5, result.FileContent.Length);
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
    public async Task CreateAsync_CreatesNewAttachment_WithBlobData()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var fileContent = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var attachment = new SysTicketAttachment
        {
            TicketId = testData.ticket.RowId,
            FileName = "document.pdf",
            FileSize = fileContent.Length,
            MimeType = "application/pdf",
            FileContent = fileContent,
            UploadedBy = testData.user.RowId,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(attachment);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, attachment.RowId);
        Assert.NotNull(attachment.UploadDate);
        Assert.True(attachment.IsActive);

        // Verify in database
        var savedAttachment = await _context.TicketAttachments.FindAsync(result);
        Assert.NotNull(savedAttachment);
        Assert.Equal("document.pdf", savedAttachment.FileName);
        Assert.NotNull(savedAttachment.FileContent);
        Assert.Equal(10, savedAttachment.FileContent.Length);
    }

    [Fact]
    public async Task CreateAsync_HandlesNullFileContent()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var attachment = new SysTicketAttachment
        {
            TicketId = testData.ticket.RowId,
            FileName = "empty.txt",
            FileSize = 0,
            MimeType = "text/plain",
            FileContent = null,
            UploadedBy = testData.user.RowId,
            CreationUser = "test"
        };

        // Act
        var result = await _repository.CreateAsync(attachment);

        // Assert
        Assert.True(result > 0);
        var savedAttachment = await _context.TicketAttachments.FindAsync(result);
        Assert.NotNull(savedAttachment);
        Assert.Null(savedAttachment.FileContent);
    }

    [Fact]
    public async Task CreateAsync_HandlesLargeBlobData()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        // Create 1MB file
        var largeFileContent = new byte[1024 * 1024];
        new Random().NextBytes(largeFileContent);
        
        var attachment = new SysTicketAttachment
        {
            TicketId = testData.ticket.RowId,
            FileName = "large_file.bin",
            FileSize = largeFileContent.Length,
            MimeType = "application/octet-stream",
            FileContent = largeFileContent,
            UploadedBy = testData.user.RowId,
            CreationUser = "test"
        };

        // Act
        var result = await _repository.CreateAsync(attachment);

        // Assert
        Assert.True(result > 0);
        var savedAttachment = await _context.TicketAttachments.FindAsync(result);
        Assert.NotNull(savedAttachment);
        Assert.Equal(1024 * 1024, savedAttachment.FileContent!.Length);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesAttachment_ReturnsRowsAffected()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var attachment = new SysTicketAttachment
        {
            TicketId = testData.ticket.RowId,
            FileName = "to_delete.pdf",
            FileSize = 1024,
            MimeType = "application/pdf",
            FileContent = new byte[] { 1, 2, 3 },
            UploadedBy = testData.user.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketAttachments.AddAsync(attachment);
        await _context.SaveChangesAsync();
        var attachmentId = attachment.RowId;

        // Act
        var result = await _repository.DeleteAsync(attachmentId, "delete_user");

        // Assert
        Assert.Equal(1, result);

        // Verify attachment is soft deleted
        var deletedAttachment = await _context.TicketAttachments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.RowId == attachmentId);
        Assert.NotNull(deletedAttachment);
        Assert.False(deletedAttachment.IsActive);
        Assert.Equal("delete_user", deletedAttachment.UpdateUser);
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

    #region GetAttachmentCountAsync Tests

    [Fact]
    public async Task GetAttachmentCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        for (int i = 0; i < 3; i++)
        {
            await _context.TicketAttachments.AddAsync(new SysTicketAttachment
            {
                TicketId = testData.ticket.RowId,
                FileName = $"file{i}.pdf",
                FileSize = 1024,
                MimeType = "application/pdf",
                FileContent = new byte[] { 1, 2, 3 },
                UploadedBy = testData.user.RowId,
                IsActive = true,
                CreationUser = "test"
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAttachmentCountAsync(testData.ticket.RowId);

        // Assert
        Assert.Equal(3, result);
    }

    #endregion

    #region GetTotalAttachmentSizeAsync Tests

    [Fact]
    public async Task GetTotalAttachmentSizeAsync_ReturnsCorrectTotalSize()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var attachments = new List<SysTicketAttachment>
        {
            new SysTicketAttachment
            {
                TicketId = testData.ticket.RowId,
                FileName = "file1.pdf",
                FileSize = 1024,
                MimeType = "application/pdf",
                FileContent = new byte[1024],
                UploadedBy = testData.user.RowId,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketAttachment
            {
                TicketId = testData.ticket.RowId,
                FileName = "file2.pdf",
                FileSize = 2048,
                MimeType = "application/pdf",
                FileContent = new byte[2048],
                UploadedBy = testData.user.RowId,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.TicketAttachments.AddRangeAsync(attachments);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetTotalAttachmentSizeAsync(testData.ticket.RowId);

        // Assert
        Assert.Equal(3072, result); // 1024 + 2048
    }

    #endregion

    #region GetAttachmentMetadataAsync Tests

    [Fact]
    public async Task GetAttachmentMetadataAsync_ReturnsMetadataWithoutFileContent()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var attachment = new SysTicketAttachment
        {
            TicketId = testData.ticket.RowId,
            FileName = "test.pdf",
            FileSize = 1024,
            MimeType = "application/pdf",
            FileContent = new byte[1024],
            UploadedBy = testData.user.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketAttachments.AddAsync(attachment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAttachmentMetadataAsync(testData.ticket.RowId);

        // Assert
        Assert.Single(result);
        Assert.Equal("test.pdf", result[0].FileName);
        Assert.Equal(1024, result[0].FileSize);
        Assert.Null(result[0].FileContent); // Should not include file content
    }

    #endregion

    #region GetFileContentAsync Tests

    [Fact]
    public async Task GetFileContentAsync_ReturnsFileContent()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var attachment = new SysTicketAttachment
        {
            TicketId = testData.ticket.RowId,
            FileName = "test.pdf",
            FileSize = fileContent.Length,
            MimeType = "application/pdf",
            FileContent = fileContent,
            UploadedBy = testData.user.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketAttachments.AddAsync(attachment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetFileContentAsync(attachment.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.Length);
        Assert.Equal(fileContent, result);
    }

    [Fact]
    public async Task GetFileContentAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetFileContentAsync(999);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region CanAddAttachmentAsync Tests

    [Fact]
    public async Task CanAddAttachmentAsync_ReturnsTrue_WhenWithinLimits()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        // Add one small attachment
        await _context.TicketAttachments.AddAsync(new SysTicketAttachment
        {
            TicketId = testData.ticket.RowId,
            FileName = "small.pdf",
            FileSize = 1024,
            MimeType = "application/pdf",
            FileContent = new byte[1024],
            UploadedBy = testData.user.RowId,
            IsActive = true,
            CreationUser = "test"
        });
        await _context.SaveChangesAsync();

        // Act - Try to add another small file
        var result = await _repository.CanAddAttachmentAsync(testData.ticket.RowId, 1024);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanAddAttachmentAsync_ReturnsFalse_WhenExceedsMaxAttachments()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        // Add 10 attachments (assuming max is 10)
        for (int i = 0; i < 10; i++)
        {
            await _context.TicketAttachments.AddAsync(new SysTicketAttachment
            {
                TicketId = testData.ticket.RowId,
                FileName = $"file{i}.pdf",
                FileSize = 1024,
                MimeType = "application/pdf",
                FileContent = new byte[1024],
                UploadedBy = testData.user.RowId,
                IsActive = true,
                CreationUser = "test"
            });
        }
        await _context.SaveChangesAsync();

        // Act - Try to add one more
        var result = await _repository.CanAddAttachmentAsync(testData.ticket.RowId, 1024);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region GetByFileTypeAsync Tests

    [Fact]
    public async Task GetByFileTypeAsync_ReturnsAttachmentsOfSpecificType()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var attachments = new List<SysTicketAttachment>
        {
            new SysTicketAttachment
            {
                TicketId = testData.ticket.RowId,
                FileName = "doc.pdf",
                FileSize = 1024,
                MimeType = "application/pdf",
                FileContent = new byte[1024],
                UploadedBy = testData.user.RowId,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketAttachment
            {
                TicketId = testData.ticket.RowId,
                FileName = "image.jpg",
                FileSize = 2048,
                MimeType = "image/jpeg",
                FileContent = new byte[2048],
                UploadedBy = testData.user.RowId,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.TicketAttachments.AddRangeAsync(attachments);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByFileTypeAsync("application/pdf");

        // Assert
        Assert.Single(result);
        Assert.Equal("doc.pdf", result[0].FileName);
    }

    #endregion

    #region GetAttachmentStatisticsAsync Tests

    [Fact]
    public async Task GetAttachmentStatisticsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        var testData = await SetupTestDataAsync();
        
        var attachments = new List<SysTicketAttachment>
        {
            new SysTicketAttachment
            {
                TicketId = testData.ticket.RowId,
                FileName = "file1.pdf",
                FileSize = 1024,
                MimeType = "application/pdf",
                FileContent = new byte[1024],
                UploadedBy = testData.user.RowId,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketAttachment
            {
                TicketId = testData.ticket.RowId,
                FileName = "file2.jpg",
                FileSize = 2048,
                MimeType = "image/jpeg",
                FileContent = new byte[2048],
                UploadedBy = testData.user.RowId,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.TicketAttachments.AddRangeAsync(attachments);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAttachmentStatisticsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("TotalAttachments"));
        Assert.True(result.ContainsKey("TotalSize"));
        Assert.Equal(2L, result["TotalAttachments"]);
        Assert.Equal(3072L, result["TotalSize"]); // 1024 + 2048
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketAttachmentRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketAttachmentRepository(_context, null!));
    }

    #endregion
}
