using FluentValidation;

namespace ThinkOnErp.Application.Features.TicketConfig.Commands.UpdateConfigValue;

/// <summary>
/// Validator for UpdateConfigValueCommand
/// </summary>
public class UpdateConfigValueCommandValidator : AbstractValidator<UpdateConfigValueCommand>
{
    public UpdateConfigValueCommandValidator()
    {
        RuleFor(x => x.ConfigKey)
            .NotEmpty().WithMessage("Configuration key is required")
            .MaximumLength(100).WithMessage("Configuration key must not exceed 100 characters");

        RuleFor(x => x.ConfigValue)
            .NotEmpty().WithMessage("Configuration value is required");

        RuleFor(x => x.UpdateUser)
            .NotEmpty().WithMessage("Update user is required");
    }
}
