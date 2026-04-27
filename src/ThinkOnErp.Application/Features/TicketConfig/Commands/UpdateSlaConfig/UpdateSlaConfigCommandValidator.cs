using FluentValidation;

namespace ThinkOnErp.Application.Features.TicketConfig.Commands.UpdateSlaConfig;

/// <summary>
/// Validator for UpdateSlaConfigCommand
/// </summary>
public class UpdateSlaConfigCommandValidator : AbstractValidator<UpdateSlaConfigCommand>
{
    public UpdateSlaConfigCommandValidator()
    {
        RuleFor(x => x.LowPriorityHours)
            .GreaterThan(0)
            .WithMessage("Low priority hours must be greater than 0")
            .LessThanOrEqualTo(168)
            .WithMessage("Low priority hours cannot exceed 168 hours (1 week)");

        RuleFor(x => x.MediumPriorityHours)
            .GreaterThan(0)
            .WithMessage("Medium priority hours must be greater than 0")
            .LessThanOrEqualTo(72)
            .WithMessage("Medium priority hours cannot exceed 72 hours");

        RuleFor(x => x.HighPriorityHours)
            .GreaterThan(0)
            .WithMessage("High priority hours must be greater than 0")
            .LessThanOrEqualTo(24)
            .WithMessage("High priority hours cannot exceed 24 hours");

        RuleFor(x => x.CriticalPriorityHours)
            .GreaterThan(0)
            .WithMessage("Critical priority hours must be greater than 0")
            .LessThanOrEqualTo(8)
            .WithMessage("Critical priority hours cannot exceed 8 hours");

        RuleFor(x => x.EscalationThresholdPercentage)
            .GreaterThanOrEqualTo(50)
            .WithMessage("Escalation threshold must be at least 50%")
            .LessThanOrEqualTo(100)
            .WithMessage("Escalation threshold cannot exceed 100%");

        RuleFor(x => x.UpdateUser)
            .NotEmpty()
            .WithMessage("Update user is required")
            .MaximumLength(100)
            .WithMessage("Update user cannot exceed 100 characters");

        // Business rule: Priority hours should be in descending order
        RuleFor(x => x)
            .Must(x => x.CriticalPriorityHours < x.HighPriorityHours)
            .WithMessage("Critical priority hours must be less than High priority hours")
            .Must(x => x.HighPriorityHours < x.MediumPriorityHours)
            .WithMessage("High priority hours must be less than Medium priority hours")
            .Must(x => x.MediumPriorityHours < x.LowPriorityHours)
            .WithMessage("Medium priority hours must be less than Low priority hours");
    }
}
