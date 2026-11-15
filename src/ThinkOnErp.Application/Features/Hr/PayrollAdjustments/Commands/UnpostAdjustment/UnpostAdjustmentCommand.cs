using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.PayrollAdjustments.Commands.UnpostAdjustment;

public sealed record UnpostAdjustmentCommand(
    long Id,
    string Username,
    long? BranchId = null,
    string? Reason = null
) : IRequest<ApiResponse<PayrollAdjustment>>;

public sealed class UnpostAdjustmentCommandValidator : AbstractValidator<UnpostAdjustmentCommand>
{
    public UnpostAdjustmentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Username).NotEmpty();
    }
}

public sealed class UnpostAdjustmentCommandHandler : IRequestHandler<UnpostAdjustmentCommand, ApiResponse<PayrollAdjustment>>
{
    private readonly IPayrollAdjustmentRepository _adjustmentRepository;
    private readonly IAttendanceCorrectionRepository? _attendanceRepository;
    private readonly IHrUnitOfWork? _unitOfWork;

    public UnpostAdjustmentCommandHandler(
        IPayrollAdjustmentRepository adjustmentRepository,
        IAttendanceCorrectionRepository? attendanceRepository = null,
        IHrUnitOfWork? unitOfWork = null)
    {
        _adjustmentRepository = adjustmentRepository;
        _attendanceRepository = attendanceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<PayrollAdjustment>> Handle(UnpostAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var result = await PayrollAdjustmentCommon.ExecuteLockedAsync(
            request.Id,
            _adjustmentRepository,
            _unitOfWork,
            _attendanceRepository,
            () => PayrollAdjustmentCommon.UnpostAdjustmentCoreAsync(
                request.Id,
                request.Username,
                request.BranchId,
                request.Reason,
                _adjustmentRepository,
                cancellationToken),
            cancellationToken);

        return ApiResponse<PayrollAdjustment>.CreateSuccess(result, "Adjustment returned to pending.");
    }
}
