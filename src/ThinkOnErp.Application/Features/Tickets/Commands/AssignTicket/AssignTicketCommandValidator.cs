using FluentValidation;

namespace ThinkOnErp.Application.Features.Tickets.Commands.AssignTicket;

/// <summary>
/// Validator for AssignTicketCommand.
/// Validates ticket assignment data according to business rules.
/// </summary>
public class AssignTicketCommandValidator : AbstractValidator<AssignTicketCommand>
{
    public AssignTicketCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .GreaterThan(0).WithMessage("Valid Ticket ID is required.");

        RuleFor(x => x.AssigneeId)
            .GreaterThan(0).WithMessage("Valid Assignee ID is required.")
            .When(x => x.AssigneeId.HasValue);

        RuleFor(x => x.UpdateUser)
            .NotEmpty().WithMessage("Update user is required.");

        RuleFor(x => x.AssignmentReason)
            .MaximumLength(500).WithMessage("Assignment reason cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.AssignmentReason));
    }
}