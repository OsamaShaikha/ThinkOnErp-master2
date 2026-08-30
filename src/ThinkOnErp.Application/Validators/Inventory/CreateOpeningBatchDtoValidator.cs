using FluentValidation;
using ThinkOnErp.Application.DTOs.Inventory.OpeningBalance;

namespace ThinkOnErp.Application.Validators.Inventory;

public sealed class CreateOpeningBatchDtoValidator : AbstractValidator<CreateOpeningBatchDto>
{
    public CreateOpeningBatchDtoValidator()
    {
        RuleFor(x => x.BranchId)
            .GreaterThan(0).WithMessage("Branch ID is required");

        RuleFor(x => x.FiscalYearId)
            .GreaterThan(0).WithMessage("Fiscal year ID is required");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Opening balance batch must contain at least one line item");

        RuleForEach(x => x.Lines).SetValidator(new CreateOpeningLineDtoValidator());
    }
}

public sealed class CreateOpeningLineDtoValidator : AbstractValidator<CreateOpeningLineDto>
{
    public CreateOpeningLineDtoValidator()
    {
        RuleFor(x => x.WarehouseId)
            .GreaterThan(0).WithMessage("Warehouse ID is required");

        RuleFor(x => x.ItemId)
            .GreaterThan(0).WithMessage("Item ID is required");

        RuleFor(x => x.UomCode)
            .NotEmpty().WithMessage("UOM code is required");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than 0");

        RuleFor(x => x.UnitCost)
            .GreaterThanOrEqualTo(0).WithMessage("Unit cost cannot be negative");
    }
}
