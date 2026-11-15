using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.ClosePeriod;

public sealed record ClosePeriodCommand(
    long CompanyId,
    string PeriodCode,
    string Reason,
    string User
) : IRequest<ApiResponse<PayrollPeriod>>;

public sealed class ClosePeriodCommandValidator : AbstractValidator<ClosePeriodCommand>
{
    public ClosePeriodCommandValidator()
    {
        RuleFor(x => x.CompanyId).GreaterThan(0);
        RuleFor(x => x.PeriodCode).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("A reason is required to close a payroll period.");
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class ClosePeriodCommandHandler : IRequestHandler<ClosePeriodCommand, ApiResponse<PayrollPeriod>>
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;
    private readonly IHrUnitOfWork? _unitOfWork;

    public ClosePeriodCommandHandler(
        IPayrollPeriodRepository payrollPeriodRepo,
        IHrUnitOfWork? unitOfWork = null)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<PayrollPeriod>> Handle(ClosePeriodCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<PayrollPeriod>.CreateFailure("A reason is required to close a payroll period.", statusCode: 400);

        Func<Task<PayrollPeriod>> action = async () =>
        {
            var period = await _payrollPeriodRepo.GetPeriodByCodeAsync(request.CompanyId, request.PeriodCode, cancellationToken)
                ?? throw new KeyNotFoundException($"Payroll period '{request.PeriodCode}' not found.");

            if (period.Status == "CLOSED")
                throw new InvalidOperationException($"Payroll period '{request.PeriodCode}' is already closed.");

            var runs = await _payrollPeriodRepo.GetRunsByPeriodAsync(request.CompanyId, request.PeriodCode, cancellationToken);
            var unfinalized = runs.Where(r => r.Status is "DRAFT" or "CALCULATED").ToList();
            if (unfinalized.Count > 0)
            {
                var runIds = string.Join(", ", unfinalized.Select(r => $"{r.Id} ({r.Status})"));
                throw new InvalidOperationException($"Cannot close payroll period '{request.PeriodCode}' with unfinalized payroll runs: {runIds}. Runs must be APPROVED, POSTED, CANCELLED, or SUPERSEDED.");
            }

            if (await _payrollPeriodRepo.HasPendingAdjustmentsForPeriodAsync(request.CompanyId, request.PeriodCode, cancellationToken))
            {
                throw new InvalidOperationException($"Cannot close payroll period '{request.PeriodCode}' while there are pending payroll adjustments.");
            }

            period.Status = "CLOSED";
            period.UpdateUser = request.User;
            period.UpdateDate = DateTime.UtcNow;

            await _payrollPeriodRepo.UpdatePeriodAsync(period, cancellationToken);
            await _payrollPeriodRepo.SaveChangesAsync(cancellationToken);
            return period;
        };

        var result = _unitOfWork == null
            ? await action()
            : await _unitOfWork.ExecuteForPayrollPeriodAsync(request.CompanyId, request.PeriodCode, action, cancellationToken);

        return ApiResponse<PayrollPeriod>.CreateSuccess(result, $"Payroll period '{request.PeriodCode}' closed successfully.");
    }
}
