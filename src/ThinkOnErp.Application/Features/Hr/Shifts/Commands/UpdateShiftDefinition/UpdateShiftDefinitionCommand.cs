using System;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Commands.UpdateShiftDefinition;

public sealed record UpdateShiftDefinitionCommand(string Code, WriteShiftDefinitionDto Dto, string IfMatch, string Username)
    : IRequest<ApiResponse<ShiftDefinitionDto>>;

public sealed class UpdateShiftDefinitionCommandValidator : AbstractValidator<UpdateShiftDefinitionCommand>
{
    public UpdateShiftDefinitionCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Shift code is required.");
        RuleFor(x => x.Dto).NotNull().WithMessage("Shift definition payload is required.");
        RuleFor(x => x.IfMatch).NotEmpty().WithMessage("If-Match header is required.");
    }
}

public sealed class UpdateShiftDefinitionCommandHandler : IRequestHandler<UpdateShiftDefinitionCommand, ApiResponse<ShiftDefinitionDto>>
{
    private readonly IShiftDefinitionRepository _repository;

    public UpdateShiftDefinitionCommandHandler(IShiftDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<ShiftDefinitionDto>> Handle(UpdateShiftDefinitionCommand request, CancellationToken ct)
    {
        var code = ShiftDefinitionHelper.Code(request.Code);
        var version = ShiftDefinitionHelper.Version(request.IfMatch);
        var shift = ShiftDefinitionHelper.Build(code, request.Dto);

        try
        {
            await _repository.UpdateAsync(code, shift, version, request.Username, ct);
        }
        catch (ShiftVersionConflictException ex)
        {
            return ApiResponse<ShiftDefinitionDto>.CreateFailure(ex.Message, statusCode: 412);
        }

        var updated = await _repository.GetAsync(code, ct);
        if (updated == null)
            return ApiResponse<ShiftDefinitionDto>.CreateFailure("Shift not found.", statusCode: 404);

        var referenced = await _repository.ReferencedCodesAsync(new[] { code }, ct);
        var dto = ShiftDefinitionHelper.Map(updated, referenced.Contains(code));
        return ApiResponse<ShiftDefinitionDto>.CreateSuccess(dto, "Shift definition updated successfully.");
    }
}
