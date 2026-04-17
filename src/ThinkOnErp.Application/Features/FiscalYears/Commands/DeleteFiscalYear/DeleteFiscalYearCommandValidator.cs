using FluentValidation;

namespace ThinkOnErp.Application.Features.FiscalYears.Commands.DeleteFiscalYear;

public class DeleteFiscalYearCommandValidator : AbstractValidator<DeleteFiscalYearCommand>
{
    public DeleteFiscalYearCommandValidator()
    {
        RuleFor(x => x.FiscalYearId)
            .GreaterThan(0)
            .WithMessage("Fiscal year ID is required");
    }
}
