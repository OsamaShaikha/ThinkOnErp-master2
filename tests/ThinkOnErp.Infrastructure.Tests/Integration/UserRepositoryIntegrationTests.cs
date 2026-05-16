using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oracle.EntityFrameworkCore;
using Xunit;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;

namespace ThinkOnErp.Infrastructure.Tests.Integration;

/// <summary>
/// Integration tests for UserRepository using real Oracle database connection.
/// Tests EF Core implementation against actual Oracle database.
/// 
/// **Validates: Requirements REQ-12**
/// - Requirement 12: Testing Strategy - Integration tests that verify database operations
/// - Tests CRUD operations with EF Core
/// - Tests authentication scenarios (refresh tokens, force logout)
/// - Tests transaction rollback
/// - Verifies data persistence
/// - Tests multi-tenancy filtering by branch and company
/// </summary>
public class UserRepositoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ThinkOnErpDbContext _context;
    private readonly IUserRepository _repository;
    private readonly ILogger<UserRepository> _logger;
    private readonly List<long> _createdIds = new();

    public UserRepositoryIntegrationTests()
    {
        // Setup configuration with Oracle connection string
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OracleDb"] = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=178.104.126.99)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XEPDB1)));User Id=THINKON_ERP;Password=THINKON_ERP;Pooling=true;Min Pool Size=5;Max Pool Size=100;Connection Timeout=15;"
            })
            .Build();

        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Add DbContext with Oracle provider
        services.AddDbContext<ThinkOnErpDbContext>(options =>
        {
            options.UseOracle(
                configuration.GetConnectionString("OracleDb"),
                oracleOptions =>
                {
                    oracleOptions.UseOracleSQLCompatibility("11");
                    oracleOptions.CommandTimeout(30);
                });
        });

        // Add repository
        services.AddScoped<IUserRepository, UserRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ThinkOnErpDbContext>();
        _repository = _serviceProvider.GetRequiredService<IUserRepository>();
        _logger = _serviceProvider.GetRequiredService<ILogger<UserRepository>>();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnUsersFromDatabase()
    {
        // Act
        var users = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(users);
        Assert.NotEmpty(users);
        Assert.All(users, u =>
        {
            Assert.True(u.RowId > 0);
            Assert.False(string.IsNullOrWhiteSpace(u.UserName));
            Assert.False(string.IsNullOrWhiteSpace(u.FullName));
        });
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnUser()
    {
        // Arrange - Get first user from database
        var users = await _repository.GetAllAsync();
        Assert.NotEmpty(users);
        var existingId = users.First().RowId;

        // Act
        var user = await _repository.GetByIdAsync(existingId);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(existingId, user.RowId);
        Assert.False(string.IsNullOrWhiteSpace(user.UserName));
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var invalidId = -999999L;

        // Act
        var user = await _repository.GetByIdAsync(invalidId);

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task CreateAsync_ShouldInsertUserAndReturnGeneratedId()
    {
        // Arrange - Get a valid branch ID from existing users
        var existingUsers = await _repository.GetAllAsync();
        Assert.NotEmpty(existingUsers);
        var branchId = existingUsers.First().BranchId;

        var uniqueUsername = $"testuser_{Guid.NewGuid().ToString().Substring(0, 8)}";
        var newUser = new SysUser
        {
            UserName = uniqueUsername,
            Password = "hashed_password_123",
            FullName = $"Test User {Guid.NewGuid()}",
            FullNameE = $"Test User EN {Guid.NewGuid()}",
            Email = $"{uniqueUsername}@test.com",
            PhoneNumber = "123-456-7890",
            BranchId = branchId,
            IsAdmin = false,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        var generatedId = await _repository.CreateAsync(newUser);
        _createdIds.Add(generatedId);

        // Assert
        Assert.True(generatedId > 0, "Generated ID should be positive");
        Assert.Equal(generatedId, newUser.RowId);

        // Verify persistence
        var retrievedUser = await _repository.GetByIdAsync(generatedId);
        Assert.NotNull(retrievedUser);
        Assert.Equal(newUser.UserName, retrievedUser.UserName);
        Assert.Equal(newUser.FullName, retrievedUser.FullName);
        Assert.Equal(newUser.Email, retrievedUser.Email);
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyExistingUser()
    {
        // Arrange - Create a user first
        var existingUsers = await _repository.GetAllAsync();
        Assert.NotEmpty(existingUsers);
        var branchId = existingUsers.First().BranchId;

        var uniqueUsername = $"updateuser_{Guid.NewGuid().ToString().Substring(0, 8)}";
        var newUser = new SysUser
        {
            UserName = uniqueUsername,
            Password = "hashed_password",
            FullName = $"Update Test {Guid.NewGuid()}",
            Email = $"{uniqueUsername}@test.com",
            BranchId = branchId,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newUser);
        _createdIds.Add(createdId);

        // Act - Update the user
        var userToUpdate = await _repository.GetByIdAsync(createdId);
        Assert.NotNull(userToUpdate);
        
        var updatedFullName = $"Updated User {Guid.NewGuid()}";
        var updatedEmail = $"updated_{uniqueUsername}@test.com";
        userToUpdate.FullName = updatedFullName;
        userToUpdate.Email = updatedEmail;
        userToUpdate.UpdateUser = "IntegrationTest";
        userToUpdate.UpdateDate = DateTime.Now;

        var rowsAffected = await _repository.UpdateAsync(userToUpdate);

        // Assert
        Assert.Equal(1, rowsAffected);

        // Verify the update persisted
        var retrievedUser = await _repository.GetByIdAsync(createdId);
        Assert.NotNull(retrievedUser);
        Assert.Equal(updatedFullName, retrievedUser.FullName);
        Assert.Equal(updatedEmail, retrievedUser.Email);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteUser()
    {
        // Arrange - Create a user first
        var existingUsers = await _repository.GetAllAsync();
        Assert.NotEmpty(existingUsers);
        var branchId = existingUsers.First().BranchId;

        var uniqueUsername = $"deleteuser_{Guid.NewGuid().ToString().Substring(0, 8)}";
        var newUser = new SysUser
        {
            UserName = uniqueUsername,
            Password = "hashed_password",
            FullName = $"Delete Test {Guid.NewGuid()}",
            Email = $"{uniqueUsername}@test.com",
            BranchId = branchId,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newUser);

        // Act - Delete the user (soft delete)
        var rowsAffected = await _repository.DeleteAsync(createdId);

        // Assert
        Assert.Equal(1, rowsAffected);

        // Verify the user is soft deleted (not returned by GetByIdAsync due to query filter)
        var retrievedUser = await _repository.GetByIdAsync(createdId);
        Assert.Null(retrievedUser);
    }

    [Fact]
    public async Task GetByBranchIdAsync_ShouldReturnUsersForSpecificBranch()
    {
        // Arrange - Get a valid branch ID
        var allUsers = await _repository.GetAllAsync();
        Assert.NotEmpty(allUsers);
        var branchId = allUsers.First().BranchId;
        Assert.NotNull(branchId);

        // Act
        var branchUsers = await _repository.GetByBranchIdAsync(branchId.Value);

        // Assert
        Assert.NotNull(branchUsers);
        Assert.NotEmpty(branchUsers);
        Assert.All(branchUsers, u => Assert.Equal(branchId, u.BranchId));
    }

    [Fact]
    public async Task GetByCompanyIdAsync_ShouldReturnUsersForSpecificCompany()
    {
        // Arrange - Get a valid company ID through branch
        var allUsers = await _repository.GetAllAsync();
        Assert.NotEmpty(allUsers);
        var userWithBranch = allUsers.FirstOrDefault(u => u.BranchId.HasValue);
        
        if (userWithBranch == null)
        {
            // Skip test if no user has a branch
            return;
        }

        var branch = await _context.Branches.FindAsync(userWithBranch.BranchId.Value);
        if (branch == null || !branch.ParRowId.HasValue)
        {
            // Skip test if branch doesn't have a company
            return;
        }

        var companyId = branch.ParRowId.Value;

        // Act
        var companyUsers = await _repository.GetByCompanyIdAsync(companyId);

        // Assert
        Assert.NotNull(companyUsers);
        Assert.NotEmpty(companyUsers);
    }

    [Fact]
    public async Task ForceLogoutAsync_ShouldClearRefreshTokenAndSetForceLogoutDate()
    {
        // Arrange - Create a user with refresh token
        var existingUsers = await _repository.GetAllAsync();
        Assert.NotEmpty(existingUsers);
        var branchId = existingUsers.First().BranchId;

        var uniqueUsername = $"logoutuser_{Guid.NewGuid().ToString().Substring(0, 8)}";
        var newUser = new SysUser
        {
            UserName = uniqueUsername,
            Password = "hashed_password",
            FullName = $"Logout Test {Guid.NewGuid()}",
            Email = $"{uniqueUsername}@test.com",
            BranchId = branchId,
            RefreshToken = "test_refresh_token",
            RefreshTokenExpiry = DateTime.Now.AddDays(7),
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newUser);
        _createdIds.Add(createdId);

        // Act - Force logout
        var rowsAffected = await _repository.ForceLogoutAsync(createdId, "IntegrationTest");

        // Assert
        Assert.Equal(1, rowsAffected);

        // Verify force logout was applied
        var loggedOutUser = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.RowId == createdId);
        Assert.NotNull(loggedOutUser);
        Assert.NotNull(loggedOutUser.ForceLogoutDate);
        Assert.Null(loggedOutUser.RefreshToken);
        Assert.Null(loggedOutUser.RefreshTokenExpiry);
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldUpdatePasswordHash()
    {
        // Arrange - Create a user first
        var existingUsers = await _repository.GetAllAsync();
        Assert.NotEmpty(existingUsers);
        var branchId = existingUsers.First().BranchId;

        var uniqueUsername = $"pwduser_{Guid.NewGuid().ToString().Substring(0, 8)}";
        var newUser = new SysUser
        {
            UserName = uniqueUsername,
            Password = "old_hashed_password",
            FullName = $"Password Test {Guid.NewGuid()}",
            Email = $"{uniqueUsername}@test.com",
            BranchId = branchId,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newUser);
        _createdIds.Add(createdId);

        var newPasswordHash = "new_hashed_password_123";

        // Act - Change password
        var rowsAffected = await _repository.ChangePasswordAsync(createdId, newPasswordHash, "IntegrationTest");

        // Assert
        Assert.Equal(1, rowsAffected);

        // Verify password was changed
        var updatedUser = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.RowId == createdId);
        Assert.NotNull(updatedUser);
        Assert.Equal(newPasswordHash, updatedUser.Password);
    }

    [Fact]
    public async Task GetAdminUsersAsync_ShouldReturnOnlyAdminUsers()
    {
        // Act
        var adminUsers = await _repository.GetAdminUsersAsync();

        // Assert
        Assert.NotNull(adminUsers);
        if (adminUsers.Any())
        {
            Assert.All(adminUsers, u => Assert.True(u.IsAdmin));
        }
    }

    [Fact]
    public async Task TransactionRollback_ShouldNotPersistChanges()
    {
        // Arrange
        var existingUsers = await _repository.GetAllAsync();
        Assert.NotEmpty(existingUsers);
        var branchId = existingUsers.First().BranchId;

        var uniqueUsername = $"rollbackuser_{Guid.NewGuid().ToString().Substring(0, 8)}";
        var newUser = new SysUser
        {
            UserName = uniqueUsername,
            Password = "hashed_password",
            FullName = $"Rollback Test {Guid.NewGuid()}",
            Email = $"{uniqueUsername}@test.com",
            BranchId = branchId,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act - Create within a transaction and rollback
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            
            var generatedId = newUser.RowId;
            Assert.True(generatedId > 0);

            // Rollback the transaction
            await transaction.RollbackAsync();

            // Assert - Verify the user was not persisted
            var retrievedUser = await _repository.GetByIdAsync(generatedId);
            Assert.Null(retrievedUser);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [Fact]
    public async Task TransactionCommit_ShouldPersistChanges()
    {
        // Arrange
        var existingUsers = await _repository.GetAllAsync();
        Assert.NotEmpty(existingUsers);
        var branchId = existingUsers.First().BranchId;

        var uniqueUsername = $"commituser_{Guid.NewGuid().ToString().Substring(0, 8)}";
        var newUser = new SysUser
        {
            UserName = uniqueUsername,
            Password = "hashed_password",
            FullName = $"Commit Test {Guid.NewGuid()}",
            Email = $"{uniqueUsername}@test.com",
            BranchId = branchId,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        long generatedId = 0;

        // Act - Create within a transaction and commit
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            
            generatedId = newUser.RowId;
            _createdIds.Add(generatedId);
            Assert.True(generatedId > 0);

            // Commit the transaction
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // Assert - Verify the user was persisted
        var retrievedUser = await _repository.GetByIdAsync(generatedId);
        Assert.NotNull(retrievedUser);
        Assert.Equal(newUser.UserName, retrievedUser.UserName);
    }

    [Fact]
    public async Task SequenceGeneration_ShouldGenerateUniqueIds()
    {
        // Arrange
        var existingUsers = await _repository.GetAllAsync();
        Assert.NotEmpty(existingUsers);
        var branchId = existingUsers.First().BranchId;

        var username1 = $"sequser1_{Guid.NewGuid().ToString().Substring(0, 8)}";
        var username2 = $"sequser2_{Guid.NewGuid().ToString().Substring(0, 8)}";

        var user1 = new SysUser
        {
            UserName = username1,
            Password = "hash",
            FullName = $"Sequence Test 1 {Guid.NewGuid()}",
            Email = $"{username1}@test.com",
            BranchId = branchId,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        var user2 = new SysUser
        {
            UserName = username2,
            Password = "hash",
            FullName = $"Sequence Test 2 {Guid.NewGuid()}",
            Email = $"{username2}@test.com",
            BranchId = branchId,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        var id1 = await _repository.CreateAsync(user1);
        var id2 = await _repository.CreateAsync(user2);
        _createdIds.Add(id1);
        _createdIds.Add(id2);

        // Assert
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task EagerLoading_ShouldLoadBranch()
    {
        // Arrange - Get a user that has a branch
        var users = await _repository.GetAllAsync();
        var userWithBranch = users.FirstOrDefault(u => u.BranchId.HasValue);
        
        if (userWithBranch == null)
        {
            // Skip test if no user has a branch
            return;
        }

        // Act
        var user = await _repository.GetByIdAsync(userWithBranch.RowId);

        // Assert
        Assert.NotNull(user);
        if (user.BranchId.HasValue)
        {
            Assert.NotNull(user.Branch);
            Assert.Equal(user.BranchId.Value, user.Branch.RowId);
        }
    }

    public void Dispose()
    {
        // Cleanup - Delete all created test data
        foreach (var id in _createdIds)
        {
            try
            {
                _repository.DeleteAsync(id).GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}
