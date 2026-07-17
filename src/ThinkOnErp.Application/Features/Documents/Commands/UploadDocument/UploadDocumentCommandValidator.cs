using FluentValidation;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Application.Features.Documents.Commands.UploadDocument;

public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.FileStream)
            .NotNull().WithMessage("File stream is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.");

        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("File cannot be empty.");

        RuleFor(x => x.OwnerType)
            .Must(t => t is ThinkOnErp.Domain.Constants.SysCodeKeys.OwnerTypes.Company or 
                           ThinkOnErp.Domain.Constants.SysCodeKeys.OwnerTypes.Branch or 
                           ThinkOnErp.Domain.Constants.SysCodeKeys.OwnerTypes.SuperAdmin)
            .WithMessage("Owner type must be 1 (Company), 2 (Branch), or 3 (SuperAdmin).");

        RuleFor(x => x.OwnerId)
            .GreaterThan(0).WithMessage("Owner ID must be greater than 0.");

        RuleFor(x => x.Category)
            .Must(c => string.IsNullOrEmpty(c) || SysDocument.AllowedDocumentCategories.Contains(c))
            .WithMessage($"Category must be one of: {string.Join(", ", SysDocument.AllowedDocumentCategories)}");

        RuleFor(x => x.CreationUser)
            .NotEmpty().WithMessage("Creation user is required.");
    }
}
