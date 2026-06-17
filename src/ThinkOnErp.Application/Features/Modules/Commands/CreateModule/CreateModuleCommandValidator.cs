using FluentValidation;

namespace ThinkOnErp.Application.Features.Modules.Commands.CreateModule;

public class CreateModuleCommandValidator : AbstractValidator<CreateModuleCommand>
{
    public CreateModuleCommandValidator()
    {
        RuleFor(x => x.ModuleCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ModuleName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ModuleNameE).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CreationUser).NotEmpty();
    }
}
