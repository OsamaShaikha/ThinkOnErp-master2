using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for TicketConfigRepository using EF Core InMemory provider.
/// Tests all CRUD operations, configuration management, and exception scenarios.
/// </summary>
public class TicketConfigRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly TicketConfigRepository _repository;
    private readonly Mock<ILogger<TicketConfigRepository>> _loggerMock;

    public TicketConfigRepositoryTests()
    {
        // Create InMemory database with unique name for each test instance
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_TicketConfig_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<TicketConfigRepository>>();
        _repository = new TicketConfigRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllConfigurations_OrderedByKey()
    {
        // Arrange
        var configs = new List<SysTicketConfig>
        {
            new SysTicketConfig
            {
                ConfigKey = "MaxAttachmentSize",
                ConfigValue = "10485760",
                ConfigType = "FileAttachment",
                Description = "Maximum attachment size in bytes",
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketConfig
            {
                ConfigKey = "DefaultSlaHours",
                ConfigValue = "24",
                ConfigType = "SLA",
                Description = "Default SLA hours",
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketConfig
            {
                ConfigKey = "AllowedFileTypes",
                ConfigValue = "pdf,jpg,png,doc,docx",
                ConfigType = "FileAttachment",
                Description = "Allowed file types",
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.TicketConfigs.AddRangeAsync(configs);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("AllowedFileTypes", result[0].ConfigKey); // Ordered alphabetically
        Assert.Equal("DefaultSlaHours", result[1].ConfigKey);
        Assert.Equal("MaxAttachmentSize", result[2].ConfigKey);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoConfigurations()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region GetByKeyAsync Tests

    [Fact]
    public async Task GetByKeyAsync_ReturnsConfiguration_WhenExists()
    {
        // Arrange
        var config = new SysTicketConfig
        {
            ConfigKey = "MaxAttachmentSize",
            ConfigValue = "10485760",
            ConfigType = "FileAttachment",
            Description = "Maximum attachment size in bytes",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketConfigs.AddAsync(config);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByKeyAsync("MaxAttachmentSize");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MaxAttachmentSize", result.ConfigKey);
        Assert.Equal("10485760", result.ConfigValue);
    }

    [Fact]
    public async Task GetByKeyAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByKeyAsync("NonExistentKey");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByKeyAsync_IsCaseInsensitive()
    {
        // Arrange
        var config = new SysTicketConfig
        {
            ConfigKey = "MaxAttachmentSize",
            ConfigValue = "10485760",
            ConfigType = "FileAttachment",
            Description = "Maximum attachment size in bytes",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketConfigs.AddAsync(config);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByKeyAsync("maxattachmentsize");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MaxAttachmentSize", result.ConfigKey);
    }

    #endregion

    #region GetByTypeAsync Tests

    [Fact]
    public async Task GetByTypeAsync_ReturnsConfigurationsOfSpecificType()
    {
        // Arrange
        var configs = new List<SysTicketConfig>
        {
            new SysTicketConfig
            {
                ConfigKey = "MaxAttachmentSize",
                ConfigValue = "10485760",
                ConfigType = "FileAttachment",
                Description = "Maximum attachment size",
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketConfig
            {
                ConfigKey = "AllowedFileTypes",
                ConfigValue = "pdf,jpg,png",
                ConfigType = "FileAttachment",
                Description = "Allowed file types",
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketConfig
            {
                ConfigKey = "DefaultSlaHours",
                ConfigValue = "24",
                ConfigType = "SLA",
                Description = "Default SLA hours",
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.TicketConfigs.AddRangeAsync(configs);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTypeAsync("FileAttachment");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.Equal("FileAttachment", c.ConfigType));
    }

    [Fact]
    public async Task GetByTypeAsync_ReturnsEmptyList_WhenNoMatchingType()
    {
        // Arrange
        var config = new SysTicketConfig
        {
            ConfigKey = "TestKey",
            ConfigValue = "TestValue",
            ConfigType = "SLA",
            Description = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketConfigs.AddAsync(config);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTypeAsync("Notification");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CreatesNewConfiguration_ReturnsGeneratedId()
    {
        // Arrange
        var config = new SysTicketConfig
        {
            ConfigKey = "NewConfig",
            ConfigValue = "NewValue",
            ConfigType = "General",
            Description = "New configuration",
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(config);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, config.RowId);
        Assert.NotNull(config.CreationDate);
        Assert.True(config.IsActive);

        // Verify in database
        var savedConfig = await _context.TicketConfigs.FindAsync(result);
        Assert.NotNull(savedConfig);
        Assert.Equal("NewConfig", savedConfig.ConfigKey);
    }

    [Fact]
    public async Task CreateAsync_SetsCreationDate_WhenNotProvided()
    {
        // Arrange
        var config = new SysTicketConfig
        {
            ConfigKey = "TestKey",
            ConfigValue = "TestValue",
            ConfigType = "General",
            Description = "Test",
            CreationUser = "test"
        };

        // Act
        await _repository.CreateAsync(config);

        // Assert
        Assert.NotNull(config.CreationDate);
        Assert.True(config.CreationDate.Value <= DateTime.Now);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesExistingConfiguration_ReturnsRowId()
    {
        // Arrange
        var config = new SysTicketConfig
        {
            ConfigKey = "TestKey",
            ConfigValue = "OriginalValue",
            ConfigType = "General",
            Description = "Original description",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketConfigs.AddAsync(config);
        await _context.SaveChangesAsync();

        // Modify the config
        config.ConfigValue = "UpdatedValue";
        config.Description = "Updated description";
        config.UpdateUser = "update_user";

        // Act
        var result = await _repository.UpdateAsync(config);

        // Assert
        Assert.Equal(config.RowId, result);
        Assert.NotNull(config.UpdateDate);

        // Verify in database
        var updatedConfig = await _context.TicketConfigs.FindAsync(config.RowId);
        Assert.NotNull(updatedConfig);
        Assert.Equal("UpdatedValue", updatedConfig.ConfigValue);
    }

    #endregion

    #region UpdateByKeyAsync Tests

    [Fact]
    public async Task UpdateByKeyAsync_UpdatesConfigurationValue_ReturnsTrue()
    {
        // Arrange
        var config = new SysTicketConfig
        {
            ConfigKey = "MaxAttachmentSize",
            ConfigValue = "10485760",
            ConfigType = "FileAttachment",
            Description = "Maximum attachment size",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketConfigs.AddAsync(config);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.UpdateByKeyAsync("MaxAttachmentSize", "20971520", "admin");

        // Assert
        Assert.True(result);

        // Verify in database
        var updatedConfig = await _repository.GetByKeyAsync("MaxAttachmentSize");
        Assert.NotNull(updatedConfig);
        Assert.Equal("20971520", updatedConfig.ConfigValue);
        Assert.Equal("admin", updatedConfig.UpdateUser);
    }

    [Fact]
    public async Task UpdateByKeyAsync_ReturnsFalse_WhenKeyNotExists()
    {
        // Act
        var result = await _repository.UpdateByKeyAsync("NonExistentKey", "NewValue", "admin");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesConfiguration_ReturnsTrue()
    {
        // Arrange
        var config = new SysTicketConfig
        {
            ConfigKey = "ToDelete",
            ConfigValue = "Value",
            ConfigType = "General",
            Description = "To be deleted",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketConfigs.AddAsync(config);
        await _context.SaveChangesAsync();
        var configId = config.RowId;

        // Act
        var result = await _repository.DeleteAsync(configId, "delete_user");

        // Assert
        Assert.True(result);

        // Verify config is soft deleted
        var deletedConfig = await _context.TicketConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.RowId == configId);
        Assert.NotNull(deletedConfig);
        Assert.False(deletedConfig.IsActive);
        Assert.Equal("delete_user", deletedConfig.UpdateUser);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotExists()
    {
        // Act
        var result = await _repository.DeleteAsync(999, "user");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Configuration Type Tests

    [Fact]
    public async Task GetByTypeAsync_SupportsSlaType()
    {
        // Arrange
        var config = new SysTicketConfig
        {
            ConfigKey = "DefaultSlaHours",
            ConfigValue = "24",
            ConfigType = "SLA",
            Description = "Default SLA hours",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketConfigs.AddAsync(config);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTypeAsync("SLA");

        // Assert
        Assert.Single(result);
        Assert.Equal("DefaultSlaHours", result[0].ConfigKey);
    }

    [Fact]
    public async Task GetByTypeAsync_SupportsNotificationType()
    {
        // Arrange
        var config = new SysTicketConfig
        {
            ConfigKey = "EmailNotificationEnabled",
            ConfigValue = "true",
            ConfigType = "Notification",
            Description = "Enable email notifications",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketConfigs.AddAsync(config);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTypeAsync("Notification");

        // Assert
        Assert.Single(result);
        Assert.Equal("EmailNotificationEnabled", result[0].ConfigKey);
    }

    [Fact]
    public async Task GetByTypeAsync_SupportsWorkflowType()
    {
        // Arrange
        var config = new SysTicketConfig
        {
            ConfigKey = "AutoAssignEnabled",
            ConfigValue = "true",
            ConfigType = "Workflow",
            Description = "Enable auto-assignment",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketConfigs.AddAsync(config);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTypeAsync("Workflow");

        // Assert
        Assert.Single(result);
        Assert.Equal("AutoAssignEnabled", result[0].ConfigKey);
    }

    #endregion

    #region Null Handling Tests

    [Fact]
    public async Task CreateAsync_HandlesNullDescription()
    {
        // Arrange
        var config = new SysTicketConfig
        {
            ConfigKey = "TestKey",
            ConfigValue = "TestValue",
            ConfigType = "General",
            Description = null,
            CreationUser = "test"
        };

        // Act
        var result = await _repository.CreateAsync(config);

        // Assert
        Assert.True(result > 0);
        var savedConfig = await _context.TicketConfigs.FindAsync(result);
        Assert.NotNull(savedConfig);
        Assert.Null(savedConfig.Description);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketConfigRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketConfigRepository(_context, null!));
    }

    #endregion
}
