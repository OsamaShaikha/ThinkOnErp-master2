using FluentValidation;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Application.Features.Documents.Commands.UpdateDocument;

public class UpdateDocumentCommandValidator : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Valid document ID is required.");

        RuleFor(x => x.DocumentType)
            .Must(dt => !dt.HasValue || (dt.Value >= 1 && dt.Value <= 12))
            .WithMessage("Document type must be a valid code between 1 (Contracts) and 12 (Other) if provided.");

        RuleFor(x => x.UpdateUser)
            .NotEmpty().WithMessage("Update user is required.");
    }
}
