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
/// Integration tests for CompanyRepository using real Oracle database connection.
/// Tests EF Core implementation against actual Oracle database.
/// 
/// **Validates: Requirements REQ-12**
/// - Requirement 12: Testing Strategy - Integration tests that verify database operations
/// - Tests CRUD operations with EF Core
/// - Tests BLOB handling for company logos
/// - Tests transaction rollback
/// - Verifies data persistence
/// - Tests default branch setting
/// </summary>
public class CompanyRepositoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ThinkOnErpDbContext _context;
    private readonly ICompanyRepository _repository;
    private readonly ILogger<CompanyRepository> _logger;
    private readonly List<long> _createdIds = new();

    public CompanyRepositoryIntegrationTests()
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
        services.AddScoped<ICompanyRepository, CompanyRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ThinkOnErpDbContext>();
        _repository = _serviceProvider.GetRequiredService<ICompanyRepository>();
        _logger = _serviceProvider.GetRequiredService<ILogger<CompanyRepository>>();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnCompaniesFromDatabase()
    {
        // Act
        var companies = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(companies);
        Assert.NotEmpty(companies);
        Assert.All(companies, c =>
        {
            Assert.True(c.RowId > 0);
            Assert.False(string.IsNullOrWhiteSpace(c.RowDesc));
            Assert.False(string.IsNullOrWhiteSpace(c.RowDescE));
        });
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnCompany()
    {
        // Arrange - Get first company from database
        var companies = await _repository.GetAllAsync();
        Assert.NotEmpty(companies);
        var existingId = companies.First().RowId;

        // Act
        var company = await _repository.GetByIdAsync(existingId);

        // Assert
        Assert.NotNull(company);
        Assert.Equal(existingId, company.RowId);
        Assert.False(string.IsNullOrWhiteSpace(company.RowDesc));
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var invalidId = -999999L;

        // Act
        var company = await _repository.GetByIdAsync(invalidId);

        // Assert
        Assert.Null(company);
    }

    [Fact]
    public async Task CreateAsync_ShouldInsertCompanyAndReturnGeneratedId()
    {
        // Arrange
        var newCompany = new SysCompany
        {
            RowDesc = $"شركة اختبار {Guid.NewGuid()}",
            RowDescE = $"Test Company {Guid.NewGuid()}",
            CompanyCode = $"TEST-{Guid.NewGuid().ToString().Substring(0, 8)}",
            LegalName = "الاسم القانوني للاختبار",
            LegalNameE = "Test Legal Name",
            TaxNumber = $"TAX-{Guid.NewGuid().ToString().Substring(0, 10)}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        var generatedId = await _repository.CreateAsync(newCompany);
        _createdIds.Add(generatedId);

        // Assert
        Assert.True(generatedId > 0, "Generated ID should be positive");
        Assert.Equal(generatedId, newCompany.RowId);

        // Verify persistence
        var retrievedCompany = await _repository.GetByIdAsync(generatedId);
        Assert.NotNull(retrievedCompany);
        Assert.Equal(newCompany.RowDesc, retrievedCompany.RowDesc);
        Assert.Equal(newCompany.RowDescE, retrievedCompany.RowDescE);
        Assert.Equal(newCompany.CompanyCode, retrievedCompany.CompanyCode);
        Assert.Equal(newCompany.TaxNumber, retrievedCompany.TaxNumber);
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyExistingCompany()
    {
        // Arrange - Create a company first
        var newCompany = new SysCompany
        {
            RowDesc = $"شركة للتحديث {Guid.NewGuid()}",
            RowDescE = $"Update Test {Guid.NewGuid()}",
            CompanyCode = $"UPD-{Guid.NewGuid().ToString().Substring(0, 8)}",
            TaxNumber = "111-222-3333",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newCompany);
        _createdIds.Add(createdId);

        // Act - Update the company
        var companyToUpdate = await _repository.GetByIdAsync(createdId);
        Assert.NotNull(companyToUpdate);
        
        var updatedDesc = $"شركة محدثة {Guid.NewGuid()}";
        var updatedTaxNumber = "999-888-7777";
        companyToUpdate.RowDesc = updatedDesc;
        companyToUpdate.TaxNumber = updatedTaxNumber;
        companyToUpdate.UpdateUser = "IntegrationTest";
        companyToUpdate.UpdateDate = DateTime.Now;

        var rowsAffected = await _repository.UpdateAsync(companyToUpdate);

        // Assert
        Assert.Equal(1, rowsAffected);

        // Verify the update persisted
        var retrievedCompany = await _repository.GetByIdAsync(createdId);
        Assert.NotNull(retrievedCompany);
        Assert.Equal(updatedDesc, retrievedCompany.RowDesc);
        Assert.Equal(updatedTaxNumber, retrievedCompany.TaxNumber);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteCompany()
    {
        // Arrange - Create a company first
        var newCompany = new SysCompany
        {
            RowDesc = $"شركة للحذف {Guid.NewGuid()}",
            RowDescE = $"Delete Test {Guid.NewGuid()}",
            CompanyCode = $"DEL-{Guid.NewGuid().ToString().Substring(0, 8)}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newCompany);

        // Act - Delete the company (soft delete)
        var rowsAffected = await _repository.DeleteAsync(createdId);

        // Assert
        Assert.Equal(1, rowsAffected);

        // Verify the company is soft deleted (not returned by GetByIdAsync due to query filter)
        var retrievedCompany = await _repository.GetByIdAsync(createdId);
        Assert.Null(retrievedCompany);
    }

    [Fact]
    public async Task UpdateLogoAsync_ShouldStoreBlobData()
    {
        // Arrange - Create a company first
        var newCompany = new SysCompany
        {
            RowDesc = $"شركة شعار {Guid.NewGuid()}",
            RowDescE = $"Logo Test {Guid.NewGuid()}",
            CompanyCode = $"LOGO-{Guid.NewGuid().ToString().Substring(0, 8)}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newCompany);
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
    public async Task GetLogoAsync_WithNonExistentCompany_ShouldReturnNull()
    {
        // Arrange
        var invalidId = -999999L;

        // Act
        var logo = await _repository.GetLogoAsync(invalidId);

        // Assert
        Assert.Null(logo);
    }

    [Fact]
    public async Task GetLogoAsync_WithCompanyWithoutLogo_ShouldReturnNull()
    {
        // Arrange - Create a company without a logo
        var newCompany = new SysCompany
        {
            RowDesc = $"شركة بدون شعار {Guid.NewGuid()}",
            RowDescE = $"No Logo Test {Guid.NewGuid()}",
            CompanyCode = $"NOLOG-{Guid.NewGuid().ToString().Substring(0, 8)}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var createdId = await _repository.CreateAsync(newCompany);
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
        var newCompany = new SysCompany
        {
            RowDesc = $"شركة تراجع {Guid.NewGuid()}",
            RowDescE = $"Rollback Test {Guid.NewGuid()}",
            CompanyCode = $"ROLL-{Guid.NewGuid().ToString().Substring(0, 8)}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act - Create within a transaction and rollback
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Companies.Add(newCompany);
            await _context.SaveChangesAsync();
            
            var generatedId = newCompany.RowId;
            Assert.True(generatedId > 0);

            // Rollback the transaction
            await transaction.RollbackAsync();

            // Assert - Verify the company was not persisted
            var retrievedCompany = await _repository.GetByIdAsync(generatedId);
            Assert.Null(retrievedCompany);
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
        var newCompany = new SysCompany
        {
            RowDesc = $"شركة تثبيت {Guid.NewGuid()}",
            RowDescE = $"Commit Test {Guid.NewGuid()}",
            CompanyCode = $"COMM-{Guid.NewGuid().ToString().Substring(0, 8)}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        long generatedId = 0;

        // Act - Create within a transaction and commit
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Companies.Add(newCompany);
            await _context.SaveChangesAsync();
            
            generatedId = newCompany.RowId;
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

        // Assert - Verify the company was persisted
        var retrievedCompany = await _repository.GetByIdAsync(generatedId);
        Assert.NotNull(retrievedCompany);
        Assert.Equal(newCompany.RowDesc, retrievedCompany.RowDesc);
    }

    [Fact]
    public async Task SequenceGeneration_ShouldGenerateUniqueIds()
    {
        // Arrange
        var company1 = new SysCompany
        {
            RowDesc = $"شركة تسلسل 1 {Guid.NewGuid()}",
            RowDescE = $"Sequence Test 1 {Guid.NewGuid()}",
            CompanyCode = $"SEQ1-{Guid.NewGuid().ToString().Substring(0, 8)}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        var company2 = new SysCompany
        {
            RowDesc = $"شركة تسلسل 2 {Guid.NewGuid()}",
            RowDescE = $"Sequence Test 2 {Guid.NewGuid()}",
            CompanyCode = $"SEQ2-{Guid.NewGuid().ToString().Substring(0, 8)}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };

        // Act
        var id1 = await _repository.CreateAsync(company1);
        var id2 = await _repository.CreateAsync(company2);
        _createdIds.Add(id1);
        _createdIds.Add(id2);

        // Assert
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task EagerLoading_ShouldLoadCurrency()
    {
        // Arrange - Get a company that has a currency
        var companies = await _repository.GetAllAsync();
        var companyWithCurrency = companies.FirstOrDefault(c => c.CurrId.HasValue);
        
        if (companyWithCurrency == null)
        {
            // Skip test if no company has a currency
            return;
        }

        // Act
        var company = await _repository.GetByIdAsync(companyWithCurrency.RowId);

        // Assert
        Assert.NotNull(company);
        if (company.CurrId.HasValue)
        {
            Assert.NotNull(company.Currency);
            Assert.Equal(company.CurrId.Value, company.Currency.RowId);
        }
    }

    [Fact]
    public async Task SetDefaultBranchAsync_ShouldSetDefaultBranch()
    {
        // Arrange - Create a company and a branch
        var newCompany = new SysCompany
        {
            RowDesc = $"شركة فرع افتراضي {Guid.NewGuid()}",
            RowDescE = $"Default Branch Test {Guid.NewGuid()}",
            CompanyCode = $"DEFBR-{Guid.NewGuid().ToString().Substring(0, 8)}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        var companyId = await _repository.CreateAsync(newCompany);
        _createdIds.Add(companyId);

        // Create a branch for this company
        var newBranch = new SysBranch
        {
            ParRowId = companyId,
            RowDesc = $"فرع اختبار {Guid.NewGuid()}",
            RowDescE = $"Test Branch {Guid.NewGuid()}",
            CreationUser = "IntegrationTest",
            CreationDate = DateTime.Now
        };
        _context.Branches.Add(newBranch);
        await _context.SaveChangesAsync();
        var branchId = newBranch.RowId;

        // Act - Set the default branch
        var rowsAffected = await _repository.SetDefaultBranchAsync(companyId, branchId, "IntegrationTest");

        // Assert
        Assert.Equal(1, rowsAffected);

        // Verify the default branch was set
        var updatedCompany = await _repository.GetByIdAsync(companyId);
        Assert.NotNull(updatedCompany);
        Assert.Equal(branchId, updatedCompany.DefaultBranchId);

        // Cleanup branch
        _context.Branches.Remove(newBranch);
        await _context.SaveChangesAsync();
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
