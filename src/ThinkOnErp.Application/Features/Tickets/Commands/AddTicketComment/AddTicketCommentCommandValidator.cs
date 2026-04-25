using FluentValidation;

namespace ThinkOnErp.Application.Features.Tickets.Commands.AddTicketComment;

/// <summary>
/// Validator for AddTicketCommentCommand.
/// Validates comment data according to business rules.
/// </summary>
public class AddTicketCommentCommandValidator : AbstractValidator<AddTicketCommentCommand>
{
    public AddTicketCommentCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .GreaterThan(0).WithMessage("Valid Ticket ID is required.");

        RuleFor(x => x.CommentText)
            .NotEmpty().WithMessage("Comment text is required.")
            .Length(1, 2000).WithMessage("Comment text must be between 1 and 2000 characters.");

        RuleFor(x => x.CreationUser)
            .NotEmpty().WithMessage("Creation user is required.");
    }
}