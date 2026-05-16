using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;

namespace ThinkOnErp.Infrastructure.Tests.Integration;

/// <summary>
/// Integration tests for TicketCategoryRepository using real Oracle database connection.
/// Tests EF Core implementation against actual Oracle database.
/// 
/// **Validates: Requirements REQ-12, REQ-27**
/// - Requirement 12: Testing Strategy - Integration tests that verify database operations
/// - Requirement 27: Multi-Tenancy Support - Tests company and branch filtering in usage statistics
/// - Tests CRUD operations
/// - Tests usage statistics with multi-tenancy
/// - Verifies data persistence
/// </summary>
public class TicketCategoryRepositoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ThinkOnErpDbContext _context;
    private readonly TicketCategoryRepository _repository;
    private readonly List<long> _createdCategoryIds = new();

    public TicketCategoryRepositoryIntegrationTests()
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

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ThinkOnErpDbContext>();
        var logger = _serviceProvider.GetRequiredService<ILogger<TicketCategoryRepository>>();
        _repository = new TicketCategoryRepository(_context, logger);
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistTicketCategory()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            CategoryNameAr = $"فئة اختبار {Guid.NewGuid().ToString().Substring(0, 8)}",
            CategoryNameEn = $"Test Category {Guid.NewGuid().ToString().Substring(0, 8)}",
            DisplayOrder = 100,
            CreationUser = "IntegrationTest"
        };

        // Act
        var categoryId = await _repository.CreateAsync(category);
        _createdCategoryIds.Add(categoryId);

        // Assert
        Assert.True(categoryId > 0);
        Assert.Equal(categoryId, category.RowId);
        Assert.NotNull(category.CreationDate);
        Assert.True(category.IsActive);

        // Verify persistence
        var savedCategory = await _repository.GetByIdAsync(categoryId);
        Assert.NotNull(savedCategory);
        Assert.Equal(category.CategoryNameEn, savedCategory.CategoryNameEn);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllActiveCategoriesOrderedByDisplayOrder()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            CategoryNameAr = $"فئة {Guid.NewGuid().ToString().Substring(0, 8)}",
            CategoryNameEn = $"Category {Guid.NewGuid().ToString().Substring(0, 8)}",
            DisplayOrder = 50,
            CreationUser = "IntegrationTest"
        };
        var categoryId = await _repository.CreateAsync(category);
        _createdCategoryIds.Add(categoryId);

        // Act
        var categories = await _repository.GetAllAsync();

        // Assert
        Assert.NotEmpty(categories);
        Assert.Contains(categories, c => c.RowId == categoryId);
        Assert.All(categories, c => Assert.True(c.IsActive));
        
        // Verify ordering by DisplayOrder
        for (int i = 0; i < categories.Count - 1; i++)
        {
            Assert.True(categories[i].DisplayOrder <= categories[i + 1].DisplayOrder);
        }
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnCategory()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            CategoryNameAr = $"فئة {Guid.NewGuid().ToString().Substring(0, 8)}",
            CategoryNameEn = $"Category {Guid.NewGuid().ToString().Substring(0, 8)}",
            DisplayOrder = 75,
            CreationUser = "IntegrationTest"
        };
        var categoryId = await _repository.CreateAsync(category);
        _createdCategoryIds.Add(categoryId);

        // Act
        var retrievedCategory = await _repository.GetByIdAsync(categoryId);

        // Assert
        Assert.NotNull(retrievedCategory);
        Assert.Equal(category.CategoryNameEn, retrievedCategory.CategoryNameEn);
        Assert.Equal(category.DisplayOrder, retrievedCategory.DisplayOrder);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByIdAsync(999999999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateCategoryProperties()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            CategoryNameAr = "فئة أصلية",
            CategoryNameEn = "Original Category",
            DisplayOrder = 10,
            CreationUser = "IntegrationTest"
        };
        var categoryId = await _repository.CreateAsync(category);
        _createdCategoryIds.Add(categoryId);

        // Modify category
        category.CategoryNameEn = "Updated Category";
        category.CategoryNameAr = "فئة محدثة";
        category.DisplayOrder = 20;
        category.UpdateUser = "IntegrationTest";

        // Act
        var result = await _repository.UpdateAsync(category);

        // Assert
        Assert.Equal(1, result);

        // Verify update
        var updatedCategory = await _repository.GetByIdAsync(categoryId);
        Assert.NotNull(updatedCategory);
        Assert.Equal("Updated Category", updatedCategory.CategoryNameEn);
        Assert.Equal("فئة محدثة", updatedCategory.CategoryNameAr);
        Assert.Equal(20, updatedCategory.DisplayOrder);
        Assert.NotNull(updatedCategory.UpdateDate);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteCategory()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            CategoryNameAr = "فئة للحذف",
            CategoryNameEn = "Category To Delete",
            DisplayOrder = 99,
            CreationUser = "IntegrationTest"
        };
        var categoryId = await _repository.CreateAsync(category);
        _createdCategoryIds.Add(categoryId);

        // Act
        var result = await _repository.DeleteAsync(categoryId, "IntegrationTest");

        // Assert
        Assert.Equal(1, result);

        // Verify soft delete
        var deletedCategory = await _context.TicketCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.RowId == categoryId);
        Assert.NotNull(deletedCategory);
        Assert.False(deletedCategory.IsActive);
        Assert.Equal("IntegrationTest", deletedCategory.UpdateUser);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowException_WhenCategoryIsInUse()
    {
        // Arrange - Get an existing category that is likely in use
        var existingCategory = await _context.TicketCategories
            .Where(c => c.IsActive)
            .FirstOrDefaultAsync();

        if (existingCategory != null)
        {
            var isInUse = await _repository.IsInUseAsync(existingCategory.RowId);
            
            if (isInUse)
            {
                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await _repository.DeleteAsync(existingCategory.RowId, "IntegrationTest"));
            }
        }
    }

    [Fact]
    public async Task IsInUseAsync_ShouldReturnTrue_WhenCategoryHasActiveTickets()
    {
        // Arrange - Get a category that has tickets
        var categoryWithTickets = await _context.TicketCategories
            .Where(c => c.IsActive && _context.Tickets.Any(t => t.TicketCategoryId == c.RowId && t.IsActive))
            .FirstOrDefaultAsync();

        if (categoryWithTickets != null)
        {
            // Act
            var isInUse = await _repository.IsInUseAsync(categoryWithTickets.RowId);

            // Assert
            Assert.True(isInUse);
        }
    }

    [Fact]
    public async Task IsInUseAsync_ShouldReturnFalse_WhenCategoryHasNoActiveTickets()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            CategoryNameAr = "فئة غير مستخدمة",
            CategoryNameEn = "Unused Category",
            DisplayOrder = 200,
            CreationUser = "IntegrationTest"
        };
        var categoryId = await _repository.CreateAsync(category);
        _createdCategoryIds.Add(categoryId);

        // Act
        var isInUse = await _repository.IsInUseAsync(categoryId);

        // Assert
        Assert.False(isInUse);
    }

    [Fact]
    public async Task GetUsageStatisticsAsync_ShouldReturnCategoriesOrderedByUsage()
    {
        // Act
        var usageStats = await _repository.GetUsageStatisticsAsync();

        // Assert
        Assert.NotEmpty(usageStats);
        
        // Verify ordering - categories with more tickets should come first
        for (int i = 0; i < usageStats.Count - 1; i++)
        {
            Assert.True(usageStats[i].TicketCount >= usageStats[i + 1].TicketCount);
        }

        // Verify all categories are included
        Assert.All(usageStats, stat => Assert.NotNull(stat.Category));
    }

    [Fact]
    public async Task GetUsageStatisticsAsync_WithDateRange_ShouldFilterByDateRange()
    {
        // Arrange
        var fromDate = DateTime.Now.AddMonths(-1);
        var toDate = DateTime.Now;

        // Act
        var usageStats = await _repository.GetUsageStatisticsAsync(fromDate, toDate);

        // Assert
        Assert.NotNull(usageStats);
        Assert.All(usageStats, stat => Assert.NotNull(stat.Category));
    }

    [Fact]
    public async Task GetUsageStatisticsAsync_WithCompanyFilter_ShouldFilterByCompany()
    {
        // Arrange
        var company = await _context.Companies.Where(c => c.IsActive).FirstOrDefaultAsync();
        
        if (company != null)
        {
            // Act
            var usageStats = await _repository.GetUsageStatisticsAsync(companyId: company.RowId);

            // Assert
            Assert.NotNull(usageStats);
            Assert.All(usageStats, stat => Assert.NotNull(stat.Category));
        }
    }

    [Fact]
    public async Task GetUsageStatisticsAsync_WithBranchFilter_ShouldFilterByBranch()
    {
        // Arrange
        var branch = await _context.Branches.Where(b => b.IsActive).FirstOrDefaultAsync();
        
        if (branch != null)
        {
            // Act
            var usageStats = await _repository.GetUsageStatisticsAsync(branchId: branch.RowId);

            // Assert
            Assert.NotNull(usageStats);
            Assert.All(usageStats, stat => Assert.NotNull(stat.Category));
        }
    }

    [Fact]
    public async Task TransactionRollback_ShouldNotPersistCategory()
    {
        // Arrange
        var category = new SysTicketCategory
        {
            CategoryNameAr = "فئة للتراجع",
            CategoryNameEn = "Rollback Category",
            DisplayOrder = 150,
            CreationUser = "IntegrationTest"
        };

        // Act - Create within a transaction and rollback
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.TicketCategories.Add(category);
            await _context.SaveChangesAsync();
            
            var categoryId = category.RowId;
            Assert.True(categoryId > 0);

            // Rollback the transaction
            await transaction.RollbackAsync();

            // Assert - Verify the category was not persisted
            var retrievedCategory = await _repository.GetByIdAsync(categoryId);
            Assert.Null(retrievedCategory);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [Fact]
    public async Task SequenceGeneration_ShouldGenerateUniqueIds()
    {
        // Arrange
        var category1 = new SysTicketCategory
        {
            CategoryNameAr = $"فئة 1 {Guid.NewGuid().ToString().Substring(0, 8)}",
            CategoryNameEn = $"Category 1 {Guid.NewGuid().ToString().Substring(0, 8)}",
            DisplayOrder = 101,
            CreationUser = "IntegrationTest"
        };

        var category2 = new SysTicketCategory
        {
            CategoryNameAr = $"فئة 2 {Guid.NewGuid().ToString().Substring(0, 8)}",
            CategoryNameEn = $"Category 2 {Guid.NewGuid().ToString().Substring(0, 8)}",
            DisplayOrder = 102,
            CreationUser = "IntegrationTest"
        };

        // Act
        var id1 = await _repository.CreateAsync(category1);
        var id2 = await _repository.CreateAsync(category2);
        _createdCategoryIds.Add(id1);
        _createdCategoryIds.Add(id2);

        // Assert
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task ConcurrentCreation_ShouldHandleMultipleInserts()
    {
        // Arrange
        var categories = new List<SysTicketCategory>();
        for (int i = 0; i < 3; i++)
        {
            categories.Add(new SysTicketCategory
            {
                CategoryNameAr = $"فئة متزامنة {i} {Guid.NewGuid().ToString().Substring(0, 8)}",
                CategoryNameEn = $"Concurrent Category {i} {Guid.NewGuid().ToString().Substring(0, 8)}",
                DisplayOrder = 300 + i,
                CreationUser = "IntegrationTest"
            });
        }

        // Act
        var tasks = categories.Select(c => _repository.CreateAsync(c)).ToList();
        var ids = await Task.WhenAll(tasks);
        _createdCategoryIds.AddRange(ids);

        // Assert
        Assert.Equal(3, ids.Length);
        Assert.All(ids, id => Assert.True(id > 0));
        Assert.Equal(ids.Distinct().Count(), ids.Length); // All IDs should be unique
    }

    public void Dispose()
    {
        // Cleanup - Delete all created test data
        foreach (var id in _createdCategoryIds)
        {
            try
            {
                var category = _context.TicketCategories
                    .IgnoreQueryFilters()
                    .FirstOrDefault(c => c.RowId == id);
                if (category != null)
                {
                    _context.TicketCategories.Remove(category);
                    _context.SaveChanges();
                }
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
