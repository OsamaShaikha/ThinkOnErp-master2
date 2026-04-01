using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.Application.DTOs.Role;
using ThinkOnErp.Application.Features.Roles.Commands.CreateRole;
using ThinkOnErp.Application.Features.Roles.Commands.UpdateRole;
using ThinkOnErp.Application.Features.Roles.Queries.GetRoleById;
using MediatR;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// **Validates: Requirements 6.4, 7.4, 8.4, 9.4, 10.4**
/// Property 12: Update Succeeds for Valid Data
/// For any entity type, existing ID, and valid update data, verify Update succeeds and values persisted
/// </summary>
public class UpdateSucceedsForValidDataPropertyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public UpdateSucceedsForValidDataPropertyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Property(MaxTest = 100)]
    public Property UpdateRole_WithValidData_SucceedsAndPersistsChanges()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100)),
            (iteration) =>
            {
                using var scope = _factory.Services.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var originalDesc = $"Original Role {iteration}";
                var originalDescE = $"Original Role E {iteration}";
                var updatedDesc = $"Updated Role {iteration}";
                var updatedDescE = $"Updated Role E {iteration}";

                // Create a role first
                var createCommand = new CreateRoleCommand
                {
                    RowDesc = originalDesc,
                    RoleNameEn = originalDescE,
                    Note = "Original note"
                };

                var roleId = mediator.Send(createCommand).GetAwaiter().GetResult();

                // Update the role
                var updateCommand = new UpdateRoleCommand
                {
                    RoleId = roleId,
                    RoleNameAr = updatedDesc,
                    RoleNameEn = updatedDescE,
                    Note = "Updated note"
                };

                var updateResult = mediator.Send(updateCommand).GetAwaiter().GetResult();

                // Verify update succeeded
                var updateSucceeded = updateResult > 0;

                // Retrieve the updated role
                var query = new GetRoleByIdQuery { RoleId = roleId };
                var updatedRole = mediator.Send(query).GetAwaiter().GetResult();

                // Verify values were persisted
                var valuesPersisted = updatedRole != null &&
                                     updatedRole.RowDesc == updatedDesc &&
                                     updatedRole.RowDescE == updatedDescE &&
                                     updatedRole.Note == "Updated note";

                return (updateSucceeded && valuesPersisted).ToProperty();
            });
    }
}
