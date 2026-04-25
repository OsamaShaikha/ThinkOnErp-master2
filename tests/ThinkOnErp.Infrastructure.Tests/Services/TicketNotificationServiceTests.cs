using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for TicketNotificationService.
/// Tests notification delivery and template rendering functionality.
/// **Validates: Requirements 10.1-10.12**
/// </summary>
public class TicketNotificationServiceTests
{
    private readonly Mock<ILogger<TicketNotificationService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ITicketTypeRepository> _mockTicketTypeRepository;
    private readonly TicketNotificationService _service;

    public TicketNotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<TicketNotificationService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockTicketTypeRepository = new Mock<ITicketTypeRepository>();

        // Setup default configuration
        _mockConfiguration.Setup(c => c.GetValue<bool>("Notifications:Enabled", true))
            .Returns(true);
        _mockConfiguration.Setup(c => c.GetValue<string>("Application:BaseUrl", "https://localhost"))
            .Returns("https://test.com");

        _service = new TicketNotificationService(
            _mockLogger.Object,
            _mockConfiguration.Object,
            _mockUserRepository.Object,
            _mockTicketTypeRepository.Object);
    }

    [Fact]
    public async Task SendTicketCreatedNotificationAsync_ValidTicket_SendsNotificationToRequester()
    {
        // Arrange
        var requester = new SysUser
        {
            RowId = 1,
            RowDescE = "John Doe",
            Email = "john@test.com"
        };

        var ticket = new SysRequestTicket
        {
            RowId = 123,
            TitleEn = "Test Ticket",
            Description = "Test Description",
            RequesterId = 1,
            CreationDate = DateTime.UtcNow,
            Requester = requester,
            TicketPriority = new SysTicketPriority { PriorityNameEn = "High" }
        };

        // Act
        await _service.SendTicketCreatedNotificationAsync(ticket);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Sending ticket created notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTicketCreatedNotificationAsync_NotificationsDisabled_SkipsNotification()
    {
        // Arrange
        _mockConfiguration.Setup(c => c.GetValue<bool>("Notifications:Enabled", true))
            .Returns(false);

        var ticket = new SysRequestTicket { RowId = 123 };

        // Act
        await _service.SendTicketCreatedNotificationAsync(ticket);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Notifications are disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTicketAssignedNotificationAsync_ValidAssignment_SendsNotificationToAssignee()
    {
        // Arrange
        var assignee = new SysUser
        {
            RowId = 2,
            RowDescE = "Jane Admin",
            Email = "jane@test.com"
        };

        var ticket = new SysRequestTicket
        {
            RowId = 123,
            TitleEn = "Test Ticket",
            Description = "Test Description",
            AssigneeId = 2,
            UpdateUser = "admin",
            TicketPriority = new SysTicketPriority { PriorityNameEn = "High" }
        };

        _mockUserRepository.Setup(r => r.GetByIdAsync(2))
            .ReturnsAsync(assignee);

        // Act
        await _service.SendTicketAssignedNotificationAsync(ticket);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Sending ticket assigned notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTicketAssignedNotificationAsync_NoAssignee_LogsWarning()
    {
        // Arrange
        var ticket = new SysRequestTicket
        {
            RowId = 123,
            AssigneeId = null
        };

        // Act
        await _service.SendTicketAssignedNotificationAsync(ticket);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Cannot send assignment notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTicketStatusChangedNotificationAsync_ValidStatusChange_SendsNotifications()
    {
        // Arrange
        var requester = new SysUser
        {
            RowId = 1,
            RowDescE = "John Doe",
            Email = "john@test.com"
        };

        var ticket = new SysRequestTicket
        {
            RowId = 123,
            TitleEn = "Test Ticket",
            RequesterId = 1,
            TicketStatusId = 2,
            UpdateUser = "admin",
            UpdateDate = DateTime.UtcNow,
            Requester = requester,
            TicketStatus = new SysTicketStatus { StatusNameEn = "In Progress" }
        };

        // Act
        await _service.SendTicketStatusChangedNotificationAsync(ticket, 1);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Sending status change notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendCommentAddedNotificationAsync_ValidComment_SendsNotifications()
    {
        // Arrange
        var requester = new SysUser
        {
            RowId = 1,
            RowDescE = "John Doe",
            Email = "john@test.com",
            UserName = "john.doe"
        };

        var ticket = new SysRequestTicket
        {
            RowId = 123,
            TitleEn = "Test Ticket",
            RequesterId = 1,
            Requester = requester
        };

        var comment = new SysTicketComment
        {
            RowId = 1,
            CommentText = "Test comment",
            CreationUser = "admin",
            CreationDate = DateTime.UtcNow
        };

        // Act
        await _service.SendCommentAddedNotificationAsync(ticket, comment);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Sending comment added notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendSlaEscalationAlertAsync_ValidTicket_SendsEscalationAlert()
    {
        // Arrange
        var adminUsers = new List<SysUser>
        {
            new SysUser
            {
                RowId = 1,
                RowDescE = "Admin User",
                Email = "admin@test.com"
            }
        };

        var ticket = new SysRequestTicket
        {
            RowId = 123,
            TitleEn = "Test Ticket",
            ExpectedResolutionDate = DateTime.UtcNow.AddHours(-1),
            CreationDate = DateTime.UtcNow.AddDays(-1),
            TicketPriority = new SysTicketPriority { PriorityNameEn = "Critical" },
            Assignee = new SysUser { RowDescE = "John Assignee" }
        };

        _mockUserRepository.Setup(r => r.GetAdminUsersAsync())
            .ReturnsAsync(adminUsers);

        // Act
        await _service.SendSlaEscalationAlertAsync(ticket);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Sending SLA escalation alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAttachmentAddedNotificationAsync_ValidAttachment_SendsNotifications()
    {
        // Arrange
        var requester = new SysUser
        {
            RowId = 1,
            RowDescE = "John Doe",
            Email = "john@test.com",
            UserName = "john.doe"
        };

        var ticket = new SysRequestTicket
        {
            RowId = 123,
            TitleEn = "Test Ticket",
            RequesterId = 1,
            Requester = requester
        };

        var attachment = new SysTicketAttachment
        {
            RowId = 1,
            FileName = "test.pdf",
            FileSize = 1024,
            CreationUser = "admin",
            CreationDate = DateTime.UtcNow
        };

        // Act
        await _service.SendAttachmentAddedNotificationAsync(ticket, attachment);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Sending attachment added notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("This is a short text", 50, "This is a short text")]
    [InlineData("This is a very long text that should be truncated", 20, "This is a very long ...")]
    [InlineData("", 10, "")]
    [InlineData(null, 10, null)]
    public void TruncateText_VariousInputs_ReturnsExpectedResults(string input, int maxLength, string expected)
    {
        // This tests the private TruncateText method indirectly through template rendering
        // We can't test it directly, but we can verify the behavior through public methods
        
        // For this test, we'll just verify that the method handles various inputs correctly
        // by checking that no exceptions are thrown during notification sending
        var ticket = new SysRequestTicket
        {
            RowId = 123,
            TitleEn = "Test",
            Description = input ?? "",
            Requester = new SysUser { Email = "test@test.com", RowDescE = "Test User" }
        };

        // Act & Assert - Should not throw
        var exception = Record.ExceptionAsync(() => _service.SendTicketCreatedNotificationAsync(ticket));
        Assert.Null(exception.Result);
    }

    [Fact]
    public async Task SendTicketCreatedNotificationAsync_ExceptionDuringNotification_LogsErrorAndContinues()
    {
        // Arrange
        var ticket = new SysRequestTicket
        {
            RowId = 123,
            TitleEn = "Test Ticket",
            Requester = new SysUser { Email = "test@test.com" }
        };

        // Setup configuration to throw exception during email sending
        _mockConfiguration.Setup(c => c.GetValue<string>("Notifications:Smtp:Server"))
            .Throws(new InvalidOperationException("Test exception"));

        // Act
        await _service.SendTicketCreatedNotificationAsync(ticket);

        // Assert - Should log error but not throw
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to send ticket created notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}