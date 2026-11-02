using FluentValidation;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.ScreenFeatures.Commands.AssignFeaturesToSystemScreen;

public class AssignFeaturesToSystemScreenCommandValidator
    : AbstractValidator<AssignFeaturesToSystemScreenCommand>
{
    public AssignFeaturesToSystemScreenCommandValidator(
        ISystemRepository systemRepository,
        IScreenRepository screenRepository,
        ISysFeatureRepository featureRepository)
    {
        RuleFor(x => x.SystemId)
            .GreaterThan(0)
            .MustAsync(async (systemId, _) =>
                await systemRepository.GetSystemByIdAsync(systemId) != null)
            .WithMessage("The specified system does not exist.");

        When(x => x.ScreenId.HasValue, () =>
        {
            RuleFor(x => x.ScreenId!.Value)
                .GreaterThan(0)
                .MustAsync(async (screenId, _) =>
                    await screenRepository.GetScreenByIdAsync(screenId) != null)
                .WithMessage("The specified screen does not exist.");
        });

        RuleFor(x => x.FeatureIds)
            .NotNull()
            .Must(featureIds => featureIds == null ||
                featureIds.Distinct().Count() == featureIds.Count)
            .WithMessage("Feature IDs must not contain duplicates.");

        RuleForEach(x => x.FeatureIds)
            .GreaterThan(0)
            .MustAsync(async (featureId, _) =>
                await featureRepository.GetFeatureByIdAsync(featureId) != null)
            .WithMessage("One or more specified features do not exist.");
    }
}
