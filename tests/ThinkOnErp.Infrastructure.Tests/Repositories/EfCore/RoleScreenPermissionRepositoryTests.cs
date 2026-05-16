using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for RoleScreenPermissionRepository using EF Core InMemory provider.
/// Tests composite key operations, CRUD operations, and relationship queries.
/// </summary>
public class RoleScreenPermissionRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly RoleScreenPermissionRepository _repository;
    private readonly Mock<ILogger<RoleScreenPermissionRepository>> _loggerMock;

    public RoleScreenPermissionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_RoleScreenPermission_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<RoleScreenPermissionRepository>>();
        _repository = new RoleScreenPermissionRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Test Data Setup

    private async Task<(SysRole role, SysScreen screen)> CreateTestRoleAndScreenAsync()
    {
        var role = new SysRole
        {
            RowId = 1,
            RowDesc = "Test Role",
            RowDescE = "Test Role",
            IsActive = true,
            CreationUser = "test"
        };

        var system = new SysSystem
        {
            RowId = 1,
            SystemName = "Test System",
            SystemCode = "SYS",
            IsActive = true,
            CreationUser = "test"
        };

        var screen = new SysScreen
        {
            RowId = 1,
            SystemId = 1,
            ScreenName = "Test Screen",
            ScreenCode = "SCR",
            IsActive = true,
            CreationUser = "test"
        };

        await _context.Roles.AddAsync(role);
        await _context.Systems.AddAsync(system);
        await _context.Screens.AddAsync(screen);
        await _context.SaveChangesAsync();

        return (role, screen);
    }

    #endregion

    #region GetByRoleIdAsync Tests

    [Fact]
    public async Task GetByRoleIdAsync_ReturnsPermissions_OrderedByScreenDisplayOrder()
    {
        // Arrange
        var (role, _) = await CreateTestRoleAndScreenAsync();
        
        var screen2 = new SysScreen
        {
            RowId = 2,
            SystemId = 1,
            ScreenName = "Screen 2",
            ScreenCode = "SCR2",
            DisplayOrder = 2,
            IsActive = true,
            CreationUser = "test"
        };
        var screen3 = new SysScreen
        {
            RowId = 3,
            SystemId = 1,
            ScreenName = "Screen 3",
            ScreenCode = "SCR3",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Screens.AddRangeAsync(screen2, screen3);
        await _context.SaveChangesAsync();

        var permissions = new List<SysRoleScreenPermission>
        {
            new SysRoleScreenPermission
            {
                RoleId = role.RowId,
                ScreenId = 1,
                CanView = true,
                CanInsert = false,
                CanUpdate = false,
                CanDelete = false,
                CreationUser = "test"
            },
            new SysRoleScreenPermission
            {
                RoleId = role.RowId,
                ScreenId = 2,
                CanView = true,
                CanInsert = true,
                CanUpdate = false,
                CanDelete = false,
                CreationUser = "test"
            },
            new SysRoleScreenPermission
            {
                RoleId = role.RowId,
                ScreenId = 3,
                CanView = true,
                CanInsert = true,
                CanUpdate = true,
                CanDelete = false,
                CreationUser = "test"
            }
        };
        await _context.RoleScreenPermissions.AddRangeAsync(permissions);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByRoleIdAsync(role.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(3, result[0].ScreenId); // DisplayOrder = 1
        Assert.Equal(2, result[1].ScreenId); // DisplayOrder = 2
        Assert.Equal(1, result[2].ScreenId); // DisplayOrder = null (last)
    }

    [Fact]
    public async Task GetByRoleIdAsync_ReturnsEmptyList_WhenNoPermissions()
    {
        // Arrange
        var (role, _) = await CreateTestRoleAndScreenAsync();

        // Act
        var result = await _repository.GetByRoleIdAsync(role.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByRoleIdAsync_IncludesNavigationProperties()
    {
        // Arrange
        var (role, screen) = await CreateTestRoleAndScreenAsync();
        
        var permission = new SysRoleScreenPermission
        {
            RoleId = role.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test"
        };
        await _context.RoleScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByRoleIdAsync(role.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.NotNull(result[0].Role);
        Assert.NotNull(result[0].Screen);
        Assert.Equal("Test Role", result[0].Role.RowDesc);
        Assert.Equal("Test Screen", result[0].Screen.ScreenName);
    }

    #endregion

    #region GetByIdAsync Tests (Composite Key)

    [Fact]
    public async Task GetByIdAsync_ReturnsPermission_WhenExists()
    {
        // Arrange
        var (role, screen) = await CreateTestRoleAndScreenAsync();
        
        var permission = new SysRoleScreenPermission
        {
            RoleId = role.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = true,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test_user"
        };
        await _context.RoleScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(role.RowId, screen.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(role.RowId, result.RoleId);
        Assert.Equal(screen.RowId, result.ScreenId);
        Assert.True(result.CanView);
        Assert.True(result.CanInsert);
        Assert.False(result.CanUpdate);
        Assert.False(result.CanDelete);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByIdAsync(999, 999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenOnlyRoleIdMatches()
    {
        // Arrange
        var (role, screen) = await CreateTestRoleAndScreenAsync();
        
        var permission = new SysRoleScreenPermission
        {
            RoleId = role.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test"
        };
        await _context.RoleScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(role.RowId, 999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenOnlyScreenIdMatches()
    {
        // Arrange
        var (role, screen) = await CreateTestRoleAndScreenAsync();
        
        var permission = new SysRoleScreenPermission
        {
            RoleId = role.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test"
        };
        await _context.RoleScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(999, screen.RowId);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CreatesNewPermission_ReturnsGeneratedId()
    {
        // Arrange
        var (role, screen) = await CreateTestRoleAndScreenAsync();
        
        var permission = new SysRoleScreenPermission
        {
            RoleId = role.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = true,
            CanUpdate = true,
            CanDelete = false,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(permission);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, permission.RowId);
        Assert.NotNull(permission.CreationDate);

        // Verify in database
        var savedPermission = await _context.RoleScreenPermissions
            .FirstOrDefaultAsync(p => p.RoleId == role.RowId && p.ScreenId == screen.RowId);
        Assert.NotNull(savedPermission);
        Assert.True(savedPermission.CanView);
        Assert.True(savedPermission.CanInsert);
        Assert.True(savedPermission.CanUpdate);
        Assert.False(savedPermission.CanDelete);
    }

    [Fact]
    public async Task CreateAsync_SetsCreationDate_WhenNotProvided()
    {
        // Arrange
        var (role, screen) = await CreateTestRoleAndScreenAsync();
        
        var permission = new SysRoleScreenPermission
        {
            RoleId = role.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateAsync(permission);

        // Assert
        Assert.NotNull(permission.CreationDate);
        Assert.True(permission.CreationDate.Value <= DateTime.Now);
    }

    [Fact]
    public async Task CreateAsync_WithAllPermissionsFalse_Succeeds()
    {
        // Arrange
        var (role, screen) = await CreateTestRoleAndScreenAsync();
        
        var permission = new SysRoleScreenPermission
        {
            RoleId = role.RowId,
            ScreenId = screen.RowId,
            CanView = false,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(permission);

        // Assert
        Assert.True(result > 0);
        var savedPermission = await _context.RoleScreenPermissions.FindAsync(result);
        Assert.NotNull(savedPermission);
        Assert.False(savedPermission.CanView);
        Assert.False(savedPermission.CanInsert);
        Assert.False(savedPermission.CanUpdate);
        Assert.False(savedPermission.CanDelete);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesExistingPermission_ReturnsRowsAffected()
    {
        // Arrange
        var (role, screen) = await CreateTestRoleAndScreenAsync();
        
        var permission = new SysRoleScreenPermission
        {
            RoleId = role.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test_user"
        };
        await _context.RoleScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();

        // Modify permissions
        permission.CanInsert = true;
        permission.CanUpdate = true;
        permission.UpdateUser = "update_user";

        // Act
        var result = await _repository.UpdateAsync(permission);

        // Assert
        Assert.Equal(1, result);
        Assert.NotNull(permission.UpdateDate);

        // Verify in database
        var updatedPermission = await _context.RoleScreenPermissions
            .FirstOrDefaultAsync(p => p.RoleId == role.RowId && p.ScreenId == screen.RowId);
        Assert.NotNull(updatedPermission);
        Assert.True(updatedPermission.CanInsert);
        Assert.True(updatedPermission.CanUpdate);
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdateDate_Automatically()
    {
        // Arrange
        var (role, screen) = await CreateTestRoleAndScreenAsync();
        
        var permission = new SysRoleScreenPermission
        {
            RoleId = role.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test_user"
        };
        await _context.RoleScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();

        permission.CanDelete = true;

        // Act
        await _repository.UpdateAsync(permission);

        // Assert
        Assert.NotNull(permission.UpdateDate);
        Assert.True(permission.UpdateDate.Value <= DateTime.Now);
    }

    #endregion

    #region DeleteAsync Tests (Composite Key)

    [Fact]
    public async Task DeleteAsync_DeletesPermission_ReturnsRowsAffected()
    {
        // Arrange
        var (role, screen) = await CreateTestRoleAndScreenAsync();
        
        var permission = new SysRoleScreenPermission
        {
            RoleId = role.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test_user"
        };
        await _context.RoleScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.DeleteAsync(role.RowId, screen.RowId);

        // Assert
        Assert.Equal(1, result);

        // Verify permission is deleted
        var deletedPermission = await _context.RoleScreenPermissions
            .FirstOrDefaultAsync(p => p.RoleId == role.RowId && p.ScreenId == screen.RowId);
        Assert.Null(deletedPermission);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentCompositeKey_ReturnsZero()
    {
        // Act
        var result = await _repository.DeleteAsync(999, 999);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task DeleteAsync_WithPartialMatch_ReturnsZero()
    {
        // Arrange
        var (role, screen) = await CreateTestRoleAndScreenAsync();
        
        var permission = new SysRoleScreenPermission
        {
            RoleId = role.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test_user"
        };
        await _context.RoleScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.DeleteAsync(role.RowId, 999);

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region SetPermissionAsync Tests (Upsert)

    [Fact]
    public async Task SetPermissionAsync_CreatesNewPermission_WhenNotExists()
    {
        // Arrange
        var (role, screen) = await CreateTestRoleAndScreenAsync();

        // Act
        var result = await _repository.SetPermissionAsync(
            role.RowId, screen.RowId, true, true, false, false, "test_user");

        // Assert
        Assert.True(result > 0);

        var permission = await _context.RoleScreenPermissions
            .FirstOrDefaultAsync(p => p.RoleId == role.RowId && p.ScreenId == screen.RowId);
        Assert.NotNull(permission);
        Assert.True(permission.CanView);
        Assert.True(permission.CanInsert);
        Assert.False(permission.CanUpdate);
        Assert.False(permission.CanDelete);
    }

    [Fact]
    public async Task SetPermissionAsync_UpdatesExistingPermission_WhenExists()
    {
        // Arrange
        var (role, screen) = await CreateTestRoleAndScreenAsync();
        
        var permission = new SysRoleScreenPermission
        {
            RoleId = role.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "original_user"
        };
        await _context.RoleScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();
        var originalId = permission.RowId;

        // Act
        var result = await _repository.SetPermissionAsync(
            role.RowId, screen.RowId, true, true, true, true, "update_user");

        // Assert
        Assert.Equal(originalId, result); // Returns existing ID

        var updatedPermission = await _context.RoleScreenPermissions
            .FirstOrDefaultAsync(p => p.RoleId == role.RowId && p.ScreenId == screen.RowId);
        Assert.NotNull(updatedPermission);
        Assert.True(updatedPermission.CanView);
        Assert.True(updatedPermission.CanInsert);
        Assert.True(updatedPermission.CanUpdate);
        Assert.True(updatedPermission.CanDelete);
        Assert.Equal("update_user", updatedPermission.UpdateUser);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new RoleScreenPermissionRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new RoleScreenPermissionRepository(_context, null!));
    }

    #endregion
}
