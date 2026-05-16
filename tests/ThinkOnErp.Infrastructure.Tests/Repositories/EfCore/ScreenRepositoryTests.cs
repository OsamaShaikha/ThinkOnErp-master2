using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for ScreenRepository using EF Core InMemory provider.
/// Tests CRUD operations, relationship queries, and soft delete functionality.
/// </summary>
public class ScreenRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ScreenRepository _repository;
    private readonly Mock<ILogger<ScreenRepository>> _loggerMock;

    public ScreenRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_Screen_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<ScreenRepository>>();
        _repository = new ScreenRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Test Data Setup

    private async Task<SysSystem> CreateTestSystemAsync()
    {
        var system = new SysSystem
        {
            RowId = 1,
            SystemName = "Test System",
            SystemCode = "SYS",
            IsActive = true,
            CreationUser = "test"
        };

        await _context.Systems.AddAsync(system);
        await _context.SaveChangesAsync();

        return system;
    }

    #endregion

    #region GetAllScreensAsync Tests

    [Fact]
    public async Task GetAllScreensAsync_ReturnsAllScreens_OrderedByDisplayOrder()
    {
        // Arrange
        var system = await CreateTestSystemAsync();
        
        var screens = new List<SysScreen>
        {
            new SysScreen
            {
                RowId = 1,
                SystemId = system.RowId,
                ScreenName = "Screen C",
                ScreenCode = "SCR-C",
                DisplayOrder = 3,
                IsActive = true,
                CreationUser = "test"
            },
            new SysScreen
            {
                RowId = 2,
                SystemId = system.RowId,
                ScreenName = "Screen A",
                ScreenCode = "SCR-A",
                DisplayOrder = 1,
                IsActive = true,
                CreationUser = "test"
            },
            new SysScreen
            {
                RowId = 3,
                SystemId = system.RowId,
                ScreenName = "Screen B",
                ScreenCode = "SCR-B",
                DisplayOrder = 2,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.Screens.AddRangeAsync(screens);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllScreensAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("Screen A", result[0].ScreenName);
        Assert.Equal("Screen B", result[1].ScreenName);
        Assert.Equal("Screen C", result[2].ScreenName);
    }

    [Fact]
    public async Task GetAllScreensAsync_ReturnsEmptyList_WhenNoScreens()
    {
        // Act
        var result = await _repository.GetAllScreensAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllScreensAsync_FiltersOutInactiveScreens()
    {
        // Arrange
        var system = await CreateTestSystemAsync();
        
        var screens = new List<SysScreen>
        {
            new SysScreen
            {
                RowId = 1,
                SystemId = system.RowId,
                ScreenName = "Active Screen",
                ScreenCode = "ACTIVE",
                IsActive = true,
                CreationUser = "test"
            },
            new SysScreen
            {
                RowId = 2,
                SystemId = system.RowId,
                ScreenName = "Inactive Screen",
                ScreenCode = "INACTIVE",
                IsActive = false,
                CreationUser = "test"
            }
        };
        await _context.Screens.AddRangeAsync(screens);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllScreensAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Active Screen", result[0].ScreenName);
    }

    [Fact]
    public async Task GetAllScreensAsync_IncludesNavigationProperties()
    {
        // Arrange
        var system = await CreateTestSystemAsync();
        
        var parentScreen = new SysScreen
        {
            RowId = 1,
            SystemId = system.RowId,
            ScreenName = "Parent Screen",
            ScreenCode = "PARENT",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Screens.AddAsync(parentScreen);
        await _context.SaveChangesAsync();

        var childScreen = new SysScreen
        {
            RowId = 2,
            SystemId = system.RowId,
            ParentScreenId = parentScreen.RowId,
            ScreenName = "Child Screen",
            ScreenCode = "CHILD",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Screens.AddAsync(childScreen);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllScreensAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        var child = result.FirstOrDefault(s => s.ScreenCode == "CHILD");
        Assert.NotNull(child);
        Assert.NotNull(child.System);
        Assert.NotNull(child.ParentScreen);
        Assert.Equal("Test System", child.System.SystemName);
        Assert.Equal("Parent Screen", child.ParentScreen.ScreenName);
    }

    #endregion

    #region GetScreensBySystemIdAsync Tests

    [Fact]
    public async Task GetScreensBySystemIdAsync_ReturnsScreensForSystem()
    {
        // Arrange
        var system1 = await CreateTestSystemAsync();
        var system2 = new SysSystem
        {
            RowId = 2,
            SystemName = "System 2",
            SystemCode = "SYS2",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Systems.AddAsync(system2);
        await _context.SaveChangesAsync();

        var screens = new List<SysScreen>
        {
            new SysScreen
            {
                RowId = 1,
                SystemId = system1.RowId,
                ScreenName = "Screen 1",
                ScreenCode = "SCR1",
                IsActive = true,
                CreationUser = "test"
            },
            new SysScreen
            {
                RowId = 2,
                SystemId = system1.RowId,
                ScreenName = "Screen 2",
                ScreenCode = "SCR2",
                IsActive = true,
                CreationUser = "test"
            },
            new SysScreen
            {
                RowId = 3,
                SystemId = system2.RowId,
                ScreenName = "Screen 3",
                ScreenCode = "SCR3",
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.Screens.AddRangeAsync(screens);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetScreensBySystemIdAsync(system1.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal(system1.RowId, s.SystemId));
    }

    [Fact]
    public async Task GetScreensBySystemIdAsync_ReturnsEmptyList_WhenNoScreensForSystem()
    {
        // Arrange
        var system = await CreateTestSystemAsync();

        // Act
        var result = await _repository.GetScreensBySystemIdAsync(system.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetScreensBySystemIdAsync_FiltersOutInactiveScreens()
    {
        // Arrange
        var system = await CreateTestSystemAsync();
        
        var screens = new List<SysScreen>
        {
            new SysScreen
            {
                RowId = 1,
                SystemId = system.RowId,
                ScreenName = "Active",
                ScreenCode = "ACTIVE",
                IsActive = true,
                CreationUser = "test"
            },
            new SysScreen
            {
                RowId = 2,
                SystemId = system.RowId,
                ScreenName = "Inactive",
                ScreenCode = "INACTIVE",
                IsActive = false,
                CreationUser = "test"
            }
        };
        await _context.Screens.AddRangeAsync(screens);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetScreensBySystemIdAsync(system.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Active", result[0].ScreenName);
    }

    #endregion

    #region GetScreenByIdAsync Tests

    [Fact]
    public async Task GetScreenByIdAsync_ReturnsScreen_WhenExists()
    {
        // Arrange
        var system = await CreateTestSystemAsync();
        
        var screen = new SysScreen
        {
            RowId = 1,
            SystemId = system.RowId,
            ScreenName = "Test Screen",
            ScreenCode = "TEST",
            ScreenUrl = "/test",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Screens.AddAsync(screen);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetScreenByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.RowId);
        Assert.Equal("Test Screen", result.ScreenName);
        Assert.Equal("TEST", result.ScreenCode);
        Assert.Equal("/test", result.ScreenUrl);
    }

    [Fact]
    public async Task GetScreenByIdAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetScreenByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetScreenByIdAsync_ReturnsNull_WhenScreenIsInactive()
    {
        // Arrange
        var system = await CreateTestSystemAsync();
        
        var screen = new SysScreen
        {
            RowId = 1,
            SystemId = system.RowId,
            ScreenName = "Inactive Screen",
            ScreenCode = "INACTIVE",
            IsActive = false,
            CreationUser = "test"
        };
        await _context.Screens.AddAsync(screen);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetScreenByIdAsync(1);

        // Assert
        Assert.Null(result); // Global query filter excludes inactive records
    }

    #endregion

    #region CreateScreenAsync Tests

    [Fact]
    public async Task CreateScreenAsync_CreatesNewScreen_ReturnsGeneratedId()
    {
        // Arrange
        var system = await CreateTestSystemAsync();
        
        var screen = new SysScreen
        {
            SystemId = system.RowId,
            ScreenName = "New Screen",
            ScreenCode = "NEW",
            ScreenUrl = "/new",
            DisplayOrder = 1,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateScreenAsync(screen);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, screen.RowId);
        Assert.NotNull(screen.CreationDate);
        Assert.True(screen.IsActive);

        // Verify in database
        var savedScreen = await _context.Screens.FindAsync(result);
        Assert.NotNull(savedScreen);
        Assert.Equal("NEW", savedScreen.ScreenCode);
    }

    [Fact]
    public async Task CreateScreenAsync_SetsCreationDate_WhenNotProvided()
    {
        // Arrange
        var system = await CreateTestSystemAsync();
        
        var screen = new SysScreen
        {
            SystemId = system.RowId,
            ScreenName = "Test Screen",
            ScreenCode = "TEST",
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateScreenAsync(screen);

        // Assert
        Assert.NotNull(screen.CreationDate);
        Assert.True(screen.CreationDate.Value <= DateTime.Now);
    }

    [Fact]
    public async Task CreateScreenAsync_SetsIsActiveToTrue_ByDefault()
    {
        // Arrange
        var system = await CreateTestSystemAsync();
        
        var screen = new SysScreen
        {
            SystemId = system.RowId,
            ScreenName = "Test Screen",
            ScreenCode = "TEST",
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateScreenAsync(screen);

        // Assert
        Assert.True(screen.IsActive);
    }

    [Fact]
    public async Task CreateScreenAsync_WithParentScreen_Succeeds()
    {
        // Arrange
        var system = await CreateTestSystemAsync();
        
        var parentScreen = new SysScreen
        {
            SystemId = system.RowId,
            ScreenName = "Parent",
            ScreenCode = "PARENT",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Screens.AddAsync(parentScreen);
        await _context.SaveChangesAsync();

        var childScreen = new SysScreen
        {
            SystemId = system.RowId,
            ParentScreenId = parentScreen.RowId,
            ScreenName = "Child",
            ScreenCode = "CHILD",
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateScreenAsync(childScreen);

        // Assert
        Assert.True(result > 0);
        var savedScreen = await _context.Screens.FindAsync(result);
        Assert.NotNull(savedScreen);
        Assert.Equal(parentScreen.RowId, savedScreen.ParentScreenId);
    }

    #endregion

    #region UpdateScreenAsync Tests

    [Fact]
    public async Task UpdateScreenAsync_UpdatesExistingScreen()
    {
        // Arrange
        var system = await CreateTestSystemAsync();
        
        var screen = new SysScreen
        {
            SystemId = system.RowId,
            ScreenName = "Original Name",
            ScreenCode = "ORIG",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Screens.AddAsync(screen);
        await _context.SaveChangesAsync();

        // Modify the screen
        screen.ScreenName = "Updated Name";
        screen.ScreenUrl = "/updated";
        screen.UpdateUser = "update_user";

        // Act
        await _repository.UpdateScreenAsync(screen);

        // Assert
        Assert.NotNull(screen.UpdateDate);

        // Verify in database
        var updatedScreen = await _context.Screens.FindAsync(screen.RowId);
        Assert.NotNull(updatedScreen);
        Assert.Equal("Updated Name", updatedScreen.ScreenName);
        Assert.Equal("/updated", updatedScreen.ScreenUrl);
    }

    [Fact]
    public async Task UpdateScreenAsync_SetsUpdateDate_Automatically()
    {
        // Arrange
        var system = await CreateTestSystemAsync();
        
        var screen = new SysScreen
        {
            SystemId = system.RowId,
            ScreenName = "Test Screen",
            ScreenCode = "TEST",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Screens.AddAsync(screen);
        await _context.SaveChangesAsync();

        screen.ScreenName = "Updated";

        // Act
        await _repository.UpdateScreenAsync(screen);

        // Assert
        Assert.NotNull(screen.UpdateDate);
        Assert.True(screen.UpdateDate.Value <= DateTime.Now);
    }

    #endregion

    #region DeleteScreenAsync Tests

    [Fact]
    public async Task DeleteScreenAsync_SoftDeletesScreen()
    {
        // Arrange
        var system = await CreateTestSystemAsync();
        
        var screen = new SysScreen
        {
            SystemId = system.RowId,
            ScreenName = "To Delete",
            ScreenCode = "DEL",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Screens.AddAsync(screen);
        await _context.SaveChangesAsync();
        var screenId = screen.RowId;

        // Act
        await _repository.DeleteScreenAsync(screenId, "delete_user");

        // Assert
        // Verify screen is soft deleted (IsActive = false)
        var deletedScreen = await _context.Screens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.RowId == screenId);
        Assert.NotNull(deletedScreen);
        Assert.False(deletedScreen.IsActive);
        Assert.Equal("delete_user", deletedScreen.UpdateUser);
        Assert.NotNull(deletedScreen.UpdateDate);
    }

    [Fact]
    public async Task DeleteScreenAsync_WithNonExistentId_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        await _repository.DeleteScreenAsync(999, "user");
    }

    [Fact]
    public async Task DeleteScreenAsync_OnAlreadyDeletedScreen_Succeeds()
    {
        // Arrange
        var system = await CreateTestSystemAsync();
        
        var screen = new SysScreen
        {
            SystemId = system.RowId,
            ScreenName = "Already Deleted",
            ScreenCode = "DELETED",
            IsActive = false,
            CreationUser = "test_user"
        };
        await _context.Screens.AddAsync(screen);
        await _context.SaveChangesAsync();

        // Act & Assert - Should not throw
        await _repository.DeleteScreenAsync(screen.RowId, "user");
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new ScreenRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new ScreenRepository(_context, null!));
    }

    #endregion
}
