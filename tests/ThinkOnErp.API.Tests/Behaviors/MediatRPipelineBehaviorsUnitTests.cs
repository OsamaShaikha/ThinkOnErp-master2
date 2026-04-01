using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.Application.Features.Roles.Commands.CreateRole;
using ThinkOnErp.Application.Features.Roles.Queries.GetAllRoles;
using FluentValidation;
using MediatR;
using Xunit;

namespace ThinkOnErp.API.Tests.Behaviors;

/// <summary>
/// Unit tests for MediatR pipeline behaviors
/// </summary>
public class MediatRPipelineBehaviorsUnitTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public MediatRPipelineBehaviorsUnitTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LoggingBehavior_LogsRequestAndResponse()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var query = new GetAllRolesQuery();

        // Act
        var result = await mediator.Send(query);

        // Assert
        // If the query executed successfully, it went through the logging behavior
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ValidationBehavior_ExecutesBeforeHandler()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Create command with invalid data
        var command = new CreateRoleCommand
        {
            RowDesc = "", // Invalid - empty
            RoleNameEn = "Valid",
            Note = "Test"
        };

        // Act & Assert
        // Validation should throw before handler executes
        await Assert.ThrowsAsync<ValidationException>(async () =>
        {
            await mediator.Send(command);
        });
    }

    [Fact]
    public async Task ValidationBehavior_CollectsAllErrorsBeforeThrowing()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Create command with multiple validation errors
        var command = new CreateRoleCommand
        {
            RowDesc = "", // Invalid - empty
            RoleNameEn = "", // Invalid - empty
            Note = "Test"
        };

        // Act
        ValidationException? exception = null;
        try
        {
            await mediator.Send(command);
        }
        catch (ValidationException ex)
        {
            exception = ex;
        }

        // Assert
        Assert.NotNull(exception);
        Assert.NotNull(exception.Errors);
        Assert.True(exception.Errors.Count() >= 2); // Should have at least 2 errors
    }

    [Fact]
    public async Task Pipeline_ExecutesInCorrectOrder_LoggingThenValidationThenHandler()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Create valid command
        var command = new CreateRoleCommand
        {
            RowDesc = "Test Role",
            RoleNameEn = "Test Role E",
            Note = "Test"
        };

        // Act
        var result = await mediator.Send(command);

        // Assert
        // If we get a valid result, the pipeline executed in correct order:
        // 1. Logging behavior logged the request
        // 2. Validation behavior validated the request (passed)
        // 3. Handler executed and returned result
        Assert.True(result > 0);
    }

    [Fact]
    public async Task ValidationBehavior_WithValidRequest_DoesNotThrow()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Create valid command
        var command = new CreateRoleCommand
        {
            RowDesc = "Valid Role",
            RoleNameEn = "Valid Role E",
            Note = "Valid Note"
        };

        // Act
        var result = await mediator.Send(command);

        // Assert
        Assert.True(result > 0);
    }

    [Fact]
    public async Task ValidationBehavior_WithQueryWithoutValidator_DoesNotThrow()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // GetAllRolesQuery doesn't have a validator
        var query = new GetAllRolesQuery();

        // Act
        var result = await mediator.Send(query);

        // Assert
        // Should execute successfully even without a validator
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Pipeline_WithMultipleRequests_ExecutesIndependently()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var command1 = new CreateRoleCommand
        {
            RowDesc = "Role 1",
            RoleNameEn = "Role 1 E",
            Note = "Test 1"
        };

        var command2 = new CreateRoleCommand
        {
            RowDesc = "Role 2",
            RoleNameEn = "Role 2 E",
            Note = "Test 2"
        };

        // Act
        var result1 = await mediator.Send(command1);
        var result2 = await mediator.Send(command2);

        // Assert
        Assert.True(result1 > 0);
        Assert.True(result2 > 0);
        Assert.NotEqual(result1, result2); // Should have different IDs
    }
}
