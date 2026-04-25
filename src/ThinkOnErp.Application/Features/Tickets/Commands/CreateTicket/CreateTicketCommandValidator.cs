using FluentValidation;

namespace ThinkOnErp.Application.Features.Tickets.Commands.CreateTicket;

/// <summary>
/// Validator for CreateTicketCommand.
/// Validates ticket creation data according to business rules.
/// </summary>
public class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator()
    {
        RuleFor(x => x.TitleAr)
            .NotEmpty().WithMessage("Arabic title is required.")
            .Length(5, 200).WithMessage("Arabic title must be between 5 and 200 characters.");

        RuleFor(x => x.TitleEn)
            .NotEmpty().WithMessage("English title is required.")
            .Length(5, 200).WithMessage("English title must be between 5 and 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .Length(10, 5000).WithMessage("Description must be between 10 and 5000 characters.");

        RuleFor(x => x.CompanyId)
            .GreaterThan(0).WithMessage("Valid Company ID is required.");

        RuleFor(x => x.BranchId)
            .GreaterThan(0).WithMessage("Valid Branch ID is required.");

        RuleFor(x => x.RequesterId)
            .GreaterThan(0).WithMessage("Valid Requester ID is required.");

        RuleFor(x => x.TicketTypeId)
            .GreaterThan(0).WithMessage("Valid Ticket Type ID is required.");

        RuleFor(x => x.TicketPriorityId)
            .GreaterThan(0).WithMessage("Valid Ticket Priority ID is required.");

        RuleFor(x => x.CreationUser)
            .NotEmpty().WithMessage("Creation user is required.");

        // Validate attachments if provided
        RuleForEach(x => x.Attachments)
            .SetValidator(new CreateAttachmentValidator())
            .When(x => x.Attachments != null);

        RuleFor(x => x.Attachments)
            .Must(attachments => attachments == null || attachments.Count <= 5)
            .WithMessage("Maximum 5 attachments allowed per ticket.");
    }
}

/// <summary>
/// Validator for CreateAttachmentDto within CreateTicketCommand.
/// </summary>
public class CreateAttachmentValidator : AbstractValidator<ThinkOnErp.Application.DTOs.Ticket.CreateAttachmentDto>
{
    private static readonly string[] AllowedFileTypes = { 
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", 
        ".jpg", ".jpeg", ".png", ".txt" 
    };

    private const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB

    public CreateAttachmentValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.")
            .Must(BeValidFileType).WithMessage($"File type must be one of: {string.Join(", ", AllowedFileTypes)}");

        RuleFor(x => x.FileContent)
            .NotEmpty().WithMessage("File content is required.")
            .Must(BeValidBase64).WithMessage("File content must be valid Base64.")
            .Must(BeWithinSizeLimit).WithMessage($"File size must not exceed {MaxFileSizeBytes / (1024 * 1024)}MB.");

        RuleFor(x => x.MimeType)
            .NotEmpty().WithMessage("MIME type is required.");
    }

    private bool BeValidFileType(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;
        
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return AllowedFileTypes.Contains(extension);
    }

    private bool BeValidBase64(string base64Content)
    {
        if (string.IsNullOrEmpty(base64Content)) return false;

        try
        {
            Convert.FromBase64String(base64Content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool BeWithinSizeLimit(string base64Content)
    {
        if (string.IsNullOrEmpty(base64Content)) return false;

        try
        {
            var bytes = Convert.FromBase64String(base64Content);
            return bytes.Length <= MaxFileSizeBytes;
        }
        catch
        {
            return false;
        }
    }
}