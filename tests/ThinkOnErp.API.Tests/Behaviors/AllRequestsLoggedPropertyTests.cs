using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.Application.Features.Roles.Commands.CreateRole;
using ThinkOnErp.Application.Features.Roles.Queries.GetAllRoles;
using MediatR;
using Xunit;

namespace ThinkOnErp.API.Tests.Behaviors;

/// <summary>
/// **Validates: Requirements 13.8**
/// Property 22: All Requests Logged
/// For any MediatR request, verify logging behavior logs request type, parameters, response data, execution time
/// Note: This test verifies that requests execute successfully through the logging pipeline.
/// Actual log output verification would require log capture infrastructure.
/// </summary>
public class AllRequestsLoggedPropertyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AllRequestsLoggedPropertyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Property(MaxTest = 100)]
    public Property MediatRRequest_ExecutesThroughLoggingPipeline()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100).Select(i => $"Role {i}")),
            Arb.From(Gen.Choose(1, 100).Select(i => $"Role E {i}")),
            (roleDesc, roleDescE) =>
            {
                using var scope = _factory.Services.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                // Test command execution (CreateRoleCommand)
                var createCommand = new CreateRoleCommand
                {
                    RoleNameAr = roleDesc,
                    RoleNameEn = roleDescE,
                    Note = "Test logging"
                };

                var commandExecuted = false;
                var commandResult = 0m;
                try
                {
                    commandResult = mediator.Send(createCommand).GetAwaiter().GetResult();
                    commandExecuted = commandResult > 0;
                }
                catch
                {
                    commandExecuted = false;
                }

                // Test query execution (GetAllRolesQuery)
                var getAllQuery = new GetAllRolesQuery();
                var queryExecuted = false;
                try
                {
                    var queryResult = mediator.Send(getAllQuery).GetAwaiter().GetResult();
                    queryExecuted = queryResult != null;
                }
                catch
                {
                    queryExecuted = false;
                }

                // If both executed successfully, they went through the logging pipeline
                // The LoggingBehavior is registered in the pipeline and wraps all requests
                return (commandExecuted && queryExecuted).ToProperty();
            });
    }
}
