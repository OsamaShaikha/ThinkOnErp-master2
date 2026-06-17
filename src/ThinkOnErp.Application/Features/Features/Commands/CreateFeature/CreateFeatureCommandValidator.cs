using FluentValidation;

namespace ThinkOnErp.Application.Features.Features.Commands.CreateFeature;

public class CreateFeatureCommandValidator : AbstractValidator<CreateFeatureCommand>
{
    public CreateFeatureCommandValidator()
    {
        RuleFor(x => x.FeatureCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FeatureName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FeatureNameE).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CreationUser).NotEmpty();
    }
}
