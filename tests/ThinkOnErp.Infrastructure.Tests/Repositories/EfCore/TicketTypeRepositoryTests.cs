using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for TicketTypeRepository using EF Core InMemory provider.
/// Tests all CRUD operations, usage statistics, and exception scenarios.
/// </summary>
public class TicketTypeRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly TicketTypeRepository _repository;
    private readonly Mock<ILogger<TicketTypeRepository>> _loggerMock;

    public TicketTypeRepositoryTests()
    {
        // Create InMemory database with unique name for each test instance
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_TicketType_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<TicketTypeRepository>>();
        _repository = new TicketTypeRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllTicketTypes_OrderedByName()
    {
        // Arrange
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

        var types = new List<SysTicketType>
        {
            new SysTicketType
            {
                RowId = 1,
                TypeNameAr = "طلب دعم",
                TypeNameEn = "Support Request",
                DefaultPriorityId = priority.RowId,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketType
            {
                RowId = 2,
                TypeNameAr = "بلاغ خطأ",
                TypeNameEn = "Bug Report",
                DefaultPriorityId = priority.RowId,
                IsActive = true,
                CreationUser = "test"
            },
            new SysTicketType
            {
                RowId = 3,
                TypeNameAr = "طلب ميزة",
                TypeNameEn = "Feature Request",
                DefaultPriorityId = priority.RowId,
                IsActive = true,
                CreationUser = "test"
            }
        };
        await _context.TicketTypes.AddRangeAsync(types);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("Bug Report", result[0].TypeNameEn); // Ordered alphabetically
        Assert.Equal("Feature Request", result[1].TypeNameEn);
        Assert.Equal("Support Request", result[2].TypeNameEn);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoTypes()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_IncludesDefaultPriority()
    {
        // Arrange
        var priority = new SysTicketPriority
        {
            RowId = 1,
            PriorityNameEn = "High",
            PriorityLevel = 2,
            SlaHours = 8,
            DisplayOrder = 2,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketPriorities.AddAsync(priority);

        var type = new SysTicketType
        {
            TypeNameEn = "Urgent Issue",
            DefaultPriorityId = priority.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketTypes.AddAsync(type);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.Single(result);
        Assert.NotNull(result[0].DefaultPriority);
        Assert.Equal("High", result[0].DefaultPriority.PriorityNameEn);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsTicketType_WhenExists()
    {
        // Arrange
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
            TypeNameAr = "طلب دعم",
            TypeNameEn = "Support Request",
            TypeDescriptionAr = "وصف",
            TypeDescriptionEn = "Description",
            DefaultPriorityId = priority.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketTypes.AddAsync(type);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.RowId);
        Assert.Equal("Support Request", result.TypeNameEn);
        Assert.NotNull(result.DefaultPriority);
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
    public async Task CreateAsync_CreatesNewTicketType_ReturnsGeneratedId()
    {
        // Arrange
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
        await _context.SaveChangesAsync();

        var type = new SysTicketType
        {
            TypeNameAr = "نوع جديد",
            TypeNameEn = "New Type",
            TypeDescriptionEn = "Test Description",
            DefaultPriorityId = priority.RowId,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(type);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, type.RowId);
        Assert.NotNull(type.CreationDate);
        Assert.True(type.IsActive);

        // Verify in database
        var savedType = await _context.TicketTypes.FindAsync(result);
        Assert.NotNull(savedType);
        Assert.Equal("New Type", savedType.TypeNameEn);
    }

    [Fact]
    public async Task CreateAsync_SetsCreationDate_WhenNotProvided()
    {
        // Arrange
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
        await _context.SaveChangesAsync();

        var type = new SysTicketType
        {
            TypeNameEn = "Test Type",
            DefaultPriorityId = priority.RowId,
            CreationUser = "test"
        };

        // Act
        await _repository.CreateAsync(type);

        // Assert
        Assert.NotNull(type.CreationDate);
        Assert.True(type.CreationDate.Value <= DateTime.Now);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesExistingTicketType_ReturnsRowsAffected()
    {
        // Arrange
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
            TypeNameEn = "Original Name",
            DefaultPriorityId = priority.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketTypes.AddAsync(type);
        await _context.SaveChangesAsync();

        // Modify the type
        type.TypeNameEn = "Updated Name";
        type.TypeDescriptionEn = "Updated Description";
        type.UpdateUser = "update_user";

        // Act
        var result = await _repository.UpdateAsync(type);

        // Assert
        Assert.Equal(1, result);
        Assert.NotNull(type.UpdateDate);

        // Verify in database
        var updatedType = await _context.TicketTypes.FindAsync(type.RowId);
        Assert.NotNull(updatedType);
        Assert.Equal("Updated Name", updatedType.TypeNameEn);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesTicketType_ReturnsRowsAffected()
    {
        // Arrange
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
            TypeNameEn = "To Delete",
            DefaultPriorityId = priority.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketTypes.AddAsync(type);
        await _context.SaveChangesAsync();
        var typeId = type.RowId;

        // Act
        var result = await _repository.DeleteAsync(typeId, "delete_user");

        // Assert
        Assert.Equal(1, result);

        // Verify type is soft deleted
        var deletedType = await _context.TicketTypes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.RowId == typeId);
        Assert.NotNull(deletedType);
        Assert.False(deletedType.IsActive);
        Assert.Equal("delete_user", deletedType.UpdateUser);
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
    public async Task DeleteAsync_ThrowsException_WhenTypeIsInUse()
    {
        // Arrange
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
            TypeNameEn = "In Use Type",
            DefaultPriorityId = priority.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketTypes.AddAsync(type);

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
            TitleEn = "Test Ticket",
            Description = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _repository.DeleteAsync(type.RowId, "user"));
    }

    #endregion

    #region IsInUseAsync Tests

    [Fact]
    public async Task IsInUseAsync_ReturnsTrue_WhenTypeIsUsed()
    {
        // Arrange
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
            TypeNameEn = "Used Type",
            DefaultPriorityId = priority.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketTypes.AddAsync(type);

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
            TitleEn = "Test Ticket",
            Description = "Test",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.Tickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsInUseAsync(type.RowId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsInUseAsync_ReturnsFalse_WhenTypeIsNotUsed()
    {
        // Arrange
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
            TypeNameEn = "Unused Type",
            DefaultPriorityId = priority.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketTypes.AddAsync(type);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsInUseAsync(type.RowId);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region GetByUsageAsync Tests

    [Fact]
    public async Task GetByUsageAsync_ReturnsTypesOrderedByUsage()
    {
        // Arrange
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

        var type1 = new SysTicketType
        {
            RowId = 1,
            TypeNameEn = "Type 1",
            DefaultPriorityId = priority.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        var type2 = new SysTicketType
        {
            RowId = 2,
            TypeNameEn = "Type 2",
            DefaultPriorityId = priority.RowId,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketTypes.AddRangeAsync(type1, type2);

        // Create test data
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

        // Create 3 tickets for type1 and 1 ticket for type2
        for (int i = 0; i < 3; i++)
        {
            await _context.Tickets.AddAsync(new SysRequestTicket
            {
                CompanyId = company.RowId,
                BranchId = branch.RowId,
                RequesterId = user.RowId,
                TicketTypeId = type1.RowId,
                TicketStatusId = status.RowId,
                TicketPriorityId = priority.RowId,
                TitleEn = $"Ticket {i}",
                Description = "Test",
                IsActive = true,
                CreationUser = "test",
                CreationDate = DateTime.Now
            });
        }

        await _context.Tickets.AddAsync(new SysRequestTicket
        {
            CompanyId = company.RowId,
            BranchId = branch.RowId,
            RequesterId = user.RowId,
            TicketTypeId = type2.RowId,
            TicketStatusId = status.RowId,
            TicketPriorityId = priority.RowId,
            TitleEn = "Ticket Type2",
            Description = "Test",
            IsActive = true,
            CreationUser = "test",
            CreationDate = DateTime.Now
        });

        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUsageAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Type 1", result[0].TicketType.TypeNameEn);
        Assert.Equal(3, result[0].TicketCount);
        Assert.Equal("Type 2", result[1].TicketType.TypeNameEn);
        Assert.Equal(1, result[1].TicketCount);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketTypeRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketTypeRepository(_context, null!));
    }

    #endregion
}
