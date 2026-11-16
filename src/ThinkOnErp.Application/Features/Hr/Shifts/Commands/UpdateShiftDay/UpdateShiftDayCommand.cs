using System;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Commands.UpdateShiftDay;

public sealed record UpdateShiftDayCommand(
    string EmployeeCode,
    long Id,
    WriteShiftDayDto Dto,
    string IfMatch,
    string Username) : IRequest<ApiResponse<ShiftDayDto>>;

public sealed class UpdateShiftDayCommandValidator : AbstractValidator<UpdateShiftDayCommand>
{
    public UpdateShiftDayCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().WithMessage("Employee code is required.");
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("Shift day ID must be positive.");
        RuleFor(x => x.Dto).NotNull().WithMessage("Shift day payload is required.");
        RuleFor(x => x.IfMatch).NotEmpty().WithMessage("If-Match header is required.");
    }
}

public sealed class UpdateShiftDayCommandHandler : IRequestHandler<UpdateShiftDayCommand, ApiResponse<ShiftDayDto>>
{
    private readonly IShiftDayRepository _repository;

    public UpdateShiftDayCommandHandler(IShiftDayRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<ShiftDayDto>> Handle(UpdateShiftDayCommand request, CancellationToken ct)
    {
        var day = ShiftDayHelper.Build(request.EmployeeCode, request.Dto);
        var expectedVersion = ShiftAssignmentRules.ParseVersion(request.IfMatch);
        var shiftVersion = ShiftDayHelper.DefinitionVersion(request.Dto);

        try
        {
            await _repository.UpdateAsync(request.EmployeeCode, request.Id, day, expectedVersion, shiftVersion, request.Username, ct);
        }
        catch (InvalidOperationException ex) when (ex is ShiftVersionConflictException or ShiftDayVersionConflictException)
        {
            return ApiResponse<ShiftDayDto>.CreateFailure(ex.Message, statusCode: 412);
        }

        var result = await _repository.GetAsync(request.EmployeeCode, request.Id, ct);
        if (result == null)
            return ApiResponse<ShiftDayDto>.CreateFailure("Daily roster entry not found.", statusCode: 404);

        var definitions = await _repository.DefinitionsAsync(result.ShiftCode == null ? Array.Empty<string>() : new[] { result.ShiftCode }, ct);
        var dto = ShiftDayHelper.Map(result, result.ShiftCode == null ? null : definitions.GetValueOrDefault(result.ShiftCode));
        return ApiResponse<ShiftDayDto>.CreateSuccess(dto, "Shift day updated successfully.");
    }
}
