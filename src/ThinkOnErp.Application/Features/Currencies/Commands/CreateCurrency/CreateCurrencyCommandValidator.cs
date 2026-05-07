using FluentValidation;

namespace ThinkOnErp.Application.Features.Currencies.Commands.CreateCurrency;

public class CreateCurrencyCommandValidator : AbstractValidator<CreateCurrencyCommand>
{
    public CreateCurrencyCommandValidator()
    {
        RuleFor(x => x.CurrencyNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CurrencyNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ShortNameAr).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ShortNameEn).NotEmpty().MaximumLength(50);
        RuleFor(x => x.SingularNameAr).NotEmpty().MaximumLength(50);
        RuleFor(x => x.SingularNameEn).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DualNameAr).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DualNameEn).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CollectiveNameAr).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CollectiveNameEn).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FractionNameAr).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FractionNameEn).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CurrRate).GreaterThan(0).When(x => x.CurrRate.HasValue);
        RuleFor(x => x.CreationUser).NotEmpty();
    }
}
