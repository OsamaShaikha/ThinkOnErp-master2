using FluentValidation;

namespace ThinkOnErp.Application.Features.Currencies.Commands.CreateCurrency;

public class CreateCurrencyCommandValidator : AbstractValidator<CreateCurrencyCommand>
{
    public CreateCurrencyCommandValidator()
    {
        RuleFor(x => x.CurrencyNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CurrencyNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ShortDesc).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ShortDescE).NotEmpty().MaximumLength(50);
        RuleFor(x => x.SingulerDesc).NotEmpty().MaximumLength(50);
        RuleFor(x => x.SingulerDescE).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DualDesc).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DualDescE).NotEmpty().MaximumLength(50);
        RuleFor(x => x.SumDesc).NotEmpty().MaximumLength(50);
        RuleFor(x => x.SumDescE).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FracDesc).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FracDescE).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CurrRate).GreaterThan(0).When(x => x.CurrRate.HasValue);
        RuleFor(x => x.CreationUser).NotEmpty();
    }
}
