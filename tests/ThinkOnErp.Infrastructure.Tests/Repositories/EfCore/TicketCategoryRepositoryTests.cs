using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for TicketCategoryRepository using EF Core InMemory provider.
/// Tests all CRUD operations, usage statistics, multi-tenancy filtering, and exception scenarios.
/// </summary>
public class TicketCategoryRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly TicketCategoryRepository _repository;
    private readonly Mock<ILogger<TicketCategoryRepository>> _loggerMock;

    public TicketCategoryRepositoryTests()
    {
        // Create InMemory database with unique name for each test instance
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_TicketCategory_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<TicketCategoryRepository>>();
        _repository = new TicketCategoryRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllCategories_OrderedByDisplayOrder()
    {
        // Arrange
        var categories = new List<SysTicketCategory>
        {
            new SysTicketCategory
            {
                CategoryNameAr = "تقني",
                CategoryNameEn = "Technical",
                DisplayOrder = 2,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketCategory
            {
                CategoryNameAr = "مالي",
                CategoryNameEn = "Financial",
                DisplayOrder = 1,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketCategory
            {
                CategoryNameAr = "عام",
                CategoryNameEn = "General",
                DisplayOrder = 3,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.TicketCategories.AddRangeAsync(categories);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("Financial", result[0].CategoryNameEn); // Ordered by DisplayOrder
        Assert.Equal("Technical", result[1].CategoryNameEn);
        Assert.Equal("General", result[2].CategoryNameEn);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoCategories()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsCategory_WhenExists()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            RowId = 1,
            CategoryNameAr = "تقني",
            CategoryNameEn = "Technical",
            CategoryDescriptionAr = "وصف تقني",
            CategoryDescriptionEn = "Technical Description",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketCategories.AddAsync(category);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.RowId);
        Assert.Equal("Technical", result.CategoryNameEn);
        Assert.Equal("Technical Description", result.CategoryDescriptionEn);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CreatesNewCategory_ReturnsGeneratedId()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            CategoryNameAr = "فئة جديدة",
            CategoryNameEn = "New Category",
            CategoryDescriptionEn = "Test Description",
            DisplayOrder = 1,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(category);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, category.RowId);
        Assert.NotNull(category.CreationDate);
        Assert.True(category.IsActive);

        // Verify in database
        var savedCategory = await _context.TicketCategories.FindAsync(result);
        Assert.NotNull(savedCategory);
        Assert.Equal("New Category", savedCategory.CategoryNameEn);
    }

    [Fact]
    public async Task CreateAsync_SetsCreationDate_WhenNotProvided()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            CategoryNameEn = "Test Category",
            DisplayOrder = 1,
            CreationUser = "test"
        };

        // Act
        await _repository.CreateAsync(category);

        // Assert
        Assert.NotNull(category.CreationDate);
        Assert.True(category.CreationDate.Value <= DateTime.Now);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesExistingCategory_ReturnsRowsAffected()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            CategoryNameEn = "Original Name",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketCategories.AddAsync(category);
        await _context.SaveChangesAsync();

        // Modify the category
        category.CategoryNameEn = "Updated Name";
        category.CategoryDescriptionEn = "Updated Description";
        category.DisplayOrder = 2;
        category.UpdateUser = "update_user";

        // Act
        var result = await _repository.UpdateAsync(category);

        // Assert
        Assert.Equal(1, result);
        Assert.NotNull(category.UpdateDate);

        // Verify in database
        var updatedCategory = await _context.TicketCategories.FindAsync(category.RowId);
        Assert.NotNull(updatedCategory);
        Assert.Equal("Updated Name", updatedCategory.CategoryNameEn);
        Assert.Equal(2, updatedCategory.DisplayOrder);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesCategory_ReturnsRowsAffected()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            CategoryNameEn = "To Delete",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketCategories.AddAsync(category);
        await _context.SaveChangesAsync();
        var categoryId = category.RowId;

        // Act
        var result = await _repository.DeleteAsync(categoryId, "delete_user");

        // Assert
        Assert.Equal(1, result);

        // Verify category is soft deleted
        var deletedCategory = await _context.TicketCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.RowId == categoryId);
        Assert.NotNull(deletedCategory);
        Assert.False(deletedCategory.IsActive);
        Assert.Equal("delete_user", deletedCategory.UpdateUser);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ReturnsZero()
    {
        // Act
        var result = await _repository.DeleteAsync(999, "user");

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task DeleteAsync_ThrowsException_WhenCategoryIsInUse()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            RowId = 1,
            CategoryNameEn = "In Use Category",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketCategories.AddAsync(category);

        // Create test data for ticket
        var company = new SysCompany
        {
            RowId = 1,
            RowDesc = "Test",
            RowDescE = "Test",
            CompanyCode = "TEST",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Companies.AddAsync(company);

        var branch = new SysBranch
        {
            RowId = 1,
            ParRowId = company.RowId,
            RowDesc = "Test",
            RowDescE = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Branches.AddAsync(branch);

        var user = new SysUser
        {
            RowId = 1,
            CompanyId = company.RowId,
            BranchId = branch.RowId,
            UserName = "test",
            PasswordHash = "hash",
            FullName = "Test",
            Email = "test@test.com",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Users.AddAsync(user);

        var priority = new SysTicketPriority
        {
            RowId = 1,
            PriorityNameEn = "Normal",
            PriorityLevel = 3,
            SlaHours = 24,
            DisplayOrder = 3,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketPriorities.AddAsync(priority);

        var type = new SysTicketType
        {
            RowId = 1,
            TypeNameEn = "Support",
            DefaultPriorityId = priority.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketTypes.AddAsync(type);

        var status = new SysTicketStatus
        {
            RowId = 1,
            StatusNameEn = "Open",
            StatusCode = "OPEN",
            IsFinalStatus = false,
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(status);

        var ticket = new SysRequestTicket
        {
            CompanyId = company.RowId,
            BranchId = branch.RowId,
            RequesterId = user.RowId,
            TicketTypeId = type.RowId,
            TicketStatusId = status.RowId,
            TicketPriorityId = priority.RowId,
            TicketCategoryId = category.RowId,
            TitleEn = "Test Ticket",
            Description = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _repository.DeleteAsync(category.RowId, "user"));
    }

    #endregion

    #region IsInUseAsync Tests

    [Fact]
    public async Task IsInUseAsync_ReturnsTrue_WhenCategoryIsUsed()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            RowId = 1,
            CategoryNameEn = "Used Category",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketCategories.AddAsync(category);

        // Create test data for ticket
        var company = new SysCompany
        {
            RowId = 1,
            RowDesc = "Test",
            RowDescE = "Test",
            CompanyCode = "TEST",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Companies.AddAsync(company);

        var branch = new SysBranch
        {
            RowId = 1,
            ParRowId = company.RowId,
            RowDesc = "Test",
            RowDescE = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Branches.AddAsync(branch);

        var user = new SysUser
        {
            RowId = 1,
            CompanyId = company.RowId,
            BranchId = branch.RowId,
            UserName = "test",
            PasswordHash = "hash",
            FullName = "Test",
            Email = "test@test.com",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Users.AddAsync(user);

        var priority = new SysTicketPriority
        {
            RowId = 1,
            PriorityNameEn = "Normal",
            PriorityLevel = 3,
            SlaHours = 24,
            DisplayOrder = 3,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketPriorities.AddAsync(priority);

        var type = new SysTicketType
        {
            RowId = 1,
            TypeNameEn = "Support",
            DefaultPriorityId = priority.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketTypes.AddAsync(type);

        var status = new SysTicketStatus
        {
            RowId = 1,
            StatusNameEn = "Open",
            StatusCode = "OPEN",
            IsFinalStatus = false,
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(status);

        var ticket = new SysRequestTicket
        {
            CompanyId = company.RowId,
            BranchId = branch.RowId,
            RequesterId = user.RowId,
            TicketTypeId = type.RowId,
            TicketStatusId = status.RowId,
            TicketPriorityId = priority.RowId,
            TicketCategoryId = category.RowId,
            TitleEn = "Test Ticket",
            Description = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsInUseAsync(category.RowId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsInUseAsync_ReturnsFalse_WhenCategoryIsNotUsed()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            RowId = 1,
            CategoryNameEn = "Unused Category",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketCategories.AddAsync(category);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsInUseAsync(category.RowId);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region GetUsageStatisticsAsync Tests

    [Fact]
    public async Task GetUsageStatisticsAsync_ReturnsStatistics_WithMultiTenancyFiltering()
    {
        // Arrange
        var category1 = new SysTicketCategory
        {
            RowId = 1,
            CategoryNameEn = "Category 1",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        var category2 = new SysTicketCategory
        {
            RowId = 2,
            CategoryNameEn = "Category 2",
            DisplayOrder = 2,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketCategories.AddRangeAsync(category1, category2);

        // Create test data
        var company1 = new SysCompany
        {
            RowId = 1,
            RowDesc = "Company 1",
            RowDescE = "Company 1",
            CompanyCode = "COMP1",
            IsActive = true,
            CreationUser = "test"
        };
        var company2 = new SysCompany
        {
            RowId = 2,
            RowDesc = "Company 2",
            RowDescE = "Company 2",
            CompanyCode = "COMP2",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Companies.AddRangeAsync(company1, company2);

        var branch1 = new SysBranch
        {
            RowId = 1,
            ParRowId = company1.RowId,
            RowDesc = "Branch 1",
            RowDescE = "Branch 1",
            IsActive = true,
            CreationUser = "test"
        };
        var branch2 = new SysBranch
        {
            RowId = 2,
            ParRowId = company2.RowId,
            RowDesc = "Branch 2",
            RowDescE = "Branch 2",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Branches.AddRangeAsync(branch1, branch2);

        var user = new SysUser
        {
            RowId = 1,
            CompanyId = company1.RowId,
            BranchId = branch1.RowId,
            UserName = "test",
            PasswordHash = "hash",
            FullName = "Test",
            Email = "test@test.com",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Users.AddAsync(user);

        var priority = new SysTicketPriority
        {
            RowId = 1,
            PriorityNameEn = "Normal",
            PriorityLevel = 3,
            SlaHours = 24,
            DisplayOrder = 3,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketPriorities.AddAsync(priority);

        var type = new SysTicketType
        {
            RowId = 1,
            TypeNameEn = "Support",
            DefaultPriorityId = priority.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketTypes.AddAsync(type);

        var status = new SysTicketStatus
        {
            RowId = 1,
            StatusNameEn = "Open",
            StatusCode = "OPEN",
            IsFinalStatus = false,
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(status);

        // Create 3 tickets for category1 in company1 and 1 ticket for category2 in company2
        for (int i = 0; i < 3; i++)
        {
            await _context.Tickets.AddAsync(new SysRequestTicket
            {
                CompanyId = company1.RowId,
                BranchId = branch1.RowId,
                RequesterId = user.RowId,
                TicketTypeId = type.RowId,
                TicketStatusId = status.RowId,
                TicketPriorityId = priority.RowId,
                TicketCategoryId = category1.RowId,
                TitleEn = $"Ticket {i}",
                Description = "Test",
                IsActive = true,
                CreationUser = "test",
                CreationDate = DateTime.Now
            });
        }

        await _context.Tickets.AddAsync(new SysRequestTicket
        {
            CompanyId = company2.RowId,
            BranchId = branch2.RowId,
            RequesterId = user.RowId,
            TicketTypeId = type.RowId,
            TicketStatusId = status.RowId,
            TicketPriorityId = priority.RowId,
            TicketCategoryId = category2.RowId,
            TitleEn = "Ticket Category2",
            Description = "Test",
            IsActive = true,
            CreationUser = "test",
            CreationDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        // Act - Filter by company1
        var result = await _repository.GetUsageStatisticsAsync(companyId: company1.RowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count); // Both categories returned, but only category1 has tickets
        Assert.Equal("Category 1", result[0].Category.CategoryNameEn);
        Assert.Equal(3, result[0].TicketCount);
        Assert.Equal("Category 2", result[1].Category.CategoryNameEn);
        Assert.Equal(0, result[1].TicketCount);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketCategoryRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketCategoryRepository(_context, null!));
    }

    #endregion
}
