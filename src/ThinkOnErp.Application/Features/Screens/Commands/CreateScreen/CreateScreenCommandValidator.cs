using FluentValidation;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Screens.Commands.CreateScreen;

public class CreateScreenCommandValidator : AbstractValidator<CreateScreenCommand>
{
    public CreateScreenCommandValidator(
        ISystemRepository systemRepository,
        IScreenRepository screenRepository)
    {
        RuleFor(x => x)
            .MustAsync(async (cmd, _) =>
            {
                var sysIds = cmd.SystemIds ?? (cmd.SystemId > 0 ? new List<long> { cmd.SystemId } : new List<long>());
                if (sysIds.Count == 0) return false;
                foreach (var id in sysIds)
                {
                    if (await systemRepository.GetSystemByIdAsync(id) == null)
                        return false;
                }
                return true;
            })
            .WithMessage("At least one valid system must be specified.");

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
