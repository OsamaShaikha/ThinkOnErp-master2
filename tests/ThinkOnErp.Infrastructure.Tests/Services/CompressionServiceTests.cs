using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for CompressionService
/// Tests GZip compression and decompression functionality for archived audit data
/// </summary>
public class CompressionServiceTests
{
    private readonly Mock<ILogger<CompressionService>> _mockLogger;
    private readonly CompressionService _compressionService;

    public CompressionServiceTests()
    {
        _mockLogger = new Mock<ILogger<CompressionService>>();
        _compressionService = new CompressionService(_mockLogger.Object);
    }

    [Fact]
    public void Compress_WithNullInput_ReturnsNull()
    {
        // Act
        var result = _compressionService.Compress(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Compress_WithEmptyString_ReturnsEmptyString()
    {
        // Act
        var result = _compressionService.Compress(string.Empty);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Compress_WithValidString_ReturnsBase64EncodedCompressedData()
    {
        // Arrange
        var input = "This is a test string that should be compressed using GZip algorithm.";

        // Act
        var result = _compressionService.Compress(input);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.NotEqual(input, result);
        
        // Verify it's valid Base64
        Assert.True(IsValidBase64(result));
    }

    [Fact]
    public void Compress_WithLargeString_ReducesSize()
    {
        // Arrange - Create a large string with repetitive content (compresses well)
        var input = string.Join("\n", Enumerable.Repeat("This is a repetitive line that should compress very well.", 1000));

        // Act
        var compressed = _compressionService.Compress(input);

        // Assert
        Assert.NotNull(compressed);
        var originalSize = _compressionService.GetSizeInBytes(input);
        var compressedSize = _compressionService.GetSizeInBytes(compressed);
        
        // Compressed size should be significantly smaller for repetitive data
        Assert.True(compressedSize < originalSize, 
            $"Compressed size ({compressedSize}) should be less than original size ({originalSize})");
    }

    [Fact]
    public void Decompress_WithNullInput_ReturnsNull()
    {
        // Act
        var result = _compressionService.Decompress(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Decompress_WithEmptyString_ReturnsEmptyString()
    {
        // Act
        var result = _compressionService.Decompress(string.Empty);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Decompress_WithValidCompressedData_ReturnsOriginalString()
    {
        // Arrange
        var original = "This is a test string that should be compressed and then decompressed.";
        var compressed = _compressionService.Compress(original);

        // Act
        var decompressed = _compressionService.Decompress(compressed);

        // Assert
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void Decompress_WithInvalidBase64_ThrowsException()
    {
        // Arrange
        var invalidBase64 = "This is not valid Base64!@#$%";

        // Act & Assert
        Assert.Throws<FormatException>(() => _compressionService.Decompress(invalidBase64));
    }

    [Fact]
    public void CompressDecompress_RoundTrip_PreservesData()
    {
        // Arrange
        var testCases = new[]
        {
            "Simple text",
            "Text with special characters: !@#$%^&*()_+-=[]{}|;':\",./<>?",
            "Text with unicode: 你好世界 🌍 مرحبا بالعالم",
            "JSON data: {\"name\":\"John\",\"age\":30,\"city\":\"New York\"}",
            "Multi-line\ntext\nwith\nnewlines",
            new string('A', 10000) // Large repetitive string
        };

        foreach (var testCase in testCases)
        {
            // Act
            var compressed = _compressionService.Compress(testCase);
            var decompressed = _compressionService.Decompress(compressed);

            // Assert
            Assert.Equal(testCase, decompressed);
        }
    }

    [Fact]
    public void CalculateCompressionRatio_WithValidData_ReturnsCorrectRatio()
    {
        // Arrange
        var original = new string('A', 1000);
        var compressed = _compressionService.Compress(original);

        // Act
        var ratio = _compressionService.CalculateCompressionRatio(original, compressed);

        // Assert
        Assert.True(ratio > 0 && ratio < 1, "Compression ratio should be between 0 and 1");
    }

    [Fact]
    public void CalculateCompressionRatio_WithNullOriginal_ReturnsZero()
    {
        // Act
        var ratio = _compressionService.CalculateCompressionRatio(null, "compressed");

        // Assert
        Assert.Equal(0, ratio);
    }

    [Fact]
    public void CalculateCompressionRatio_WithNullCompressed_ReturnsZero()
    {
        // Act
        var ratio = _compressionService.CalculateCompressionRatio("original", null);

        // Assert
        Assert.Equal(0, ratio);
    }

    [Fact]
    public void GetSizeInBytes_WithNullInput_ReturnsZero()
    {
        // Act
        var size = _compressionService.GetSizeInBytes(null);

        // Assert
        Assert.Equal(0, size);
    }

    [Fact]
    public void GetSizeInBytes_WithEmptyString_ReturnsZero()
    {
        // Act
        var size = _compressionService.GetSizeInBytes(string.Empty);

        // Assert
        Assert.Equal(0, size);
    }

    [Fact]
    public void GetSizeInBytes_WithValidString_ReturnsCorrectSize()
    {
        // Arrange
        var input = "Hello"; // 5 ASCII characters = 5 bytes in UTF-8

        // Act
        var size = _compressionService.GetSizeInBytes(input);

        // Assert
        Assert.Equal(5, size);
    }

    [Fact]
    public void GetSizeInBytes_WithUnicodeString_ReturnsCorrectSize()
    {
        // Arrange
        var input = "你好"; // 2 Chinese characters = 6 bytes in UTF-8 (3 bytes each)

        // Act
        var size = _compressionService.GetSizeInBytes(input);

        // Assert
        Assert.Equal(6, size);
    }

    [Fact]
    public void Compress_WithJsonPayload_CompressesEffectively()
    {
        // Arrange - Simulate a typical audit log JSON payload
        var jsonPayload = @"{
            ""userId"": 12345,
            ""action"": ""UPDATE"",
            ""entityType"": ""User"",
            ""entityId"": 67890,
            ""changes"": {
                ""firstName"": { ""old"": ""John"", ""new"": ""Jonathan"" },
                ""lastName"": { ""old"": ""Doe"", ""new"": ""Smith"" },
                ""email"": { ""old"": ""john.doe@example.com"", ""new"": ""jonathan.smith@example.com"" }
            },
            ""timestamp"": ""2024-01-15T10:30:00Z"",
            ""ipAddress"": ""192.168.1.100"",
            ""userAgent"": ""Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36""
        }";

        // Act
        var compressed = _compressionService.Compress(jsonPayload);
        var decompressed = _compressionService.Decompress(compressed);

        // Assert
        Assert.Equal(jsonPayload, decompressed);
        
        var originalSize = _compressionService.GetSizeInBytes(jsonPayload);
        var compressedSize = _compressionService.GetSizeInBytes(compressed);
        
        // JSON typically compresses well due to repetitive structure
        Assert.True(compressedSize < originalSize);
    }

    [Fact]
    public void Compress_WithStackTrace_CompressesEffectively()
    {
        // Arrange - Simulate a typical stack trace
        var stackTrace = @"System.InvalidOperationException: Operation is not valid due to the current state of the object.
   at ThinkOnErp.Application.Features.Users.Commands.UpdateUser.UpdateUserCommandHandler.Handle(UpdateUserCommand request, CancellationToken cancellationToken) in C:\Projects\ThinkOnErp\src\ThinkOnErp.Application\Features\Users\Commands\UpdateUser\UpdateUserCommandHandler.cs:line 45
   at MediatR.Pipeline.RequestExceptionProcessorBehavior`2.Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate`1 next) in C:\Projects\MediatR\src\MediatR\Pipeline\RequestExceptionProcessorBehavior.cs:line 32
   at MediatR.Pipeline.RequestExceptionActionProcessorBehavior`2.Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate`1 next) in C:\Projects\MediatR\src\MediatR\Pipeline\RequestExceptionActionProcessorBehavior.cs:line 32";

        // Act
        var compressed = _compressionService.Compress(stackTrace);
        var decompressed = _compressionService.Decompress(compressed);

        // Assert
        Assert.Equal(stackTrace, decompressed);
        
        var originalSize = _compressionService.GetSizeInBytes(stackTrace);
        var compressedSize = _compressionService.GetSizeInBytes(compressed);
        
        // Stack traces compress well due to repetitive paths
        Assert.True(compressedSize < originalSize);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void Compress_WithVariousSizes_WorksCorrectly(int size)
    {
        // Arrange
        var input = new string('X', size);

        // Act
        var compressed = _compressionService.Compress(input);
        var decompressed = _compressionService.Decompress(compressed);

        // Assert
        Assert.Equal(input, decompressed);
        Assert.NotNull(compressed);
    }

    private bool IsValidBase64(string input)
    {
        try
        {
            Convert.FromBase64String(input);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
