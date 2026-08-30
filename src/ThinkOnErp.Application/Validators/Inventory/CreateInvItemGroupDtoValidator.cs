using FluentValidation;
using ThinkOnErp.Application.DTOs.Inventory.ItemGroups;

namespace ThinkOnErp.Application.Validators.Inventory;

public sealed class CreateInvItemGroupDtoValidator : AbstractValidator<CreateInvItemGroupDto>
{
    public CreateInvItemGroupDtoValidator()
    {
        RuleFor(x => x.GroupCode)
            .NotEmpty().WithMessage("Group code is required")
            .MaximumLength(30).WithMessage("Group code cannot exceed 30 characters");

        RuleFor(x => x.GroupNameAr)
            .NotEmpty().WithMessage("Arabic group name is required")
            .MaximumLength(150).WithMessage("Arabic group name cannot exceed 150 characters");
    }
}
