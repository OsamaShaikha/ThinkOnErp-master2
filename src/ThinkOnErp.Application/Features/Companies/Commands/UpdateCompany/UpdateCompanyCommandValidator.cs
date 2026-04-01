using FluentValidation;

namespace ThinkOnErp.Application.Features.Companies.Commands.UpdateCompany;

public class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.RowId).GreaterThan(0);
        RuleFor(x => x.RowDesc).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RowDescE).NotEmpty().MaximumLength(100);
        RuleFor(x => x.UpdateUser).NotEmpty();
    }
}
