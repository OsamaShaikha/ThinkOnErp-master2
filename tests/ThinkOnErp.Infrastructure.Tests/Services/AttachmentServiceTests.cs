using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for AttachmentService.
/// Tests file validation, encoding/decoding, and security checks.
/// **Validates: Requirements 7.1-7.12**
/// </summary>
public class AttachmentServiceTests
{
    private readonly Mock<ILogger<AttachmentService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly AttachmentService _service;

    public AttachmentServiceTests()
    {
        _mockLogger = new Mock<ILogger<AttachmentService>>();
        _mockConfiguration = new Mock<IConfiguration>();

        // Setup configuration sections instead of GetValue extension method
        var maxFileSizeSection = new Mock<IConfigurationSection>();
        maxFileSizeSection.Setup(s => s.Value).Returns((10 * 1024 * 1024).ToString());
        _mockConfiguration.Setup(c => c.GetSection("Attachments:MaxFileSizeBytes")).Returns(maxFileSizeSection.Object);

        var maxAttachmentCountSection = new Mock<IConfigurationSection>();
        maxAttachmentCountSection.Setup(s => s.Value).Returns("5");
        _mockConfiguration.Setup(c => c.GetSection("Attachments:MaxAttachmentCount")).Returns(maxAttachmentCountSection.Object);

        var allowedExtensionsSection = new Mock<IConfigurationSection>();
        allowedExtensionsSection.Setup(s => s.Value).Returns((string)null);
        _mockConfiguration.Setup(c => c.GetSection("Attachments:AllowedExtensions")).Returns(allowedExtensionsSection.Object);

        _service = new AttachmentService(_mockLogger.Object, _mockConfiguration.Object);
    }

    [Theory]
    [InlineData("test.pdf", "application/pdf", true)]
    [InlineData("test.doc", "application/msword", true)]
    [InlineData("test.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", true)]
    [InlineData("test.xls", "application/vnd.ms-excel", true)]
    [InlineData("test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", true)]
    [InlineData("test.jpg", "image/jpeg", true)]
    [InlineData("test.jpeg", "image/jpeg", true)]
    [InlineData("test.png", "image/png", true)]
    [InlineData("test.txt", "text/plain", true)]
    [InlineData("test.exe", "application/x-msdownload", false)]
    [InlineData("test.pdf", "image/jpeg", false)]
    [InlineData("test.jpg", "application/pdf", false)]
    [InlineData("", "application/pdf", false)]
    [InlineData("test.pdf", "", false)]
    public void IsValidFileType_VariousInputs_ReturnsExpectedResult(string fileName, string mimeType, bool expected)
    {
        // Act
        var result = _service.IsValidFileType(fileName, mimeType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsValidFileSize_SmallFile_ReturnsTrue()
    {
        // Arrange - 1KB file
        var fileBytes = new byte[1024];
        var base64Content = Convert.ToBase64String(fileBytes);

        // Act
        var result = _service.IsValidFileSize(base64Content);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidFileSize_LargeFile_ReturnsFalse()
    {
        // Arrange - 11MB file (exceeds 10MB limit)
        var fileBytes = new byte[11 * 1024 * 1024];
        var base64Content = Convert.ToBase64String(fileBytes);

        // Act
        var result = _service.IsValidFileSize(base64Content);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidFileSize_EmptyString_ReturnsFalse()
    {
        // Act
        var result = _service.IsValidFileSize("");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidBase64Content_ValidBase64_ReturnsTrue()
    {
        // Arrange
        var fileBytes = new byte[] { 1, 2, 3, 4, 5 };
        var base64Content = Convert.ToBase64String(fileBytes);

        // Act
        var result = _service.IsValidBase64Content(base64Content);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64!@#")]
    [InlineData("ABC")] // Invalid length (not multiple of 4)
    public void IsValidBase64Content_InvalidBase64_ReturnsFalse(string invalidBase64)
    {
        // Act
        var result = _service.IsValidBase64Content(invalidBase64);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DecodeBase64Content_ValidBase64_ReturnsBytes()
    {
        // Arrange
        var expectedBytes = new byte[] { 1, 2, 3, 4, 5 };
        var base64Content = Convert.ToBase64String(expectedBytes);

        // Act
        var result = _service.DecodeBase64Content(base64Content);

        // Assert
        Assert.Equal(expectedBytes, result);
    }

    [Fact]
    public void DecodeBase64Content_InvalidBase64_ThrowsArgumentException()
    {
        // Arrange
        var invalidBase64 = "not-base64!@#";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.DecodeBase64Content(invalidBase64));
    }

    [Fact]
    public void EncodeToBase64_ValidBytes_ReturnsBase64String()
    {
        // Arrange
        var fileBytes = new byte[] { 1, 2, 3, 4, 5 };
        var expectedBase64 = Convert.ToBase64String(fileBytes);

        // Act
        var result = _service.EncodeToBase64(fileBytes);

        // Assert
        Assert.Equal(expectedBase64, result);
    }

    [Fact]
    public void EncodeToBase64_EmptyBytes_ThrowsArgumentException()
    {
        // Arrange
        var emptyBytes = Array.Empty<byte>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.EncodeToBase64(emptyBytes));
    }

    [Fact]
    public void EncodeToBase64_NullBytes_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.EncodeToBase64(null));
    }

    [Fact]
    public async Task ValidateFileContentAsync_PdfWithValidSignature_ReturnsTrue()
    {
        // Arrange - PDF file signature
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4
        var mimeType = "application/pdf";
        var fileName = "test.pdf";

        // Act
        var result = await _service.ValidateFileContentAsync(pdfBytes, mimeType, fileName);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateFileContentAsync_JpegWithValidSignature_ReturnsTrue()
    {
        // Arrange - JPEG file signature
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        var mimeType = "image/jpeg";
        var fileName = "test.jpg";

        // Act
        var result = await _service.ValidateFileContentAsync(jpegBytes, mimeType, fileName);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateFileContentAsync_PngWithValidSignature_ReturnsTrue()
    {
        // Arrange - PNG file signature
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var mimeType = "image/png";
        var fileName = "test.png";

        // Act
        var result = await _service.ValidateFileContentAsync(pngBytes, mimeType, fileName);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateFileContentAsync_TextFile_ReturnsTrue()
    {
        // Arrange
        var textBytes = System.Text.Encoding.UTF8.GetBytes("This is a test text file.");
        var mimeType = "text/plain";
        var fileName = "test.txt";

        // Act
        var result = await _service.ValidateFileContentAsync(textBytes, mimeType, fileName);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateFileContentAsync_InvalidSignature_ReturnsFalse()
    {
        // Arrange - Wrong signature for PDF
        var invalidBytes = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        var mimeType = "application/pdf";
        var fileName = "test.pdf";

        // Act
        var result = await _service.ValidateFileContentAsync(invalidBytes, mimeType, fileName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateFileContentAsync_EmptyBytes_ReturnsFalse()
    {
        // Arrange
        var emptyBytes = Array.Empty<byte>();
        var mimeType = "application/pdf";
        var fileName = "test.pdf";

        // Act
        var result = await _service.ValidateFileContentAsync(emptyBytes, mimeType, fileName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetMaxFileSizeBytes_ReturnsConfiguredValue()
    {
        // Act
        var result = _service.GetMaxFileSizeBytes();

        // Assert
        Assert.Equal(10 * 1024 * 1024, result);
    }

    [Fact]
    public void GetMaxAttachmentCount_ReturnsConfiguredValue()
    {
        // Act
        var result = _service.GetMaxAttachmentCount();

        // Assert
        Assert.Equal(5, result);
    }

    [Fact]
    public void GetAllowedFileExtensions_ReturnsDefaultExtensions()
    {
        // Act
        var result = _service.GetAllowedFileExtensions();

        // Assert
        Assert.Contains(".pdf", result);
        Assert.Contains(".doc", result);
        Assert.Contains(".docx", result);
        Assert.Contains(".xls", result);
        Assert.Contains(".xlsx", result);
        Assert.Contains(".jpg", result);
        Assert.Contains(".jpeg", result);
        Assert.Contains(".png", result);
        Assert.Contains(".txt", result);
    }

    [Fact]
    public void GetAllowedMimeTypes_ReturnsAllMimeTypes()
    {
        // Act
        var result = _service.GetAllowedMimeTypes();

        // Assert
        Assert.Contains("application/pdf", result);
        Assert.Contains("application/msword", result);
        Assert.Contains("image/jpeg", result);
        Assert.Contains("image/png", result);
        Assert.Contains("text/plain", result);
    }

    [Fact]
    public void IsValidFileType_NullFileName_ReturnsFalse()
    {
        // Act
        var result = _service.IsValidFileType(null, "application/pdf");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidFileType_NullMimeType_ReturnsFalse()
    {
        // Act
        var result = _service.IsValidFileType("test.pdf", null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidFileSize_NullContent_ReturnsFalse()
    {
        // Act
        var result = _service.IsValidFileSize(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidBase64Content_NullContent_ReturnsFalse()
    {
        // Act
        var result = _service.IsValidBase64Content(null);

        // Assert
        Assert.False(result);
    }
}
