using FluentValidation;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Commands.UploadAttachment;

/// <summary>
/// Validator for UploadAttachmentCommand.
/// Validates file attachment data according to business rules.
/// </summary>
public class UploadAttachmentCommandValidator : AbstractValidator<UploadAttachmentCommand>
{
    private readonly IAttachmentService _attachmentService;

    public UploadAttachmentCommandValidator(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;

        RuleFor(x => x.TicketId)
            .GreaterThan(0).WithMessage("Valid Ticket ID is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.")
            .Must((command, fileName) => BeValidFileType(fileName, command.MimeType))
            .WithMessage($"File type must be one of: {string.Join(", ", _attachmentService.GetAllowedFileExtensions())}");

        RuleFor(x => x.FileContent)
            .NotEmpty().WithMessage("File content is required.")
            .Must(BeValidBase64).WithMessage("File content must be valid Base64.")
            .Must(BeWithinSizeLimit).WithMessage($"File size must not exceed {_attachmentService.GetMaxFileSizeBytes() / (1024 * 1024)}MB.");

        RuleFor(x => x.MimeType)
            .NotEmpty().WithMessage("MIME type is required.");

        RuleFor(x => x.CreationUser)
            .NotEmpty().WithMessage("Creation user is required.");
    }

    private bool BeValidFileType(string fileName, string mimeType)
    {
        return _attachmentService.IsValidFileType(fileName, mimeType);
    }

    private bool BeValidBase64(string base64Content)
    {
        return _attachmentService.IsValidBase64Content(base64Content);
    }

    private bool BeWithinSizeLimit(string base64Content)
    {
        return _attachmentService.IsValidFileSize(base64Content);
    }
}