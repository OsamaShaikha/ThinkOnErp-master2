using FluentValidation;

namespace ThinkOnErp.Application.Features.FiscalYears.Commands.CreateFiscalYear;

public class CreateFiscalYearCommandValidator : AbstractValidator<CreateFiscalYearCommand>
{
    public CreateFiscalYearCommandValidator()
    {
        RuleFor(x => x.CompanyId)
            .GreaterThan(0)
            .WithMessage("Company ID is required");

        RuleFor(x => x.BranchId)
            .GreaterThan(0)
            .WithMessage("Branch ID is required");

        RuleFor(x => x.FiscalYearCode)
            .NotEmpty()
            .WithMessage("Fiscal year code is required")
            .MaximumLength(20)
            .WithMessage("Fiscal year code cannot exceed 20 characters");

        RuleFor(x => x.StartDate)
            .NotEmpty()
            .WithMessage("Start date is required");

        RuleFor(x => x.EndDate)
            .NotEmpty()
            .WithMessage("End date is required")
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date");

        RuleFor(x => x.CreationUser)
            .NotEmpty()
            .WithMessage("Creation user is required");
    }
}
