using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for RoleRepository using EF Core InMemory provider.
/// Tests all CRUD operations, exception scenarios, and null handling.
/// </summary>
public class RoleRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly RoleRepository _repository;
    private readonly Mock<ILogger<RoleRepository>> _loggerMock;

    public RoleRepositoryTests()
    {
        // Create InMemory database with unique name for each test instance
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_Role_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<RoleRepository>>();
        _repository = new RoleRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllRoles_OrderedByRowDesc()
    {
        // Arrange
        var roles = new List<SysRole>
        {
            new SysRole
            {
                RowId = 1,
                RowDesc = "مدير النظام",
                RowDescE = "System Administrator",
                RoleCode = "SYSADMIN",
                IsActive = true,
                CreationUser = "test"
            },
            new SysRole
            {
                RowId = 2,
                RowDesc = "محاسب",
                RowDescE = "Accountant",
                RoleCode = "ACCOUNTANT",
                IsActive = true,
                CreationUser = "test"
            },
            new SysRole
            {
                RowId = 3,
                RowDesc = "مستخدم",
                RowDescE = "User",
                RoleCode = "USER",
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.Roles.AddRangeAsync(roles);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("محاسب", result[0].RowDesc); // Ordered by RowDesc
        Assert.Equal("مدير النظام", result[1].RowDesc);
        Assert.Equal("مستخدم", result[2].RowDesc);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoRoles()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_FiltersOutInactiveRoles()
    {
        // Arrange
        var roles = new List<SysRole>
        {
            new SysRole
            {
                RowId = 1,
                RowDesc = "Active Role",
                RowDescE = "Active Role",
                RoleCode = "ACTIVE",
                IsActive = true,
                CreationUser = "test"
            },
            new SysRole
            {
                RowId = 2,
                RowDesc = "Inactive Role",
                RowDescE = "Inactive Role",
                RoleCode = "INACTIVE",
                IsActive = false, // Soft deleted
                CreationUser = "test"
            }
        };
        await _context.Roles.AddRangeAsync(roles);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Active Role", result[0].RowDesc);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsRole_WhenExists()
    {
        // Arrange
        var role = new SysRole
        {
            RowId = 1,
            RowDesc = "دور الاختبار",
            RowDescE = "Test Role",
            RoleCode = "TEST-ROLE",
            Description = "Test role description",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Roles.AddAsync(role);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.RowId);
        Assert.Equal("Test Role", result.RowDescE);
        Assert.Equal("TEST-ROLE", result.RoleCode);
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
    public async Task GetByIdAsync_ReturnsNull_WhenRoleIsInactive()
    {
        // Arrange
        var role = new SysRole
        {
            RowId = 1,
            RowDesc = "Inactive Role",
            RowDescE = "Inactive Role",
            RoleCode = "INACTIVE",
            IsActive = false, // Soft deleted
            CreationUser = "test"
        };
        await _context.Roles.AddAsync(role);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(1);

        // Assert
        Assert.Null(result); // Global query filter excludes inactive records
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CreatesNewRole_ReturnsGeneratedId()
    {
        // Arrange
        var role = new SysRole
        {
            RowDesc = "دور جديد",
            RowDescE = "New Role",
            RoleCode = "NEW-ROLE",
            Description = "A new test role",
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(role);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, role.RowId);
        Assert.NotNull(role.CreationDate);
        Assert.True(role.IsActive);

        // Verify in database
        var savedRole = await _context.Roles.FindAsync(result);
        Assert.NotNull(savedRole);
        Assert.Equal("NEW-ROLE", savedRole.RoleCode);
    }

    [Fact]
    public async Task CreateAsync_SetsCreationDate_WhenNotProvided()
    {
        // Arrange
        var role = new SysRole
        {
            RowDesc = "Test Role",
            RowDescE = "Test Role",
            RoleCode = "TEST",
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateAsync(role);

        // Assert
        Assert.NotNull(role.CreationDate);
        Assert.True(role.CreationDate.Value <= DateTime.Now);
    }

    [Fact]
    public async Task CreateAsync_SetsIsActiveToTrue_ByDefault()
    {
        // Arrange
        var role = new SysRole
        {
            RowDesc = "Test Role",
            RowDescE = "Test Role",
            RoleCode = "TEST",
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateAsync(role);

        // Assert
        Assert.True(role.IsActive);
    }

    [Fact]
    public async Task CreateAsync_WithNullOptionalFields_Succeeds()
    {
        // Arrange
        var role = new SysRole
        {
            RowDesc = "Minimal Role",
            RowDescE = "Minimal Role",
            RoleCode = "MIN",
            Description = null,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(role);

        // Assert
        Assert.True(result > 0);
        var savedRole = await _context.Roles.FindAsync(result);
        Assert.NotNull(savedRole);
        Assert.Null(savedRole.Description);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesExistingRole_ReturnsRowsAffected()
    {
        // Arrange
        var role = new SysRole
        {
            RowDesc = "Original Name",
            RowDescE = "Original Name",
            RoleCode = "ORIG",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Roles.AddAsync(role);
        await _context.SaveChangesAsync();

        // Modify the role
        role.RowDescE = "Updated Name";
        role.Description = "Updated description";
        role.UpdateUser = "update_user";

        // Act
        var result = await _repository.UpdateAsync(role);

        // Assert
        Assert.Equal(1, result);
        Assert.NotNull(role.UpdateDate);

        // Verify in database
        var updatedRole = await _context.Roles.FindAsync(role.RowId);
        Assert.NotNull(updatedRole);
        Assert.Equal("Updated Name", updatedRole.RowDescE);
        Assert.Equal("Updated description", updatedRole.Description);
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdateDate_Automatically()
    {
        // Arrange
        var role = new SysRole
        {
            RowDesc = "Test Role",
            RowDescE = "Test Role",
            RoleCode = "TEST",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Roles.AddAsync(role);
        await _context.SaveChangesAsync();

        role.RowDescE = "Updated Description";

        // Act
        await _repository.UpdateAsync(role);

        // Assert
        Assert.NotNull(role.UpdateDate);
        Assert.True(role.UpdateDate.Value <= DateTime.Now);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesRole_ReturnsRowsAffected()
    {
        // Arrange
        var role = new SysRole
        {
            RowDesc = "To Delete",
            RowDescE = "To Delete",
            RoleCode = "DEL",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Roles.AddAsync(role);
        await _context.SaveChangesAsync();
        var roleId = role.RowId;

        // Act
        var result = await _repository.DeleteAsync(roleId);

        // Assert
        Assert.Equal(1, result);

        // Verify role is soft deleted (IsActive = false)
        var deletedRole = await _context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.RowId == roleId);
        Assert.NotNull(deletedRole);
        Assert.False(deletedRole.IsActive);
        Assert.NotNull(deletedRole.UpdateDate);
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
    public async Task DeleteAsync_OnAlreadyDeletedRole_ReturnsOne()
    {
        // Arrange
        var role = new SysRole
        {
            RowDesc = "Already Deleted",
            RowDescE = "Already Deleted",
            RoleCode = "DELETED",
            IsActive = false, // Already soft deleted
            CreationUser = "test_user"
        };
        await _context.Roles.AddAsync(role);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.DeleteAsync(role.RowId);

        // Assert
        Assert.Equal(1, result); // Can delete again (idempotent)
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task CreateAsync_WithNullRole_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _repository.CreateAsync(null!));
    }

    [Fact]
    public async Task UpdateAsync_WithNullRole_ThrowsException()
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
    public async Task CreateAsync_WithVeryLongDescriptions_Succeeds()
    {
        // Arrange
        var longString = new string('A', 200); // Max length for RowDesc
        var role = new SysRole
        {
            RowDesc = longString,
            RowDescE = longString,
            RoleCode = "LONG",
            CreationUser = "test"
        };

        // Act
        var result = await _repository.CreateAsync(role);

        // Assert
        Assert.True(result > 0);
    }

    [Fact]
    public async Task CreateAsync_WithSpecialCharacters_Succeeds()
    {
        // Arrange
        var role = new SysRole
        {
            RowDesc = "دور !@#$%^&*()",
            RowDescE = "Role !@#$%^&*()",
            RoleCode = "SPECIAL",
            CreationUser = "test"
        };

        // Act
        var result = await _repository.CreateAsync(role);

        // Assert
        Assert.True(result > 0);
    }

    [Fact]
    public async Task CreateAsync_WithSameRoleCodeForDifferentRoles_Succeeds()
    {
        // Arrange - Note: Business logic validation should be in application layer
        var role1 = new SysRole
        {
            RowDesc = "Role 1",
            RowDescE = "Role 1",
            RoleCode = "DUPLICATE",
            CreationUser = "test"
        };
        var role2 = new SysRole
        {
            RowDesc = "Role 2",
            RowDescE = "Role 2",
            RoleCode = "DUPLICATE",
            CreationUser = "test"
        };

        // Act
        var result1 = await _repository.CreateAsync(role1);
        var result2 = await _repository.CreateAsync(role2);

        // Assert
        Assert.True(result1 > 0);
        Assert.True(result2 > 0);
        Assert.NotEqual(result1, result2);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new RoleRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new RoleRepository(_context, null!));
    }

    #endregion
}
