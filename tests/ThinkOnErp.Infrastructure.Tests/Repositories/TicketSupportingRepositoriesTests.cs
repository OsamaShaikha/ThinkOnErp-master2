using ThinkOnErp.Domain.Entities;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories;

/// <summary>
/// Unit tests for ticket supporting entities validation and business logic.
/// Tests basic validation rules and business logic without requiring database connections.
/// </summary>
public class TicketSupportingRepositoriesTests
{
    [Fact]
    public void SysTicketAttachment_IsValid_WithValidAttachment_ShouldReturnTrue()
    {
        // Arrange
        var validAttachment = new SysTicketAttachment
        {
            FileName = "test.pdf",
            FileSize = 1024,
            MimeType = "application/pdf",
            FileContent = new byte[1024]
        };

        // Act & Assert
        Assert.True(validAttachment.IsValid);
        Assert.True(validAttachment.IsFileSizeValid);
        Assert.True(validAttachment.IsFileExtensionValid);
        Assert.True(validAttachment.IsMimeTypeValid);
    }

    [Fact]
    public void SysTicketAttachment_IsValid_WithInvalidAttachment_ShouldReturnFalse()
    {
        // Arrange
        var invalidAttachment = new SysTicketAttachment
        {
            FileName = "test.exe", // Invalid extension
            FileSize = SysTicketAttachment.MaxFileSizeBytes + 1, // Too large
            MimeType = "application/x-executable", // Invalid MIME type
            FileContent = new byte[1024]
        };

        // Act & Assert
        Assert.False(invalidAttachment.IsValid);
        Assert.False(invalidAttachment.IsFileSizeValid);
        Assert.False(invalidAttachment.IsFileExtensionValid);
        Assert.False(invalidAttachment.IsMimeTypeValid);
    }

    [Fact]
    public void SysTicketAttachment_GetFormattedFileSize_ShouldReturnCorrectFormat()
    {
        // Arrange & Act & Assert
        var attachment1 = new SysTicketAttachment { FileSize = 512 };
        Assert.Equal("512 B", attachment1.GetFormattedFileSize());

        var attachment2 = new SysTicketAttachment { FileSize = 1536 }; // 1.5 KB
        Assert.Equal("1.5 KB", attachment2.GetFormattedFileSize());

        var attachment3 = new SysTicketAttachment { FileSize = 2097152 }; // 2 MB
        Assert.Equal("2.0 MB", attachment3.GetFormattedFileSize());
    }

    [Fact]
    public void SysTicketComment_GetTruncatedText_ShouldTruncateCorrectly()
    {
        // Arrange
        var comment = new SysTicketComment
        {
            CommentText = "This is a very long comment that should be truncated when displayed in lists to avoid cluttering the interface."
        };

        // Act
        var truncated = comment.GetTruncatedText(50);

        // Assert
        Assert.Equal("This is a very long comment that should be truncat...", truncated);
    }

    [Fact]
    public void SysTicketComment_IsVisibleToRequester_ShouldReturnCorrectValue()
    {
        // Arrange & Act & Assert
        var publicComment = new SysTicketComment { IsInternal = false };
        Assert.True(publicComment.IsVisibleToRequester);
        Assert.False(publicComment.IsAdminOnly);

        var internalComment = new SysTicketComment { IsInternal = true };
        Assert.False(internalComment.IsVisibleToRequester);
        Assert.True(internalComment.IsAdminOnly);
    }

    [Fact]
    public void SysTicketAttachment_ValidateContentType_WithPdfFile_ShouldReturnTrue()
    {
        // Arrange
        var pdfAttachment = new SysTicketAttachment
        {
            FileName = "test.pdf",
            MimeType = "application/pdf",
            FileContent = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 } // %PDF-1.4
        };

        // Act & Assert
        Assert.True(pdfAttachment.ValidateContentType());
    }

    [Fact]
    public void SysTicketAttachment_ValidateContentType_WithJpegFile_ShouldReturnTrue()
    {
        // Arrange
        var jpegAttachment = new SysTicketAttachment
        {
            FileName = "test.jpg",
            MimeType = "image/jpeg",
            FileContent = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 } // JPEG header
        };

        // Act & Assert
        Assert.True(jpegAttachment.ValidateContentType());
    }

    [Fact]
    public void SysTicketAttachment_ValidateContentType_WithPngFile_ShouldReturnTrue()
    {
        // Arrange
        var pngAttachment = new SysTicketAttachment
        {
            FileName = "test.png",
            MimeType = "image/png",
            FileContent = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } // PNG header
        };

        // Act & Assert
        Assert.True(pngAttachment.ValidateContentType());
    }

    [Fact]
    public void SysTicketAttachment_Constants_ShouldHaveCorrectValues()
    {
        // Assert
        Assert.Equal(10 * 1024 * 1024, SysTicketAttachment.MaxFileSizeBytes); // 10MB
        Assert.Equal(5, SysTicketAttachment.MaxAttachmentsPerTicket);
        Assert.Contains(".pdf", SysTicketAttachment.AllowedFileExtensions);
        Assert.Contains(".jpg", SysTicketAttachment.AllowedFileExtensions);
        Assert.Contains(".png", SysTicketAttachment.AllowedFileExtensions);
        Assert.Contains("application/pdf", SysTicketAttachment.AllowedMimeTypes);
        Assert.Contains("image/jpeg", SysTicketAttachment.AllowedMimeTypes);
        Assert.Contains("image/png", SysTicketAttachment.AllowedMimeTypes);
    }
}