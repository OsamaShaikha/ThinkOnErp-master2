using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.DeleteCurrencyPrecision;

public sealed record DeleteCurrencyPrecisionCommand(
    long CompanyId,
    string CurrencyCode
) : IRequest<ApiResponse<bool>>;

public sealed class DeleteCurrencyPrecisionCommandValidator : AbstractValidator<DeleteCurrencyPrecisionCommand>
{
    public DeleteCurrencyPrecisionCommandValidator()
    {
        RuleFor(x => x.CompanyId).GreaterThan(0);
        RuleFor(x => x.CurrencyCode).NotEmpty().MaximumLength(10);
    }
}

public sealed class DeleteCurrencyPrecisionCommandHandler : IRequestHandler<DeleteCurrencyPrecisionCommand, ApiResponse<bool>>
{
    private readonly IPolicyRepository _policyRepo;
    private readonly IHrUnitOfWork? _unitOfWork;

    public DeleteCurrencyPrecisionCommandHandler(
        IPolicyRepository policyRepo,
        IHrUnitOfWork? unitOfWork = null)
    {
        _policyRepo = policyRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> Handle(DeleteCurrencyPrecisionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CompanyId <= 0) throw new ArgumentException("CompanyId must be positive.");
        var code = request.CurrencyCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 10) throw new ArgumentException("CurrencyCode is required and cannot exceed 10 characters.");

        var deleted = false;
        Func<Task> write = async () =>
        {
            deleted = await _policyRepo.DeletePayrollCurrencyPrecisionAsync(request.CompanyId, code, cancellationToken);
            if (deleted) await _policyRepo.SaveChangesAsync(cancellationToken);
        };

        if (_unitOfWork == null)
        {
            await write();
        }
        else
        {
            await _unitOfWork.ExecuteForCompanyPayrollDatesAsync(request.CompanyId, DateTime.MinValue.Date, DateTime.MaxValue.Date, async () =>
            {
                await write();
                return true;
            }, cancellationToken);
        }

        return ApiResponse<bool>.CreateSuccess(deleted, deleted ? "Payroll currency precision removed." : "No payroll currency precision override exists.");
    }
}
