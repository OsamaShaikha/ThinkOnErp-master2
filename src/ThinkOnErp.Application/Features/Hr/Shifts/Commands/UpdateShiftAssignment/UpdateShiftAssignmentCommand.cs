using System;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Commands.UpdateShiftAssignment;

public sealed record UpdateShiftAssignmentCommand(
    string EmployeeCode,
    long Id,
    WriteShiftAssignmentDto Dto,
    string IfMatch,
    string Username) : IRequest<ApiResponse<ShiftAssignmentDto>>;

public sealed class UpdateShiftAssignmentCommandValidator : AbstractValidator<UpdateShiftAssignmentCommand>
{
    public UpdateShiftAssignmentCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().WithMessage("Employee code is required.");
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("Shift assignment ID must be positive.");
        RuleFor(x => x.Dto).NotNull().WithMessage("Shift assignment payload is required.");
        RuleFor(x => x.IfMatch).NotEmpty().WithMessage("If-Match header is required.");
    }
}

public sealed class UpdateShiftAssignmentCommandHandler : IRequestHandler<UpdateShiftAssignmentCommand, ApiResponse<ShiftAssignmentDto>>
{
    private readonly IShiftAssignmentRepository _repository;

    public UpdateShiftAssignmentCommandHandler(IShiftAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<ShiftAssignmentDto>> Handle(UpdateShiftAssignmentCommand request, CancellationToken ct)
    {
        var proposed = ShiftAssignmentHelper.Build(request.EmployeeCode, request.Dto);
        var expectedVersion = ShiftAssignmentRules.ParseVersion(request.IfMatch);
        var shiftVersion = ShiftAssignmentRules.ParseVersion(request.Dto.ShiftVersion);

        try
        {
            await _repository.UpdateAsync(request.EmployeeCode, request.Id, proposed, expectedVersion, shiftVersion, request.Username, ct);
        }
        catch (InvalidOperationException ex) when (ex is ShiftVersionConflictException or ShiftAssignmentVersionConflictException)
        {
            return ApiResponse<ShiftAssignmentDto>.CreateFailure(ex.Message, statusCode: 412);
        }

        var assignment = await _repository.GetAsync(request.EmployeeCode, request.Id, ct);
        if (assignment == null)
            return ApiResponse<ShiftAssignmentDto>.CreateFailure("Shift assignment not found.", statusCode: 404);

        var definitions = await _repository.DefinitionsAsync(new[] { assignment.ShiftCode }, ct);
        var dto = ShiftAssignmentHelper.Map(assignment, definitions.GetValueOrDefault(assignment.ShiftCode));
        return ApiResponse<ShiftAssignmentDto>.CreateSuccess(dto, "Shift assignment updated successfully.");
    }
}
