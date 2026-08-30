using FluentValidation;
using ThinkOnErp.Application.DTOs.Inventory.Documents;

namespace ThinkOnErp.Application.Validators.Inventory;

public sealed class CreateTrxDocumentDtoValidator : AbstractValidator<CreateTrxDocumentDto>
{
    public CreateTrxDocumentDtoValidator()
    {
        RuleFor(x => x.BranchId)
            .GreaterThan(0).WithMessage("Branch ID is required and must be greater than 0");

        RuleFor(x => x.DocYear)
            .InclusiveBetween(2000, 2099).WithMessage("Document year must be a valid 4-digit year");

        RuleFor(x => x.DocType)
            .GreaterThan(0).WithMessage("Document type code is required");

        RuleFor(x => x.TrxType)
            .GreaterThan(0).WithMessage("Transaction type code is required");

        RuleFor(x => x.ExchangeRate)
            .GreaterThan(0).WithMessage("Exchange rate must be greater than 0");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Document must contain at least one line item");

        RuleForEach(x => x.Lines).SetValidator(new CreateTrxDocumentLineDtoValidator());
    }
}

public sealed class CreateTrxDocumentLineDtoValidator : AbstractValidator<CreateTrxDocumentLineDto>
{
    public CreateTrxDocumentLineDtoValidator()
    {
        RuleFor(x => x.ItemId)
            .GreaterThan(0).WithMessage("Item ID is required");

        RuleFor(x => x.UomCode)
            .NotEmpty().WithMessage("Unit of Measure (UOM) code is required")
            .MaximumLength(20).WithMessage("UOM code cannot exceed 20 characters");

        RuleFor(x => x.UomFactor)
            .GreaterThan(0).WithMessage("UOM conversion factor must be greater than 0");

        RuleFor(x => x)
            .Must(x => x.QuantityIn > 0 || x.QuantityOut > 0)
            .WithMessage("Line item must have either Quantity In or Quantity Out greater than 0");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Unit price cannot be negative");

        RuleFor(x => x.DiscountPercent)
            .InclusiveBetween(0, 100).WithMessage("Discount percent must be between 0 and 100");

        RuleFor(x => x.TaxRate)
            .GreaterThanOrEqualTo(0).WithMessage("Tax rate cannot be negative");
    }
}
