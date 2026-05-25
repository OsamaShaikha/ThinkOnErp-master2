using FluentValidation;

namespace ThinkOnErp.Application.Features.Branches.Commands.CreateBranch;

public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.BranchNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BranchNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.TaxNumber).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.TaxNumber));
        RuleFor(x => x.CreationUser).NotEmpty();

        RuleFor(x => x.BranchLogo)
            .Must(l => l == null || l.Length <= 5 * 1024 * 1024)
            .WithMessage("Branch logo size cannot exceed 5MB")
            .When(x => x.BranchLogo != null);
    }
}
