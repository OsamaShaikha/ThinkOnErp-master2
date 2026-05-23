using FluentValidation;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Application.Features.Documents.Commands.UpdateDocument;

public class UpdateDocumentCommandValidator : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Valid document ID is required.");

        RuleFor(x => x.Category)
            .Must(c => string.IsNullOrEmpty(c) || SysDocument.AllowedDocumentCategories.Contains(c))
            .WithMessage($"Category must be one of: {string.Join(", ", SysDocument.AllowedDocumentCategories)}");

        RuleFor(x => x.UpdateUser)
            .NotEmpty().WithMessage("Update user is required.");
    }
}
