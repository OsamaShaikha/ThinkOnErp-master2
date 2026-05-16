using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for UserRepository using EF Core InMemory provider.
/// Tests all CRUD operations, authentication scenarios, exception scenarios, and null handling.
/// </summary>
public class UserRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly UserRepository _repository;
    private readonly Mock<ILogger<UserRepository>> _loggerMock;

    public UserRepositoryTests()
    {
        // Create InMemory database with unique name for each test instance
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_User_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<UserRepository>>();
        _repository = new UserRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllUsers_OrderedByUserName()
    {
        // Arrange
        var users = new List<SysUser>
        {
            new SysUser
            {
                RowId = 1,
                UserName = "charlie",
                Password = "hash1",
                FullName = "Charlie",
                Email = "charlie@test.com",
                IsActive = true,
                CreationUser = "test"
            },
            new SysUser
            {
                RowId = 2,
                UserName = "alice",
                Password = "hash2",
                FullName = "Alice",
                Email = "alice@test.com",
                IsActive = true,
                CreationUser = "test"
            },
            new SysUser
            {
                RowId = 3,
                UserName = "bob",
                Password = "hash3",
                FullName = "Bob",
                Email = "bob@test.com",
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("alice", result[0].UserName); // Ordered by UserName
        Assert.Equal("bob", result[1].UserName);
        Assert.Equal("charlie", result[2].UserName);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoUsers()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_FiltersOutInactiveUsers()
    {
        // Arrange
        var users = new List<SysUser>
        {
            new SysUser
            {
                RowId = 1,
                UserName = "active_user",
                Password = "hash",
                FullName = "Active User",
                Email = "active@test.com",
                IsActive = true,
                CreationUser = "test"
            },
            new SysUser
            {
                RowId = 2,
                UserName = "inactive_user",
                Password = "hash",
                FullName = "Inactive User",
                Email = "inactive@test.com",
                IsActive = false, // Soft deleted
                CreationUser = "test"
            }
        };
        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("active_user", result[0].UserName);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsUser_WhenExists()
    {
        // Arrange
        var user = new SysUser
        {
            RowId = 1,
            UserName = "testuser",
            Password = "hashed_password",
            FullName = "Test User",
            FullNameE = "Test User EN",
            Email = "test@example.com",
            PhoneNumber = "123-456-7890",
            IsActive = true,
            IsAdmin = false,
            CreationUser = "test"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.RowId);
        Assert.Equal("testuser", result.UserName);
        Assert.Equal("Test User", result.FullName);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenUserIsInactive()
    {
        // Arrange
        var user = new SysUser
        {
            RowId = 1,
            UserName = "inactive",
            Password = "hash",
            FullName = "Inactive User",
            Email = "inactive@test.com",
            IsActive = false, // Soft deleted
            CreationUser = "test"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(1);

        // Assert
        Assert.Null(result); // Global query filter excludes inactive records
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CreatesNewUser_ReturnsGeneratedId()
    {
        // Arrange
        var user = new SysUser
        {
            UserName = "newuser",
            Password = "hashed_password_123",
            FullName = "New User",
            FullNameE = "New User EN",
            Email = "newuser@example.com",
            PhoneNumber = "555-1234",
            BranchId = 1,
            IsAdmin = false,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(user);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, user.RowId);
        Assert.NotNull(user.CreationDate);
        Assert.True(user.IsActive);

        // Verify in database
        var savedUser = await _context.Users.FindAsync(result);
        Assert.NotNull(savedUser);
        Assert.Equal("newuser", savedUser.UserName);
    }

    [Fact]
    public async Task CreateAsync_SetsCreationDate_WhenNotProvided()
    {
        // Arrange
        var user = new SysUser
        {
            UserName = "testuser",
            Password = "hash",
            FullName = "Test",
            Email = "test@test.com",
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateAsync(user);

        // Assert
        Assert.NotNull(user.CreationDate);
        Assert.True(user.CreationDate.Value <= DateTime.Now);
    }

    [Fact]
    public async Task CreateAsync_SetsIsActiveToTrue_ByDefault()
    {
        // Arrange
        var user = new SysUser
        {
            UserName = "testuser",
            Password = "hash",
            FullName = "Test",
            Email = "test@test.com",
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateAsync(user);

        // Assert
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task CreateAsync_WithNullOptionalFields_Succeeds()
    {
        // Arrange
        var user = new SysUser
        {
            UserName = "minimaluser",
            Password = "hash",
            FullName = "Minimal",
            Email = "minimal@test.com",
            FullNameE = null,
            PhoneNumber = null,
            BranchId = null,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(user);

        // Assert
        Assert.True(result > 0);
        var savedUser = await _context.Users.FindAsync(result);
        Assert.NotNull(savedUser);
        Assert.Null(savedUser.FullNameE);
        Assert.Null(savedUser.PhoneNumber);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesExistingUser_ReturnsRowsAffected()
    {
        // Arrange
        var user = new SysUser
        {
            UserName = "originaluser",
            Password = "hash",
            FullName = "Original Name",
            Email = "original@test.com",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Modify the user
        user.FullName = "Updated Name";
        user.Email = "updated@test.com";
        user.UpdateUser = "update_user";

        // Act
        var result = await _repository.UpdateAsync(user);

        // Assert
        Assert.Equal(1, result);
        Assert.NotNull(user.UpdateDate);

        // Verify in database
        var updatedUser = await _context.Users.FindAsync(user.RowId);
        Assert.NotNull(updatedUser);
        Assert.Equal("Updated Name", updatedUser.FullName);
        Assert.Equal("updated@test.com", updatedUser.Email);
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdateDate_Automatically()
    {
        // Arrange
        var user = new SysUser
        {
            UserName = "testuser",
            Password = "hash",
            FullName = "Test",
            Email = "test@test.com",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        user.FullName = "Updated";

        // Act
        await _repository.UpdateAsync(user);

        // Assert
        Assert.NotNull(user.UpdateDate);
        Assert.True(user.UpdateDate.Value <= DateTime.Now);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesUser_ReturnsRowsAffected()
    {
        // Arrange
        var user = new SysUser
        {
            UserName = "todelete",
            Password = "hash",
            FullName = "To Delete",
            Email = "delete@test.com",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        var userId = user.RowId;

        // Act
        var result = await _repository.DeleteAsync(userId);

        // Assert
        Assert.Equal(1, result);

        // Verify user is soft deleted (IsActive = false)
        var deletedUser = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.RowId == userId);
        Assert.NotNull(deletedUser);
        Assert.False(deletedUser.IsActive);
        Assert.NotNull(deletedUser.UpdateDate);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ReturnsZero()
    {
        // Act
        var result = await _repository.DeleteAsync(999);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task DeleteAsync_OnAlreadyDeletedUser_ReturnsOne()
    {
        // Arrange
        var user = new SysUser
        {
            UserName = "deleted",
            Password = "hash",
            FullName = "Already Deleted",
            Email = "deleted@test.com",
            IsActive = false, // Already soft deleted
            CreationUser = "test_user"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.DeleteAsync(user.RowId);

        // Assert
        Assert.Equal(1, result); // Can delete again (idempotent)
    }

    #endregion

    #region GetByBranchIdAsync Tests

    [Fact]
    public async Task GetByBranchIdAsync_ReturnsUsersForBranch()
    {
        // Arrange
        var users = new List<SysUser>
        {
            new SysUser
            {
                RowId = 1,
                UserName = "user1",
                Password = "hash",
                FullName = "User 1",
                Email = "user1@test.com",
                BranchId = 1,
                IsActive = true,
                CreationUser = "test"
            },
            new SysUser
            {
                RowId = 2,
                UserName = "user2",
                Password = "hash",
                FullName = "User 2",
                Email = "user2@test.com",
                BranchId = 1,
                IsActive = true,
                CreationUser = "test"
            },
            new SysUser
            {
                RowId = 3,
                UserName = "user3",
                Password = "hash",
                FullName = "User 3",
                Email = "user3@test.com",
                BranchId = 2,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByBranchIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, u => Assert.Equal(1, u.BranchId));
    }

    [Fact]
    public async Task GetByBranchIdAsync_ReturnsEmptyList_WhenNoUsersForBranch()
    {
        // Act
        var result = await _repository.GetByBranchIdAsync(999);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region GetByCompanyIdAsync Tests

    [Fact]
    public async Task GetByCompanyIdAsync_ReturnsUsersForCompany()
    {
        // Arrange
        var branches = new List<SysBranch>
        {
            new SysBranch
            {
                RowId = 1,
                ParRowId = 1,
                RowDesc = "Branch 1",
                RowDescE = "Branch 1",
                IsActive = true,
                CreationUser = "test"
            },
            new SysBranch
            {
                RowId = 2,
                ParRowId = 1,
                RowDesc = "Branch 2",
                RowDescE = "Branch 2",
                IsActive = true,
                CreationUser = "test"
            },
            new SysBranch
            {
                RowId = 3,
                ParRowId = 2,
                RowDesc = "Branch 3",
                RowDescE = "Branch 3",
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.Branches.AddRangeAsync(branches);
        await _context.SaveChangesAsync();

        var users = new List<SysUser>
        {
            new SysUser
            {
                RowId = 1,
                UserName = "user1",
                Password = "hash",
                FullName = "User 1",
                Email = "user1@test.com",
                BranchId = 1,
                IsActive = true,
                CreationUser = "test"
            },
            new SysUser
            {
                RowId = 2,
                UserName = "user2",
                Password = "hash",
                FullName = "User 2",
                Email = "user2@test.com",
                BranchId = 2,
                IsActive = true,
                CreationUser = "test"
            },
            new SysUser
            {
                RowId = 3,
                UserName = "user3",
                Password = "hash",
                FullName = "User 3",
                Email = "user3@test.com",
                BranchId = 3,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByCompanyIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, u => u.UserName == "user1");
        Assert.Contains(result, u => u.UserName == "user2");
    }

    [Fact]
    public async Task GetByCompanyIdAsync_ReturnsEmptyList_WhenNoUsersForCompany()
    {
        // Act
        var result = await _repository.GetByCompanyIdAsync(999);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region ForceLogoutAsync Tests

    [Fact]
    public async Task ForceLogoutAsync_SetsForceLogoutDateAndClearsTokens_ReturnsRowsAffected()
    {
        // Arrange
        var user = new SysUser
        {
            UserName = "logoutuser",
            Password = "hash",
            FullName = "Logout User",
            Email = "logout@test.com",
            RefreshToken = "old_token",
            RefreshTokenExpiry = DateTime.Now.AddDays(7),
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        var userId = user.RowId;

        // Act
        var result = await _repository.ForceLogoutAsync(userId, "admin_user");

        // Assert
        Assert.Equal(1, result);

        // Verify force logout was applied
        var loggedOutUser = await _context.Users.FindAsync(userId);
        Assert.NotNull(loggedOutUser);
        Assert.NotNull(loggedOutUser.ForceLogoutDate);
        Assert.Null(loggedOutUser.RefreshToken);
        Assert.Null(loggedOutUser.RefreshTokenExpiry);
        Assert.Equal("admin_user", loggedOutUser.UpdateUser);
    }

    [Fact]
    public async Task ForceLogoutAsync_WithNonExistentId_ReturnsZero()
    {
        // Act
        var result = await _repository.ForceLogoutAsync(999, "admin");

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region ChangePasswordAsync Tests

    [Fact]
    public async Task ChangePasswordAsync_UpdatesPassword_ReturnsRowsAffected()
    {
        // Arrange
        var user = new SysUser
        {
            UserName = "pwduser",
            Password = "old_hash",
            FullName = "Password User",
            Email = "pwd@test.com",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        var userId = user.RowId;

        var newPasswordHash = "new_hash_123";

        // Act
        var result = await _repository.ChangePasswordAsync(userId, newPasswordHash, "pwd_user");

        // Assert
        Assert.Equal(1, result);

        // Verify password was changed
        var updatedUser = await _context.Users.FindAsync(userId);
        Assert.NotNull(updatedUser);
        Assert.Equal(newPasswordHash, updatedUser.Password);
        Assert.Equal("pwd_user", updatedUser.UpdateUser);
        Assert.NotNull(updatedUser.UpdateDate);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithNonExistentId_ReturnsZero()
    {
        // Act
        var result = await _repository.ChangePasswordAsync(999, "new_hash", "user");

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region GetAdminUsersAsync Tests

    [Fact]
    public async Task GetAdminUsersAsync_ReturnsOnlyAdminUsers()
    {
        // Arrange
        var users = new List<SysUser>
        {
            new SysUser
            {
                RowId = 1,
                UserName = "admin1",
                Password = "hash",
                FullName = "Admin 1",
                Email = "admin1@test.com",
                IsAdmin = true,
                IsActive = true,
                CreationUser = "test"
            },
            new SysUser
            {
                RowId = 2,
                UserName = "user1",
                Password = "hash",
                FullName = "User 1",
                Email = "user1@test.com",
                IsAdmin = false,
                IsActive = true,
                CreationUser = "test"
            },
            new SysUser
            {
                RowId = 3,
                UserName = "admin2",
                Password = "hash",
                FullName = "Admin 2",
                Email = "admin2@test.com",
                IsAdmin = true,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAdminUsersAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, u => Assert.True(u.IsAdmin));
    }

    [Fact]
    public async Task GetAdminUsersAsync_ReturnsEmptyList_WhenNoAdminUsers()
    {
        // Arrange
        var user = new SysUser
        {
            UserName = "regularuser",
            Password = "hash",
            FullName = "Regular User",
            Email = "regular@test.com",
            IsAdmin = false,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAdminUsersAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task CreateAsync_WithNullUser_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _repository.CreateAsync(null!));
    }

    [Fact]
    public async Task UpdateAsync_WithNullUser_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _repository.UpdateAsync(null!));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetByIdAsync_WithZeroId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(0);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithNegativeId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(-1);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_WithVeryLongUserName_Succeeds()
    {
        // Arrange
        var longUserName = new string('A', 100); // Max length for UserName
        var user = new SysUser
        {
            UserName = longUserName,
            Password = "hash",
            FullName = "Test",
            Email = "test@test.com",
            CreationUser = "test"
        };

        // Act
        var result = await _repository.CreateAsync(user);

        // Assert
        Assert.True(result > 0);
    }

    [Fact]
    public async Task CreateAsync_WithSpecialCharactersInEmail_Succeeds()
    {
        // Arrange
        var user = new SysUser
        {
            UserName = "testuser",
            Password = "hash",
            FullName = "Test",
            Email = "test+special@example.co.uk",
            CreationUser = "test"
        };

        // Act
        var result = await _repository.CreateAsync(user);

        // Assert
        Assert.True(result > 0);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new UserRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new UserRepository(_context, null!));
    }

    #endregion
}
