using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Shifts.Commands.DeleteShiftAssignment;

public sealed record DeleteShiftAssignmentCommand(string EmployeeCode, long Id, string IfMatch)
    : IRequest<ApiResponse<bool>>;

public sealed class DeleteShiftAssignmentCommandValidator : AbstractValidator<DeleteShiftAssignmentCommand>
{
    public DeleteShiftAssignmentCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().WithMessage("Employee code is required.");
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("Shift assignment ID must be positive.");
        RuleFor(x => x.IfMatch).NotEmpty().WithMessage("If-Match header is required.");
    }
}

public sealed class DeleteShiftAssignmentCommandHandler : IRequestHandler<DeleteShiftAssignmentCommand, ApiResponse<bool>>
{
    private readonly IShiftAssignmentRepository _repository;

    public DeleteShiftAssignmentCommandHandler(IShiftAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<bool>> Handle(DeleteShiftAssignmentCommand request, CancellationToken ct)
    {
        try
        {
            await _repository.DeleteAsync(request.EmployeeCode, request.Id, ShiftAssignmentRules.ParseVersion(request.IfMatch), ct);
            return ApiResponse<bool>.CreateSuccess(true);
        }
        catch (ShiftAssignmentVersionConflictException ex)
        {
            return ApiResponse<bool>.CreateFailure(ex.Message, statusCode: 412);
        }
    }
}
