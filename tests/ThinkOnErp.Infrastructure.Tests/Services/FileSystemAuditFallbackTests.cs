using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for FileSystemAuditFallback service.
/// Validates Task 16.3: FileSystemAuditFallback for database outages
/// Validates Task 16.4: Fallback event replay mechanism
/// </summary>
public class FileSystemAuditFallbackTests : IDisposable
{
    private readonly Mock<ILogger<FileSystemAuditFallback>> _mockLogger;
    private readonly string _testFallbackPath;
    private readonly FileSystemAuditFallback _fallbackService;

    public FileSystemAuditFallbackTests()
    {
        _mockLogger = new Mock<ILogger<FileSystemAuditFallback>>();
        _testFallbackPath = Path.Combine(Path.GetTempPath(), $"audit_fallback_test_{Guid.NewGuid():N}");
        
        var options = new FileSystemAuditFallbackOptions
        {
            FallbackPath = _testFallbackPath,
            MaxTotalSizeBytes = 1024 * 1024, // 1 MB for testing
            MaxReplayAttempts = 3
        };

        _fallbackService = new FileSystemAuditFallback(_mockLogger.Object, options);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testFallbackPath))
        {
            Directory.Delete(_testFallbackPath, true);
        }
    }

    [Fact]
    public async Task WriteAsync_ShouldCreateFallbackFile_WithStructuredJson()
    {
        // Arrange
        var auditEvent = new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 123,
            CompanyId = 1,
            BranchId = 10,
            Action = "UPDATE",
            EntityType = "SysUser",
            EntityId = 456,
            OldValue = "{\"name\":\"Old Name\"}",
            NewValue = "{\"name\":\"New Name\"}",
            IpAddress = "192.168.1.1",
            UserAgent = "Test Agent"
        };

        // Act
        await _fallbackService.WriteAsync(auditEvent);

        // Assert
        var files = Directory.GetFiles(_testFallbackPath, "audit_fallback_*.json");
        Assert.Single(files);

        var fileContent = await File.ReadAllTextAsync(files[0]);
        Assert.Contains("\"eventType\":", fileContent);
        Assert.Contains("\"correlationId\":", fileContent);
        Assert.Contains(auditEvent.CorrelationId, fileContent);
        Assert.Contains("\"action\": \"UPDATE\"", fileContent);
    }

    [Fact]
    public async Task WriteAsync_ShouldCreateDirectory_IfNotExists()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}");
        var options = new FileSystemAuditFallbackOptions { FallbackPath = nonExistentPath };
        var service = new FileSystemAuditFallback(_mockLogger.Object, options);

        var auditEvent = new AuthenticationAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 1,
            Action = "LOGIN",
            EntityType = "Authentication",
            Success = true
        };

        try
        {
            // Act
            await service.WriteAsync(auditEvent);

            // Assert
            Assert.True(Directory.Exists(nonExistentPath));
            var files = Directory.GetFiles(nonExistentPath, "audit_fallback_*.json");
            Assert.Single(files);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(nonExistentPath))
            {
                Directory.Delete(nonExistentPath, true);
            }
        }
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldCreateSingleFile_WithMultipleEvents()
    {
        // Arrange
        var events = new List<AuditEvent>
        {
            new DataChangeAuditEvent
            {
                CorrelationId = Guid.NewGuid().ToString(),
                ActorType = "USER",
                ActorId = 1,
                Action = "INSERT",
                EntityType = "SysUser",
                EntityId = 100
            },
            new DataChangeAuditEvent
            {
                CorrelationId = Guid.NewGuid().ToString(),
                ActorType = "USER",
                ActorId = 1,
                Action = "UPDATE",
                EntityType = "SysUser",
                EntityId = 101
            },
            new DataChangeAuditEvent
            {
                CorrelationId = Guid.NewGuid().ToString(),
                ActorType = "USER",
                ActorId = 1,
                Action = "DELETE",
                EntityType = "SysUser",
                EntityId = 102
            }
        };

        // Act
        await _fallbackService.WriteBatchAsync(events);

        // Assert
        var files = Directory.GetFiles(_testFallbackPath, "audit_fallback_batch_*.json");
        Assert.Single(files);

        var fileContent = await File.ReadAllTextAsync(files[0]);
        Assert.Contains("\"eventCount\": 3", fileContent);
        Assert.Contains("INSERT", fileContent);
        Assert.Contains("UPDATE", fileContent);
        Assert.Contains("DELETE", fileContent);
    }

    [Fact]
    public async Task ReplayFallbackEventsAsync_ShouldReplayAndDeleteFiles()
    {
        // Arrange
        var event1 = new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 1,
            Action = "INSERT",
            EntityType = "SysUser",
            EntityId = 100
        };

        var event2 = new AuthenticationAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 2,
            Action = "LOGIN",
            EntityType = "Authentication",
            Success = true
        };

        await _fallbackService.WriteAsync(event1);
        await _fallbackService.WriteAsync(event2);

        var replayedEvents = new List<AuditEvent>();
        Task ReplayAction(AuditEvent e)
        {
            replayedEvents.Add(e);
            return Task.CompletedTask;
        }

        // Act
        var replayedCount = await _fallbackService.ReplayFallbackEventsAsync(ReplayAction);

        // Assert
        Assert.Equal(2, replayedCount);
        Assert.Equal(2, replayedEvents.Count);
        
        // Files should be deleted after successful replay
        var remainingFiles = Directory.GetFiles(_testFallbackPath, "audit_fallback_*.json");
        Assert.Empty(remainingFiles);
    }

    [Fact]
    public async Task ReplayFallbackEventsAsync_ShouldHandleReplayFailures()
    {
        // Arrange
        var event1 = new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 1,
            Action = "INSERT",
            EntityType = "SysUser",
            EntityId = 100
        };

        await _fallbackService.WriteAsync(event1);

        var attemptCount = 0;
        Task ReplayAction(AuditEvent e)
        {
            attemptCount++;
            throw new Exception("Simulated replay failure");
        }

        // Act
        var replayedCount = await _fallbackService.ReplayFallbackEventsAsync(ReplayAction);

        // Assert
        Assert.Equal(0, replayedCount);
        Assert.Equal(1, attemptCount);
        
        // File should still exist after failed replay
        var remainingFiles = Directory.GetFiles(_testFallbackPath, "audit_fallback_*.json");
        Assert.Single(remainingFiles);
    }

    [Fact]
    public async Task GetMetrics_ShouldReturnAccurateMetrics()
    {
        // Arrange
        var event1 = new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 1,
            Action = "INSERT",
            EntityType = "SysUser",
            EntityId = 100
        };

        var event2 = new AuthenticationAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 2,
            Action = "LOGIN",
            EntityType = "Authentication",
            Success = true
        };

        // Act
        await _fallbackService.WriteAsync(event1);
        await _fallbackService.WriteAsync(event2);

        var metrics = _fallbackService.GetMetrics();

        // Assert
        Assert.Equal(2, metrics.TotalEventsWritten);
        Assert.Equal(2, metrics.PendingFiles);
        Assert.True(metrics.TotalSizeBytes > 0);
        Assert.Equal(_testFallbackPath, metrics.FallbackPath);
    }

    [Fact]
    public async Task GetPendingFileCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var event1 = new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 1,
            Action = "INSERT",
            EntityType = "SysUser",
            EntityId = 100
        };

        // Act
        var countBefore = _fallbackService.GetPendingFileCount();
        await _fallbackService.WriteAsync(event1);
        var countAfter = _fallbackService.GetPendingFileCount();

        // Assert
        Assert.Equal(0, countBefore);
        Assert.Equal(1, countAfter);
    }

    [Fact]
    public async Task WriteAsync_ShouldHandleNullEvent()
    {
        // Act
        await _fallbackService.WriteAsync(null!);

        // Assert
        var files = Directory.GetFiles(_testFallbackPath, "audit_fallback_*.json");
        Assert.Empty(files);
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldHandleEmptyBatch()
    {
        // Act
        await _fallbackService.WriteBatchAsync(new List<AuditEvent>());

        // Assert
        var files = Directory.GetFiles(_testFallbackPath, "audit_fallback_*.json");
        Assert.Empty(files);
    }

    [Fact]
    public async Task ReplayFallbackEventsAsync_ShouldProcessBatchFiles()
    {
        // Arrange
        var events = new List<AuditEvent>
        {
            new DataChangeAuditEvent
            {
                CorrelationId = Guid.NewGuid().ToString(),
                ActorType = "USER",
                ActorId = 1,
                Action = "INSERT",
                EntityType = "SysUser",
                EntityId = 100
            },
            new DataChangeAuditEvent
            {
                CorrelationId = Guid.NewGuid().ToString(),
                ActorType = "USER",
                ActorId = 1,
                Action = "UPDATE",
                EntityType = "SysUser",
                EntityId = 101
            }
        };

        await _fallbackService.WriteBatchAsync(events);

        var replayedEvents = new List<AuditEvent>();
        Task ReplayAction(AuditEvent e)
        {
            replayedEvents.Add(e);
            return Task.CompletedTask;
        }

        // Act
        var replayedCount = await _fallbackService.ReplayFallbackEventsAsync(ReplayAction);

        // Assert
        Assert.Equal(1, replayedCount); // 1 batch file processed
        Assert.Equal(2, replayedEvents.Count); // 2 events replayed
        
        // Batch file should be deleted after successful replay
        var remainingFiles = Directory.GetFiles(_testFallbackPath, "audit_fallback_*.json");
        Assert.Empty(remainingFiles);
    }

    [Fact]
    public async Task ClearAllAsync_ShouldDeleteAllFallbackFiles()
    {
        // Arrange
        await _fallbackService.WriteAsync(new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 1,
            Action = "INSERT",
            EntityType = "SysUser",
            EntityId = 100
        });

        await _fallbackService.WriteAsync(new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 1,
            Action = "UPDATE",
            EntityType = "SysUser",
            EntityId = 101
        });

        // Act
        await _fallbackService.ClearAllAsync();

        // Assert
        var files = Directory.GetFiles(_testFallbackPath, "audit_fallback_*.json");
        Assert.Empty(files);
    }

    [Fact]
    public async Task WriteAsync_ShouldSupportDifferentAuditEventTypes()
    {
        // Arrange & Act
        await _fallbackService.WriteAsync(new DataChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 1,
            Action = "INSERT",
            EntityType = "SysUser",
            EntityId = 100
        });

        await _fallbackService.WriteAsync(new AuthenticationAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "USER",
            ActorId = 2,
            Action = "LOGIN",
            EntityType = "Authentication",
            Success = true
        });

        await _fallbackService.WriteAsync(new PermissionChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "ADMIN",
            ActorId = 3,
            Action = "GRANT",
            EntityType = "Permission",
            RoleId = 5,
            PermissionId = 10
        });

        await _fallbackService.WriteAsync(new ExceptionAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "SYSTEM",
            ActorId = 0,
            Action = "EXCEPTION",
            EntityType = "System",
            ExceptionType = "NullReferenceException",
            ExceptionMessage = "Test exception",
            StackTrace = "Test stack trace"
        });

        await _fallbackService.WriteAsync(new ConfigurationChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "ADMIN",
            ActorId = 4,
            Action = "UPDATE",
            EntityType = "Configuration",
            SettingName = "MaxConnections",
            OldValue = "100",
            NewValue = "200",
            Source = "ConfigFile"
        });

        // Assert
        var files = Directory.GetFiles(_testFallbackPath, "audit_fallback_*.json");
        Assert.Equal(5, files.Length);
    }
}
