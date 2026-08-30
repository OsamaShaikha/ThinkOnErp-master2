using FluentValidation;
using ThinkOnErp.Application.DTOs.Inventory.Bom;

namespace ThinkOnErp.Application.Validators.Inventory;

public sealed class CreateInvBomDtoValidator : AbstractValidator<CreateInvBomDto>
{
    public CreateInvBomDtoValidator()
    {
        RuleFor(x => x.BomCode)
            .NotEmpty().WithMessage("BOM code is required")
            .MaximumLength(30).WithMessage("BOM code cannot exceed 30 characters");

        RuleFor(x => x.BomNameAr)
            .NotEmpty().WithMessage("Arabic BOM name is required")
            .MaximumLength(200).WithMessage("Arabic BOM name cannot exceed 200 characters");

        RuleFor(x => x.ParentItemId)
            .GreaterThan(0).WithMessage("Parent item ID is required");

        RuleFor(x => x.OutputQty)
            .GreaterThan(0).WithMessage("Output quantity must be greater than 0");

        RuleFor(x => x.UomCode)
            .NotEmpty().WithMessage("UOM code is required");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("BOM must contain at least one component line");

        RuleForEach(x => x.Lines).SetValidator(new CreateInvBomLineDtoValidator());
    }
}

public sealed class CreateInvBomLineDtoValidator : AbstractValidator<CreateInvBomLineDto>
{
    public CreateInvBomLineDtoValidator()
    {
        RuleFor(x => x.ComponentItemId)
            .GreaterThan(0).WithMessage("Component item ID is required");

        RuleFor(x => x.UomCode)
            .NotEmpty().WithMessage("Component UOM code is required");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Component quantity must be greater than 0");
    }
}
