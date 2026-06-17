using FluentValidation;

namespace ThinkOnErp.Application.Features.Features.Commands.UpdateFeature;

public class UpdateFeatureCommandValidator : AbstractValidator<UpdateFeatureCommand>
{
    public UpdateFeatureCommandValidator()
    {
        RuleFor(x => x.FeatureId).GreaterThan(0);
        RuleFor(x => x.FeatureCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FeatureName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FeatureNameE).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UpdateUser).NotEmpty();
    }
}
