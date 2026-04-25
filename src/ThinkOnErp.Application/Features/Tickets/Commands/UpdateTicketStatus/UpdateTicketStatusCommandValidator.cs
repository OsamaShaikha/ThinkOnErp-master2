using FluentValidation;

namespace ThinkOnErp.Application.Features.Tickets.Commands.UpdateTicketStatus;

/// <summary>
/// Validator for UpdateTicketStatusCommand.
/// Validates status update data according to business rules.
/// </summary>
public class UpdateTicketStatusCommandValidator : AbstractValidator<UpdateTicketStatusCommand>
{
    public UpdateTicketStatusCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .GreaterThan(0).WithMessage("Valid Ticket ID is required.");

        RuleFor(x => x.NewStatusId)
            .GreaterThan(0).WithMessage("Valid Status ID is required.");

        RuleFor(x => x.UpdateUser)
            .NotEmpty().WithMessage("Update user is required.");

        RuleFor(x => x.StatusChangeReason)
            .MaximumLength(1000).WithMessage("Status change reason cannot exceed 1000 characters.")
            .When(x => !string.IsNullOrEmpty(x.StatusChangeReason));
    }
}