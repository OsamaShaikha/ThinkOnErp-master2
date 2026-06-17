using FluentValidation;

namespace ThinkOnErp.Application.Features.Screens.Commands.UpdateScreen;

public class UpdateScreenCommandValidator : AbstractValidator<UpdateScreenCommand>
{
    public UpdateScreenCommandValidator()
    {
        RuleFor(x => x.ScreenId).GreaterThan(0);
        RuleFor(x => x.SystemId).GreaterThan(0);
        RuleFor(x => x.ScreenCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ScreenName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ScreenNameE).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UpdateUser).NotEmpty();
    }
}
