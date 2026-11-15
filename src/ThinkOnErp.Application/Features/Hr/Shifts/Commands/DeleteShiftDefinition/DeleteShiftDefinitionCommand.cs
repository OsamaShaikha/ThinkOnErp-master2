using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Features.Hr.Shifts.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Commands.DeleteShiftDefinition;

public sealed record DeleteShiftDefinitionCommand(string Code, string IfMatch) : IRequest<ApiResponse<bool>>;

public sealed class DeleteShiftDefinitionCommandValidator : AbstractValidator<DeleteShiftDefinitionCommand>
{
    public DeleteShiftDefinitionCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Shift code is required.");
        RuleFor(x => x.IfMatch).NotEmpty().WithMessage("If-Match header is required.");
    }
}

public sealed class DeleteShiftDefinitionCommandHandler : IRequestHandler<DeleteShiftDefinitionCommand, ApiResponse<bool>>
{
    private readonly IShiftDefinitionRepository _repository;

    public DeleteShiftDefinitionCommandHandler(IShiftDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<bool>> Handle(DeleteShiftDefinitionCommand request, CancellationToken ct)
    {
        var code = ShiftDefinitionHelper.Code(request.Code);
        var version = ShiftDefinitionHelper.Version(request.IfMatch);

        try
        {
            await _repository.DeleteAsync(code, version, ct);
            return ApiResponse<bool>.CreateSuccess(true);
        }
        catch (ShiftVersionConflictException ex)
        {
            return ApiResponse<bool>.CreateFailure(ex.Message, statusCode: 412);
        }
    }
}
