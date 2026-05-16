using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.EfCore;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories.EfCore;

/// <summary>
/// Unit tests for TicketPriorityRepository using EF Core InMemory provider.
/// Tests all CRUD operations, SLA calculations, and escalation logic.
/// </summary>
public class TicketPriorityRepositoryTests : IDisposable
{
    private readonly ThinkOnErpDbContext _context;
    private readonly TicketPriorityRepository _repository;
    private readonly Mock<ILogger<TicketPriorityRepository>> _loggerMock;

    public TicketPriorityRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ThinkOnErpDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_TicketPriority_{Guid.NewGuid()}")
            .Options;

        _context = new ThinkOnErpDbContext(options);
        _loggerMock = new Mock<ILogger<TicketPriorityRepository>>();
        _repository = new TicketPriorityRepository(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllPriorities_OrderedByPriorityLevel()
    {
        // Arrange
        var priorities = new List<SysTicketPriority>
        {
            new SysTicketPriority { RowId = 1, PriorityNameEn = "Low", PriorityLevel = 4, SlaTargetHours = 72, EscalationThresholdHours = 60, IsActive = true, CreationUser = "test" },
            new SysTicketPriority { RowId = 2, PriorityNameEn = "Critical", PriorityLevel = 1, SlaTargetHours = 4, EscalationThresholdHours = 2, IsActive = true, CreationUser = "test" },
            new SysTicketPriority { RowId = 3, PriorityNameEn = "Medium", PriorityLevel = 3, SlaTargetHours = 24, EscalationThresholdHours = 18, IsActive = true, CreationUser = "test" }
        };
        await _context.TicketPriorities.AddRangeAsync(priorities);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].PriorityLevel); // Critical
        Assert.Equal(3, result[1].PriorityLevel); // Medium
        Assert.Equal(4, result[2].PriorityLevel); // Low
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoPriorities()
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
    public async Task GetByIdAsync_ReturnsPriority_WhenExists()
    {
        // Arrange
        var priority = new SysTicketPriority
        {
            RowId = 1,
            PriorityNameAr = "حرج",
            PriorityNameEn = "Critical",
            PriorityLevel = 1,
            SlaTargetHours = 4,
            EscalationThresholdHours = 2,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketPriorities.AddAsync(priority);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.RowId);
        Assert.Equal("Critical", result.PriorityNameEn);
        Assert.Equal(1, result.PriorityLevel);
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

    #region GetByLevelAsync Tests

    [Fact]
    public async Task GetByLevelAsync_ReturnsPriority_WhenExists()
    {
        // Arrange
        var priority = new SysTicketPriority
        {
            PriorityNameEn = "High",
            PriorityLevel = 2,
            SlaTargetHours = 8,
            EscalationThresholdHours = 6,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketPriorities.AddAsync(priority);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByLevelAsync(2);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.PriorityLevel);
        Assert.Equal("High", result.PriorityNameEn);
    }

    [Fact]
    public async Task GetByLevelAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByLevelAsync(99);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetDefaultPriorityAsync Tests

    [Fact]
    public async Task GetDefaultPriorityAsync_ReturnsMediumPriority()
    {
        // Arrange
        var priorities = new List<SysTicketPriority>
        {
            new SysTicketPriority { PriorityNameEn = "Critical", PriorityLevel = 1, SlaTargetHours = 4, EscalationThresholdHours = 2, IsActive = true, CreationUser = "test" },
            new SysTicketPriority { PriorityNameEn = "Medium", PriorityLevel = 3, SlaTargetHours = 24, EscalationThresholdHours = 18, IsActive = true, CreationUser = "test" }
        };
        await _context.TicketPriorities.AddRangeAsync(priorities);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDefaultPriorityAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.PriorityLevel);
        Assert.Equal("Medium", result.PriorityNameEn);
    }

    [Fact]
    public async Task GetDefaultPriorityAsync_ReturnsNull_WhenMediumNotFound()
    {
        // Act
        var result = await _repository.GetDefaultPriorityAsync();

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetHighPrioritiesAsync Tests

    [Fact]
    public async Task GetHighPrioritiesAsync_ReturnsOnlyCriticalAndHigh()
    {
        // Arrange
        var priorities = new List<SysTicketPriority>
        {
            new SysTicketPriority { PriorityNameEn = "Critical", PriorityLevel = 1, SlaTargetHours = 4, EscalationThresholdHours = 2, IsActive = true, CreationUser = "test" },
            new SysTicketPriority { PriorityNameEn = "High", PriorityLevel = 2, SlaTargetHours = 8, EscalationThresholdHours = 6, IsActive = true, CreationUser = "test" },
            new SysTicketPriority { PriorityNameEn = "Medium", PriorityLevel = 3, SlaTargetHours = 24, EscalationThresholdHours = 18, IsActive = true, CreationUser = "test" },
            new SysTicketPriority { PriorityNameEn = "Low", PriorityLevel = 4, SlaTargetHours = 72, EscalationThresholdHours = 60, IsActive = true, CreationUser = "test" }
        };
        await _context.TicketPriorities.AddRangeAsync(priorities);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetHighPrioritiesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.True(p.PriorityLevel <= 2));
        Assert.Equal("Critical", result[0].PriorityNameEn);
        Assert.Equal("High", result[1].PriorityNameEn);
    }

    [Fact]
    public async Task GetHighPrioritiesAsync_ReturnsEmptyList_WhenNoHighPriorities()
    {
        // Arrange
        var priority = new SysTicketPriority
        {
            PriorityNameEn = "Low",
            PriorityLevel = 4,
            SlaTargetHours = 72,
            EscalationThresholdHours = 60,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketPriorities.AddAsync(priority);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetHighPrioritiesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region CalculateSlaDeadlineAsync Tests

    [Fact]
    public async Task CalculateSlaDeadlineAsync_ReturnsCorrectDeadline()
    {
        // Arrange
        var priority = new SysTicketPriority
        {
            RowId = 1,
            PriorityNameEn = "Critical",
            PriorityLevel = 1,
            SlaTargetHours = 4,
            EscalationThresholdHours = 2,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketPriorities.AddAsync(priority);
        await _context.SaveChangesAsync();

        var creationDate = new DateTime(2024, 1, 1, 10, 0, 0);

        // Act
        var result = await _repository.CalculateSlaDeadlineAsync(1, creationDate);

        // Assert
        var expectedDeadline = creationDate.AddHours(4);
        Assert.Equal(expectedDeadline, result);
    }

    [Fact]
    public async Task CalculateSlaDeadlineAsync_ThrowsException_WhenPriorityNotFound()
    {
        // Arrange
        var creationDate = DateTime.Now;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _repository.CalculateSlaDeadlineAsync(999, creationDate));
    }

    #endregion

    #region GetUsageStatisticsAsync Tests

    [Fact]
    public async Task GetUsageStatisticsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        var criticalPriority = new SysTicketPriority
        {
            RowId = 1,
            PriorityNameEn = "Critical",
            PriorityLevel = 1,
            SlaTargetHours = 4,
            EscalationThresholdHours = 2,
            IsActive = true,
            CreationUser = "test"
        };
        var mediumPriority = new SysTicketPriority
        {
            RowId = 2,
            PriorityNameEn = "Medium",
            PriorityLevel = 3,
            SlaTargetHours = 24,
            EscalationThresholdHours = 18,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketPriorities.AddRangeAsync(criticalPriority, mediumPriority);

        var now = DateTime.Now;
        var tickets = new List<SysRequestTicket>
        {
            new SysRequestTicket 
            { 
                RowId = 1, 
                TicketPriorityId = 1, 
                CompanyId = 1, 
                BranchId = 1, 
                CreationDate = now,
                ExpectedResolutionDate = now.AddHours(4),
                ActualResolutionDate = now.AddHours(3),
                CreationUser = "test"
            },
            new SysRequestTicket 
            { 
                RowId = 2, 
                TicketPriorityId = 1, 
                CompanyId = 1, 
                BranchId = 1, 
                CreationDate = now,
                ExpectedResolutionDate = now.AddHours(4),
                ActualResolutionDate = now.AddHours(5),
                CreationUser = "test"
            },
            new SysRequestTicket 
            { 
                RowId = 3, 
                TicketPriorityId = 2, 
                CompanyId = 1, 
                BranchId = 1, 
                CreationDate = now,
                CreationUser = "test"
            }
        };
        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUsageStatisticsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        var criticalStats = result.First(r => r.Priority.PriorityLevel == 1);
        Assert.Equal(2, criticalStats.TicketCount);
        Assert.Equal(50m, criticalStats.SlaComplianceRate); // 1 out of 2 met SLA

        var mediumStats = result.First(r => r.Priority.PriorityLevel == 3);
        Assert.Equal(1, mediumStats.TicketCount);
        Assert.Equal(0m, mediumStats.SlaComplianceRate); // No resolution date
    }

    [Fact]
    public async Task GetUsageStatisticsAsync_WithFilters_ReturnsFilteredResults()
    {
        // Arrange
        var priority = new SysTicketPriority
        {
            RowId = 1,
            PriorityNameEn = "Critical",
            PriorityLevel = 1,
            SlaTargetHours = 4,
            EscalationThresholdHours = 2,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketPriorities.AddAsync(priority);

        var tickets = new List<SysRequestTicket>
        {
            new SysRequestTicket { RowId = 1, TicketPriorityId = 1, CompanyId = 1, BranchId = 1, CreationDate = DateTime.Now.AddDays(-10), CreationUser = "test" },
            new SysRequestTicket { RowId = 2, TicketPriorityId = 1, CompanyId = 1, BranchId = 1, CreationDate = DateTime.Now.AddDays(-5), CreationUser = "test" },
            new SysRequestTicket { RowId = 3, TicketPriorityId = 1, CompanyId = 2, BranchId = 2, CreationDate = DateTime.Now, CreationUser = "test" }
        };
        await _context.Tickets.AddRangeAsync(tickets);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUsageStatisticsAsync(
            fromDate: DateTime.Now.AddDays(-7),
            toDate: DateTime.Now.AddDays(1),
            companyId: 1);

        // Assert
        Assert.NotNull(result);
        var stats = result.First();
        Assert.Equal(1, stats.TicketCount); // Only 1 ticket matches all filters
    }

    #endregion

    #region GetEscalationCandidatesAsync Tests

    [Fact]
    public async Task GetEscalationCandidatesAsync_ReturnsTicketsNeedingEscalation()
    {
        // Arrange
        var priority = new SysTicketPriority
        {
            RowId = 1,
            PriorityNameEn = "Critical",
            PriorityLevel = 1,
            SlaTargetHours = 4,
            EscalationThresholdHours = 2,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketPriorities.AddAsync(priority);

        var openStatus = new SysTicketStatus
        {
            RowId = 1,
            StatusCode = "OPEN",
            StatusNameEn = "Open",
            IsFinalStatus = false,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(openStatus);

        var ticketType = new SysTicketType
        {
            RowId = 1,
            TypeNameEn = "Bug",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketTypes.AddAsync(ticketType);

        // Ticket that should be escalated (created 3 hours ago, escalation threshold is 2 hours)
        var escalationTicket = new SysRequestTicket
        {
            RowId = 1,
            TicketPriorityId = 1,
            TicketStatusId = 1,
            TicketTypeId = 1,
            CompanyId = 1,
            BranchId = 1,
            CreationDate = DateTime.Now.AddHours(-3),
            ExpectedResolutionDate = DateTime.Now.AddHours(1),
            CreationUser = "test"
        };

        // Ticket that should not be escalated (created 1 hour ago)
        var normalTicket = new SysRequestTicket
        {
            RowId = 2,
            TicketPriorityId = 1,
            TicketStatusId = 1,
            TicketTypeId = 1,
            CompanyId = 1,
            BranchId = 1,
            CreationDate = DateTime.Now.AddHours(-1),
            ExpectedResolutionDate = DateTime.Now.AddHours(3),
            CreationUser = "test"
        };

        await _context.Tickets.AddRangeAsync(escalationTicket, normalTicket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetEscalationCandidatesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(1, result[0].RowId);
    }

    [Fact]
    public async Task GetEscalationCandidatesAsync_ExcludesClosedTickets()
    {
        // Arrange
        var priority = new SysTicketPriority
        {
            RowId = 1,
            PriorityNameEn = "Critical",
            PriorityLevel = 1,
            SlaTargetHours = 4,
            EscalationThresholdHours = 2,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketPriorities.AddAsync(priority);

        var closedStatus = new SysTicketStatus
        {
            RowId = 1,
            StatusCode = "CLOSED",
            StatusNameEn = "Closed",
            IsFinalStatus = true,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketStatuses.AddAsync(closedStatus);

        var ticketType = new SysTicketType
        {
            RowId = 1,
            TypeNameEn = "Bug",
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketTypes.AddAsync(ticketType);

        var closedTicket = new SysRequestTicket
        {
            RowId = 1,
            TicketPriorityId = 1,
            TicketStatusId = 1,
            TicketTypeId = 1,
            CompanyId = 1,
            BranchId = 1,
            CreationDate = DateTime.Now.AddHours(-3),
            CreationUser = "test"
        };
        await _context.Tickets.AddAsync(closedTicket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetEscalationCandidatesAsync();

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
            () => new TicketPriorityRepository(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new TicketPriorityRepository(_context, null!));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetByLevelAsync_WithZeroLevel_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByLevelAsync(0);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByLevelAsync_WithNegativeLevel_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByLevelAsync(-1);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CalculateSlaDeadlineAsync_WithFutureCreationDate_ReturnsCorrectDeadline()
    {
        // Arrange
        var priority = new SysTicketPriority
        {
            RowId = 1,
            PriorityNameEn = "Medium",
            PriorityLevel = 3,
            SlaTargetHours = 24,
            EscalationThresholdHours = 18,
            IsActive = true,
            CreationUser = "test"
        };
        await _context.TicketPriorities.AddAsync(priority);
        await _context.SaveChangesAsync();

        var futureDate = DateTime.Now.AddDays(1);

        // Act
        var result = await _repository.CalculateSlaDeadlineAsync(1, futureDate);

        // Assert
        var expectedDeadline = futureDate.AddHours(24);
        Assert.Equal(expectedDeadline, result);
    }

    #endregion
}
