using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.Application.Features.Users.Commands.ChangePassword;
using ThinkOnErp.Application.Features.Users.Commands.CreateUser;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;
using MediatR;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// **Validates: Requirements 10.6**
/// Property 14: Change Password Updates Hash
/// For any user and new valid password, verify password hashed using SHA-256 and stored hash updated
/// </summary>
public class ChangePasswordUpdatesHashPropertyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ChangePasswordUpdatesHashPropertyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Property(MaxTest = 100)]
    public Property ChangePassword_UpdatesHashUsingSHA256()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100).Select(i => $"user{i}")),
            Arb.From(Gen.Choose(1, 100).Select(i => $"OldPass{i}123!")),
            Arb.From(Gen.Choose(1, 100).Select(i => $"NewPass{i}456!")),
            (userName, oldPassword, newPassword) =>
            {
                using var scope = _factory.Services.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var passwordHashingService = new PasswordHashingService();

                // Create a user first
                var createCommand = new CreateUserCommand
                {
                    NameAr = $"User {userName}",
                    NameEn = $"User {userName} E",
                    UserName = userName,
                    Password = oldPassword,
                    IsAdmin = false
                };

                var userId = mediator.Send(createCommand).GetAwaiter().GetResult();

                // Get the old password hash
                var userBeforeChange = userRepository.GetByIdAsync(userId).GetAwaiter().GetResult();
                var oldPasswordHash = userBeforeChange?.Password;

                // Change password
                var changePasswordCommand = new ChangePasswordCommand
                {
                    UserId = userId,
                    NewPassword = newPassword
                };

                var changeResult = mediator.Send(changePasswordCommand).GetAwaiter().GetResult();

                // Get the new password hash
                var userAfterChange = userRepository.GetByIdAsync(userId).GetAwaiter().GetResult();
                var newPasswordHash = userAfterChange?.Password;

                // Verify the new hash matches SHA-256 of new password
                var expectedNewHash = passwordHashingService.HashPassword(newPassword);
                var hashMatchesExpected = newPasswordHash == expectedNewHash;

                // Verify hash was actually updated
                var hashWasUpdated = oldPasswordHash != newPasswordHash;

                // Verify hash is 64 characters (SHA-256 hex)
                var hashIsCorrectLength = newPasswordHash?.Length == 64;

                return (changeResult && hashMatchesExpected && hashWasUpdated && hashIsCorrectLength == true).ToProperty();
            });
    }
}
