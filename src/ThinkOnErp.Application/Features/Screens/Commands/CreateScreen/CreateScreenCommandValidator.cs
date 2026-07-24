using FluentValidation;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Screens.Commands.CreateScreen;

public class CreateScreenCommandValidator : AbstractValidator<CreateScreenCommand>
{
    public CreateScreenCommandValidator(
        ISystemRepository systemRepository,
        IScreenRepository screenRepository)
    {
        RuleFor(x => x.SystemId)
            .GreaterThan(0)
            .MustAsync(async (systemId, _) =>
                await systemRepository.GetSystemByIdAsync(systemId) != null)
            .WithMessage("The specified system does not exist.");

        RuleFor(x => x.ParentScreenId)
            .MustAsync(async (parentScreenId, _) =>
                !parentScreenId.HasValue ||
                await screenRepository.GetScreenByIdAsync(parentScreenId.Value) != null)
            .WithMessage("The specified parent screen does not exist.");

        RuleFor(x => x.ScreenCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ScreenName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ScreenNameE).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CreationUser).NotEmpty();
    }
}
