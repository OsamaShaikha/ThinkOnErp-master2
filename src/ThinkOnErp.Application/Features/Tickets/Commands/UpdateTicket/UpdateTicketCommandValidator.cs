using FluentValidation;

namespace ThinkOnErp.Application.Features.Tickets.Commands.UpdateTicket;

/// <summary>
/// Validator for UpdateTicketCommand.
/// Validates ticket update data according to business rules.
/// </summary>
public class UpdateTicketCommandValidator : AbstractValidator<UpdateTicketCommand>
{
    public UpdateTicketCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .GreaterThan(0).WithMessage("Valid Ticket ID is required.");

        RuleFor(x => x.TitleAr)
            .NotEmpty().WithMessage("Arabic title is required.")
            .Length(5, 200).WithMessage("Arabic title must be between 5 and 200 characters.");

        RuleFor(x => x.TitleEn)
            .NotEmpty().WithMessage("English title is required.")
            .Length(5, 200).WithMessage("English title must be between 5 and 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .Length(10, 5000).WithMessage("Description must be between 10 and 5000 characters.");

        RuleFor(x => x.TicketTypeId)
            .GreaterThan(0).WithMessage("Valid Ticket Type ID is required.");

        RuleFor(x => x.TicketPriorityId)
            .GreaterThan(0).WithMessage("Valid Ticket Priority ID is required.");

        RuleFor(x => x.UpdateUser)
            .NotEmpty().WithMessage("Update user is required.");
    }
}