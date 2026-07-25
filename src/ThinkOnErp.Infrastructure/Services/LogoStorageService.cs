using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Services;

public class LogoStorageService : ILogoStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LogoStorageService> _logger;

    public LogoStorageService(ILogger<LogoStorageService> logger, string basePath)
    {
        _logger = logger;
        _basePath = basePath;

        try
        {
            Directory.CreateDirectory(_basePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create logo directory {Path}, logos will not be saved", _basePath);
        }
    }

    public async Task<string> SaveLogoAsync(byte[] logoBytes, string entityType, long entityId)
    {
        if (logoBytes == null || logoBytes.Length == 0)
            throw new ArgumentException("Logo bytes cannot be empty");

        var ext = DetectExtension(logoBytes);
        var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);
        var relativePath = $"{entityType}/{entityId}_{uniqueSuffix}{ext}";
        var fullPath = Path.Combine(_basePath, relativePath);
        var dir = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _logger.LogInformation("Saving logo to {FilePath}", fullPath);

        await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await fs.WriteAsync(logoBytes);

        return relativePath;
    }

    public async Task<byte[]?> GetLogoAsync(string logoPath)
    {
        if (string.IsNullOrEmpty(logoPath))
            return null;

        var fullPath = Path.Combine(_basePath, logoPath);
        fullPath = Path.GetFullPath(fullPath);

        if (!fullPath.StartsWith(Path.GetFullPath(_basePath), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path traversal attempt detected: {LogoPath}", logoPath);
            return null;
        }

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Logo file not found: {FullPath}", fullPath);
            return null;
        }

        _logger.LogInformation("Reading logo from {FilePath}", fullPath);
        return await File.ReadAllBytesAsync(fullPath);
    }

    public Task DeleteLogoAsync(string logoPath)
    {
        if (string.IsNullOrEmpty(logoPath))
            return Task.CompletedTask;

        var fullPath = Path.Combine(_basePath, logoPath);
        fullPath = Path.GetFullPath(fullPath);

        if (!fullPath.StartsWith(Path.GetFullPath(_basePath), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path traversal attempt detected: {LogoPath}", logoPath);
            return Task.CompletedTask;
        }

        if (File.Exists(fullPath))
        {
            _logger.LogInformation("Deleting logo file: {FullPath}", fullPath);
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private static string DetectExtension(byte[] bytes)
    {
        if (bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return ".jpg";
        if (bytes.Length > 3 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return ".png";
        if (bytes.Length > 3 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            return ".gif";
        if (bytes.Length > 3 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46)
            return ".webp";
        if (bytes.Length > 3 && bytes[0] == 0x42 && bytes[1] == 0x4D)
            return ".bmp";
        return ".png";
    }
}
