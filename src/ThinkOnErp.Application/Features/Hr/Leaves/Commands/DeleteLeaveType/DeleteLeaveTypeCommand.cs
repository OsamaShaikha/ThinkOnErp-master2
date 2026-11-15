using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.DeleteLeaveType;

public sealed record DeleteLeaveTypeCommand(string LeaveTypeCode, string User) : IRequest<ApiResponse<bool>>;

public sealed class DeleteLeaveTypeCommandValidator : AbstractValidator<DeleteLeaveTypeCommand>
{
    public DeleteLeaveTypeCommandValidator()
    {
        RuleFor(x => x.LeaveTypeCode).NotEmpty().MaximumLength(50);
    }
}

public sealed class DeleteLeaveTypeCommandHandler : IRequestHandler<DeleteLeaveTypeCommand, ApiResponse<bool>>
{
    private readonly ILeaveRepository _leaveRepo;

    public DeleteLeaveTypeCommandHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<bool>> Handle(DeleteLeaveTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _leaveRepo.GetLeaveTypeByCodeAsync(request.LeaveTypeCode, cancellationToken);
        if (entity == null)
        {
            return ApiResponse<bool>.CreateFailure($"Leave type with code '{request.LeaveTypeCode}' not found.", statusCode: 404);
        }

        entity.IsActive = false;
        entity.UpdateUser = request.User;
        entity.UpdateDate = DateTime.UtcNow;

        await _leaveRepo.UpdateLeaveTypeAsync(entity, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.CreateSuccess(true, "Leave type deactivated successfully.");
    }
}
