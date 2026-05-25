using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Services;

public class DocumentStorageService : IDocumentStorageService
{
    private static string? _cachedBasePath;
    private static readonly object _cacheLock = new();
    private readonly string _basePath;
    private readonly ILogger<DocumentStorageService> _logger;

    public DocumentStorageService(IConfiguration configuration, ILogger<DocumentStorageService> logger,
        ISysSettingRepository settingRepo)
    {
        if (_cachedBasePath == null)
        {
            lock (_cacheLock)
            {
                if (_cachedBasePath == null)
                {
                    try
                    {
                        var setting = settingRepo.GetByCodeAsync(1).GetAwaiter().GetResult();
                        _cachedBasePath = setting?.SettingValue;
                    }
                    catch
                    {
                        _cachedBasePath = null;
                    }
                }
            }
        }
        _basePath = _cachedBasePath
            ?? configuration.GetValue<string>("DocumentStorage:BasePath")
            ?? "uploads";
        _logger = logger;

        if (!Path.IsPathRooted(_basePath))
        {
            _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _basePath);
        }

        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string relativePath)
    {
        if (!ValidatePath(relativePath))
            throw new DocumentStorageException(relativePath, "Invalid path - possible path traversal attack");

        var fullPath = GetFullPath(relativePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _logger.LogInformation("Saving document to {FilePath}", fullPath);

        try
        {
            await using var fileStreamOutput = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await fileStream.CopyToAsync(fileStreamOutput);
            return relativePath;
        }
        catch (Exception ex) when (ex is not DocumentStorageException)
        {
            _logger.LogError(ex, "Failed to save file to {FilePath}", fullPath);
            throw new DocumentStorageException(relativePath, "Failed to save file to disk", ex);
        }
    }

    public async Task<Stream> GetFileAsync(string relativePath)
    {
        if (!ValidatePath(relativePath))
            throw new DocumentStorageException(relativePath, "Invalid path - possible path traversal attack");

        var fullPath = GetFullPath(relativePath);

        if (!File.Exists(fullPath))
            throw new DocumentStorageException(relativePath, "File not found on disk");

        _logger.LogInformation("Reading document from {FilePath}", fullPath);

        try
        {
            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await Task.FromResult<Stream>(stream);
        }
        catch (Exception ex) when (ex is not DocumentStorageException)
        {
            _logger.LogError(ex, "Failed to read file from {FilePath}", fullPath);
            throw new DocumentStorageException(relativePath, "Failed to read file from disk", ex);
        }
    }

    public Task DeleteFileAsync(string relativePath)
    {
        if (!ValidatePath(relativePath))
            throw new DocumentStorageException(relativePath, "Invalid path - possible path traversal attack");

        var fullPath = GetFullPath(relativePath);

        if (File.Exists(fullPath))
        {
            _logger.LogInformation("Deleting document from {FilePath}", fullPath);
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetFullPath(string relativePath) =>
        Path.Combine(_basePath, relativePath);

    public bool ValidatePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        var fullPath = Path.GetFullPath(Path.Combine(_basePath, relativePath));

        // Ensure the resolved path is within the base directory (prevent path traversal)
        return fullPath.StartsWith(Path.GetFullPath(_basePath), StringComparison.OrdinalIgnoreCase);
    }
}
