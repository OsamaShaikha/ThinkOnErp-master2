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
/// Integration tests for RoleRepository using real Oracle database connection.
/// Tests EF Core implementation against actual Oracle database.
/// 
/// **Validates: Requirements REQ-12**
/// - Requirement 12: Testing Strategy - Integration tests that verify database operations
/// - Tests CRUD operations with EF Core
/// - Tests transaction rollback
/// - Verifies data persistence
/// - Tests soft delete functionality
/// </summary>
public class RoleRepositoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ThinkOnErpDbContext _context;
    private readonly IRoleRepository _repository;
    private readonly ILogger<RoleRepository> _logger;
    private readonly List<long> _createdIds = new();

    public RoleRepositoryIntegrationTests()
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
        services.AddScoped<IRoleRepository, RoleRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ThinkOnErpDbContext>();
        _repository = _serviceProvider.GetRequiredService<IRoleRepository>();
        _logger = _serviceProvider.GetRequiredService<ILogger<RoleRepository>>();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnRolesFromDatabase()
    {
        // Act
        var roles = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(roles);
        Assert.NotEmpty(roles);
        Assert.All(roles, r =>
        {
            Assert.True(r.RowId > 0);
            Assert.False(string.IsNullOrWhiteSpace(r.RowDesc));
        });
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnRole()
    {
        // Arrange - Get first role from database
        var roles = await _repository.GetAllAsync();
        Assert.NotEmpty(roles);
        var existingId = roles.First().RowId;

        // Act
        var role = await _repository.GetByIdAsync(existingId);

        // Assert
        Assert.NotNull(role);
        Assert.Equal(existingId, role.RowId);
        Assert.False(string.IsNullOrWhiteSpace(role.RowDesc));
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var invalidId = -999999L;

        // Act
        var role = await _repository.GetByIdAsync(invalidId);

        // Assert
        Assert.Null(role);
    }

    [Fact]
    public async Task CreateAsync_ShouldInsertRoleAndReturnGeneratedId()
    {
        // Arrange
        var uniqueCode = $"TEST-{Guid.NewGuid().ToString().Substring(0, 8)}";
        var newRole = new SysRole
        {
            RowDesc = $"دور اختبار {Guid.NewGuid()}",
            RowDescE = $"Test Role {Guid.NewGuid()}",
            RoleCode = uniqueCode,
            Description = "Integration test role",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        var generatedId = await _repository.CreateAsync(newRole);
        _createdIds.Add(generatedId);

        // Assert
        Assert.True(generatedId > 0, "Generated ID should be positive");
        Assert.Equal(generatedId, newRole.RowId);

        // Verify persistence
        var retrievedRole = await _repository.GetByIdAsync(generatedId);
        Assert.NotNull(retrievedRole);
        Assert.Equal(newRole.RowDesc, retrievedRole.RowDesc);
        Assert.Equal(newRole.RowDescE, retrievedRole.RowDescE);
        Assert.Equal(newRole.RoleCode, retrievedRole.RoleCode);
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyExistingRole()
    {
        // Arrange - Create a role first
        var uniqueCode = $"UPD-{Guid.NewGuid().ToString().Substring(0, 8)}";
        var newRole = new SysRole
        {
            RowDesc = $"دور للتحديث {Guid.NewGuid()}",
            RowDescE = $"Update Test {Guid.NewGuid()}",
            RoleCode = uniqueCode,
            Description = "Original description",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newRole);
        _createdIds.Add(createdId);

        // Act - Update the role
        var roleToUpdate = await _repository.GetByIdAsync(createdId);
        Assert.NotNull(roleToUpdate);
        
        var updatedDesc = $"دور محدث {Guid.NewGuid()}";
        var updatedDescription = "Updated description";
        roleToUpdate.RowDesc = updatedDesc;
        roleToUpdate.Description = updatedDescription;
        roleToUpdate.UpdateUser = "IntegrationTest";
        roleToUpdate.UpdateDate = DateTime.Now;

        var rowsAffected = await _repository.UpdateAsync(roleToUpdate);

        // Assert
        Assert.Equal(1, rowsAffected);

        // Verify the update persisted
        var retrievedRole = await _repository.GetByIdAsync(createdId);
        Assert.NotNull(retrievedRole);
        Assert.Equal(updatedDesc, retrievedRole.RowDesc);
        Assert.Equal(updatedDescription, retrievedRole.Description);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteRole()
    {
        // Arrange - Create a role first
        var uniqueCode = $"DEL-{Guid.NewGuid().ToString().Substring(0, 8)}";
        var newRole = new SysRole
        {
            RowDesc = $"دور للحذف {Guid.NewGuid()}",
            RowDescE = $"Delete Test {Guid.NewGuid()}",
            RoleCode = uniqueCode,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newRole);

        // Act - Delete the role (soft delete)
        var rowsAffected = await _repository.DeleteAsync(createdId);

        // Assert
        Assert.Equal(1, rowsAffected);

        // Verify the role is soft deleted (not returned by GetByIdAsync due to query filter)
        var retrievedRole = await _repository.GetByIdAsync(createdId);
        Assert.Null(retrievedRole);
    }

    [Fact]
    public async Task TransactionRollback_ShouldNotPersistChanges()
    {
        // Arrange
        var uniqueCode = $"ROLL-{Guid.NewGuid().ToString().Substring(0, 8)}";
        var newRole = new SysRole
        {
            RowDesc = $"دور تراجع {Guid.NewGuid()}",
            RowDescE = $"Rollback Test {Guid.NewGuid()}",
            RoleCode = uniqueCode,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act - Create within a transaction and rollback
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Roles.Add(newRole);
            await _context.SaveChangesAsync();
            
            var generatedId = newRole.RowId;
            Assert.True(generatedId > 0);

            // Rollback the transaction
            await transaction.RollbackAsync();

            // Assert - Verify the role was not persisted
            var retrievedRole = await _repository.GetByIdAsync(generatedId);
            Assert.Null(retrievedRole);
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
        var uniqueCode = $"COMM-{Guid.NewGuid().ToString().Substring(0, 8)}";
        var newRole = new SysRole
        {
            RowDesc = $"دور تثبيت {Guid.NewGuid()}",
            RowDescE = $"Commit Test {Guid.NewGuid()}",
            RoleCode = uniqueCode,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        long generatedId = 0;

        // Act - Create within a transaction and commit
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Roles.Add(newRole);
            await _context.SaveChangesAsync();
            
            generatedId = newRole.RowId;
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

        // Assert - Verify the role was persisted
        var retrievedRole = await _repository.GetByIdAsync(generatedId);
        Assert.NotNull(retrievedRole);
        Assert.Equal(newRole.RowDesc, retrievedRole.RowDesc);
    }

    [Fact]
    public async Task SequenceGeneration_ShouldGenerateUniqueIds()
    {
        // Arrange
        var code1 = $"SEQ1-{Guid.NewGuid().ToString().Substring(0, 8)}";
        var code2 = $"SEQ2-{Guid.NewGuid().ToString().Substring(0, 8)}";

        var role1 = new SysRole
        {
            RowDesc = $"دور تسلسل 1 {Guid.NewGuid()}",
            RowDescE = $"Sequence Test 1 {Guid.NewGuid()}",
            RoleCode = code1,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        var role2 = new SysRole
        {
            RowDesc = $"دور تسلسل 2 {Guid.NewGuid()}",
            RowDescE = $"Sequence Test 2 {Guid.NewGuid()}",
            RoleCode = code2,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        var id1 = await _repository.CreateAsync(role1);
        var id2 = await _repository.CreateAsync(role2);
        _createdIds.Add(id1);
        _createdIds.Add(id2);

        // Assert
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task CreateAsync_WithMinimalFields_ShouldSucceed()
    {
        // Arrange - Create role with only required fields
        var uniqueCode = $"MIN-{Guid.NewGuid().ToString().Substring(0, 8)}";
        var newRole = new SysRole
        {
            RowDesc = $"دور بسيط {Guid.NewGuid()}",
            RowDescE = $"Minimal Role {Guid.NewGuid()}",
            RoleCode = uniqueCode,
            Description = null, // Optional field
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        var generatedId = await _repository.CreateAsync(newRole);
        _createdIds.Add(generatedId);

        // Assert
        Assert.True(generatedId > 0);
        var retrievedRole = await _repository.GetByIdAsync(generatedId);
        Assert.NotNull(retrievedRole);
        Assert.Null(retrievedRole.Description);
    }

    [Fact]
    public async Task CreateAsync_WithLongDescriptions_ShouldSucceed()
    {
        // Arrange - Create role with maximum length descriptions
        var longDesc = new string('ا', 200); // Max length for RowDesc
        var longDescE = new string('A', 200); // Max length for RowDescE
        var uniqueCode = $"LONG-{Guid.NewGuid().ToString().Substring(0, 8)}";

        var newRole = new SysRole
        {
            RowDesc = longDesc,
            RowDescE = longDescE,
            RoleCode = uniqueCode,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        var generatedId = await _repository.CreateAsync(newRole);
        _createdIds.Add(generatedId);

        // Assert
        Assert.True(generatedId > 0);
        var retrievedRole = await _repository.GetByIdAsync(generatedId);
        Assert.NotNull(retrievedRole);
        Assert.Equal(longDesc, retrievedRole.RowDesc);
        Assert.Equal(longDescE, retrievedRole.RowDescE);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnRolesOrderedByRowDesc()
    {
        // Arrange - Create multiple roles
        var code1 = $"ORD1-{Guid.NewGuid().ToString().Substring(0, 8)}";
        var code2 = $"ORD2-{Guid.NewGuid().ToString().Substring(0, 8)}";
        var code3 = $"ORD3-{Guid.NewGuid().ToString().Substring(0, 8)}";

        var role1 = new SysRole
        {
            RowDesc = "ج - دور ثالث",
            RowDescE = "C - Third Role",
            RoleCode = code1,
            CreationUser = "IntegrationTest"
        };

        var role2 = new SysRole
        {
            RowDesc = "أ - دور أول",
            RowDescE = "A - First Role",
            RoleCode = code2,
            CreationUser = "IntegrationTest"
        };

        var role3 = new SysRole
        {
            RowDesc = "ب - دور ثاني",
            RowDescE = "B - Second Role",
            RoleCode = code3,
            CreationUser = "IntegrationTest"
        };

        var id1 = await _repository.CreateAsync(role1);
        var id2 = await _repository.CreateAsync(role2);
        var id3 = await _repository.CreateAsync(role3);
        _createdIds.AddRange(new[] { id1, id2, id3 });

        // Act
        var allRoles = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(allRoles);
        Assert.True(allRoles.Count >= 3);

        // Find our created roles in the result
        var createdRoles = allRoles.Where(r => _createdIds.Contains(r.RowId)).ToList();
        Assert.Equal(3, createdRoles.Count);

        // Verify they are ordered by RowDesc
        Assert.Equal("أ - دور أول", createdRoles[0].RowDesc);
        Assert.Equal("ب - دور ثاني", createdRoles[1].RowDesc);
        Assert.Equal("ج - دور ثالث", createdRoles[2].RowDesc);
    }

    [Fact]
    public async Task DeleteAsync_OnAlreadyDeletedRole_ShouldSucceed()
    {
        // Arrange - Create and delete a role
        var uniqueCode = $"DELDEL-{Guid.NewGuid().ToString().Substring(0, 8)}";
        var newRole = new SysRole
        {
            RowDesc = $"دور للحذف المزدوج {Guid.NewGuid()}",
            RowDescE = $"Double Delete Test {Guid.NewGuid()}",
            RoleCode = uniqueCode,
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newRole);

        // First delete
        await _repository.DeleteAsync(createdId);

        // Act - Delete again (should be idempotent)
        var rowsAffected = await _repository.DeleteAsync(createdId);

        // Assert
        Assert.Equal(1, rowsAffected); // Should still return 1 (idempotent)
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
