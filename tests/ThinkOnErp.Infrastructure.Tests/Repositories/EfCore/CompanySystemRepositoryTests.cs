using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for CompanySystemRepository using EF Core InMemory provider.
/// Tests composite key operations, CRUD operations, and relationship queries.
/// </summary>
public class CompanySystemRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly CompanySystemRepository _repository;
    private readonly Mock<ILogger<CompanySystemRepository>> _loggerMock;

    public CompanySystemRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_CompanySystem_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<CompanySystemRepository>>();
        _repository = new CompanySystemRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Test Data Setup

    private async Task<(SysCompany company, SysSystem system)> CreateTestCompanyAndSystemAsync()
    {
        var company = new SysCompany
        {
            RowId = 1,
            RowDesc = "Test Company",
            RowDescE = "Test Company",
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

        await _context.Companies.AddAsync(company);
        await _context.Systems.AddAsync(system);
        await _context.SaveChangesAsync();

        return (company, system);
    }

    #endregion

    #region GetByCompanyIdAsync Tests

    [Fact]
    public async Task GetByCompanyIdAsync_ReturnsSystemAssignments_OrderedBySystemId()
    {
        // Arrange
        var (company, _) = await CreateTestCompanyAndSystemAsync();
        
        var system2 = new SysSystem
        {
            RowId = 2,
            SystemName = "System 2",
            SystemCode = "SYS2",
            IsActive = true,
            CreationUser = "test"
        };
        var system3 = new SysSystem
        {
            RowId = 3,
            SystemName = "System 3",
            SystemCode = "SYS3",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Systems.AddRangeAsync(system2, system3);
        await _context.SaveChangesAsync();

        var companySystems = new List<SysCompanySystem>
        {
            new SysCompanySystem
            {
                CompanyId = company.RowId,
                SystemId = 3,
                IsAllowed = true,
                CreationUser = "test"
            },
            new SysCompanySystem
            {
                CompanyId = company.RowId,
                SystemId = 1,
                IsAllowed = true,
                CreationUser = "test"
            },
            new SysCompanySystem
            {
                CompanyId = company.RowId,
                SystemId = 2,
                IsAllowed = false,
                CreationUser = "test"
            }
        };
        await _context.CompanySystems.AddRangeAsync(companySystems);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByCompanyIdAsync(company.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].SystemId); // Ordered by SystemId
        Assert.Equal(2, result[1].SystemId);
        Assert.Equal(3, result[2].SystemId);
    }

    [Fact]
    public async Task GetByCompanyIdAsync_ReturnsEmptyList_WhenNoAssignments()
    {
        // Arrange
        var (company, _) = await CreateTestCompanyAndSystemAsync();

        // Act
        var result = await _repository.GetByCompanyIdAsync(company.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region GetBySystemIdAsync Tests

    [Fact]
    public async Task GetBySystemIdAsync_ReturnsCompanyAssignments_OrderedByCompanyId()
    {
        // Arrange
        var (_, system) = await CreateTestCompanyAndSystemAsync();
        
        var company2 = new SysCompany
        {
            RowId = 2,
            RowDesc = "Company 2",
            RowDescE = "Company 2",
            IsActive = true,
            CreationUser = "test"
        };
        var company3 = new SysCompany
        {
            RowId = 3,
            RowDesc = "Company 3",
            RowDescE = "Company 3",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Companies.AddRangeAsync(company2, company3);
        await _context.SaveChangesAsync();

        var companySystems = new List<SysCompanySystem>
        {
            new SysCompanySystem
            {
                CompanyId = 3,
                SystemId = system.RowId,
                IsAllowed = true,
                CreationUser = "test"
            },
            new SysCompanySystem
            {
                CompanyId = 1,
                SystemId = system.RowId,
                IsAllowed = true,
                CreationUser = "test"
            },
            new SysCompanySystem
            {
                CompanyId = 2,
                SystemId = system.RowId,
                IsAllowed = false,
                CreationUser = "test"
            }
        };
        await _context.CompanySystems.AddRangeAsync(companySystems);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetBySystemIdAsync(system.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].CompanyId); // Ordered by CompanyId
        Assert.Equal(2, result[1].CompanyId);
        Assert.Equal(3, result[2].CompanyId);
    }

    [Fact]
    public async Task GetBySystemIdAsync_ReturnsEmptyList_WhenNoAssignments()
    {
        // Arrange
        var (_, system) = await CreateTestCompanyAndSystemAsync();

        // Act
        var result = await _repository.GetBySystemIdAsync(system.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region GetByIdAsync Tests (Composite Key)

    [Fact]
    public async Task GetByIdAsync_ReturnsAssignment_WhenExists()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = true,
            GrantedBy = 100,
            Notes = "Test notes",
            CreationUser = "test_user"
        };
        await _context.CompanySystems.AddAsync(companySystem);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(company.RowId, system.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(company.RowId, result.CompanyId);
        Assert.Equal(system.RowId, result.SystemId);
        Assert.True(result.IsAllowed);
        Assert.Equal(100, result.GrantedBy);
        Assert.Equal("Test notes", result.Notes);
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
    public async Task GetByIdAsync_ReturnsNull_WhenOnlyCompanyIdMatches()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = true,
            CreationUser = "test"
        };
        await _context.CompanySystems.AddAsync(companySystem);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(company.RowId, 999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenOnlySystemIdMatches()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = true,
            CreationUser = "test"
        };
        await _context.CompanySystems.AddAsync(companySystem);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(999, system.RowId);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region IsSystemAllowedAsync Tests

    [Fact]
    public async Task IsSystemAllowedAsync_ReturnsTrue_WhenSystemIsAllowed()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = true,
            CreationUser = "test"
        };
        await _context.CompanySystems.AddAsync(companySystem);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsSystemAllowedAsync(company.RowId, system.RowId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsSystemAllowedAsync_ReturnsFalse_WhenSystemIsNotAllowed()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = false,
            CreationUser = "test"
        };
        await _context.CompanySystems.AddAsync(companySystem);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsSystemAllowedAsync(company.RowId, system.RowId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsSystemAllowedAsync_ReturnsFalse_WhenAssignmentDoesNotExist()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();

        // Act
        var result = await _repository.IsSystemAllowedAsync(company.RowId, system.RowId);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CreatesNewAssignment_ReturnsGeneratedId()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = true,
            GrantedBy = 100,
            Notes = "Test assignment",
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(companySystem);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, companySystem.RowId);
        Assert.NotNull(companySystem.CreationDate);
        Assert.NotNull(companySystem.GrantedDate);

        // Verify in database
        var savedAssignment = await _context.CompanySystems
            .FirstOrDefaultAsync(cs => cs.CompanyId == company.RowId && cs.SystemId == system.RowId);
        Assert.NotNull(savedAssignment);
        Assert.True(savedAssignment.IsAllowed);
        Assert.Equal(100, savedAssignment.GrantedBy);
    }

    [Fact]
    public async Task CreateAsync_SetsCreationDate_WhenNotProvided()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = true,
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateAsync(companySystem);

        // Assert
        Assert.NotNull(companySystem.CreationDate);
        Assert.True(companySystem.CreationDate.Value <= DateTime.Now);
    }

    [Fact]
    public async Task CreateAsync_SetsGrantedDate_WhenIsAllowedIsTrue()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = true,
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateAsync(companySystem);

        // Assert
        Assert.NotNull(companySystem.GrantedDate);
        Assert.True(companySystem.GrantedDate.Value <= DateTime.Now);
    }

    [Fact]
    public async Task CreateAsync_DoesNotSetGrantedDate_WhenIsAllowedIsFalse()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = false,
            CreationUser = "test_user"
        };

        // Act
        await _repository.CreateAsync(companySystem);

        // Assert
        Assert.Null(companySystem.GrantedDate);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesExistingAssignment_ReturnsRowsAffected()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = false,
            CreationUser = "test_user"
        };
        await _context.CompanySystems.AddAsync(companySystem);
        await _context.SaveChangesAsync();

        // Modify assignment
        companySystem.IsAllowed = true;
        companySystem.GrantedBy = 200;
        companySystem.Notes = "Updated notes";
        companySystem.UpdateUser = "update_user";

        // Act
        var result = await _repository.UpdateAsync(companySystem);

        // Assert
        Assert.Equal(1, result);
        Assert.NotNull(companySystem.UpdateDate);

        // Verify in database
        var updatedAssignment = await _context.CompanySystems
            .FirstOrDefaultAsync(cs => cs.CompanyId == company.RowId && cs.SystemId == system.RowId);
        Assert.NotNull(updatedAssignment);
        Assert.True(updatedAssignment.IsAllowed);
        Assert.Equal(200, updatedAssignment.GrantedBy);
        Assert.Equal("Updated notes", updatedAssignment.Notes);
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdateDate_Automatically()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = true,
            CreationUser = "test_user"
        };
        await _context.CompanySystems.AddAsync(companySystem);
        await _context.SaveChangesAsync();

        companySystem.IsAllowed = false;

        // Act
        await _repository.UpdateAsync(companySystem);

        // Assert
        Assert.NotNull(companySystem.UpdateDate);
        Assert.True(companySystem.UpdateDate.Value <= DateTime.Now);
    }

    #endregion

    #region DeleteAsync Tests (Composite Key)

    [Fact]
    public async Task DeleteAsync_DeletesAssignment_ReturnsRowsAffected()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = true,
            CreationUser = "test_user"
        };
        await _context.CompanySystems.AddAsync(companySystem);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.DeleteAsync(company.RowId, system.RowId);

        // Assert
        Assert.Equal(1, result);

        // Verify assignment is deleted
        var deletedAssignment = await _context.CompanySystems
            .FirstOrDefaultAsync(cs => cs.CompanyId == company.RowId && cs.SystemId == system.RowId);
        Assert.Null(deletedAssignment);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentCompositeKey_ReturnsZero()
    {
        // Act
        var result = await _repository.DeleteAsync(999, 999);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task DeleteAsync_WithPartialMatch_ReturnsZero()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = true,
            CreationUser = "test_user"
        };
        await _context.CompanySystems.AddAsync(companySystem);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.DeleteAsync(company.RowId, 999);

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region SetSystemAccessAsync Tests (Upsert)

    [Fact]
    public async Task SetSystemAccessAsync_CreatesNewAssignment_WhenNotExists()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();

        // Act
        var result = await _repository.SetSystemAccessAsync(
            company.RowId, system.RowId, true, 100, "Test notes", "test_user");

        // Assert
        Assert.True(result > 0);

        var assignment = await _context.CompanySystems
            .FirstOrDefaultAsync(cs => cs.CompanyId == company.RowId && cs.SystemId == system.RowId);
        Assert.NotNull(assignment);
        Assert.True(assignment.IsAllowed);
        Assert.Equal(100, assignment.GrantedBy);
        Assert.Equal("Test notes", assignment.Notes);
        Assert.NotNull(assignment.GrantedDate);
        Assert.Null(assignment.RevokedDate);
    }

    [Fact]
    public async Task SetSystemAccessAsync_UpdatesExistingAssignment_WhenExists()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = false,
            CreationUser = "original_user"
        };
        await _context.CompanySystems.AddAsync(companySystem);
        await _context.SaveChangesAsync();
        var originalId = companySystem.RowId;

        // Act
        var result = await _repository.SetSystemAccessAsync(
            company.RowId, system.RowId, true, 200, "Updated notes", "update_user");

        // Assert
        Assert.Equal(originalId, result); // Returns existing ID

        var updatedAssignment = await _context.CompanySystems
            .FirstOrDefaultAsync(cs => cs.CompanyId == company.RowId && cs.SystemId == system.RowId);
        Assert.NotNull(updatedAssignment);
        Assert.True(updatedAssignment.IsAllowed);
        Assert.Equal(200, updatedAssignment.GrantedBy);
        Assert.Equal("Updated notes", updatedAssignment.Notes);
        Assert.NotNull(updatedAssignment.GrantedDate);
        Assert.Null(updatedAssignment.RevokedDate);
    }

    [Fact]
    public async Task SetSystemAccessAsync_SetsRevokedDate_WhenIsAllowedIsFalse()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();

        // Act
        var result = await _repository.SetSystemAccessAsync(
            company.RowId, system.RowId, false, 100, "Revoked", "test_user");

        // Assert
        var assignment = await _context.CompanySystems
            .FirstOrDefaultAsync(cs => cs.CompanyId == company.RowId && cs.SystemId == system.RowId);
        Assert.NotNull(assignment);
        Assert.False(assignment.IsAllowed);
        Assert.Null(assignment.GrantedDate);
        Assert.NotNull(assignment.RevokedDate);
    }

    #endregion

    #region GetAllowedSystemIdsAsync Tests

    [Fact]
    public async Task GetAllowedSystemIdsAsync_ReturnsOnlyAllowedSystems()
    {
        // Arrange
        var (company, _) = await CreateTestCompanyAndSystemAsync();
        
        var system2 = new SysSystem
        {
            RowId = 2,
            SystemName = "System 2",
            SystemCode = "SYS2",
            IsActive = true,
            CreationUser = "test"
        };
        var system3 = new SysSystem
        {
            RowId = 3,
            SystemName = "System 3",
            SystemCode = "SYS3",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Systems.AddRangeAsync(system2, system3);
        await _context.SaveChangesAsync();

        var companySystems = new List<SysCompanySystem>
        {
            new SysCompanySystem
            {
                CompanyId = company.RowId,
                SystemId = 1,
                IsAllowed = true,
                CreationUser = "test"
            },
            new SysCompanySystem
            {
                CompanyId = company.RowId,
                SystemId = 2,
                IsAllowed = false, // Not allowed
                CreationUser = "test"
            },
            new SysCompanySystem
            {
                CompanyId = company.RowId,
                SystemId = 3,
                IsAllowed = true,
                CreationUser = "test"
            }
        };
        await _context.CompanySystems.AddRangeAsync(companySystems);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllowedSystemIdsAsync(company.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(1L, result);
        Assert.Contains(3L, result);
        Assert.DoesNotContain(2L, result);
    }

    [Fact]
    public async Task GetAllowedSystemIdsAsync_ReturnsEmptyList_WhenNoAllowedSystems()
    {
        // Arrange
        var (company, system) = await CreateTestCompanyAndSystemAsync();
        
        var companySystem = new SysCompanySystem
        {
            CompanyId = company.RowId,
            SystemId = system.RowId,
            IsAllowed = false,
            CreationUser = "test"
        };
        await _context.CompanySystems.AddAsync(companySystem);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllowedSystemIdsAsync(company.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllowedSystemIdsAsync_ReturnsEmptyList_WhenNoAssignments()
    {
        // Arrange
        var (company, _) = await CreateTestCompanyAndSystemAsync();

        // Act
        var result = await _repository.GetAllowedSystemIdsAsync(company.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new CompanySystemRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new CompanySystemRepository(_context, null!));
    }

    #endregion
}
