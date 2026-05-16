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
/// Integration tests for BranchRepository using real Oracle database connection.
/// Tests EF Core implementation against actual Oracle database.
/// 
/// **Validates: Requirements REQ-12**
/// - Requirement 12: Testing Strategy - Integration tests that verify database operations
/// - Tests CRUD operations with EF Core
/// - Tests BLOB handling for branch logos
/// - Tests transaction rollback
/// - Verifies data persistence
/// - Tests multi-tenancy filtering by company
/// </summary>
public class BranchRepositoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ThinkOnErpDbContext _context;
    private readonly IBranchRepository _repository;
    private readonly ILogger<BranchRepository> _logger;
    private readonly List<long> _createdIds = new();

    public BranchRepositoryIntegrationTests()
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
        services.AddScoped<IBranchRepository, BranchRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ThinkOnErpDbContext>();
        _repository = _serviceProvider.GetRequiredService<IBranchRepository>();
        _logger = _serviceProvider.GetRequiredService<ILogger<BranchRepository>>();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnBranchesFromDatabase()
    {
        // Act
        var branches = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(branches);
        Assert.NotEmpty(branches);
        Assert.All(branches, b =>
        {
            Assert.True(b.RowId > 0);
            Assert.False(string.IsNullOrWhiteSpace(b.RowDesc));
            Assert.False(string.IsNullOrWhiteSpace(b.RowDescE));
        });
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnBranch()
    {
        // Arrange - Get first branch from database
        var branches = await _repository.GetAllAsync();
        Assert.NotEmpty(branches);
        var existingId = branches.First().RowId;

        // Act
        var branch = await _repository.GetByIdAsync(existingId);

        // Assert
        Assert.NotNull(branch);
        Assert.Equal(existingId, branch.RowId);
        Assert.False(string.IsNullOrWhiteSpace(branch.RowDesc));
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var invalidId = -999999L;

        // Act
        var branch = await _repository.GetByIdAsync(invalidId);

        // Assert
        Assert.Null(branch);
    }

    [Fact]
    public async Task CreateAsync_ShouldInsertBranchAndReturnGeneratedId()
    {
        // Arrange - Get a valid company ID from existing branches
        var existingBranches = await _repository.GetAllAsync();
        Assert.NotEmpty(existingBranches);
        var companyId = existingBranches.First().ParRowId;

        var newBranch = new SysBranch
        {
            ParRowId = companyId,
            RowDesc = $"Test Branch {Guid.NewGuid()}",
            RowDescE = $"Test Branch EN {Guid.NewGuid()}",
            Phone = "123-456-7890",
            Email = "test@branch.com",
            IsHeadBranch = false,
            DefaultLang = "en",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        var generatedId = await _repository.CreateAsync(newBranch);
        _createdIds.Add(generatedId);

        // Assert
        Assert.True(generatedId > 0, "Generated ID should be positive");
        Assert.Equal(generatedId, newBranch.RowId);

        // Verify persistence
        var retrievedBranch = await _repository.GetByIdAsync(generatedId);
        Assert.NotNull(retrievedBranch);
        Assert.Equal(newBranch.RowDesc, retrievedBranch.RowDesc);
        Assert.Equal(newBranch.RowDescE, retrievedBranch.RowDescE);
        Assert.Equal(newBranch.Phone, retrievedBranch.Phone);
        Assert.Equal(newBranch.Email, retrievedBranch.Email);
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyExistingBranch()
    {
        // Arrange - Create a branch first
        var existingBranches = await _repository.GetAllAsync();
        Assert.NotEmpty(existingBranches);
        var companyId = existingBranches.First().ParRowId;

        var newBranch = new SysBranch
        {
            ParRowId = companyId,
            RowDesc = $"Update Test {Guid.NewGuid()}",
            RowDescE = $"Update Test EN {Guid.NewGuid()}",
            Phone = "111-222-3333",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newBranch);
        _createdIds.Add(createdId);

        // Act - Update the branch
        var branchToUpdate = await _repository.GetByIdAsync(createdId);
        Assert.NotNull(branchToUpdate);
        
        var updatedDesc = $"Updated {Guid.NewGuid()}";
        var updatedPhone = "999-888-7777";
        branchToUpdate.RowDesc = updatedDesc;
        branchToUpdate.Phone = updatedPhone;
        branchToUpdate.UpdateUser = "IntegrationTest";
        branchToUpdate.UpdateDate = DateTime.Now;

        var rowsAffected = await _repository.UpdateAsync(branchToUpdate);

        // Assert
        Assert.Equal(1, rowsAffected);

        // Verify the update persisted
        var retrievedBranch = await _repository.GetByIdAsync(createdId);
        Assert.NotNull(retrievedBranch);
        Assert.Equal(updatedDesc, retrievedBranch.RowDesc);
        Assert.Equal(updatedPhone, retrievedBranch.Phone);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteBranch()
    {
        // Arrange - Create a branch first
        var existingBranches = await _repository.GetAllAsync();
        Assert.NotEmpty(existingBranches);
        var companyId = existingBranches.First().ParRowId;

        var newBranch = new SysBranch
        {
            ParRowId = companyId,
            RowDesc = $"Delete Test {Guid.NewGuid()}",
            RowDescE = $"Delete Test EN {Guid.NewGuid()}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newBranch);

        // Act - Delete the branch (soft delete)
        var rowsAffected = await _repository.DeleteAsync(createdId);

        // Assert
        Assert.Equal(1, rowsAffected);

        // Verify the branch is soft deleted (not returned by GetByIdAsync due to query filter)
        var retrievedBranch = await _repository.GetByIdAsync(createdId);
        Assert.Null(retrievedBranch);
    }

    [Fact]
    public async Task GetByCompanyIdAsync_ShouldReturnBranchesForSpecificCompany()
    {
        // Arrange - Get a valid company ID
        var allBranches = await _repository.GetAllAsync();
        Assert.NotEmpty(allBranches);
        var companyId = allBranches.First().ParRowId;
        Assert.NotNull(companyId);

        // Act
        var companyBranches = await _repository.GetByCompanyIdAsync(companyId.Value);

        // Assert
        Assert.NotNull(companyBranches);
        Assert.NotEmpty(companyBranches);
        Assert.All(companyBranches, b => Assert.Equal(companyId, b.ParRowId));
    }

    [Fact]
    public async Task UpdateLogoAsync_ShouldStoreBlobData()
    {
        // Arrange - Create a branch first
        var existingBranches = await _repository.GetAllAsync();
        Assert.NotEmpty(existingBranches);
        var companyId = existingBranches.First().ParRowId;

        var newBranch = new SysBranch
        {
            ParRowId = companyId,
            RowDesc = $"Logo Test {Guid.NewGuid()}",
            RowDescE = $"Logo Test EN {Guid.NewGuid()}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newBranch);
        _createdIds.Add(createdId);

        // Create test logo data (simulating a small image)
        var logoData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header

        // Act - Update the logo
        var rowsAffected = await _repository.UpdateLogoAsync(createdId, logoData, "IntegrationTest");

        // Assert
        Assert.Equal(1, rowsAffected);

        // Verify the logo was stored
        var retrievedLogo = await _repository.GetLogoAsync(createdId);
        Assert.NotNull(retrievedLogo);
        Assert.Equal(logoData.Length, retrievedLogo.Length);
        Assert.Equal(logoData, retrievedLogo);
    }

    [Fact]
    public async Task GetLogoAsync_WithNonExistentBranch_ShouldReturnNull()
    {
        // Arrange
        var invalidId = -999999L;

        // Act
        var logo = await _repository.GetLogoAsync(invalidId);

        // Assert
        Assert.Null(logo);
    }

    [Fact]
    public async Task GetLogoAsync_WithBranchWithoutLogo_ShouldReturnNull()
    {
        // Arrange - Create a branch without a logo
        var existingBranches = await _repository.GetAllAsync();
        Assert.NotEmpty(existingBranches);
        var companyId = existingBranches.First().ParRowId;

        var newBranch = new SysBranch
        {
            ParRowId = companyId,
            RowDesc = $"No Logo Test {Guid.NewGuid()}",
            RowDescE = $"No Logo Test EN {Guid.NewGuid()}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newBranch);
        _createdIds.Add(createdId);

        // Act
        var logo = await _repository.GetLogoAsync(createdId);

        // Assert
        Assert.Null(logo);
    }

    [Fact]
    public async Task TransactionRollback_ShouldNotPersistChanges()
    {
        // Arrange
        var existingBranches = await _repository.GetAllAsync();
        Assert.NotEmpty(existingBranches);
        var companyId = existingBranches.First().ParRowId;

        var newBranch = new SysBranch
        {
            ParRowId = companyId,
            RowDesc = $"Rollback Test {Guid.NewGuid()}",
            RowDescE = $"Rollback Test EN {Guid.NewGuid()}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act - Create within a transaction and rollback
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Branches.Add(newBranch);
            await _context.SaveChangesAsync();
            
            var generatedId = newBranch.RowId;
            Assert.True(generatedId > 0);

            // Rollback the transaction
            await transaction.RollbackAsync();

            // Assert - Verify the branch was not persisted
            var retrievedBranch = await _repository.GetByIdAsync(generatedId);
            Assert.Null(retrievedBranch);
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
        var existingBranches = await _repository.GetAllAsync();
        Assert.NotEmpty(existingBranches);
        var companyId = existingBranches.First().ParRowId;

        var newBranch = new SysBranch
        {
            ParRowId = companyId,
            RowDesc = $"Commit Test {Guid.NewGuid()}",
            RowDescE = $"Commit Test EN {Guid.NewGuid()}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        long generatedId = 0;

        // Act - Create within a transaction and commit
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Branches.Add(newBranch);
            await _context.SaveChangesAsync();
            
            generatedId = newBranch.RowId;
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

        // Assert - Verify the branch was persisted
        var retrievedBranch = await _repository.GetByIdAsync(generatedId);
        Assert.NotNull(retrievedBranch);
        Assert.Equal(newBranch.RowDesc, retrievedBranch.RowDesc);
    }

    [Fact]
    public async Task SequenceGeneration_ShouldGenerateUniqueIds()
    {
        // Arrange
        var existingBranches = await _repository.GetAllAsync();
        Assert.NotEmpty(existingBranches);
        var companyId = existingBranches.First().ParRowId;

        var branch1 = new SysBranch
        {
            ParRowId = companyId,
            RowDesc = $"Sequence Test 1 {Guid.NewGuid()}",
            RowDescE = $"Sequence Test 1 EN {Guid.NewGuid()}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        var branch2 = new SysBranch
        {
            ParRowId = companyId,
            RowDesc = $"Sequence Test 2 {Guid.NewGuid()}",
            RowDescE = $"Sequence Test 2 EN {Guid.NewGuid()}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        var id1 = await _repository.CreateAsync(branch1);
        var id2 = await _repository.CreateAsync(branch2);
        _createdIds.Add(id1);
        _createdIds.Add(id2);

        // Assert
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task EagerLoading_ShouldLoadBaseCurrency()
    {
        // Arrange - Get a branch that has a base currency
        var branches = await _repository.GetAllAsync();
        var branchWithCurrency = branches.FirstOrDefault(b => b.BaseCurrencyId.HasValue);
        
        if (branchWithCurrency == null)
        {
            // Skip test if no branch has a currency
            return;
        }

        // Act
        var branch = await _repository.GetByIdAsync(branchWithCurrency.RowId);

        // Assert
        Assert.NotNull(branch);
        if (branch.BaseCurrencyId.HasValue)
        {
            Assert.NotNull(branch.BaseCurrency);
            Assert.Equal(branch.BaseCurrencyId.Value, branch.BaseCurrency.RowId);
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
