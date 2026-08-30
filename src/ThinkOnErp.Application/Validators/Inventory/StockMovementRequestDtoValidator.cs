using FluentValidation;
using ThinkOnErp.Application.DTOs.Inventory.StockMovements;

namespace ThinkOnErp.Application.Validators.Inventory;

public sealed class StockMovementRequestDtoValidator : AbstractValidator<StockMovementRequestDto>
{
    public StockMovementRequestDtoValidator()
    {
        RuleFor(x => x.ItemId)
            .GreaterThan(0).WithMessage("Item ID is required");

        RuleFor(x => x.WarehouseId)
            .GreaterThan(0).WithMessage("Warehouse ID is required");

        RuleFor(x => x.TransactionType)
            .GreaterThan(0).WithMessage("Transaction type code is required");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Movement quantity must be greater than 0");

        RuleFor(x => x.UomCode)
            .NotEmpty().WithMessage("UOM code is required");
    }
}
