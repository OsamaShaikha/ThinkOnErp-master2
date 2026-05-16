using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for TicketStatusRepository using EF Core InMemory provider.
/// Tests all CRUD operations, workflow validation, and statistics methods.
/// </summary>
public class TicketStatusRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly TicketStatusRepository _repository;
    private readonly Mock<ILogger<TicketStatusRepository>> _loggerMock;

    public TicketStatusRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_TicketStatus_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<TicketStatusRepository>>();
        _repository = new TicketStatusRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllStatuses_OrderedByDisplayOrder()
    {
        // Arrange
        var statuses = new List<SysTicketStatus>
        {
            new SysTicketStatus { RowId = 1, StatusCode = "OPEN", StatusNameEn = "Open", DisplayOrder = 1, IsActive = true, CreationUser = "test" },
            new SysTicketStatus { RowId = 2, StatusCode = "CLOSED", StatusNameEn = "Closed", DisplayOrder = 5, IsActive = true, CreationUser = "test" },
            new SysTicketStatus { RowId = 3, StatusCode = "IN_PROGRESS", StatusNameEn = "In Progress", DisplayOrder = 2, IsActive = true, CreationUser = "test" }
        };
        await _context.TicketStatuses.AddRangeAsync(statuses);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("OPEN", result[0].StatusCode);
        Assert.Equal("IN_PROGRESS", result[1].StatusCode);
        Assert.Equal("CLOSED", result[2].StatusCode);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoStatuses()
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
    public async Task GetByIdAsync_ReturnsStatus_WhenExists()
    {
        // Arrange
        var status = new SysTicketStatus
        {
            RowId = 1,
            StatusCode = "OPEN",
            StatusNameAr = "مفتوح",
            StatusNameEn = "Open",
            DisplayOrder = 1,
            IsFinalStatus = false,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(status);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.RowId);
        Assert.Equal("OPEN", result.StatusCode);
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

    #region GetByCodeAsync Tests

    [Fact]
    public async Task GetByCodeAsync_ReturnsStatus_WhenExists()
    {
        // Arrange
        var status = new SysTicketStatus
        {
            StatusCode = "IN_PROGRESS",
            StatusNameEn = "In Progress",
            DisplayOrder = 2,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(status);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByCodeAsync("IN_PROGRESS");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("IN_PROGRESS", result.StatusCode);
        Assert.Equal("In Progress", result.StatusNameEn);
    }

    [Fact]
    public async Task GetByCodeAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByCodeAsync("NONEXISTENT");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region IsTransitionAllowedAsync Tests

    [Fact]
    public async Task IsTransitionAllowedAsync_ReturnsTrue_WhenFromStatusIsNotFinal()
    {
        // Arrange
        var openStatus = new SysTicketStatus
        {
            RowId = 1,
            StatusCode = "OPEN",
            StatusNameEn = "Open",
            IsFinalStatus = false,
            IsActive = true,
            CreationUser = "test"
        };
        var inProgressStatus = new SysTicketStatus
        {
            RowId = 2,
            StatusCode = "IN_PROGRESS",
            StatusNameEn = "In Progress",
            IsFinalStatus = false,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddRangeAsync(openStatus, inProgressStatus);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsTransitionAllowedAsync(1, 2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsTransitionAllowedAsync_ReturnsFalse_WhenFromStatusIsFinal()
    {
        // Arrange
        var closedStatus = new SysTicketStatus
        {
            RowId = 1,
            StatusCode = "CLOSED",
            StatusNameEn = "Closed",
            IsFinalStatus = true,
            IsActive = true,
            CreationUser = "test"
        };
        var openStatus = new SysTicketStatus
        {
            RowId = 2,
            StatusCode = "OPEN",
            StatusNameEn = "Open",
            IsFinalStatus = false,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddRangeAsync(closedStatus, openStatus);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsTransitionAllowedAsync(1, 2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsTransitionAllowedAsync_ReturnsFalse_WhenFromStatusNotFound()
    {
        // Arrange
        var status = new SysTicketStatus
        {
            RowId = 1,
            StatusCode = "OPEN",
            StatusNameEn = "Open",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(status);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsTransitionAllowedAsync(999, 1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsTransitionAllowedAsync_ReturnsFalse_WhenToStatusNotFound()
    {
        // Arrange
        var status = new SysTicketStatus
        {
            RowId = 1,
            StatusCode = "OPEN",
            StatusNameEn = "Open",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(status);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsTransitionAllowedAsync(1, 999);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region GetDefaultInitialStatusAsync Tests

    [Fact]
    public async Task GetDefaultInitialStatusAsync_ReturnsOpenStatus()
    {
        // Arrange
        var openStatus = new SysTicketStatus
        {
            StatusCode = SysTicketStatus.StatusCodes.Open,
            StatusNameEn = "Open",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(openStatus);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDefaultInitialStatusAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SysTicketStatus.StatusCodes.Open, result.StatusCode);
    }

    [Fact]
    public async Task GetDefaultInitialStatusAsync_ReturnsNull_WhenOpenStatusNotFound()
    {
        // Act
        var result = await _repository.GetDefaultInitialStatusAsync();

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetFinalStatusesAsync Tests

    [Fact]
    public async Task GetFinalStatusesAsync_ReturnsOnlyFinalStatuses()
    {
        // Arrange
        var statuses = new List<SysTicketStatus>
        {
            new SysTicketStatus { StatusCode = "OPEN", StatusNameEn = "Open", DisplayOrder = 1, IsFinalStatus = false, IsActive = true, CreationUser = "test" },
            new SysTicketStatus { StatusCode = "CLOSED", StatusNameEn = "Closed", DisplayOrder = 5, IsFinalStatus = true, IsActive = true, CreationUser = "test" },
            new SysTicketStatus { StatusCode = "CANCELLED", StatusNameEn = "Cancelled", DisplayOrder = 6, IsFinalStatus = true, IsActive = true, CreationUser = "test" }
        };
        await _context.TicketStatuses.AddRangeAsync(statuses);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetFinalStatusesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.True(s.IsFinalStatus));
    }

    [Fact]
    public async Task GetFinalStatusesAsync_ReturnsEmptyList_WhenNoFinalStatuses()
    {
        // Arrange
        var status = new SysTicketStatus
        {
            StatusCode = "OPEN",
            StatusNameEn = "Open",
            IsFinalStatus = false,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(status);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetFinalStatusesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region GetUsageStatisticsAsync Tests

    [Fact]
    public async Task GetUsageStatisticsAsync_ReturnsCorrectCounts()
    {
        // Arrange
        var openStatus = new SysTicketStatus
        {
            RowId = 1,
            StatusCode = "OPEN",
            StatusNameEn = "Open",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        var closedStatus = new SysTicketStatus
        {
            RowId = 2,
            StatusCode = "CLOSED",
            StatusNameEn = "Closed",
            DisplayOrder = 2,
            IsFinalStatus = true,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddRangeAsync(openStatus, closedStatus);

        var tickets = new List<SysRequestTicket>
        {
            new SysRequestTicket { RowId = 1, TicketStatusId = 1, CompanyId = 1, BranchId = 1, CreationDate = DateTime.Now, CreationUser = "test" },
            new SysRequestTicket { RowId = 2, TicketStatusId = 1, CompanyId = 1, BranchId = 1, CreationDate = DateTime.Now, CreationUser = "test" },
            new SysRequestTicket { RowId = 3, TicketStatusId = 2, CompanyId = 1, BranchId = 1, CreationDate = DateTime.Now, CreationUser = "test" }
        };
        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUsageStatisticsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        
        var openStats = result.First(r => r.Status.StatusCode == "OPEN");
        Assert.Equal(2, openStats.TicketCount);
        
        var closedStats = result.First(r => r.Status.StatusCode == "CLOSED");
        Assert.Equal(1, closedStats.TicketCount);
    }

    [Fact]
    public async Task GetUsageStatisticsAsync_WithDateFilter_ReturnsFilteredResults()
    {
        // Arrange
        var status = new SysTicketStatus
        {
            RowId = 1,
            StatusCode = "OPEN",
            StatusNameEn = "Open",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(status);

        var tickets = new List<SysRequestTicket>
        {
            new SysRequestTicket { RowId = 1, TicketStatusId = 1, CompanyId = 1, BranchId = 1, CreationDate = DateTime.Now.AddDays(-10), CreationUser = "test" },
            new SysRequestTicket { RowId = 2, TicketStatusId = 1, CompanyId = 1, BranchId = 1, CreationDate = DateTime.Now.AddDays(-5), CreationUser = "test" },
            new SysRequestTicket { RowId = 3, TicketStatusId = 1, CompanyId = 1, BranchId = 1, CreationDate = DateTime.Now, CreationUser = "test" }
        };
        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUsageStatisticsAsync(
            fromDate: DateTime.Now.AddDays(-7),
            toDate: DateTime.Now.AddDays(1));

        // Assert
        Assert.NotNull(result);
        var stats = result.First();
        Assert.Equal(2, stats.TicketCount); // Only tickets within date range
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CreatesNewStatus_ReturnsGeneratedId()
    {
        // Arrange
        var status = new SysTicketStatus
        {
            StatusCode = "PENDING",
            StatusNameAr = "معلق",
            StatusNameEn = "Pending",
            DisplayOrder = 3,
            IsFinalStatus = false,
            IsActive = true,
            CreationUser = "test_user"
        };

        // Act
        var result = await _repository.CreateAsync(status);

        // Assert
        Assert.True(result > 0);
        Assert.Equal(result, status.RowId);
        Assert.NotNull(status.CreationDate);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesExistingStatus_ReturnsRowsAffected()
    {
        // Arrange
        var status = new SysTicketStatus
        {
            StatusCode = "OPEN",
            StatusNameEn = "Open",
            DisplayOrder = 1,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(status);
        await _context.SaveChangesAsync();

        status.StatusNameEn = "Opened";
        status.DisplayOrder = 2;

        // Act
        var result = await _repository.UpdateAsync(status);

        // Assert
        Assert.Equal(1, result);
        var updated = await _context.TicketStatuses.FindAsync(status.RowId);
        Assert.Equal("Opened", updated!.StatusNameEn);
        Assert.Equal(2, updated.DisplayOrder);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesStatus_ReturnsRowsAffected()
    {
        // Arrange
        var status = new SysTicketStatus
        {
            StatusCode = "TEMP",
            StatusNameEn = "Temporary",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(status);
        await _context.SaveChangesAsync();
        var statusId = status.RowId;

        // Act
        var result = await _repository.DeleteAsync(statusId);

        // Assert
        Assert.Equal(1, result);
        var deleted = await _context.TicketStatuses.FindAsync(statusId);
        Assert.NotNull(deleted);
        Assert.False(deleted.IsActive);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ReturnsZero()
    {
        // Act
        var result = await _repository.DeleteAsync(999);

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketStatusRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketStatusRepository(_context, null!));
    }

    #endregion
}
