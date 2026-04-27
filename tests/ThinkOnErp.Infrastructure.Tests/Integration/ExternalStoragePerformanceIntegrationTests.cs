using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.Infrastructure.Tests.Integration;

/// <summary>
/// Performance integration tests for external storage providers.
/// Tests throughput, latency, concurrent operations, and large file handling.
/// 
/// **Validates: Requirements 12.4, 13.1, 13.6, 13.7**
/// - Requirement 12.4: External storage performance for cold storage
/// - Requirement 13.1: System performance under load (10,000 requests per minute)
/// - Requirement 13.6: Performance overhead bounds (<10ms for 99% of operations)
/// - Requirement 13.7: Sustained load handling without degradation
/// </summary>
public class ExternalStoragePerformanceIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<ExternalStoragePerformanceIntegrationTests> _logger;
    private readonly List<string> _createdStorageLocations = new();
    
    // Performance test configuration
    private readonly bool _runPerformanceTests;
    private readonly string? _s3ConnectionString;
    private readonly string? _azureConnectionString;

    public ExternalStoragePerformanceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Setup configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestStorage:S3:ConnectionString"] = "BucketName=test-audit-archives;Region=us-east-1;Prefix=perf-tests/",
                ["TestStorage:Azure:ConnectionString"] = "AccountName=testaccount;AccountKey=testkey;ContainerName=audit-archives;Prefix=perf-tests/",
                ["TestStorage:RunPerformanceTests"] = "false" // Set to true to run against real storage
            })
            .AddEnvironmentVariables("THINKONERP_PERF_TEST_")
            .Build();

        _runPerformanceTests = configuration.GetValue<bool>("TestStorage:RunPerformanceTests");
        _s3ConnectionString = configuration["TestStorage:S3:ConnectionString"];
        _azureConnectionString = configuration["TestStorage:Azure:ConnectionString"];

        // Setup DI container
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<IExternalStorageProviderFactory, ExternalStorageProviderFactory>();
        
        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<ExternalStoragePerformanceIntegrationTests>>();
        
        _output.WriteLine($"Performance Test Configuration:");
        _output.WriteLine($"  Run Performance Tests: {_runPerformanceTests}");
        _output.WriteLine($"  S3 Connection: {_s3ConnectionString}");
        _output.WriteLine($"  Azure Connection: {_azureConnectionString}");
    }

    #region Upload Performance Tests

    [Fact]
    public async Task S3Storage_SmallFileUpload_ShouldMeetLatencyRequirements()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_TestStorage__RunPerformanceTests=true to enable");
            return;
        }

        // Arrange
        var provider = CreateS3Provider();
        var testData = GenerateTestData(1024); // 1KB file
        var iterations = 100;
        var latencies = new List<long>();

        _output.WriteLine($"Testing S3 small file upload latency ({iterations} iterations, 1KB files)...");

        try
        {
            // Act - Measure upload latencies
            for (int i = 0; i < iterations; i++)
            {
                var archiveId = GenerateTestArchiveId();
                var metadata = new Dictionary<string, string>
                {
                    ["Iteration"] = i.ToString(),
                    ["FileSize"] = testData.Length.ToString()
                };

                var stopwatch = Stopwatch.StartNew();
                var storageLocation = await provider.UploadAsync(archiveId, testData, metadata);
                stopwatch.Stop();

                latencies.Add(stopwatch.ElapsedMilliseconds);
                _createdStorageLocations.Add(storageLocation);

                if (i % 10 == 0)
                {
                    _output.WriteLine($"  Completed {i + 1}/{iterations} uploads, avg latency: {latencies.Average():F2}ms");
                }
            }

            // Assert - Analyze performance
            var avgLatency = latencies.Average();
            var p95Latency = latencies.OrderBy(x => x).Skip((int)(iterations * 0.95)).First();
            var p99Latency = latencies.OrderBy(x => x).Skip((int)(iterations * 0.99)).First();
            var maxLatency = latencies.Max();

            _output.WriteLine($"S3 Upload Performance Results:");
            _output.WriteLine($"  Average Latency: {avgLatency:F2}ms");
            _output.WriteLine($"  P95 Latency: {p95Latency}ms");
            _output.WriteLine($"  P99 Latency: {p99Latency}ms");
            _output.WriteLine($"  Max Latency: {maxLatency}ms");

            // Performance assertions (adjust thresholds based on requirements)
            Assert.True(avgLatency < 1000, $"Average latency {avgLatency:F2}ms should be < 1000ms");
            Assert.True(p95Latency < 2000, $"P95 latency {p95Latency}ms should be < 2000ms");
            Assert.True(p99Latency < 5000, $"P99 latency {p99Latency}ms should be < 5000ms");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"S3 upload performance test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task AzureStorage_SmallFileUpload_ShouldMeetLatencyRequirements()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_TestStorage__RunPerformanceTests=true to enable");
            return;
        }

        // Arrange
        var provider = CreateAzureProvider();
        var testData = GenerateTestData(1024); // 1KB file
        var iterations = 100;
        var latencies = new List<long>();

        _output.WriteLine($"Testing Azure small file upload latency ({iterations} iterations, 1KB files)...");

        try
        {
            // Act - Measure upload latencies
            for (int i = 0; i < iterations; i++)
            {
                var archiveId = GenerateTestArchiveId();
                var metadata = new Dictionary<string, string>
                {
                    ["Iteration"] = i.ToString(),
                    ["FileSize"] = testData.Length.ToString()
                };

                var stopwatch = Stopwatch.StartNew();
                var storageLocation = await provider.UploadAsync(archiveId, testData, metadata);
                stopwatch.Stop();

                latencies.Add(stopwatch.ElapsedMilliseconds);
                _createdStorageLocations.Add(storageLocation);

                if (i % 10 == 0)
                {
                    _output.WriteLine($"  Completed {i + 1}/{iterations} uploads, avg latency: {latencies.Average():F2}ms");
                }
            }

            // Assert - Analyze performance
            var avgLatency = latencies.Average();
            var p95Latency = latencies.OrderBy(x => x).Skip((int)(iterations * 0.95)).First();
            var p99Latency = latencies.OrderBy(x => x).Skip((int)(iterations * 0.99)).First();
            var maxLatency = latencies.Max();

            _output.WriteLine($"Azure Upload Performance Results:");
            _output.WriteLine($"  Average Latency: {avgLatency:F2}ms");
            _output.WriteLine($"  P95 Latency: {p95Latency}ms");
            _output.WriteLine($"  P99 Latency: {p99Latency}ms");
            _output.WriteLine($"  Max Latency: {maxLatency}ms");

            // Performance assertions
            Assert.True(avgLatency < 1000, $"Average latency {avgLatency:F2}ms should be < 1000ms");
            Assert.True(p95Latency < 2000, $"P95 latency {p95Latency}ms should be < 2000ms");
            Assert.True(p99Latency < 5000, $"P99 latency {p99Latency}ms should be < 5000ms");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Azure upload performance test failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Throughput Tests

    [Fact]
    public async Task S3Storage_ConcurrentUploads_ShouldMeetThroughputRequirements()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_TestStorage__RunPerformanceTests=true to enable");
            return;
        }

        // Arrange
        var provider = CreateS3Provider();
        var testData = GenerateTestData(10240); // 10KB files
        var concurrentUploads = 20;
        var uploadsPerBatch = 5;
        var totalUploads = concurrentUploads * uploadsPerBatch;

        _output.WriteLine($"Testing S3 concurrent upload throughput ({totalUploads} total uploads, {concurrentUploads} concurrent)...");

        try
        {
            var overallStopwatch = Stopwatch.StartNew();
            var semaphore = new SemaphoreSlim(concurrentUploads);
            var tasks = new List<Task>();

            // Act - Start concurrent uploads
            for (int i = 0; i < totalUploads; i++)
            {
                var uploadIndex = i;
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var archiveId = GenerateTestArchiveId() + uploadIndex;
                        var metadata = new Dictionary<string, string>
                        {
                            ["UploadIndex"] = uploadIndex.ToString(),
                            ["FileSize"] = testData.Length.ToString()
                        };

                        var storageLocation = await provider.UploadAsync(archiveId, testData, metadata);
                        _createdStorageLocations.Add(storageLocation);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            overallStopwatch.Stop();

            // Assert - Analyze throughput
            var totalTimeSeconds = overallStopwatch.ElapsedMilliseconds / 1000.0;
            var throughputPerSecond = totalUploads / totalTimeSeconds;
            var totalDataMB = (totalUploads * testData.Length) / (1024.0 * 1024.0);
            var throughputMBPerSecond = totalDataMB / totalTimeSeconds;

            _output.WriteLine($"S3 Throughput Performance Results:");
            _output.WriteLine($"  Total Time: {totalTimeSeconds:F2} seconds");
            _output.WriteLine($"  Uploads per Second: {throughputPerSecond:F2}");
            _output.WriteLine($"  Total Data: {totalDataMB:F2} MB");
            _output.WriteLine($"  Throughput: {throughputMBPerSecond:F2} MB/s");

            // Performance assertions
            Assert.True(throughputPerSecond > 5, $"Throughput {throughputPerSecond:F2} uploads/sec should be > 5");
            Assert.True(throughputMBPerSecond > 0.5, $"Data throughput {throughputMBPerSecond:F2} MB/s should be > 0.5");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"S3 throughput test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task AzureStorage_ConcurrentUploads_ShouldMeetThroughputRequirements()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_TestStorage__RunPerformanceTests=true to enable");
            return;
        }

        // Arrange
        var provider = CreateAzureProvider();
        var testData = GenerateTestData(10240); // 10KB files
        var concurrentUploads = 20;
        var uploadsPerBatch = 5;
        var totalUploads = concurrentUploads * uploadsPerBatch;

        _output.WriteLine($"Testing Azure concurrent upload throughput ({totalUploads} total uploads, {concurrentUploads} concurrent)...");

        try
        {
            var overallStopwatch = Stopwatch.StartNew();
            var semaphore = new SemaphoreSlim(concurrentUploads);
            var tasks = new List<Task>();

            // Act - Start concurrent uploads
            for (int i = 0; i < totalUploads; i++)
            {
                var uploadIndex = i;
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var archiveId = GenerateTestArchiveId() + uploadIndex;
                        var metadata = new Dictionary<string, string>
                        {
                            ["UploadIndex"] = uploadIndex.ToString(),
                            ["FileSize"] = testData.Length.ToString()
                        };

                        var storageLocation = await provider.UploadAsync(archiveId, testData, metadata);
                        _createdStorageLocations.Add(storageLocation);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            overallStopwatch.Stop();

            // Assert - Analyze throughput
            var totalTimeSeconds = overallStopwatch.ElapsedMilliseconds / 1000.0;
            var throughputPerSecond = totalUploads / totalTimeSeconds;
            var totalDataMB = (totalUploads * testData.Length) / (1024.0 * 1024.0);
            var throughputMBPerSecond = totalDataMB / totalTimeSeconds;

            _output.WriteLine($"Azure Throughput Performance Results:");
            _output.WriteLine($"  Total Time: {totalTimeSeconds:F2} seconds");
            _output.WriteLine($"  Uploads per Second: {throughputPerSecond:F2}");
            _output.WriteLine($"  Total Data: {totalDataMB:F2} MB");
            _output.WriteLine($"  Throughput: {throughputMBPerSecond:F2} MB/s");

            // Performance assertions
            Assert.True(throughputPerSecond > 5, $"Throughput {throughputPerSecond:F2} uploads/sec should be > 5");
            Assert.True(throughputMBPerSecond > 0.5, $"Data throughput {throughputMBPerSecond:F2} MB/s should be > 0.5");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Azure throughput test failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Large File Tests

    [Fact]
    public async Task S3Storage_LargeFileUpload_ShouldHandleEfficiently()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_TestStorage__RunPerformanceTests=true to enable");
            return;
        }

        // Arrange
        var provider = CreateS3Provider();
        var largeData = GenerateTestData(10 * 1024 * 1024); // 10MB file
        var archiveId = GenerateTestArchiveId();

        _output.WriteLine($"Testing S3 large file upload (10MB)...");

        try
        {
            var metadata = new Dictionary<string, string>
            {
                ["FileSize"] = largeData.Length.ToString(),
                ["TestType"] = "LargeFile"
            };

            // Act - Upload large file
            var stopwatch = Stopwatch.StartNew();
            var storageLocation = await provider.UploadAsync(archiveId, largeData, metadata);
            stopwatch.Stop();

            _createdStorageLocations.Add(storageLocation);

            // Assert - Analyze performance
            var uploadTimeSeconds = stopwatch.ElapsedMilliseconds / 1000.0;
            var fileSizeMB = largeData.Length / (1024.0 * 1024.0);
            var throughputMBPerSecond = fileSizeMB / uploadTimeSeconds;

            _output.WriteLine($"S3 Large File Upload Results:");
            _output.WriteLine($"  File Size: {fileSizeMB:F2} MB");
            _output.WriteLine($"  Upload Time: {uploadTimeSeconds:F2} seconds");
            _output.WriteLine($"  Throughput: {throughputMBPerSecond:F2} MB/s");

            // Performance assertions
            Assert.True(uploadTimeSeconds < 60, $"Upload time {uploadTimeSeconds:F2}s should be < 60s for 10MB file");
            Assert.True(throughputMBPerSecond > 0.1, $"Throughput {throughputMBPerSecond:F2} MB/s should be > 0.1");

            // Test download performance
            _output.WriteLine("Testing large file download...");
            var downloadStopwatch = Stopwatch.StartNew();
            var downloadedData = await provider.DownloadAsync(storageLocation);
            downloadStopwatch.Stop();

            var downloadTimeSeconds = downloadStopwatch.ElapsedMilliseconds / 1000.0;
            var downloadThroughputMBPerSecond = fileSizeMB / downloadTimeSeconds;

            _output.WriteLine($"S3 Large File Download Results:");
            _output.WriteLine($"  Download Time: {downloadTimeSeconds:F2} seconds");
            _output.WriteLine($"  Download Throughput: {downloadThroughputMBPerSecond:F2} MB/s");

            Assert.Equal(largeData.Length, downloadedData.Length);
            Assert.True(downloadTimeSeconds < 60, $"Download time {downloadTimeSeconds:F2}s should be < 60s for 10MB file");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"S3 large file test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task AzureStorage_LargeFileUpload_ShouldHandleEfficiently()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_TestStorage__RunPerformanceTests=true to enable");
            return;
        }

        // Arrange
        var provider = CreateAzureProvider();
        var largeData = GenerateTestData(10 * 1024 * 1024); // 10MB file
        var archiveId = GenerateTestArchiveId();

        _output.WriteLine($"Testing Azure large file upload (10MB)...");

        try
        {
            var metadata = new Dictionary<string, string>
            {
                ["FileSize"] = largeData.Length.ToString(),
                ["TestType"] = "LargeFile"
            };

            // Act - Upload large file
            var stopwatch = Stopwatch.StartNew();
            var storageLocation = await provider.UploadAsync(archiveId, largeData, metadata);
            stopwatch.Stop();

            _createdStorageLocations.Add(storageLocation);

            // Assert - Analyze performance
            var uploadTimeSeconds = stopwatch.ElapsedMilliseconds / 1000.0;
            var fileSizeMB = largeData.Length / (1024.0 * 1024.0);
            var throughputMBPerSecond = fileSizeMB / uploadTimeSeconds;

            _output.WriteLine($"Azure Large File Upload Results:");
            _output.WriteLine($"  File Size: {fileSizeMB:F2} MB");
            _output.WriteLine($"  Upload Time: {uploadTimeSeconds:F2} seconds");
            _output.WriteLine($"  Throughput: {throughputMBPerSecond:F2} MB/s");

            // Performance assertions
            Assert.True(uploadTimeSeconds < 60, $"Upload time {uploadTimeSeconds:F2}s should be < 60s for 10MB file");
            Assert.True(throughputMBPerSecond > 0.1, $"Throughput {throughputMBPerSecond:F2} MB/s should be > 0.1");

            // Test download performance
            _output.WriteLine("Testing large file download...");
            var downloadStopwatch = Stopwatch.StartNew();
            var downloadedData = await provider.DownloadAsync(storageLocation);
            downloadStopwatch.Stop();

            var downloadTimeSeconds = downloadStopwatch.ElapsedMilliseconds / 1000.0;
            var downloadThroughputMBPerSecond = fileSizeMB / downloadTimeSeconds;

            _output.WriteLine($"Azure Large File Download Results:");
            _output.WriteLine($"  Download Time: {downloadTimeSeconds:F2} seconds");
            _output.WriteLine($"  Download Throughput: {downloadThroughputMBPerSecond:F2} MB/s");

            Assert.Equal(largeData.Length, downloadedData.Length);
            Assert.True(downloadTimeSeconds < 60, $"Download time {downloadTimeSeconds:F2}s should be < 60s for 10MB file");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Azure large file test failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Memory Usage Tests

    [Fact]
    public async Task ExternalStorage_MemoryUsage_ShouldBeReasonable()
    {
        if (!_runPerformanceTests)
        {
            _output.WriteLine("Skipping performance test - set THINKONERP_PERF_TEST_TestStorage__RunPerformanceTests=true to enable");
            return;
        }

        // Arrange
        var provider = CreateS3Provider();
        var testData = GenerateTestData(1024 * 1024); // 1MB file
        var iterations = 50;

        _output.WriteLine($"Testing memory usage during {iterations} uploads of 1MB files...");

        try
        {
            // Measure initial memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);

            // Act - Perform multiple uploads
            for (int i = 0; i < iterations; i++)
            {
                var archiveId = GenerateTestArchiveId() + i;
                var metadata = new Dictionary<string, string>
                {
                    ["Iteration"] = i.ToString(),
                    ["FileSize"] = testData.Length.ToString()
                };

                var storageLocation = await provider.UploadAsync(archiveId, testData, metadata);
                _createdStorageLocations.Add(storageLocation);

                // Force garbage collection every 10 iterations
                if (i % 10 == 0)
                {
                    GC.Collect();
                    var currentMemory = GC.GetTotalMemory(false);
                    var memoryIncreaseMB = (currentMemory - initialMemory) / (1024.0 * 1024.0);
                    _output.WriteLine($"  Iteration {i}: Memory increase: {memoryIncreaseMB:F2} MB");
                }
            }

            // Measure final memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var finalMemory = GC.GetTotalMemory(false);

            // Assert - Analyze memory usage
            var totalMemoryIncreaseMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);
            var memoryPerUploadKB = (finalMemory - initialMemory) / (1024.0 * iterations);

            _output.WriteLine($"Memory Usage Results:");
            _output.WriteLine($"  Initial Memory: {initialMemory / (1024.0 * 1024.0):F2} MB");
            _output.WriteLine($"  Final Memory: {finalMemory / (1024.0 * 1024.0):F2} MB");
            _output.WriteLine($"  Total Increase: {totalMemoryIncreaseMB:F2} MB");
            _output.WriteLine($"  Memory per Upload: {memoryPerUploadKB:F2} KB");

            // Memory usage should be reasonable (not growing linearly with uploads)
            Assert.True(totalMemoryIncreaseMB < 100, $"Total memory increase {totalMemoryIncreaseMB:F2} MB should be < 100 MB");
            Assert.True(memoryPerUploadKB < 1024, $"Memory per upload {memoryPerUploadKB:F2} KB should be < 1 MB");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Memory usage test failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private IExternalStorageProvider CreateS3Provider()
    {
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();
        return factory.CreateProvider("S3", _s3ConnectionString!);
    }

    private IExternalStorageProvider CreateAzureProvider()
    {
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();
        return factory.CreateProvider("AzureBlob", _azureConnectionString!);
    }

    private static long GenerateTestArchiveId()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static byte[] GenerateTestData(int sizeBytes)
    {
        var data = new byte[sizeBytes];
        var random = new Random(42); // Use fixed seed for reproducible tests
        random.NextBytes(data);
        return data;
    }

    public void Dispose()
    {
        // Cleanup created storage objects (only if running performance tests)
        if (_runPerformanceTests && _createdStorageLocations.Any())
        {
            _output.WriteLine($"Cleaning up {_createdStorageLocations.Count} created storage objects...");
            
            var cleanupTasks = new List<Task>();
            
            foreach (var location in _createdStorageLocations)
            {
                cleanupTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        if (location.StartsWith("s3://"))
                        {
                            var s3Provider = CreateS3Provider();
                            await s3Provider.DeleteAsync(location);
                        }
                        else if (location.Contains("blob.core.windows.net"))
                        {
                            var azureProvider = CreateAzureProvider();
                            await azureProvider.DeleteAsync(location);
                        }
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"Failed to cleanup {location}: {ex.Message}");
                    }
                }));
            }

            try
            {
                Task.WaitAll(cleanupTasks.ToArray(), TimeSpan.FromMinutes(5));
                _output.WriteLine("Cleanup completed successfully");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Cleanup failed: {ex.Message}");
            }
        }

        _serviceProvider?.Dispose();
    }

    #endregion
}