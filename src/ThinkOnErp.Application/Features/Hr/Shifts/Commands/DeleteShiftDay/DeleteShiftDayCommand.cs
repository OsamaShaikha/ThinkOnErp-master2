using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Commands.DeleteShiftDay;

public sealed record DeleteShiftDayCommand(string EmployeeCode, long Id, string IfMatch)
    : IRequest<ApiResponse<bool>>;

public sealed class DeleteShiftDayCommandValidator : AbstractValidator<DeleteShiftDayCommand>
{
    public DeleteShiftDayCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().WithMessage("Employee code is required.");
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("Shift day ID must be positive.");
        RuleFor(x => x.IfMatch).NotEmpty().WithMessage("If-Match header is required.");
    }
}

public sealed class DeleteShiftDayCommandHandler : IRequestHandler<DeleteShiftDayCommand, ApiResponse<bool>>
{
    private readonly IShiftDayRepository _repository;

    public DeleteShiftDayCommandHandler(IShiftDayRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<bool>> Handle(DeleteShiftDayCommand request, CancellationToken ct)
    {
        try
        {
            await _repository.DeleteAsync(request.EmployeeCode, request.Id, ShiftAssignmentRules.ParseVersion(request.IfMatch), ct);
            return ApiResponse<bool>.CreateSuccess(true);
        }
        catch (ShiftDayVersionConflictException ex)
        {
            return ApiResponse<bool>.CreateFailure(ex.Message, statusCode: 412);
        }
    }
}
