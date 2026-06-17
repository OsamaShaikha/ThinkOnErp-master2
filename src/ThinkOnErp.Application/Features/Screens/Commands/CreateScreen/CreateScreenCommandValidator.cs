using FluentValidation;

namespace ThinkOnErp.Application.Features.Screens.Commands.CreateScreen;

public class CreateScreenCommandValidator : AbstractValidator<CreateScreenCommand>
{
    public CreateScreenCommandValidator()
    {
        RuleFor(x => x.SystemId).GreaterThan(0);
        RuleFor(x => x.ScreenCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ScreenName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ScreenNameE).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CreationUser).NotEmpty();
    }
}
