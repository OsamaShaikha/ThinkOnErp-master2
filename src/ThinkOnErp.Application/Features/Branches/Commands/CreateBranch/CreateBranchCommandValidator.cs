using FluentValidation;

namespace ThinkOnErp.Application.Features.Branches.Commands.CreateBranch;

public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.RowDesc).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RowDescE).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.CreationUser).NotEmpty();
    }
}
