using FluentValidation;
using ThinkOnErp.Application.DTOs.Inventory.Items;

namespace ThinkOnErp.Application.Validators.Inventory;

public sealed class CreateInvItemDtoValidator : AbstractValidator<CreateInvItemDto>
{
    public CreateInvItemDtoValidator()
    {
        RuleFor(x => x.ItemCode)
            .NotEmpty().WithMessage("Item code is required")
            .MaximumLength(50).WithMessage("Item code cannot exceed 50 characters")
            .Matches("^[a-zA-Z0-9_.-]+$").WithMessage("Item code can only contain letters, numbers, hyphens, and underscores");

        RuleFor(x => x.ItemNameAr)
            .NotEmpty().WithMessage("Arabic item name is required")
            .MaximumLength(200).WithMessage("Arabic item name cannot exceed 200 characters");

        RuleFor(x => x.ItemNameEn)
            .NotEmpty().WithMessage("English item name is required")
            .MaximumLength(200).WithMessage("English item name cannot exceed 200 characters");

        RuleFor(x => x.UomBase)
            .NotEmpty().WithMessage("Base UOM is required")
            .MaximumLength(20).WithMessage("Base UOM cannot exceed 20 characters");

        RuleFor(x => x.StandardCost)
            .GreaterThanOrEqualTo(0).WithMessage("Standard cost cannot be negative");

        RuleFor(x => x.ReorderPoint)
            .GreaterThanOrEqualTo(0).WithMessage("Reorder point cannot be negative");

        RuleFor(x => x.SafetyStock)
            .GreaterThanOrEqualTo(0).WithMessage("Safety stock cannot be negative");
    }
}
