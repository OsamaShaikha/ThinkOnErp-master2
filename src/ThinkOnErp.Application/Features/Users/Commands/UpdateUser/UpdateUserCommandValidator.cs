using FluentValidation;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Users.Commands.UpdateUser;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator(
        IBranchRepository branchRepository,
        IRoleRepository roleRepository)
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.BranchIds)
            .NotNull()
            .NotEmpty()
            .Must(branchIds => branchIds == null ||
                branchIds.Distinct().Count() == branchIds.Count)
            .WithMessage("Branch IDs must not contain duplicates.");
        RuleFor(x => x.PrimaryBranchId)
            .GreaterThan(0)
            .Must((command, primaryBranchId) =>
                command.BranchIds?.Contains(primaryBranchId) == true)
            .WithMessage("Primary branch must be one of the assigned branches.");
        RuleForEach(x => x.BranchIds)
            .GreaterThan(0)
            .MustAsync(async (branchId, _) =>
                await branchRepository.GetByIdAsync(branchId) != null)
            .WithMessage("One or more specified branches do not exist.");
        RuleFor(x => x.RoleId)
            .MustAsync(async (roleId, _) =>
                !roleId.HasValue ||
                await roleRepository.GetByIdAsync(roleId.Value) != null)
            .WithMessage("The specified role does not exist.");
        RuleFor(x => x.UpdateUser).NotEmpty();
    }
}
