using System;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Commands.CreateShiftDefinition;

public sealed record CreateShiftDefinitionCommand(CreateShiftDefinitionDto Dto, string Username)
    : IRequest<ApiResponse<ShiftDefinitionDto>>;

public sealed class CreateShiftDefinitionCommandValidator : AbstractValidator<CreateShiftDefinitionCommand>
{
    public CreateShiftDefinitionCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull().WithMessage("Shift definition payload is required.");
        RuleFor(x => x.Dto.ShiftCode).NotEmpty().WithMessage("Shift code is required.");
        RuleFor(x => x.Dto.Definition).NotNull().WithMessage("Shift definition details are required.");
    }
}

public sealed class CreateShiftDefinitionCommandHandler : IRequestHandler<CreateShiftDefinitionCommand, ApiResponse<ShiftDefinitionDto>>
{
    private readonly IShiftDefinitionRepository _repository;

    public CreateShiftDefinitionCommandHandler(IShiftDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<ShiftDefinitionDto>> Handle(CreateShiftDefinitionCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request.Dto);
        var code = ShiftDefinitionHelper.Code(request.Dto.ShiftCode);
        var shift = ShiftDefinitionHelper.Build(code, request.Dto.Definition);
        shift.CreationUser = request.Username;
        shift.CreationDate = DateTime.UtcNow;

        var created = await _repository.CreateAsync(shift, ct);
        var dto = ShiftDefinitionHelper.Map(created, false);
        return ApiResponse<ShiftDefinitionDto>.CreateSuccess(dto, "Shift definition created successfully.");
    }
}
