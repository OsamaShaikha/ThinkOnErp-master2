using System;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Commands.CreateShiftAssignment;

public sealed record CreateShiftAssignmentCommand(string EmployeeCode, WriteShiftAssignmentDto Dto, string Username)
    : IRequest<ApiResponse<ShiftAssignmentDto>>;

public sealed class CreateShiftAssignmentCommandValidator : AbstractValidator<CreateShiftAssignmentCommand>
{
    public CreateShiftAssignmentCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().WithMessage("Employee code is required.");
        RuleFor(x => x.Dto).NotNull().WithMessage("Shift assignment payload is required.");
    }
}

public sealed class CreateShiftAssignmentCommandHandler : IRequestHandler<CreateShiftAssignmentCommand, ApiResponse<ShiftAssignmentDto>>
{
    private readonly IShiftAssignmentRepository _repository;

    public CreateShiftAssignmentCommandHandler(IShiftAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<ShiftAssignmentDto>> Handle(CreateShiftAssignmentCommand request, CancellationToken ct)
    {
        var proposed = ShiftAssignmentHelper.Build(request.EmployeeCode, request.Dto);
        proposed.CreationUser = request.Username;
        proposed.CreationDate = DateTime.UtcNow;

        try
        {
            var created = await _repository.CreateAsync(proposed, ShiftAssignmentRules.ParseVersion(request.Dto.ShiftVersion), ct);
            var assignment = await _repository.GetAsync(request.EmployeeCode, created.Id, ct);
            if (assignment == null)
                return ApiResponse<ShiftAssignmentDto>.CreateFailure("Shift assignment not found after creation.", statusCode: 404);

            var definitions = await _repository.DefinitionsAsync(new[] { assignment.ShiftCode }, ct);
            var dto = ShiftAssignmentHelper.Map(assignment, definitions.GetValueOrDefault(assignment.ShiftCode));
            return ApiResponse<ShiftAssignmentDto>.CreateSuccess(dto, "Shift assignment created successfully.");
        }
        catch (ShiftVersionConflictException ex)
        {
            return ApiResponse<ShiftAssignmentDto>.CreateFailure(ex.Message, statusCode: 412);
        }
    }
}
