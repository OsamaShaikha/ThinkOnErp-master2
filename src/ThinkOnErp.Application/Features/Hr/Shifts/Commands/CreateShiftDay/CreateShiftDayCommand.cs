using System;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Shifts.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Commands.CreateShiftDay;

public sealed record CreateShiftDayCommand(string EmployeeCode, WriteShiftDayDto Dto, string Username)
    : IRequest<ApiResponse<ShiftDayDto>>;

public sealed class CreateShiftDayCommandValidator : AbstractValidator<CreateShiftDayCommand>
{
    public CreateShiftDayCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().WithMessage("Employee code is required.");
        RuleFor(x => x.Dto).NotNull().WithMessage("Shift day payload is required.");
    }
}

public sealed class CreateShiftDayCommandHandler : IRequestHandler<CreateShiftDayCommand, ApiResponse<ShiftDayDto>>
{
    private readonly IShiftDayRepository _repository;

    public CreateShiftDayCommandHandler(IShiftDayRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<ShiftDayDto>> Handle(CreateShiftDayCommand request, CancellationToken ct)
    {
        var day = ShiftDayHelper.Build(request.EmployeeCode, request.Dto);
        day.CreationUser = request.Username;
        day.CreationDate = DateTime.UtcNow;

        try
        {
            var created = await _repository.CreateAsync(day, ShiftDayHelper.DefinitionVersion(request.Dto), ct);
            var result = await _repository.GetAsync(request.EmployeeCode, created.Id, ct);
            if (result == null)
                return ApiResponse<ShiftDayDto>.CreateFailure("Daily roster entry not found after creation.", statusCode: 404);

            var definitions = await _repository.DefinitionsAsync(result.ShiftCode == null ? Array.Empty<string>() : new[] { result.ShiftCode }, ct);
            var dto = ShiftDayHelper.Map(result, result.ShiftCode == null ? null : definitions.GetValueOrDefault(result.ShiftCode));
            return ApiResponse<ShiftDayDto>.CreateSuccess(dto, "Shift day created successfully.");
        }
        catch (ShiftVersionConflictException ex)
        {
            return ApiResponse<ShiftDayDto>.CreateFailure(ex.Message, statusCode: 412);
        }
    }
}
