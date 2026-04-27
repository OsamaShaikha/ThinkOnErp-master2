using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using ThinkOnErp.API.Controllers;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.TicketConfig;
using ThinkOnErp.Application.Features.TicketConfig.Commands.UpdateSlaConfig;
using ThinkOnErp.Application.Features.TicketConfig.Queries.GetSlaConfig;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Unit tests for ConfigurationController
/// Tests configuration API endpoints with AdminOnly authorization
/// </summary>
public class ConfigurationControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<ConfigurationController>> _loggerMock;
    private readonly ConfigurationController _controller;

    public ConfigurationControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<ConfigurationController>>();
        _controller = new ConfigurationController(_mediatorMock.Object, _loggerMock.Object);

        // Setup controller context with authenticated admin user
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "admin@test.com"),
            new Claim("IsAdmin", "true")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task GetSlaConfiguration_ReturnsOkWithSlaConfig()
    {
        // Arrange
        var expectedConfig = new SlaConfigDto
        {
            LowPriorityHours = 72,
            MediumPriorityHours = 24,
            HighPriorityHours = 8,
            CriticalPriorityHours = 2,
            EscalationThresholdPercentage = 80
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetSlaConfigQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedConfig);

        // Act
        var result = await _controller.GetSlaConfiguration();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<SlaConfigDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(expectedConfig.LowPriorityHours, response.Data.LowPriorityHours);
        Assert.Equal(expectedConfig.MediumPriorityHours, response.Data.MediumPriorityHours);
        Assert.Equal(expectedConfig.HighPriorityHours, response.Data.HighPriorityHours);
        Assert.Equal(expectedConfig.CriticalPriorityHours, response.Data.CriticalPriorityHours);
        Assert.Equal(expectedConfig.EscalationThresholdPercentage, response.Data.EscalationThresholdPercentage);
    }

    [Fact]
    public async Task UpdateSlaConfiguration_ValidData_ReturnsOkWithSuccess()
    {
        // Arrange
        var updateDto = new SlaConfigDto
        {
            LowPriorityHours = 96,
            MediumPriorityHours = 48,
            HighPriorityHours = 12,
            CriticalPriorityHours = 4,
            EscalationThresholdPercentage = 75
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpdateSlaConfigCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.UpdateSlaConfiguration(updateDto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<bool>>(okResult.Value);
        Assert.True(response.Success);
        Assert.True(response.Data);
        Assert.Equal("SLA configuration updated successfully", response.Message);

        // Verify the command was sent with correct values
        _mediatorMock.Verify(m => m.Send(
            It.Is<UpdateSlaConfigCommand>(cmd =>
                cmd.LowPriorityHours == updateDto.LowPriorityHours &&
                cmd.MediumPriorityHours == updateDto.MediumPriorityHours &&
                cmd.HighPriorityHours == updateDto.HighPriorityHours &&
                cmd.CriticalPriorityHours == updateDto.CriticalPriorityHours &&
                cmd.EscalationThresholdPercentage == updateDto.EscalationThresholdPercentage &&
                cmd.UpdateUser == "admin@test.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSlaConfiguration_UpdateFails_ReturnsBadRequest()
    {
        // Arrange
        var updateDto = new SlaConfigDto
        {
            LowPriorityHours = 72,
            MediumPriorityHours = 24,
            HighPriorityHours = 8,
            CriticalPriorityHours = 2,
            EscalationThresholdPercentage = 80
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpdateSlaConfigCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.UpdateSlaConfiguration(updateDto);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<bool>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Failed to update SLA configuration", response.Message);
    }

    [Fact]
    public async Task UpdateSlaConfiguration_ValidationError_ReturnsBadRequest()
    {
        // Arrange
        var updateDto = new SlaConfigDto
        {
            LowPriorityHours = 72,
            MediumPriorityHours = 24,
            HighPriorityHours = 8,
            CriticalPriorityHours = 2,
            EscalationThresholdPercentage = 80
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpdateSlaConfigCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid configuration values"));

        // Act
        var result = await _controller.UpdateSlaConfiguration(updateDto);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<bool>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Invalid configuration values", response.Message);
    }

    [Fact]
    public async Task GetSlaConfiguration_LogsInformation()
    {
        // Arrange
        var expectedConfig = new SlaConfigDto
        {
            LowPriorityHours = 72,
            MediumPriorityHours = 24,
            HighPriorityHours = 8,
            CriticalPriorityHours = 2,
            EscalationThresholdPercentage = 80
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetSlaConfigQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedConfig);

        // Act
        await _controller.GetSlaConfiguration();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrieving SLA configuration settings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrieved SLA configuration successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSlaConfiguration_LogsInformation()
    {
        // Arrange
        var updateDto = new SlaConfigDto
        {
            LowPriorityHours = 72,
            MediumPriorityHours = 24,
            HighPriorityHours = 8,
            CriticalPriorityHours = 2,
            EscalationThresholdPercentage = 80
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpdateSlaConfigCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _controller.UpdateSlaConfiguration(updateDto);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Updating SLA configuration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SLA configuration updated successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
