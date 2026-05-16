using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for UserScreenPermissionRepository using EF Core InMemory provider.
/// Tests composite key operations, CRUD operations, and relationship queries.
/// </summary>
public class UserScreenPermissionRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly UserScreenPermissionRepository _repository;
    private readonly Mock<ILogger<UserScreenPermissionRepository>> _loggerMock;

    public UserScreenPermissionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_UserScreenPermission_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<UserScreenPermissionRepository>>();
        _repository = new UserScreenPermissionRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Test Data Setup

    private async Task<(SysUser user, SysScreen screen)> CreateTestUserAndScreenAsync()
    {
        var company = new SysCompany
        {
            RowId = 1,
            RowDesc = "Test Company",
            RowDescE = "Test Company",
            CompanyCode = "TEST",
            IsActive = true,
            CreationUser = "test"
        };

        var branch = new SysBranch
        {
            RowId = 1,
            ParRowId = 1,
            RowDesc = "Test Branch",
            RowDescE = "Test Branch",
            IsActive = true,
            CreationUser = "test"
        };

        var user = new SysUser
        {
            RowId = 1,
            CompanyId = 1,
            BranchId = 1,
            UserName = "testuser",
            PasswordHash = "hash",
            FullName = "Test User",
            Email = "test@test.com",
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

        await _context.Companies.AddAsync(company);
        await _context.Branches.AddAsync(branch);
        await _context.Users.AddAsync(user);
        await _context.Systems.AddAsync(system);
        await _context.Screens.AddAsync(screen);
        await _context.SaveChangesAsync();

        return (user, screen);
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Fact]
    public async Task GetByUserIdAsync_ReturnsPermissions_OrderedByScreenDisplayOrder()
    {
        // Arrange
        var (user, _) = await CreateTestUserAndScreenAsync();
        
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

        var permissions = new List<SysUserScreenPermission>
        {
            new SysUserScreenPermission
            {
                UserId = user.RowId,
                ScreenId = 1,
                CanView = true,
                CanInsert = false,
                CanUpdate = false,
                CanDelete = false,
                CreationUser = "test"
            },
            new SysUserScreenPermission
            {
                UserId = user.RowId,
                ScreenId = 2,
                CanView = true,
                CanInsert = true,
                CanUpdate = false,
                CanDelete = false,
                CreationUser = "test"
            },
            new SysUserScreenPermission
            {
                UserId = user.RowId,
                ScreenId = 3,
                CanView = true,
                CanInsert = true,
                CanUpdate = true,
                CanDelete = false,
                CreationUser = "test"
            }
        };
        await _context.UserScreenPermissions.AddRangeAsync(permissions);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserIdAsync(user.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(3, result[0].ScreenId); // DisplayOrder = 1
        Assert.Equal(2, result[1].ScreenId); // DisplayOrder = 2
        Assert.Equal(1, result[2].ScreenId); // DisplayOrder = null (last)
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsEmptyList_WhenNoPermissions()
    {
        // Arrange
        var (user, _) = await CreateTestUserAndScreenAsync();

        // Act
        var result = await _repository.GetByUserIdAsync(user.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByUserIdAsync_IncludesNavigationProperties()
    {
        // Arrange
        var (user, screen) = await CreateTestUserAndScreenAsync();
        
        var permission = new SysUserScreenPermission
        {
            UserId = user.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test"
        };
        await _context.UserScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserIdAsync(user.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.NotNull(result[0].User);
        Assert.NotNull(result[0].Screen);
        Assert.Equal("Test User", result[0].User.FullName);
        Assert.Equal("Test Screen", result[0].Screen.ScreenName);
    }

    #endregion

    #region GetByIdAsync Tests (Composite Key)

    [Fact]
    public async Task GetByIdAsync_ReturnsPermission_WhenExists()
    {
        // Arrange
        var (user, screen) = await CreateTestUserAndScreenAsync();
        
        var permission = new SysUserScreenPermission
        {
            UserId = user.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = true,
            CanUpdate = false,
            CanDelete = false,
            AssignedBy = 1,
            Notes = "Test override",
            CreationUser = "test_user"
        };
        await _context.UserScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(user.RowId, screen.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.RowId, result.UserId);
        Assert.Equal(screen.RowId, result.ScreenId);
        Assert.True(result.CanView);
        Assert.True(result.CanInsert);
        Assert.False(result.CanUpdate);
        Assert.False(result.CanDelete);
        Assert.Equal("Test override", result.Notes);
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
    public async Task GetByIdAsync_ReturnsNull_WhenOnlyUserIdMatches()
    {
        // Arrange
        var (user, screen) = await CreateTestUserAndScreenAsync();
        
        var permission = new SysUserScreenPermission
        {
            UserId = user.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test"
        };
        await _context.UserScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(user.RowId, 999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenOnlyScreenIdMatches()
    {
        // Arrange
        var (user, screen) = await CreateTestUserAndScreenAsync();
        
        var permission = new SysUserScreenPermission
        {
            UserId = user.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test"
        };
        await _context.UserScreenPermissions.AddAsync(permission);
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
        var (user, screen) = await CreateTestUserAndScreenAsync();
        
        var permission = new SysUserScreenPermission
        {
            UserId = user.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = true,
            CanUpdate = true,
            CanDelete = false,
            AssignedBy = 1,
            Notes = "Special permission",
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(permission);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, permission.RowId);
        Assert.NotNull(permission.CreationDate);
        Assert.NotNull(permission.AssignedDate);

        // Verify in database
        var savedPermission = await _context.UserScreenPermissions
            .FirstOrDefaultAsync(p => p.UserId == user.RowId && p.ScreenId == screen.RowId);
        Assert.NotNull(savedPermission);
        Assert.True(savedPermission.CanView);
        Assert.True(savedPermission.CanInsert);
        Assert.True(savedPermission.CanUpdate);
        Assert.False(savedPermission.CanDelete);
        Assert.Equal("Special permission", savedPermission.Notes);
    }

    [Fact]
    public async Task CreateAsync_SetsCreationDateAndAssignedDate_WhenNotProvided()
    {
        // Arrange
        var (user, screen) = await CreateTestUserAndScreenAsync();
        
        var permission = new SysUserScreenPermission
        {
            UserId = user.RowId,
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
        Assert.NotNull(permission.AssignedDate);
        Assert.True(permission.CreationDate.Value <= DateTime.Now);
        Assert.True(permission.AssignedDate.Value <= DateTime.Now);
    }

    [Fact]
    public async Task CreateAsync_WithNullOptionalFields_Succeeds()
    {
        // Arrange
        var (user, screen) = await CreateTestUserAndScreenAsync();
        
        var permission = new SysUserScreenPermission
        {
            UserId = user.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            AssignedBy = null,
            Notes = null,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(permission);

        // Assert
        Assert.True(result > 0);
        var savedPermission = await _context.UserScreenPermissions.FindAsync(result);
        Assert.NotNull(savedPermission);
        Assert.Null(savedPermission.AssignedBy);
        Assert.Null(savedPermission.Notes);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesExistingPermission_ReturnsRowsAffected()
    {
        // Arrange
        var (user, screen) = await CreateTestUserAndScreenAsync();
        
        var permission = new SysUserScreenPermission
        {
            UserId = user.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            Notes = "Original note",
            CreationUser = "test_user"
        };
        await _context.UserScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();

        // Modify permissions
        permission.CanInsert = true;
        permission.CanUpdate = true;
        permission.Notes = "Updated note";
        permission.UpdateUser = "update_user";

        // Act
        var result = await _repository.UpdateAsync(permission);

        // Assert
        Assert.Equal(1, result);
        Assert.NotNull(permission.UpdateDate);

        // Verify in database
        var updatedPermission = await _context.UserScreenPermissions
            .FirstOrDefaultAsync(p => p.UserId == user.RowId && p.ScreenId == screen.RowId);
        Assert.NotNull(updatedPermission);
        Assert.True(updatedPermission.CanInsert);
        Assert.True(updatedPermission.CanUpdate);
        Assert.Equal("Updated note", updatedPermission.Notes);
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdateDate_Automatically()
    {
        // Arrange
        var (user, screen) = await CreateTestUserAndScreenAsync();
        
        var permission = new SysUserScreenPermission
        {
            UserId = user.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test_user"
        };
        await _context.UserScreenPermissions.AddAsync(permission);
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
        var (user, screen) = await CreateTestUserAndScreenAsync();
        
        var permission = new SysUserScreenPermission
        {
            UserId = user.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            CreationUser = "test_user"
        };
        await _context.UserScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.DeleteAsync(user.RowId, screen.RowId);

        // Assert
        Assert.Equal(1, result);

        // Verify permission is deleted
        var deletedPermission = await _context.UserScreenPermissions
            .FirstOrDefaultAsync(p => p.UserId == user.RowId && p.ScreenId == screen.RowId);
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

    #endregion

    #region SetPermissionAsync Tests (Upsert)

    [Fact]
    public async Task SetPermissionAsync_CreatesNewPermission_WhenNotExists()
    {
        // Arrange
        var (user, screen) = await CreateTestUserAndScreenAsync();

        // Act
        var result = await _repository.SetPermissionAsync(
            user.RowId, screen.RowId, true, true, false, false, 1, "Override permission", "test_user");

        // Assert
        Assert.True(result > 0);

        var permission = await _context.UserScreenPermissions
            .FirstOrDefaultAsync(p => p.UserId == user.RowId && p.ScreenId == screen.RowId);
        Assert.NotNull(permission);
        Assert.True(permission.CanView);
        Assert.True(permission.CanInsert);
        Assert.False(permission.CanUpdate);
        Assert.False(permission.CanDelete);
        Assert.Equal(1, permission.AssignedBy);
        Assert.Equal("Override permission", permission.Notes);
    }

    [Fact]
    public async Task SetPermissionAsync_UpdatesExistingPermission_WhenExists()
    {
        // Arrange
        var (user, screen) = await CreateTestUserAndScreenAsync();
        
        var permission = new SysUserScreenPermission
        {
            UserId = user.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            Notes = "Original",
            CreationUser = "original_user"
        };
        await _context.UserScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();
        var originalId = permission.RowId;

        // Act
        var result = await _repository.SetPermissionAsync(
            user.RowId, screen.RowId, true, true, true, true, 2, "Updated", "update_user");

        // Assert
        Assert.Equal(originalId, result); // Returns existing ID

        var updatedPermission = await _context.UserScreenPermissions
            .FirstOrDefaultAsync(p => p.UserId == user.RowId && p.ScreenId == screen.RowId);
        Assert.NotNull(updatedPermission);
        Assert.True(updatedPermission.CanView);
        Assert.True(updatedPermission.CanInsert);
        Assert.True(updatedPermission.CanUpdate);
        Assert.True(updatedPermission.CanDelete);
        Assert.Equal(2, updatedPermission.AssignedBy);
        Assert.Equal("Updated", updatedPermission.Notes);
        Assert.Equal("update_user", updatedPermission.UpdateUser);
    }

    [Fact]
    public async Task SetPermissionAsync_UpdatesAssignedDate_OnUpdate()
    {
        // Arrange
        var (user, screen) = await CreateTestUserAndScreenAsync();
        
        var permission = new SysUserScreenPermission
        {
            UserId = user.RowId,
            ScreenId = screen.RowId,
            CanView = true,
            CanInsert = false,
            CanUpdate = false,
            CanDelete = false,
            AssignedDate = DateTime.Now.AddDays(-10),
            CreationUser = "original_user"
        };
        await _context.UserScreenPermissions.AddAsync(permission);
        await _context.SaveChangesAsync();
        var originalAssignedDate = permission.AssignedDate;

        // Act
        await _repository.SetPermissionAsync(
            user.RowId, screen.RowId, true, true, true, false, 1, null, "update_user");

        // Assert
        var updatedPermission = await _context.UserScreenPermissions
            .FirstOrDefaultAsync(p => p.UserId == user.RowId && p.ScreenId == screen.RowId);
        Assert.NotNull(updatedPermission);
        Assert.NotNull(updatedPermission.AssignedDate);
        Assert.True(updatedPermission.AssignedDate > originalAssignedDate);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new UserScreenPermissionRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new UserScreenPermissionRepository(_context, null!));
    }

    #endregion
}
