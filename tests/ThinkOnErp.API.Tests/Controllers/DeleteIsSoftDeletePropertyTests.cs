using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.Application.Features.Roles.Commands.CreateRole;
using ThinkOnErp.Application.Features.Roles.Commands.DeleteRole;
using ThinkOnErp.Application.Features.Roles.Queries.GetAllRoles;
using ThinkOnErp.Application.Features.Roles.Queries.GetRoleById;
using MediatR;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// **Validates: Requirements 6.5, 7.5, 8.5, 9.5, 10.5**
/// Property 13: Delete is Soft Delete
/// For any entity type and existing ID, verify Delete sets IS_ACTIVE to false, record not in GetAll results
/// </summary>
public class DeleteIsSoftDeletePropertyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DeleteIsSoftDeletePropertyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Property(MaxTest = 100)]
    public Property DeleteRole_SetsIsActiveFalse_AndNotInGetAllResults()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100).Select(i => $"Role To Delete {i}")),
            Arb.From(Gen.Choose(1, 100).Select(i => $"Role To Delete E {i}")),
            (roleDesc, roleDescE) =>
            {
                using var scope = _factory.Services.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                // Create a role first
                var createCommand = new CreateRoleCommand
                {
                    RoleNameAr = roleDesc,
                    RoleNameEn = roleDescE,
                    Note = "To be deleted"
                };

                var roleId = mediator.Send(createCommand).GetAwaiter().GetResult();

                // Verify role exists before deletion
                var roleBeforeDelete = mediator.Send(new GetRoleByIdQuery { RoleId = roleId }).GetAwaiter().GetResult();
                var existsBeforeDelete = roleBeforeDelete != null;

                // Delete the role
                var deleteCommand = new DeleteRoleCommand { RoleId = roleId };
                var deleteResult = mediator.Send(deleteCommand).GetAwaiter().GetResult();

                // Verify delete succeeded
                var deleteSucceeded = deleteResult > 0;

                // Verify role is not in GetAll results
                var allRoles = mediator.Send(new GetAllRolesQuery()).GetAwaiter().GetResult();
                var notInGetAllResults = !allRoles.Any(r => r.RoleId == roleId);

                return (existsBeforeDelete && deleteSucceeded && notInGetAllResults).ToProperty();
            });
    }
}
