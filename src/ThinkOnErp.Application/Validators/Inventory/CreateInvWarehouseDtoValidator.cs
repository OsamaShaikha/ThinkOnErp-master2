using FluentValidation;
using ThinkOnErp.Application.DTOs.Inventory.Warehouses;

namespace ThinkOnErp.Application.Validators.Inventory;

public sealed class CreateInvWarehouseDtoValidator : AbstractValidator<CreateInvWarehouseDto>
{
    public CreateInvWarehouseDtoValidator()
    {
        RuleFor(x => x.WarehouseCode)
            .NotEmpty().WithMessage("Warehouse code is required")
            .MaximumLength(30).WithMessage("Warehouse code cannot exceed 30 characters");

        RuleFor(x => x.WarehouseNameAr)
            .NotEmpty().WithMessage("Arabic warehouse name is required")
            .MaximumLength(150).WithMessage("Arabic warehouse name cannot exceed 150 characters");
    }
}
