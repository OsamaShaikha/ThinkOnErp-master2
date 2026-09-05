using FluentValidation;

namespace ThinkOnErp.Application.Features.Currencies.Commands.UpdateCurrency;

public class UpdateCurrencyCommandValidator : AbstractValidator<UpdateCurrencyCommand>
{
    public UpdateCurrencyCommandValidator()
    {
        RuleFor(x => x.CurrencyId).GreaterThan(0);
        RuleFor(x => x.CurrencyNameLocal).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CurrencyNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ShortNameLocal).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ShortNameEn).NotEmpty().MaximumLength(50);
        RuleFor(x => x.SingularNameLocal).NotEmpty().MaximumLength(50);
        RuleFor(x => x.SingularNameEn).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DualNameLocal).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DualNameEn).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CollectiveNameLocal).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CollectiveNameEn).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FractionNameLocal).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FractionNameEn).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CurrRate).GreaterThan(0).When(x => x.CurrRate.HasValue);
        RuleFor(x => x.UpdateUser).NotEmpty();
    }
}
