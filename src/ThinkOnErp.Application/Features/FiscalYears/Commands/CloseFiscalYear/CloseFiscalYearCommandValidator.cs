using FluentValidation;

namespace ThinkOnErp.Application.Features.FiscalYears.Commands.CloseFiscalYear;

public class CloseFiscalYearCommandValidator : AbstractValidator<CloseFiscalYearCommand>
{
    public CloseFiscalYearCommandValidator()
    {
        RuleFor(x => x.FiscalYearId)
            .GreaterThan(0)
            .WithMessage("Fiscal year ID is required");

        RuleFor(x => x.UpdateUser)
            .NotEmpty()
            .WithMessage("Update user is required");
    }
}
