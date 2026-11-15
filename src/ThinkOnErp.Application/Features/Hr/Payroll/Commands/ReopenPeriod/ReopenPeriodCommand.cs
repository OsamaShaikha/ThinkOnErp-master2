using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.ReopenPeriod;

public sealed record ReopenPeriodCommand(
    long CompanyId,
    string PeriodCode,
    string Reason,
    string User
) : IRequest<ApiResponse<PayrollPeriod>>;

public sealed class ReopenPeriodCommandValidator : AbstractValidator<ReopenPeriodCommand>
{
    public ReopenPeriodCommandValidator()
    {
        RuleFor(x => x.CompanyId).GreaterThan(0);
        RuleFor(x => x.PeriodCode).NotEmpty();
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r) && r.Trim().Length >= 5)
            .WithMessage("A meaningful reason (at least 5 characters) is required to reopen a payroll period.");
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class ReopenPeriodCommandHandler : IRequestHandler<ReopenPeriodCommand, ApiResponse<PayrollPeriod>>
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;
    private readonly IHrUnitOfWork? _unitOfWork;

    public ReopenPeriodCommandHandler(
        IPayrollPeriodRepository payrollPeriodRepo,
        IHrUnitOfWork? unitOfWork = null)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<PayrollPeriod>> Handle(ReopenPeriodCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<PayrollPeriod>.CreateFailure("A reason is required to reopen a payroll period.", statusCode: 400);

        if (request.Reason.Trim().Length < 5)
            throw new ArgumentException("A meaningful reason (at least 5 characters) is required to reopen a payroll period.", nameof(request.Reason));

        Func<Task<PayrollPeriod>> action = async () =>
        {
            var period = await _payrollPeriodRepo.GetPeriodByCodeAsync(request.CompanyId, request.PeriodCode, cancellationToken)
                ?? throw new KeyNotFoundException($"Payroll period '{request.PeriodCode}' not found.");

            if (period.Status != "CLOSED")
                throw new InvalidOperationException($"Payroll period '{request.PeriodCode}' is not closed (current status: {period.Status}).");

            if (await _payrollPeriodRepo.HasSubsequentClosedPeriodsAsync(request.CompanyId, period.FiscalYear, period.Month, cancellationToken))
            {
                throw new InvalidOperationException($"Cannot reopen payroll period '{request.PeriodCode}' because subsequent periods are already closed. Periods must be reopened in reverse chronological order.");
            }

            period.Status = "OPEN";
            period.UpdateUser = request.User;
            period.UpdateDate = DateTime.UtcNow;

            await _payrollPeriodRepo.UpdatePeriodAsync(period, cancellationToken);
            await _payrollPeriodRepo.SaveChangesAsync(cancellationToken);
            return period;
        };

        var result = _unitOfWork == null
            ? await action()
            : await _unitOfWork.ExecuteForPayrollPeriodAsync(request.CompanyId, request.PeriodCode, action, cancellationToken);

        return ApiResponse<PayrollPeriod>.CreateSuccess(result, $"Payroll period '{request.PeriodCode}' reopened successfully.");
    }
}
