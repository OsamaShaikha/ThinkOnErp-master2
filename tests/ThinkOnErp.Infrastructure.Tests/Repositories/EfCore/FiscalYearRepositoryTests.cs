using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for FiscalYearRepository using EF Core InMemory provider.
/// Tests all CRUD operations, exception scenarios, and null handling.
/// </summary>
public class FiscalYearRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly FiscalYearRepository _repository;
    private readonly Mock<ILogger<FiscalYearRepository>> _loggerMock;

    public FiscalYearRepositoryTests()
    {
        // Create InMemory database with unique name for each test instance
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_FiscalYear_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<FiscalYearRepository>>();
        _repository = new FiscalYearRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllFiscalYears_OrderedByStartDateDescending()
    {
        // Arrange
        var fiscalYears = new List<SysFiscalYear>
        {
            new SysFiscalYear
            {
                RowId = 1,
                CompanyId = 1,
                BranchId = 1,
                FiscalYearCode = "FY2023",
                RowDesc = "2023",
                RowDescE = "2023",
                StartDate = new DateTime(2023, 1, 1),
                EndDate = new DateTime(2023, 12, 31),
                IsClosed = false,
                IsActive = true,
                CreationUser = "test"
            },
            new SysFiscalYear
            {
                RowId = 2,
                CompanyId = 1,
                BranchId = 1,
                FiscalYearCode = "FY2024",
                RowDesc = "2024",
                RowDescE = "2024",
                StartDate = new DateTime(2024, 1, 1),
                EndDate = new DateTime(2024, 12, 31),
                IsClosed = false,
                IsActive = true,
                CreationUser = "test"
            },
            new SysFiscalYear
            {
                RowId = 3,
                CompanyId = 1,
                BranchId = 1,
                FiscalYearCode = "FY2022",
                RowDesc = "2022",
                RowDescE = "2022",
                StartDate = new DateTime(2022, 1, 1),
                EndDate = new DateTime(2022, 12, 31),
                IsClosed = true,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.FiscalYears.AddRangeAsync(fiscalYears);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("FY2024", result[0].FiscalYearCode); // Most recent first
        Assert.Equal("FY2023", result[1].FiscalYearCode);
        Assert.Equal("FY2022", result[2].FiscalYearCode);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoFiscalYears()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_FiltersOutInactiveFiscalYears()
    {
        // Arrange
        var fiscalYears = new List<SysFiscalYear>
        {
            new SysFiscalYear
            {
                RowId = 1,
                CompanyId = 1,
                BranchId = 1,
                FiscalYearCode = "FY2023",
                StartDate = new DateTime(2023, 1, 1),
                EndDate = new DateTime(2023, 12, 31),
                IsActive = true,
                CreationUser = "test"
            },
            new SysFiscalYear
            {
                RowId = 2,
                CompanyId = 1,
                BranchId = 1,
                FiscalYearCode = "FY2022",
                StartDate = new DateTime(2022, 1, 1),
                EndDate = new DateTime(2022, 12, 31),
                IsActive = false, // Soft deleted
                CreationUser = "test"
            }
        };
        await _context.FiscalYears.AddRangeAsync(fiscalYears);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("FY2023", result[0].FiscalYearCode);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsFiscalYear_WhenExists()
    {
        // Arrange
        var fiscalYear = new SysFiscalYear
        {
            RowId = 1,
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2024",
            RowDesc = "السنة المالية 2024",
            RowDescE = "Fiscal Year 2024",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            IsClosed = false,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.FiscalYears.AddAsync(fiscalYear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.RowId);
        Assert.Equal("FY2024", result.FiscalYearCode);
        Assert.Equal("Fiscal Year 2024", result.RowDescE);
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
    public async Task GetByIdAsync_ReturnsNull_WhenFiscalYearIsInactive()
    {
        // Arrange
        var fiscalYear = new SysFiscalYear
        {
            RowId = 1,
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2023",
            StartDate = new DateTime(2023, 1, 1),
            EndDate = new DateTime(2023, 12, 31),
            IsActive = false, // Soft deleted
            CreationUser = "test"
        };
        await _context.FiscalYears.AddAsync(fiscalYear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(1);

        // Assert
        Assert.Null(result); // Global query filter excludes inactive records
    }

    #endregion

    #region GetByCompanyIdAsync Tests

    [Fact]
    public async Task GetByCompanyIdAsync_ReturnsFiscalYearsForCompany()
    {
        // Arrange
        var fiscalYears = new List<SysFiscalYear>
        {
            new SysFiscalYear
            {
                RowId = 1,
                CompanyId = 1,
                BranchId = 1,
                FiscalYearCode = "FY2024",
                StartDate = new DateTime(2024, 1, 1),
                EndDate = new DateTime(2024, 12, 31),
                IsActive = true,
                CreationUser = "test"
            },
            new SysFiscalYear
            {
                RowId = 2,
                CompanyId = 1,
                BranchId = 2,
                FiscalYearCode = "FY2024",
                StartDate = new DateTime(2024, 1, 1),
                EndDate = new DateTime(2024, 12, 31),
                IsActive = true,
                CreationUser = "test"
            },
            new SysFiscalYear
            {
                RowId = 3,
                CompanyId = 2,
                BranchId = 3,
                FiscalYearCode = "FY2024",
                StartDate = new DateTime(2024, 1, 1),
                EndDate = new DateTime(2024, 12, 31),
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.FiscalYears.AddRangeAsync(fiscalYears);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByCompanyIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, fy => Assert.Equal(1, fy.CompanyId));
    }

    [Fact]
    public async Task GetByCompanyIdAsync_ReturnsEmptyList_WhenNoFiscalYearsForCompany()
    {
        // Act
        var result = await _repository.GetByCompanyIdAsync(999);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region GetByBranchIdAsync Tests

    [Fact]
    public async Task GetByBranchIdAsync_ReturnsFiscalYearsForBranch()
    {
        // Arrange
        var fiscalYears = new List<SysFiscalYear>
        {
            new SysFiscalYear
            {
                RowId = 1,
                CompanyId = 1,
                BranchId = 1,
                FiscalYearCode = "FY2024",
                StartDate = new DateTime(2024, 1, 1),
                EndDate = new DateTime(2024, 12, 31),
                IsActive = true,
                CreationUser = "test"
            },
            new SysFiscalYear
            {
                RowId = 2,
                CompanyId = 1,
                BranchId = 1,
                FiscalYearCode = "FY2023",
                StartDate = new DateTime(2023, 1, 1),
                EndDate = new DateTime(2023, 12, 31),
                IsActive = true,
                CreationUser = "test"
            },
            new SysFiscalYear
            {
                RowId = 3,
                CompanyId = 1,
                BranchId = 2,
                FiscalYearCode = "FY2024",
                StartDate = new DateTime(2024, 1, 1),
                EndDate = new DateTime(2024, 12, 31),
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.FiscalYears.AddRangeAsync(fiscalYears);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByBranchIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, fy => Assert.Equal(1, fy.BranchId));
        Assert.Equal("FY2024", result[0].FiscalYearCode); // Most recent first
        Assert.Equal("FY2023", result[1].FiscalYearCode);
    }

    [Fact]
    public async Task GetByBranchIdAsync_ReturnsEmptyList_WhenNoFiscalYearsForBranch()
    {
        // Act
        var result = await _repository.GetByBranchIdAsync(999);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CreatesNewFiscalYear_ReturnsGeneratedId()
    {
        // Arrange
        var fiscalYear = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2024",
            RowDesc = "السنة المالية 2024",
            RowDescE = "Fiscal Year 2024",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            IsClosed = false,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(fiscalYear);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, fiscalYear.RowId);
        Assert.NotNull(fiscalYear.CreationDate);
        Assert.True(fiscalYear.IsActive);
        Assert.False(fiscalYear.IsClosed);

        // Verify in database
        var savedFiscalYear = await _context.FiscalYears.FindAsync(result);
        Assert.NotNull(savedFiscalYear);
        Assert.Equal("FY2024", savedFiscalYear.FiscalYearCode);
    }

    [Fact]
    public async Task CreateAsync_SetsCreationDate_WhenNotProvided()
    {
        // Arrange
        var fiscalYear = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2024",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateAsync(fiscalYear);

        // Assert
        Assert.NotNull(fiscalYear.CreationDate);
        Assert.True(fiscalYear.CreationDate.Value <= DateTime.Now);
    }

    [Fact]
    public async Task CreateAsync_SetsIsActiveToTrue_ByDefault()
    {
        // Arrange
        var fiscalYear = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2024",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateAsync(fiscalYear);

        // Assert
        Assert.True(fiscalYear.IsActive);
    }

    [Fact]
    public async Task CreateAsync_SetsIsClosedToFalse_ByDefault()
    {
        // Arrange
        var fiscalYear = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2024",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateAsync(fiscalYear);

        // Assert
        Assert.False(fiscalYear.IsClosed);
    }

    [Fact]
    public async Task CreateAsync_WithNullOptionalFields_Succeeds()
    {
        // Arrange
        var fiscalYear = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2024",
            RowDesc = null,
            RowDescE = null,
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(fiscalYear);

        // Assert
        Assert.True(result > 0);
        var savedFiscalYear = await _context.FiscalYears.FindAsync(result);
        Assert.NotNull(savedFiscalYear);
        Assert.Null(savedFiscalYear.RowDesc);
        Assert.Null(savedFiscalYear.RowDescE);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesExistingFiscalYear_ReturnsRowsAffected()
    {
        // Arrange
        var fiscalYear = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2024",
            RowDesc = "2024",
            RowDescE = "2024",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.FiscalYears.AddAsync(fiscalYear);
        await _context.SaveChangesAsync();

        // Modify the fiscal year
        fiscalYear.RowDescE = "Fiscal Year 2024";
        fiscalYear.UpdateUser = "update_user";

        // Act
        var result = await _repository.UpdateAsync(fiscalYear);

        // Assert
        Assert.Equal(1, result);
        Assert.NotNull(fiscalYear.UpdateDate);

        // Verify in database
        var updatedFiscalYear = await _context.FiscalYears.FindAsync(fiscalYear.RowId);
        Assert.NotNull(updatedFiscalYear);
        Assert.Equal("Fiscal Year 2024", updatedFiscalYear.RowDescE);
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdateDate_Automatically()
    {
        // Arrange
        var fiscalYear = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2024",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.FiscalYears.AddAsync(fiscalYear);
        await _context.SaveChangesAsync();

        fiscalYear.RowDescE = "Updated Description";

        // Act
        await _repository.UpdateAsync(fiscalYear);

        // Assert
        Assert.NotNull(fiscalYear.UpdateDate);
        Assert.True(fiscalYear.UpdateDate.Value <= DateTime.Now);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesFiscalYear_ReturnsRowsAffected()
    {
        // Arrange
        var fiscalYear = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2024",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.FiscalYears.AddAsync(fiscalYear);
        await _context.SaveChangesAsync();
        var fiscalYearId = fiscalYear.RowId;

        // Act
        var result = await _repository.DeleteAsync(fiscalYearId);

        // Assert
        Assert.Equal(1, result);

        // Verify fiscal year is soft deleted (IsActive = false)
        var deletedFiscalYear = await _context.FiscalYears
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.RowId == fiscalYearId);
        Assert.NotNull(deletedFiscalYear);
        Assert.False(deletedFiscalYear.IsActive);
        Assert.NotNull(deletedFiscalYear.UpdateDate);
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
    public async Task DeleteAsync_OnAlreadyDeletedFiscalYear_ReturnsOne()
    {
        // Arrange
        var fiscalYear = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2024",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            IsActive = false, // Already soft deleted
            CreationUser = "test_user"
        };
        await _context.FiscalYears.AddAsync(fiscalYear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.DeleteAsync(fiscalYear.RowId);

        // Assert
        Assert.Equal(1, result); // Can delete again (idempotent)
    }

    #endregion

    #region CloseAsync Tests

    [Fact]
    public async Task CloseAsync_ClosesFiscalYear_ReturnsRowsAffected()
    {
        // Arrange
        var fiscalYear = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2023",
            StartDate = new DateTime(2023, 1, 1),
            EndDate = new DateTime(2023, 12, 31),
            IsClosed = false,
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.FiscalYears.AddAsync(fiscalYear);
        await _context.SaveChangesAsync();
        var fiscalYearId = fiscalYear.RowId;

        // Act
        var result = await _repository.CloseAsync(fiscalYearId, "close_user");

        // Assert
        Assert.Equal(1, result);

        // Verify fiscal year is closed
        var closedFiscalYear = await _context.FiscalYears.FindAsync(fiscalYearId);
        Assert.NotNull(closedFiscalYear);
        Assert.True(closedFiscalYear.IsClosed);
        Assert.Equal("close_user", closedFiscalYear.UpdateUser);
        Assert.NotNull(closedFiscalYear.UpdateDate);
    }

    [Fact]
    public async Task CloseAsync_WithNonExistentId_ReturnsZero()
    {
        // Act
        var result = await _repository.CloseAsync(999, "close_user");

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CloseAsync_OnAlreadyClosedFiscalYear_ThrowsInvalidOperationException()
    {
        // Arrange
        var fiscalYear = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2023",
            StartDate = new DateTime(2023, 1, 1),
            EndDate = new DateTime(2023, 12, 31),
            IsClosed = true, // Already closed
            IsActive = true,
            CreationUser = "test_user"
        };
        await _context.FiscalYears.AddAsync(fiscalYear);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _repository.CloseAsync(fiscalYear.RowId, "close_user"));
        
        Assert.Contains("already closed", exception.Message);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task CreateAsync_WithNullFiscalYear_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _repository.CreateAsync(null!));
    }

    [Fact]
    public async Task UpdateAsync_WithNullFiscalYear_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _repository.UpdateAsync(null!));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CreateAsync_WithSameFiscalYearCodeForDifferentBranches_Succeeds()
    {
        // Arrange
        var fiscalYear1 = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2024",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            CreationUser = "test"
        };
        var fiscalYear2 = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 2,
            FiscalYearCode = "FY2024",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            CreationUser = "test"
        };

        // Act
        var result1 = await _repository.CreateAsync(fiscalYear1);
        var result2 = await _repository.CreateAsync(fiscalYear2);

        // Assert
        Assert.True(result1 > 0);
        Assert.True(result2 > 0);
        Assert.NotEqual(result1, result2);
    }

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
    public async Task CreateAsync_WithEndDateBeforeStartDate_Succeeds()
    {
        // Arrange - Note: Business logic validation should be in application layer
        var fiscalYear = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2024",
            StartDate = new DateTime(2024, 12, 31),
            EndDate = new DateTime(2024, 1, 1), // Invalid but repository doesn't validate
            CreationUser = "test"
        };

        // Act
        var result = await _repository.CreateAsync(fiscalYear);

        // Assert
        Assert.True(result > 0); // Repository doesn't enforce business rules
    }

    [Fact]
    public async Task CreateAsync_WithVeryLongDescriptions_Succeeds()
    {
        // Arrange
        var longString = new string('A', 200); // Max length for RowDesc
        var fiscalYear = new SysFiscalYear
        {
            CompanyId = 1,
            BranchId = 1,
            FiscalYearCode = "FY2024",
            RowDesc = longString,
            RowDescE = longString,
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            CreationUser = "test"
        };

        // Act
        var result = await _repository.CreateAsync(fiscalYear);

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
            () => new FiscalYearRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new FiscalYearRepository(_context, null!));
    }

    #endregion
}
