using FluentValidation;

namespace ThinkOnErp.Application.Features.Users.Commands.CreateUser;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.RowDesc).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RowDescE).NotEmpty().MaximumLength(100);
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.CreationUser).NotEmpty();
    }
}
