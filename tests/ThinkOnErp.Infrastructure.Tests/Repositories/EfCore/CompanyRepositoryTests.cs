using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for CompanyRepository using EF Core InMemory provider.
/// Tests all CRUD operations, BLOB handling, exception scenarios, and null handling.
/// </summary>
public class CompanyRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly CompanyRepository _repository;
    private readonly Mock<ILogger<CompanyRepository>> _loggerMock;

    public CompanyRepositoryTests()
    {
        // Create InMemory database with unique name for each test instance
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_Company_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<CompanyRepository>>();
        _repository = new CompanyRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllCompanies_OrderedByRowDesc()
    {
        // Arrange
        var companies = new List<SysCompany>
        {
            new SysCompany
            {
                RowId = 1,
                RowDesc = "شركة ب",
                RowDescE = "Company B",
                CompanyCode = "COMP-B",
                IsActive = true,
                CreationUser = "test"
            },
            new SysCompany
            {
                RowId = 2,
                RowDesc = "شركة أ",
                RowDescE = "Company A",
                CompanyCode = "COMP-A",
                IsActive = true,
                CreationUser = "test"
            },
            new SysCompany
            {
                RowId = 3,
                RowDesc = "شركة ج",
                RowDescE = "Company C",
                CompanyCode = "COMP-C",
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.Companies.AddRangeAsync(companies);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("شركة أ", result[0].RowDesc); // Ordered by RowDesc
        Assert.Equal("شركة ب", result[1].RowDesc);
        Assert.Equal("شركة ج", result[2].RowDesc);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoCompanies()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_FiltersOutInactiveCompanies()
    {
        // Arrange
        var companies = new List<SysCompany>
        {
            new SysCompany
            {
                RowId = 1,
                RowDesc = "Active Company",
                RowDescE = "Active Company",
                CompanyCode = "ACTIVE",
                IsActive = true,
                CreationUser = "test"
            },
            new SysCompany
            {
                RowId = 2,
                RowDesc = "Inactive Company",
                RowDescE = "Inactive Company",
                CompanyCode = "INACTIVE",
                IsActive = false, // Soft deleted
                CreationUser = "test"
            }
        };
        await _context.Companies.AddRangeAsync(companies);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Active Company", result[0].RowDesc);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsCompany_WhenExists()
    {
        // Arrange
        var company = new SysCompany
        {
            RowId = 1,
            RowDesc = "شركة الاختبار",
            RowDescE = "Test Company",
            LegalName = "الاسم القانوني",
            LegalNameE = "Legal Name",
            CompanyCode = "TEST-001",
            TaxNumber = "123456789",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Companies.AddAsync(company);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.RowId);
        Assert.Equal("Test Company", result.RowDescE);
        Assert.Equal("TEST-001", result.CompanyCode);
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
    public async Task GetByIdAsync_ReturnsNull_WhenCompanyIsInactive()
    {
        // Arrange
        var company = new SysCompany
        {
            RowId = 1,
            RowDesc = "Inactive Company",
            RowDescE = "Inactive Company",
            CompanyCode = "INACTIVE",
            IsActive = false, // Soft deleted
            CreationUser = "test"
        };
        await _context.Companies.AddAsync(company);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(1);

        // Assert
        Assert.Null(result); // Global query filter excludes inactive records
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CreatesNewCompany_ReturnsGeneratedId()
    {
        // Arrange
        var company = new SysCompany
        {
            RowDesc = "شركة جديدة",
            RowDescE = "New Company",
            LegalName = "الاسم القانوني",
            LegalNameE = "Legal Name",
            CompanyCode = "NEW-001",
            TaxNumber = "987654321",
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(company);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, company.RowId);
        Assert.NotNull(company.CreationDate);
        Assert.True(company.IsActive);

        // Verify in database
        var savedCompany = await _context.Companies.FindAsync(result);
        Assert.NotNull(savedCompany);
        Assert.Equal("NEW-001", savedCompany.CompanyCode);
    }

    [Fact]
    public async Task CreateAsync_SetsCreationDate_WhenNotProvided()
    {
        // Arrange
        var company = new SysCompany
        {
            RowDesc = "Test Company",
            RowDescE = "Test Company",
            CompanyCode = "TEST",
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateAsync(company);

        // Assert
        Assert.NotNull(company.CreationDate);
        Assert.True(company.CreationDate.Value <= DateTime.Now);
    }

    [Fact]
    public async Task CreateAsync_SetsIsActiveToTrue_ByDefault()
    {
        // Arrange
        var company = new SysCompany
        {
            RowDesc = "Test Company",
            RowDescE = "Test Company",
            CompanyCode = "TEST",
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateAsync(company);

        // Assert
        Assert.True(company.IsActive);
    }

    [Fact]
    public async Task CreateAsync_WithNullOptionalFields_Succeeds()
    {
        // Arrange
        var company = new SysCompany
        {
            RowDesc = "Minimal Company",
            RowDescE = "Minimal Company",
            CompanyCode = "MIN",
            LegalName = null,
            LegalNameE = null,
            TaxNumber = null,
            CountryId = null,
            CurrId = null,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(company);

        // Assert
        Assert.True(result > 0);
        var savedCompany = await _context.Companies.FindAsync(result);
        Assert.NotNull(savedCompany);
        Assert.Null(savedCompany.LegalName);
        Assert.Null(savedCompany.TaxNumber);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesExistingCompany_ReturnsRowsAffected()
    {
        // Arrange
        var company = new SysCompany
        {
            RowDesc = "Original Name",
            RowDescE = "Original Name",
            CompanyCode = "ORIG",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Companies.AddAsync(company);
        await _context.SaveChangesAsync();

        // Modify the company
        company.RowDescE = "Updated Name";
        company.TaxNumber = "NEW-TAX-123";
        company.UpdateUser = "update_user";

        // Act
        var result = await _repository.UpdateAsync(company);

        // Assert
        Assert.Equal(1, result);
        Assert.NotNull(company.UpdateDate);

        // Verify in database
        var updatedCompany = await _context.Companies.FindAsync(company.RowId);
        Assert.NotNull(updatedCompany);
        Assert.Equal("Updated Name", updatedCompany.RowDescE);
        Assert.Equal("NEW-TAX-123", updatedCompany.TaxNumber);
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdateDate_Automatically()
    {
        // Arrange
        var company = new SysCompany
        {
            RowDesc = "Test Company",
            RowDescE = "Test Company",
            CompanyCode = "TEST",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Companies.AddAsync(company);
        await _context.SaveChangesAsync();

        company.RowDescE = "Updated Description";

        // Act
        await _repository.UpdateAsync(company);

        // Assert
        Assert.NotNull(company.UpdateDate);
        Assert.True(company.UpdateDate.Value <= DateTime.Now);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesCompany_ReturnsRowsAffected()
    {
        // Arrange
        var company = new SysCompany
        {
            RowDesc = "To Delete",
            RowDescE = "To Delete",
            CompanyCode = "DEL",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Companies.AddAsync(company);
        await _context.SaveChangesAsync();
        var companyId = company.RowId;

        // Act
        var result = await _repository.DeleteAsync(companyId);

        // Assert
        Assert.Equal(1, result);

        // Verify company is soft deleted (IsActive = false)
        var deletedCompany = await _context.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.RowId == companyId);
        Assert.NotNull(deletedCompany);
        Assert.False(deletedCompany.IsActive);
        Assert.NotNull(deletedCompany.UpdateDate);
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
    public async Task DeleteAsync_OnAlreadyDeletedCompany_ReturnsOne()
    {
        // Arrange
        var company = new SysCompany
        {
            RowDesc = "Already Deleted",
            RowDescE = "Already Deleted",
            CompanyCode = "DELETED",
            IsActive = false, // Already soft deleted
            CreationUser = "test_user"
        };
        await _context.Companies.AddAsync(company);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.DeleteAsync(company.RowId);

        // Assert
        Assert.Equal(1, result); // Can delete again (idempotent)
    }

    #endregion

    #region UpdateLogoAsync Tests

    [Fact]
    public async Task UpdateLogoAsync_UpdatesCompanyLogo_ReturnsRowsAffected()
    {
        // Arrange
        var company = new SysCompany
        {
            RowDesc = "Logo Test",
            RowDescE = "Logo Test",
            CompanyCode = "LOGO",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Companies.AddAsync(company);
        await _context.SaveChangesAsync();
        var companyId = company.RowId;

        var logoData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header

        // Act
        var result = await _repository.UpdateLogoAsync(companyId, logoData, "logo_user");

        // Assert
        Assert.Equal(1, result);

        // Verify logo was updated
        var updatedCompany = await _context.Companies.FindAsync(companyId);
        Assert.NotNull(updatedCompany);
        Assert.NotNull(updatedCompany.CompanyLogo);
        Assert.Equal(logoData.Length, updatedCompany.CompanyLogo.Length);
        Assert.Equal(logoData, updatedCompany.CompanyLogo);
        Assert.Equal("logo_user", updatedCompany.UpdateUser);
    }

    [Fact]
    public async Task UpdateLogoAsync_WithNonExistentId_ReturnsZero()
    {
        // Arrange
        var logoData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        // Act
        var result = await _repository.UpdateLogoAsync(999, logoData, "user");

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task UpdateLogoAsync_WithNullLogo_ClearsLogo()
    {
        // Arrange
        var company = new SysCompany
        {
            RowDesc = "Logo Clear Test",
            RowDescE = "Logo Clear Test",
            CompanyCode = "CLEAR",
            CompanyLogo = new byte[] { 0x01, 0x02, 0x03 },
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Companies.AddAsync(company);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.UpdateLogoAsync(company.RowId, null!, "clear_user");

        // Assert
        Assert.Equal(1, result);
        var updatedCompany = await _context.Companies.FindAsync(company.RowId);
        Assert.NotNull(updatedCompany);
        Assert.Null(updatedCompany.CompanyLogo);
    }

    #endregion

    #region GetLogoAsync Tests

    [Fact]
    public async Task GetLogoAsync_ReturnsLogo_WhenExists()
    {
        // Arrange
        var logoData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var company = new SysCompany
        {
            RowDesc = "Logo Company",
            RowDescE = "Logo Company",
            CompanyCode = "LOGO",
            CompanyLogo = logoData,
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Companies.AddAsync(company);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetLogoAsync(company.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(logoData.Length, result.Length);
        Assert.Equal(logoData, result);
    }

    [Fact]
    public async Task GetLogoAsync_ReturnsNull_WhenCompanyNotExists()
    {
        // Act
        var result = await _repository.GetLogoAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLogoAsync_ReturnsNull_WhenLogoNotSet()
    {
        // Arrange
        var company = new SysCompany
        {
            RowDesc = "No Logo",
            RowDescE = "No Logo",
            CompanyCode = "NOLOGO",
            CompanyLogo = null,
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Companies.AddAsync(company);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetLogoAsync(company.RowId);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region SetDefaultBranchAsync Tests

    [Fact]
    public async Task SetDefaultBranchAsync_SetsDefaultBranch_ReturnsRowsAffected()
    {
        // Arrange
        var company = new SysCompany
        {
            RowDesc = "Company",
            RowDescE = "Company",
            CompanyCode = "COMP",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Companies.AddAsync(company);
        await _context.SaveChangesAsync();

        var branch = new SysBranch
        {
            ParRowId = company.RowId,
            RowDesc = "Branch",
            RowDescE = "Branch",
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.Branches.AddAsync(branch);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.SetDefaultBranchAsync(company.RowId, branch.RowId, "set_user");

        // Assert
        Assert.Equal(1, result);

        // Verify default branch was set
        var updatedCompany = await _context.Companies.FindAsync(company.RowId);
        Assert.NotNull(updatedCompany);
        Assert.Equal(branch.RowId, updatedCompany.DefaultBranchId);
        Assert.Equal("set_user", updatedCompany.UpdateUser);
    }

    [Fact]
    public async Task SetDefaultBranchAsync_WithNonExistentCompany_ReturnsZero()
    {
        // Act
        var result = await _repository.SetDefaultBranchAsync(999, 1, "user");

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task SetDefaultBranchAsync_WithBranchFromDifferentCompany_ThrowsException()
    {
        // Arrange
        var company1 = new SysCompany
        {
            RowDesc = "Company 1",
            RowDescE = "Company 1",
            CompanyCode = "COMP1",
            IsActive = true,
            CreationUser = "test"
        };
        var company2 = new SysCompany
        {
            RowDesc = "Company 2",
            RowDescE = "Company 2",
            CompanyCode = "COMP2",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Companies.AddRangeAsync(company1, company2);
        await _context.SaveChangesAsync();

        var branch = new SysBranch
        {
            ParRowId = company2.RowId,
            RowDesc = "Branch",
            RowDescE = "Branch",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Branches.AddAsync(branch);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _repository.SetDefaultBranchAsync(company1.RowId, branch.RowId, "user"));
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task CreateAsync_WithNullCompany_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _repository.CreateAsync(null!));
    }

    [Fact]
    public async Task UpdateAsync_WithNullCompany_ThrowsException()
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
    public async Task CreateAsync_WithVeryLongStrings_Succeeds()
    {
        // Arrange
        var longString = new string('A', 200); // Max length for RowDesc
        var company = new SysCompany
        {
            RowDesc = longString,
            RowDescE = longString,
            CompanyCode = "LONG",
            CreationUser = "test"
        };

        // Act
        var result = await _repository.CreateAsync(company);

        // Assert
        Assert.True(result > 0);
    }

    [Fact]
    public async Task CreateAsync_WithSpecialCharacters_Succeeds()
    {
        // Arrange
        var company = new SysCompany
        {
            RowDesc = "شركة !@#$%^&*()",
            RowDescE = "Company !@#$%^&*()",
            CompanyCode = "SPECIAL",
            CreationUser = "test"
        };

        // Act
        var result = await _repository.CreateAsync(company);

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
            () => new CompanyRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new CompanyRepository(_context, null!));
    }

    #endregion
}
