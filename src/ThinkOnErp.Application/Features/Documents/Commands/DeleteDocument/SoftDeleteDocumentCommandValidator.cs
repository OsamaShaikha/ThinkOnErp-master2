using FluentValidation;

namespace ThinkOnErp.Application.Features.Documents.Commands.DeleteDocument;

public class SoftDeleteDocumentCommandValidator : AbstractValidator<SoftDeleteDocumentCommand>
{
    public SoftDeleteDocumentCommandValidator()
    {
        RuleFor(x => x.Ids)
            .NotEmpty().WithMessage("At least one document ID is required.")
            .Must(ids => ids.All(id => id > 0)).WithMessage("All document IDs must be greater than 0.");

        RuleFor(x => x.UpdateUser)
            .NotEmpty().WithMessage("Update user is required.");
    }
}
