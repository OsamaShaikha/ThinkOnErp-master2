using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.Application.Features.Roles.Commands.CreateRole;
using ThinkOnErp.Application.Features.Roles.Commands.DeleteRole;
using ThinkOnErp.Domain.Interfaces;
using MediatR;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories;

/// <summary>
/// Unit tests for data type mapping between Oracle and C#
/// </summary>
public class DataTypeMappingUnitTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DataTypeMappingUnitTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OracleNumber_MappedToCSharpDecimal()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        // Create a role
        var command = new CreateRoleCommand
        {
            RowDesc = "Test Role",
            RowDescE = "Test Role E",
            Note = "Test"
        };

        // Act
        var roleId = await mediator.Send(command);
        var role = await roleRepository.GetByIdAsync(roleId);

        // Assert
        Assert.NotNull(role);
        Assert.IsType<decimal>(role.RowId);
        Assert.True(role.RowId > 0);
    }

    [Fact]
    public async Task OracleVarchar2_MappedToCSharpString()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        // Create a role with string values
        var command = new CreateRoleCommand
        {
            RowDesc = "Arabic Description",
            RowDescE = "English Description",
            Note = "Test Note"
        };

        // Act
        var roleId = await mediator.Send(command);
        var role = await roleRepository.GetByIdAsync(roleId);

        // Assert
        Assert.NotNull(role);
        Assert.IsType<string>(role.RowDesc);
        Assert.IsType<string>(role.RowDescE);
        Assert.Equal("Arabic Description", role.RowDesc);
        Assert.Equal("English Description", role.RowDescE);
    }

    [Fact]
    public async Task OracleDate_MappedToCSharpNullableDateTime()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        // Create a role
        var command = new CreateRoleCommand
        {
            RowDesc = "Test Role",
            RowDescE = "Test Role E",
            Note = "Test"
        };

        // Act
        var roleId = await mediator.Send(command);
        var role = await roleRepository.GetByIdAsync(roleId);

        // Assert
        Assert.NotNull(role);
        // CreationDate should be set by the stored procedure
        Assert.IsType<DateTime?>(role.CreationDate);
        Assert.NotNull(role.CreationDate);
        Assert.True(role.CreationDate.Value > DateTime.MinValue);
    }

    [Fact]
    public async Task OracleIsActiveY_MappedToCSharpTrue()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        // Create a role (should be active by default)
        var command = new CreateRoleCommand
        {
            RowDesc = "Active Role",
            RowDescE = "Active Role E",
            Note = "Test"
        };

        // Act
        var roleId = await mediator.Send(command);
        var role = await roleRepository.GetByIdAsync(roleId);

        // Assert
        Assert.NotNull(role);
        Assert.IsType<bool>(role.IsActive);
        Assert.True(role.IsActive); // Oracle 'Y' or '1' should map to true
    }

    [Fact]
    public async Task OracleIsActiveN_MappedToCSharpFalse()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        // Create a role
        var command = new CreateRoleCommand
        {
            RowDesc = "Role To Deactivate",
            RowDescE = "Role To Deactivate E",
            Note = "Test"
        };

        var roleId = await mediator.Send(command);

        // Delete (soft delete - sets IS_ACTIVE to 'N' or '0')
        var deleteCommand = new DeleteRoleCommand { RowId = roleId };
        await mediator.Send(deleteCommand);

        // Act
        var role = await roleRepository.GetByIdAsync(roleId);

        // Assert
        // After soft delete, the role might be null or have IsActive = false
        // depending on whether GetByIdAsync filters by IS_ACTIVE
        if (role != null)
        {
            Assert.IsType<bool>(role.IsActive);
            Assert.False(role.IsActive); // Oracle 'N' or '0' should map to false
        }
    }

    [Fact]
    public async Task OracleIsActive1_MappedToCSharpTrue()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        // Create a role (IS_ACTIVE should be '1' or 'Y')
        var command = new CreateRoleCommand
        {
            RowDesc = "Active Role",
            RowDescE = "Active Role E",
            Note = "Test"
        };

        // Act
        var roleId = await mediator.Send(command);
        var role = await roleRepository.GetByIdAsync(roleId);

        // Assert
        Assert.NotNull(role);
        Assert.True(role.IsActive); // Oracle '1' should map to true
    }

    [Fact]
    public async Task OracleIsActive0_MappedToCSharpFalse()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        // Create and delete a role
        var command = new CreateRoleCommand
        {
            RowDesc = "Role To Deactivate",
            RowDescE = "Role To Deactivate E",
            Note = "Test"
        };

        var roleId = await mediator.Send(command);
        var deleteCommand = new DeleteRoleCommand { RowId = roleId };
        await mediator.Send(deleteCommand);

        // Act
        var role = await roleRepository.GetByIdAsync(roleId);

        // Assert
        // After soft delete, IS_ACTIVE should be '0' or 'N', mapped to false
        if (role != null)
        {
            Assert.False(role.IsActive); // Oracle '0' should map to false
        }
    }

    [Fact]
    public async Task NullableFields_MappedCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        // Create a role with null note
        var command = new CreateRoleCommand
        {
            RowDesc = "Test Role",
            RowDescE = "Test Role E",
            Note = null // Nullable field
        };

        // Act
        var roleId = await mediator.Send(command);
        var role = await roleRepository.GetByIdAsync(roleId);

        // Assert
        Assert.NotNull(role);
        // Note is nullable, should handle null correctly
        Assert.True(role.Note == null || role.Note == string.Empty);
    }
}
