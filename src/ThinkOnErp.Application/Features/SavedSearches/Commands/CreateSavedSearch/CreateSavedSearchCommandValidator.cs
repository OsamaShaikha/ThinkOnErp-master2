using FluentValidation;

namespace ThinkOnErp.Application.Features.SavedSearches.Commands.CreateSavedSearch;

public class CreateSavedSearchCommandValidator
    : AbstractValidator<CreateSavedSearchCommand>
{
    public CreateSavedSearchCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.SearchName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SearchDescription)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.SearchDescription));
        RuleFor(x => x.SearchCriteria).NotEmpty();
        RuleFor(x => x.CreationUser).NotEmpty().MaximumLength(100);
    }
}
