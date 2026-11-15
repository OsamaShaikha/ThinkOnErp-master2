using System;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Commands.GenerateShiftDays;

public sealed record GenerateShiftDaysCommand(string EmployeeCode, GenerateShiftDaysDto Dto, string Username)
    : IRequest<ApiResponse<ShiftGenerationResult>>;

public sealed class GenerateShiftDaysCommandValidator : AbstractValidator<GenerateShiftDaysCommand>
{
    public GenerateShiftDaysCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().WithMessage("Employee code is required.");
        RuleFor(x => x.Dto).NotNull().WithMessage("Generation payload is required.");
    }
}

public sealed class GenerateShiftDaysCommandHandler : IRequestHandler<GenerateShiftDaysCommand, ApiResponse<ShiftGenerationResult>>
{
    private readonly IShiftGenerationRepository _repository;

    public GenerateShiftDaysCommandHandler(IShiftGenerationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<ShiftGenerationResult>> Handle(GenerateShiftDaysCommand request, CancellationToken ct)
    {
        var dto = request.Dto;
        if (dto.From == null || dto.To == null || dto.To < dto.From || dto.To.Value.DayNumber - dto.From.Value.DayNumber >= 366)
            throw new ArgumentException("Generation needs an ordered date range of at most 366 days.");

        var result = await _repository.ExecuteAsync(
            request.EmployeeCode,
            dto.From.Value.ToDateTime(TimeOnly.MinValue),
            dto.To.Value.ToDateTime(TimeOnly.MinValue),
            (days, assignments, definitions) => ShiftGenerationService.Plan(request.EmployeeCode, dto.From.Value, dto.To.Value, days, assignments, definitions),
            request.Username,
            ct);

        return ApiResponse<ShiftGenerationResult>.CreateSuccess(result, "Shift days generated successfully.");
    }
}
