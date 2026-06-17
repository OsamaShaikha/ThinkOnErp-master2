using FluentValidation;

namespace ThinkOnErp.Application.Features.Modules.Commands.UpdateModule;

public class UpdateModuleCommandValidator : AbstractValidator<UpdateModuleCommand>
{
    public UpdateModuleCommandValidator()
    {
        RuleFor(x => x.ModuleId).GreaterThan(0);
        RuleFor(x => x.ModuleCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ModuleName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ModuleNameE).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UpdateUser).NotEmpty();
    }
}
