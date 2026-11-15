using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.DeleteAdjustment;

public sealed record DeleteAdjustmentCommand(
    long Id
) : IRequest<ApiResponse<bool>>;

public sealed class DeleteAdjustmentCommandValidator : AbstractValidator<DeleteAdjustmentCommand>
{
    public DeleteAdjustmentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class DeleteAdjustmentCommandHandler : IRequestHandler<DeleteAdjustmentCommand, ApiResponse<bool>>
{
    private readonly IPayrollAdjustmentRepository _adjustmentRepository;
    private readonly IAttendanceCorrectionRepository? _attendanceRepository;
    private readonly IHrUnitOfWork? _unitOfWork;

    public DeleteAdjustmentCommandHandler(
        IPayrollAdjustmentRepository adjustmentRepository,
        IAttendanceCorrectionRepository? attendanceRepository = null,
        IHrUnitOfWork? unitOfWork = null)
    {
        _adjustmentRepository = adjustmentRepository;
        _attendanceRepository = attendanceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> Handle(DeleteAdjustmentCommand request, CancellationToken cancellationToken)
    {
        await PayrollAdjustmentCommon.ExecuteLockedAsync(
            request.Id,
            _adjustmentRepository,
            _unitOfWork,
            _attendanceRepository,
            async () =>
            {
                await PayrollAdjustmentCommon.DeleteAdjustmentCoreAsync(request.Id, _adjustmentRepository, cancellationToken);
                return true;
            },
            cancellationToken);

        return ApiResponse<bool>.CreateSuccess(true, "Adjustment deleted successfully.");
    }
}
