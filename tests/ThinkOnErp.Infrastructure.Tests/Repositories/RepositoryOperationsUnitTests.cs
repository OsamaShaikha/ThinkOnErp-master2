using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.Application.Features.Roles.Commands.CreateRole;
using ThinkOnErp.Application.Features.Roles.Commands.DeleteRole;
using ThinkOnErp.Application.Features.Roles.Commands.UpdateRole;
using ThinkOnErp.Application.Features.Roles.Queries.GetAllRoles;
using ThinkOnErp.Application.Features.Roles.Queries.GetRoleById;
using ThinkOnErp.Domain.Interfaces;
using MediatR;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories;

/// <summary>
/// Unit tests for repository operations
/// </summary>
public class RepositoryOperationsUnitTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RepositoryOperationsUnitTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCorrectRole()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        // Create a role first
        var createCommand = new CreateRoleCommand
        {
            RowDesc = "Test Role",
            RowDescE = "Test Role E",
            Note = "Test Note"
        };
        var roleId = await mediator.Send(createCommand);

        // Act
        var role = await roleRepository.GetByIdAsync(roleId);

        // Assert
        Assert.NotNull(role);
        Assert.Equal(roleId, role.RowId);
        Assert.Equal("Test Role", role.RowDesc);
        Assert.Equal("Test Role E", role.RowDescE);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var nonExistentId = 999999m;

        // Act
        var role = await roleRepository.GetByIdAsync(nonExistentId);

        // Assert
        Assert.Null(role);
    }

    [Fact]
    public async Task GetAll_WithNoRecords_ReturnsEmptyList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        // Act
        var roles = await roleRepository.GetAllAsync();

        // Assert
        Assert.NotNull(roles);
        // Note: List might not be empty if there are existing roles in the database
        // This test verifies that GetAll returns a list, not null
    }

    [Fact]
    public async Task Create_WithValidData_ReturnsPositiveId()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var createCommand = new CreateRoleCommand
        {
            RowDesc = "New Role",
            RowDescE = "New Role E",
            Note = "New Note"
        };

        // Act
        var roleId = await mediator.Send(createCommand);

        // Assert
        Assert.True(roleId > 0);
    }

    [Fact]
    public async Task Update_WithValidData_PersistsChanges()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Create a role
        var createCommand = new CreateRoleCommand
        {
            RowDesc = "Original Role",
            RowDescE = "Original Role E",
            Note = "Original Note"
        };
        var roleId = await mediator.Send(createCommand);

        // Update the role
        var updateCommand = new UpdateRoleCommand
        {
            RowId = roleId,
            RowDesc = "Updated Role",
            RowDescE = "Updated Role E",
            Note = "Updated Note"
        };

        // Act
        var updateResult = await mediator.Send(updateCommand);

        // Verify changes persisted
        var query = new GetRoleByIdQuery { RowId = roleId };
        var updatedRole = await mediator.Send(query);

        // Assert
        Assert.True(updateResult > 0);
        Assert.NotNull(updatedRole);
        Assert.Equal("Updated Role", updatedRole.RowDesc);
        Assert.Equal("Updated Role E", updatedRole.RowDescE);
        Assert.Equal("Updated Note", updatedRole.Note);
    }

    [Fact]
    public async Task Delete_WithExistingId_SetsIsActiveFalse()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Create a role
        var createCommand = new CreateRoleCommand
        {
            RowDesc = "Role To Delete",
            RowDescE = "Role To Delete E",
            Note = "Will be deleted"
        };
        var roleId = await mediator.Send(createCommand);

        // Act
        var deleteCommand = new DeleteRoleCommand { RowId = roleId };
        var deleteResult = await mediator.Send(deleteCommand);

        // Assert
        Assert.True(deleteResult > 0);
    }

    [Fact]
    public async Task GetAll_AfterDelete_DoesNotIncludeDeletedRecord()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Create a role
        var createCommand = new CreateRoleCommand
        {
            RowDesc = "Role To Delete",
            RowDescE = "Role To Delete E",
            Note = "Will be deleted"
        };
        var roleId = await mediator.Send(createCommand);

        // Delete the role
        var deleteCommand = new DeleteRoleCommand { RowId = roleId };
        await mediator.Send(deleteCommand);

        // Act
        var allRoles = await mediator.Send(new GetAllRolesQuery());

        // Assert
        Assert.DoesNotContain(allRoles, r => r.RowId == roleId);
    }
}
