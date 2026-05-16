using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for SystemRepository using EF Core InMemory provider.
/// Tests CRUD operations, soft delete, and query operations.
/// </summary>
public class SystemRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly SystemRepository _repository;
    private readonly Mock<ILogger<SystemRepository>> _loggerMock;

    public SystemRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_System_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<SystemRepository>>();
        _repository = new SystemRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetAllSystemsAsync Tests

    [Fact]
    public async Task GetAllSystemsAsync_ReturnsAllActiveSystems_OrderedByDisplayOrder()
    {
        // Arrange
        var systems = new List<SysSystem>
        {
            new SysSystem
            {
                RowId = 1,
                SystemName = "System A",
                SystemCode = "SYS_A",
                DisplayOrder = 3,
                IsActive = true,
                CreationUser = "test"
            },
            new SysSystem
            {
                RowId = 2,
                SystemName = "System B",
                SystemCode = "SYS_B",
                DisplayOrder = 1,
                IsActive = true,
                CreationUser = "test"
            },
            new SysSystem
            {
                RowId = 3,
                SystemName = "System C",
                SystemCode = "SYS_C",
                DisplayOrder = 2,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.Systems.AddRangeAsync(systems);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllSystemsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("System B", result[0].SystemName); // DisplayOrder = 1
        Assert.Equal("System C", result[1].SystemName); // DisplayOrder = 2
        Assert.Equal("System A", result[2].SystemName); // DisplayOrder = 3
    }

    [Fact]
    public async Task GetAllSystemsAsync_ExcludesInactiveSystems()
    {
        // Arrange
        var systems = new List<SysSystem>
        {
            new SysSystem
            {
                RowId = 1,
                SystemName = "Active System",
                SystemCode = "SYS_A",
                IsActive = true,
                CreationUser = "test"
            },
            new SysSystem
            {
                RowId = 2,
                SystemName = "Inactive System",
                SystemCode = "SYS_I",
                IsActive = false,
                CreationUser = "test"
            }
        };
        await _context.Systems.AddRangeAsync(systems);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllSystemsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Active System", result[0].SystemName);
    }

    [Fact]
    public async Task GetAllSystemsAsync_ReturnsEmptyList_WhenNoSystems()
    {
        // Act
        var result = await _repository.GetAllSystemsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region GetSystemByIdAsync Tests

    [Fact]
    public async Task GetSystemByIdAsync_ReturnsSystem_WhenExists()
    {
        // Arrange
        var system = new SysSystem
        {
            RowId = 1,
            SystemName = "Test System",
            SystemCode = "TEST_SYS",
            SystemNameE = "Test System English",
            Description = "Test Description",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Systems.AddAsync(system);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetSystemByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.RowId);
        Assert.Equal("Test System", result.SystemName);
        Assert.Equal("TEST_SYS", result.SystemCode);
        Assert.Equal("Test System English", result.SystemNameE);
    }

    [Fact]
    public async Task GetSystemByIdAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetSystemByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSystemByIdAsync_ReturnsInactiveSystem()
    {
        // Arrange
        var system = new SysSystem
        {
            RowId = 1,
            SystemName = "Inactive System",
            SystemCode = "INACTIVE",
            IsActive = false,
            CreationUser = "test"
        };
        await _context.Systems.AddAsync(system);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetSystemByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsActive);
    }

    #endregion

    #region CreateSystemAsync Tests

    [Fact]
    public async Task CreateSystemAsync_CreatesNewSystem_ReturnsGeneratedId()
    {
        // Arrange
        var system = new SysSystem
        {
            SystemName = "New System",
            SystemCode = "NEW_SYS",
            SystemNameE = "New System English",
            Description = "New system description",
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateSystemAsync(system);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, system.RowId);
        Assert.NotNull(system.CreationDate);
        Assert.True(system.IsActive);

        // Verify in database
        var savedSystem = await _context.Systems.FindAsync(result);
        Assert.NotNull(savedSystem);
        Assert.Equal("New System", savedSystem.SystemName);
        Assert.Equal("NEW_SYS", savedSystem.SystemCode);
    }

    [Fact]
    public async Task CreateSystemAsync_SetsCreationDate_WhenNotProvided()
    {
        // Arrange
        var system = new SysSystem
        {
            SystemName = "Test System",
            SystemCode = "TEST",
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateSystemAsync(system);

        // Assert
        Assert.NotNull(system.CreationDate);
        Assert.True(system.CreationDate.Value <= DateTime.Now);
    }

    [Fact]
    public async Task CreateSystemAsync_SetsIsActiveToTrue_ByDefault()
    {
        // Arrange
        var system = new SysSystem
        {
            SystemName = "Test System",
            SystemCode = "TEST",
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateSystemAsync(system);

        // Assert
        Assert.True(system.IsActive);
    }

    [Fact]
    public async Task CreateSystemAsync_WithAllOptionalFields_Succeeds()
    {
        // Arrange
        var system = new SysSystem
        {
            SystemName = "Complete System",
            SystemCode = "COMPLETE",
            SystemNameE = "Complete System English",
            Description = "Full description",
            DisplayOrder = 5,
            IconClass = "fa-system",
            IsActive = true,
            CreationUser = "test_user",
            CreationDate = DateTime.Now
        };

        // Act
        var result = await _repository.CreateSystemAsync(system);

        // Assert
        Assert.True(result > 0);
        var savedSystem = await _context.Systems.FindAsync(result);
        Assert.NotNull(savedSystem);
        Assert.Equal(5, savedSystem.DisplayOrder);
        Assert.Equal("fa-system", savedSystem.IconClass);
    }

    #endregion

    #region UpdateSystemAsync Tests

    [Fact]
    public async Task UpdateSystemAsync_UpdatesExistingSystem()
    {
        // Arrange
        var system = new SysSystem
        {
            RowId = 1,
            SystemName = "Original Name",
            SystemCode = "ORIG",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Systems.AddAsync(system);
        await _context.SaveChangesAsync();

        // Modify system
        system.SystemName = "Updated Name";
        system.SystemCode = "UPDATED";
        system.Description = "Updated description";

        // Act
        await _repository.UpdateSystemAsync(system);

        // Assert
        Assert.NotNull(system.UpdateDate);

        // Verify in database
        var updatedSystem = await _context.Systems.FindAsync(1L);
        Assert.NotNull(updatedSystem);
        Assert.Equal("Updated Name", updatedSystem.SystemName);
        Assert.Equal("UPDATED", updatedSystem.SystemCode);
        Assert.Equal("Updated description", updatedSystem.Description);
    }

    [Fact]
    public async Task UpdateSystemAsync_SetsUpdateDate_Automatically()
    {
        // Arrange
        var system = new SysSystem
        {
            RowId = 1,
            SystemName = "Test System",
            SystemCode = "TEST",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Systems.AddAsync(system);
        await _context.SaveChangesAsync();

        system.SystemName = "Modified Name";

        // Act
        await _repository.UpdateSystemAsync(system);

        // Assert
        Assert.NotNull(system.UpdateDate);
        Assert.True(system.UpdateDate.Value <= DateTime.Now);
    }

    #endregion

    #region DeleteSystemAsync Tests (Soft Delete)

    [Fact]
    public async Task DeleteSystemAsync_SoftDeletesSystem_SetsIsActiveToFalse()
    {
        // Arrange
        var system = new SysSystem
        {
            RowId = 1,
            SystemName = "Test System",
            SystemCode = "TEST",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Systems.AddAsync(system);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteSystemAsync(1, "delete_user");

        // Assert
        var deletedSystem = await _context.Systems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.RowId == 1);
        Assert.NotNull(deletedSystem);
        Assert.False(deletedSystem.IsActive);
        Assert.Equal("delete_user", deletedSystem.UpdateUser);
        Assert.NotNull(deletedSystem.UpdateDate);
    }

    [Fact]
    public async Task DeleteSystemAsync_WithNonExistentId_DoesNotThrow()
    {
        // Act & Assert
        await _repository.DeleteSystemAsync(999, "test_user");
        // Should not throw exception
    }

    [Fact]
    public async Task DeleteSystemAsync_OnAlreadyDeletedSystem_UpdatesAgain()
    {
        // Arrange
        var system = new SysSystem
        {
            RowId = 1,
            SystemName = "Test System",
            SystemCode = "TEST",
            IsActive = false, // Already deleted
            CreationUser = "test_user"
        };
        await _context.Systems.AddAsync(system);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteSystemAsync(1, "second_delete_user");

        // Assert
        var deletedSystem = await _context.Systems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.RowId == 1);
        Assert.NotNull(deletedSystem);
        Assert.False(deletedSystem.IsActive);
        Assert.Equal("second_delete_user", deletedSystem.UpdateUser);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new SystemRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new SystemRepository(_context, null!));
    }

    #endregion
}
