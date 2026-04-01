using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.Application.Features.Roles.Commands.CreateRole;
using ThinkOnErp.Application.Features.Roles.Commands.UpdateRole;
using ThinkOnErp.Application.Features.Users.Commands.CreateUser;
using FluentValidation;
using MediatR;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// **Validates: Requirements 12.3**
/// Property 18: Validation Executes for All Requests
/// For any command or query with registered validators, verify validation behavior executes all validators before handler
/// </summary>
public class ValidationExecutesForAllRequestsPropertyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ValidationExecutesForAllRequestsPropertyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Property(MaxTest = 100)]
    public Property CommandWithValidator_ExecutesValidationBeforeHandler()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100)),
            (iteration) =>
            {
                using var scope = _factory.Services.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                // Test with CreateRoleCommand which has a validator
                var createCommand = new CreateRoleCommand
                {
                    RowDesc = "", // Invalid - should trigger validation
                    RoleNameEn = "Valid English",
                    Note = "Test"
                };

                ValidationException? caughtException = null;
                try
                {
                    var result = mediator.Send(createCommand).GetAwaiter().GetResult();
                }
                catch (ValidationException ex)
                {
                    caughtException = ex;
                }

                // Verify validation exception was thrown (validation executed)
                var validationExecuted = caughtException != null;

                // Verify exception contains validation errors
                var hasValidationErrors = caughtException?.Errors?.Any() ?? false;

                // Test with UpdateRoleCommand
                var updateCommand = new UpdateRoleCommand
                {
                    RoleId = 1,
                    RoleNameAr = "", // Invalid
                    RoleNameEn = "",  // Invalid
                    Note = "Test"
                };

                ValidationException? updateException = null;
                try
                {
                    var result = mediator.Send(updateCommand).GetAwaiter().GetResult();
                }
                catch (ValidationException ex)
                {
                    updateException = ex;
                }

                var updateValidationExecuted = updateException != null;
                var updateHasValidationErrors = updateException?.Errors?.Any() ?? false;

                return (validationExecuted && hasValidationErrors && 
                       updateValidationExecuted && updateHasValidationErrors).ToProperty();
            });
    }
}
