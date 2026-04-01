using FluentValidation;

namespace ThinkOnErp.Application.Features.Branches.Commands.UpdateBranch;

public class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.RowId).GreaterThan(0);
        RuleFor(x => x.RowDesc).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RowDescE).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.UpdateUser).NotEmpty();
    }
}
