using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for compressing and decompressing data using GZip algorithm.
/// Used by ArchivalService to reduce storage costs for archived audit data.
/// Supports compression of CLOB fields (OLD_VALUE, NEW_VALUE, REQUEST_PAYLOAD, RESPONSE_PAYLOAD, STACK_TRACE, METADATA).
/// </summary>
public interface ICompressionService
{
    /// <summary>
    /// Compress a string using GZip algorithm
    /// </summary>
    /// <param name="data">String data to compress</param>
    /// <returns>Base64-encoded compressed data, or null if input is null/empty</returns>
    string? Compress(string? data);
    
    /// <summary>
    /// Decompress GZip-compressed data
    /// </summary>
    /// <param name="compressedData">Base64-encoded compressed data</param>
    /// <returns>Decompressed string, or null if input is null/empty</returns>
    string? Decompress(string? compressedData);
    
    /// <summary>
    /// Calculate the compression ratio for given data
    /// </summary>
    /// <param name="originalData">Original uncompressed data</param>
    /// <param name="compressedData">Compressed data (Base64-encoded)</param>
    /// <returns>Compression ratio (compressed size / original size)</returns>
    double CalculateCompressionRatio(string? originalData, string? compressedData);
    
    /// <summary>
    /// Get the size in bytes of a string
    /// </summary>
    /// <param name="data">String data</param>
    /// <returns>Size in bytes</returns>
    long GetSizeInBytes(string? data);
}

/// <summary>
/// Implementation of compression service using GZip algorithm
/// </summary>
public class CompressionService : ICompressionService
{
    private readonly ILogger<CompressionService> _logger;

    public CompressionService(ILogger<CompressionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Compress a string using GZip algorithm and return Base64-encoded result
    /// </summary>
    public string? Compress(string? data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return data;
        }

        try
        {
            // Convert string to bytes
            var bytes = Encoding.UTF8.GetBytes(data);
            
            // Compress using GZip
            using var outputStream = new MemoryStream();
            using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
            {
                gzipStream.Write(bytes, 0, bytes.Length);
            }
            
            // Convert compressed bytes to Base64 for storage in database
            var compressedBytes = outputStream.ToArray();
            var base64Result = Convert.ToBase64String(compressedBytes);
            
            _logger.LogDebug(
                "Compressed data from {OriginalSize} bytes to {CompressedSize} bytes (ratio: {Ratio:P2})",
                bytes.Length,
                compressedBytes.Length,
                (double)compressedBytes.Length / bytes.Length);
            
            return base64Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error compressing data");
            throw;
        }
    }

    /// <summary>
    /// Decompress GZip-compressed Base64-encoded data
    /// </summary>
    public string? Decompress(string? compressedData)
    {
        if (string.IsNullOrEmpty(compressedData))
        {
            return compressedData;
        }

        try
        {
            // Convert Base64 to bytes
            var compressedBytes = Convert.FromBase64String(compressedData);
            
            // Decompress using GZip
            using var inputStream = new MemoryStream(compressedBytes);
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();
            
            gzipStream.CopyTo(outputStream);
            var decompressedBytes = outputStream.ToArray();
            
            // Convert bytes back to string
            var result = Encoding.UTF8.GetString(decompressedBytes);
            
            _logger.LogDebug(
                "Decompressed data from {CompressedSize} bytes to {DecompressedSize} bytes",
                compressedBytes.Length,
                decompressedBytes.Length);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decompressing data");
            throw;
        }
    }

    /// <summary>
    /// Calculate the compression ratio for given data
    /// </summary>
    public double CalculateCompressionRatio(string? originalData, string? compressedData)
    {
        if (string.IsNullOrEmpty(originalData) || string.IsNullOrEmpty(compressedData))
        {
            return 0;
        }

        try
        {
            var originalSize = GetSizeInBytes(originalData);
            var compressedSize = GetSizeInBytes(compressedData);
            
            return compressedSize > 0 ? (double)compressedSize / originalSize : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculating compression ratio");
            return 0;
        }
    }

    /// <summary>
    /// Get the size in bytes of a string (UTF-8 encoding)
    /// </summary>
    public long GetSizeInBytes(string? data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return 0;
        }

        return Encoding.UTF8.GetByteCount(data);
    }
}
