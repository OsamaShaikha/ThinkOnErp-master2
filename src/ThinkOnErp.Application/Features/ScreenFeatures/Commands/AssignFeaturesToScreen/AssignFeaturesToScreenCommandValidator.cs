using FluentValidation;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.ScreenFeatures.Commands.AssignFeaturesToScreen;

public class AssignFeaturesToScreenCommandValidator
    : AbstractValidator<AssignFeaturesToScreenCommand>
{
    public AssignFeaturesToScreenCommandValidator(
        IScreenRepository screenRepository,
        ISysFeatureRepository featureRepository)
    {
        RuleFor(x => x.ScreenId)
            .GreaterThan(0)
            .MustAsync(async (screenId, _) =>
                await screenRepository.GetScreenByIdAsync(screenId) != null)
            .WithMessage("The specified screen does not exist.");

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
