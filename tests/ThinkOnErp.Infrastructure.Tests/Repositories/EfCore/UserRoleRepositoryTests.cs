using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for UserRoleRepository using EF Core InMemory provider.
/// Tests composite key operations, many-to-many relationship, and CRUD operations.
/// </summary>
public class UserRoleRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly UserRoleRepository _repository;
    private readonly Mock<ILogger<UserRoleRepository>> _loggerMock;

    public UserRoleRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_UserRole_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<UserRoleRepository>>();
        _repository = new UserRoleRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Test Data Setup

    private async Task<(SysUser user, SysRole role)> CreateTestUserAndRoleAsync()
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

        var role = new SysRole
        {
            RowId = 1,
            RowDesc = "Test Role",
            RowDescE = "Test Role",
            IsActive = true,
            CreationUser = "test"
        };

        await _context.Companies.AddAsync(company);
        await _context.Branches.AddAsync(branch);
        await _context.Users.AddAsync(user);
        await _context.Roles.AddAsync(role);
        await _context.SaveChangesAsync();

        return (user, role);
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Fact]
    public async Task GetByUserIdAsync_ReturnsRoleAssignments_OrderedByRoleDesc()
    {
        // Arrange
        var (user, _) = await CreateTestUserAndRoleAsync();
        
        var role2 = new SysRole
        {
            RowId = 2,
            RowDesc = "Role B",
            RowDescE = "Role B",
            IsActive = true,
            CreationUser = "test"
        };
        var role3 = new SysRole
        {
            RowId = 3,
            RowDesc = "Role A",
            RowDescE = "Role A",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Roles.AddRangeAsync(role2, role3);
        await _context.SaveChangesAsync();

        var userRoles = new List<SysUserRole>
        {
            new SysUserRole
            {
                UserId = user.RowId,
                RoleId = 1,
                CreationUser = "test"
            },
            new SysUserRole
            {
                UserId = user.RowId,
                RoleId = 2,
                CreationUser = "test"
            },
            new SysUserRole
            {
                UserId = user.RowId,
                RoleId = 3,
                CreationUser = "test"
            }
        };
        await _context.UserRoles.AddRangeAsync(userRoles);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserIdAsync(user.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("Role A", result[0].Role!.RowDesc);
        Assert.Equal("Role B", result[1].Role!.RowDesc);
        Assert.Equal("Test Role", result[2].Role!.RowDesc);
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsEmptyList_WhenNoRoles()
    {
        // Arrange
        var (user, _) = await CreateTestUserAndRoleAsync();

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
        var (user, role) = await CreateTestUserAndRoleAsync();
        
        var userRole = new SysUserRole
        {
            UserId = user.RowId,
            RoleId = role.RowId,
            CreationUser = "test"
        };
        await _context.UserRoles.AddAsync(userRole);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserIdAsync(user.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.NotNull(result[0].User);
        Assert.NotNull(result[0].Role);
        Assert.Equal("Test User", result[0].User.FullName);
        Assert.Equal("Test Role", result[0].Role.RowDesc);
    }

    #endregion

    #region GetByRoleIdAsync Tests

    [Fact]
    public async Task GetByRoleIdAsync_ReturnsUserAssignments_OrderedByFullName()
    {
        // Arrange
        var (_, role) = await CreateTestUserAndRoleAsync();
        
        var user2 = new SysUser
        {
            RowId = 2,
            CompanyId = 1,
            BranchId = 1,
            UserName = "user2",
            PasswordHash = "hash",
            FullName = "User B",
            Email = "user2@test.com",
            IsActive = true,
            CreationUser = "test"
        };
        var user3 = new SysUser
        {
            RowId = 3,
            CompanyId = 1,
            BranchId = 1,
            UserName = "user3",
            PasswordHash = "hash",
            FullName = "User A",
            Email = "user3@test.com",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Users.AddRangeAsync(user2, user3);
        await _context.SaveChangesAsync();

        var userRoles = new List<SysUserRole>
        {
            new SysUserRole
            {
                UserId = 1,
                RoleId = role.RowId,
                CreationUser = "test"
            },
            new SysUserRole
            {
                UserId = 2,
                RoleId = role.RowId,
                CreationUser = "test"
            },
            new SysUserRole
            {
                UserId = 3,
                RoleId = role.RowId,
                CreationUser = "test"
            }
        };
        await _context.UserRoles.AddRangeAsync(userRoles);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByRoleIdAsync(role.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("User A", result[0].User!.FullName);
        Assert.Equal("User B", result[1].User!.FullName);
        Assert.Equal("Test User", result[2].User!.FullName);
    }

    [Fact]
    public async Task GetByRoleIdAsync_ReturnsEmptyList_WhenNoUsers()
    {
        // Arrange
        var (_, role) = await CreateTestUserAndRoleAsync();

        // Act
        var result = await _repository.GetByRoleIdAsync(role.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region GetByIdAsync Tests (Composite Key)

    [Fact]
    public async Task GetByIdAsync_ReturnsUserRole_WhenExists()
    {
        // Arrange
        var (user, role) = await CreateTestUserAndRoleAsync();
        
        var userRole = new SysUserRole
        {
            UserId = user.RowId,
            RoleId = role.RowId,
            AssignedBy = 1,
            CreationUser = "test_user"
        };
        await _context.UserRoles.AddAsync(userRole);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(user.RowId, role.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.RowId, result.UserId);
        Assert.Equal(role.RowId, result.RoleId);
        Assert.Equal(1, result.AssignedBy);
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
        var (user, role) = await CreateTestUserAndRoleAsync();
        
        var userRole = new SysUserRole
        {
            UserId = user.RowId,
            RoleId = role.RowId,
            CreationUser = "test"
        };
        await _context.UserRoles.AddAsync(userRole);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(user.RowId, 999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenOnlyRoleIdMatches()
    {
        // Arrange
        var (user, role) = await CreateTestUserAndRoleAsync();
        
        var userRole = new SysUserRole
        {
            UserId = user.RowId,
            RoleId = role.RowId,
            CreationUser = "test"
        };
        await _context.UserRoles.AddAsync(userRole);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(999, role.RowId);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region HasRoleAsync Tests

    [Fact]
    public async Task HasRoleAsync_ReturnsTrue_WhenUserHasRole()
    {
        // Arrange
        var (user, role) = await CreateTestUserAndRoleAsync();
        
        var userRole = new SysUserRole
        {
            UserId = user.RowId,
            RoleId = role.RowId,
            CreationUser = "test"
        };
        await _context.UserRoles.AddAsync(userRole);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.HasRoleAsync(user.RowId, role.RowId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasRoleAsync_ReturnsFalse_WhenUserDoesNotHaveRole()
    {
        // Arrange
        var (user, role) = await CreateTestUserAndRoleAsync();

        // Act
        var result = await _repository.HasRoleAsync(user.RowId, role.RowId);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CreatesNewUserRole_ReturnsGeneratedId()
    {
        // Arrange
        var (user, role) = await CreateTestUserAndRoleAsync();
        
        var userRole = new SysUserRole
        {
            UserId = user.RowId,
            RoleId = role.RowId,
            AssignedBy = 1,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(userRole);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, userRole.RowId);
        Assert.NotNull(userRole.CreationDate);
        Assert.NotNull(userRole.AssignedDate);

        // Verify in database
        var savedUserRole = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == user.RowId && ur.RoleId == role.RowId);
        Assert.NotNull(savedUserRole);
        Assert.Equal(1, savedUserRole.AssignedBy);
    }

    [Fact]
    public async Task CreateAsync_SetsCreationDateAndAssignedDate_WhenNotProvided()
    {
        // Arrange
        var (user, role) = await CreateTestUserAndRoleAsync();
        
        var userRole = new SysUserRole
        {
            UserId = user.RowId,
            RoleId = role.RowId,
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateAsync(userRole);

        // Assert
        Assert.NotNull(userRole.CreationDate);
        Assert.NotNull(userRole.AssignedDate);
        Assert.True(userRole.CreationDate.Value <= DateTime.Now);
        Assert.True(userRole.AssignedDate.Value <= DateTime.Now);
    }

    #endregion

    #region AssignRoleAsync Tests

    [Fact]
    public async Task AssignRoleAsync_CreatesNewAssignment_WhenNotExists()
    {
        // Arrange
        var (user, role) = await CreateTestUserAndRoleAsync();

        // Act
        var result = await _repository.AssignRoleAsync(user.RowId, role.RowId, 1, "test_user");

        // Assert
        Assert.True(result > 0);

        var userRole = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == user.RowId && ur.RoleId == role.RowId);
        Assert.NotNull(userRole);
        Assert.Equal(1, userRole.AssignedBy);
        Assert.NotNull(userRole.AssignedDate);
    }

    [Fact]
    public async Task AssignRoleAsync_ReturnsExistingId_WhenAlreadyAssigned()
    {
        // Arrange
        var (user, role) = await CreateTestUserAndRoleAsync();
        
        var userRole = new SysUserRole
        {
            UserId = user.RowId,
            RoleId = role.RowId,
            CreationUser = "original_user"
        };
        await _context.UserRoles.AddAsync(userRole);
        await _context.SaveChangesAsync();
        var originalId = userRole.RowId;

        // Act
        var result = await _repository.AssignRoleAsync(user.RowId, role.RowId, 1, "test_user");

        // Assert
        Assert.Equal(originalId, result); // Returns existing ID

        // Verify no duplicate was created
        var count = await _context.UserRoles
            .CountAsync(ur => ur.UserId == user.RowId && ur.RoleId == role.RowId);
        Assert.Equal(1, count);
    }

    #endregion

    #region DeleteAsync Tests (Composite Key)

    [Fact]
    public async Task DeleteAsync_DeletesUserRole_ReturnsRowsAffected()
    {
        // Arrange
        var (user, role) = await CreateTestUserAndRoleAsync();
        
        var userRole = new SysUserRole
        {
            UserId = user.RowId,
            RoleId = role.RowId,
            CreationUser = "test_user"
        };
        await _context.UserRoles.AddAsync(userRole);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.DeleteAsync(user.RowId, role.RowId);

        // Assert
        Assert.Equal(1, result);

        // Verify user role is deleted
        var deletedUserRole = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == user.RowId && ur.RoleId == role.RowId);
        Assert.Null(deletedUserRole);
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

    #region RemoveRoleAsync Tests

    [Fact]
    public async Task RemoveRoleAsync_RemovesRole_ReturnsTrue()
    {
        // Arrange
        var (user, role) = await CreateTestUserAndRoleAsync();
        
        var userRole = new SysUserRole
        {
            UserId = user.RowId,
            RoleId = role.RowId,
            CreationUser = "test"
        };
        await _context.UserRoles.AddAsync(userRole);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.RemoveRoleAsync(user.RowId, role.RowId);

        // Assert
        Assert.True(result);

        var deletedUserRole = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == user.RowId && ur.RoleId == role.RowId);
        Assert.Null(deletedUserRole);
    }

    [Fact]
    public async Task RemoveRoleAsync_WithNonExistentAssignment_ReturnsFalse()
    {
        // Arrange
        var (user, role) = await CreateTestUserAndRoleAsync();

        // Act
        var result = await _repository.RemoveRoleAsync(user.RowId, role.RowId);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region RemoveAllRolesAsync Tests

    [Fact]
    public async Task RemoveAllRolesAsync_RemovesAllUserRoles_ReturnsCount()
    {
        // Arrange
        var (user, role) = await CreateTestUserAndRoleAsync();
        
        var role2 = new SysRole
        {
            RowId = 2,
            RowDesc = "Role 2",
            RowDescE = "Role 2",
            IsActive = true,
            CreationUser = "test"
        };
        var role3 = new SysRole
        {
            RowId = 3,
            RowDesc = "Role 3",
            RowDescE = "Role 3",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Roles.AddRangeAsync(role2, role3);
        await _context.SaveChangesAsync();

        var userRoles = new List<SysUserRole>
        {
            new SysUserRole { UserId = user.RowId, RoleId = role.RowId, CreationUser = "test" },
            new SysUserRole { UserId = user.RowId, RoleId = role2.RowId, CreationUser = "test" },
            new SysUserRole { UserId = user.RowId, RoleId = role3.RowId, CreationUser = "test" }
        };
        await _context.UserRoles.AddRangeAsync(userRoles);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.RemoveAllRolesAsync(user.RowId);

        // Assert
        Assert.Equal(3, result);

        // Verify all roles removed
        var remainingRoles = await _context.UserRoles
            .Where(ur => ur.UserId == user.RowId)
            .ToListAsync();
        Assert.Empty(remainingRoles);
    }

    [Fact]
    public async Task RemoveAllRolesAsync_WithNoRoles_ReturnsZero()
    {
        // Arrange
        var (user, _) = await CreateTestUserAndRoleAsync();

        // Act
        var result = await _repository.RemoveAllRolesAsync(user.RowId);

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new UserRoleRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new UserRoleRepository(_context, null!));
    }

    #endregion
}
