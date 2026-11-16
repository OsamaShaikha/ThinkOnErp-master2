using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.CreatePeriod;

public sealed record CreatePeriodCommand(
    long CompanyId,
    CreatePayrollPeriodDto Dto,
    string User
) : IRequest<ApiResponse<PayrollPeriod>>;

public sealed class CreatePeriodCommandValidator : AbstractValidator<CreatePeriodCommand>
{
    public CreatePeriodCommandValidator()
    {
        RuleFor(x => x.CompanyId).GreaterThan(0);
        RuleFor(x => x.Dto).NotNull();
        When(x => x.Dto != null, () =>
        {
            RuleFor(x => x.Dto.PeriodCode).NotEmpty();
            RuleFor(x => x.Dto.FiscalYear).GreaterThanOrEqualTo(1);
            RuleFor(x => x.Dto.Month).InclusiveBetween(1, 12);
            RuleFor(x => x.Dto.StartDate.Date).LessThanOrEqualTo(x => x.Dto.EndDate.Date);
            RuleFor(x => x.Dto.PayrollType)
                .Must(pt => pt != null && (pt.Trim().ToUpperInvariant() is "MONTHLY" or "BIWEEKLY" or "SPECIAL"))
                .WithMessage("Fiscal year, month, date range or payroll type is invalid");
        });
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class CreatePeriodCommandHandler : IRequestHandler<CreatePeriodCommand, ApiResponse<PayrollPeriod>>
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;

    public CreatePeriodCommandHandler(IPayrollPeriodRepository payrollPeriodRepo)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
    }

    public async Task<ApiResponse<PayrollPeriod>> Handle(CreatePeriodCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Dto);

        var payrollType = request.Dto.PayrollType.Trim().ToUpperInvariant();
        if (request.Dto.FiscalYear < 1 || request.Dto.Month is < 1 or > 12 || request.Dto.StartDate.Date > request.Dto.EndDate.Date ||
            payrollType is not ("MONTHLY" or "BIWEEKLY" or "SPECIAL"))
        {
            return ApiResponse<PayrollPeriod>.CreateFailure("Fiscal year, month, date range or payroll type is invalid", statusCode: 400);
        }

        var period = new PayrollPeriod
        {
            CompanyId = request.CompanyId,
            PeriodCode = request.Dto.PeriodCode.Trim(),
            NameAr = request.Dto.NameAr.Trim(),
            NameEn = request.Dto.NameEn.Trim(),
            FiscalYear = request.Dto.FiscalYear,
            Month = request.Dto.Month,
            StartDate = request.Dto.StartDate.Date,
            EndDate = request.Dto.EndDate.Date,
            PayDate = request.Dto.PayDate.Date,
            PayrollType = payrollType,
            Status = "OPEN",
            CreationUser = request.User,
            CreationDate = DateTime.UtcNow
        };

        var existing = await _payrollPeriodRepo.GetPeriodByCodeAsync(period.CompanyId, period.PeriodCode, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Payroll period '{period.PeriodCode}' already exists for company {period.CompanyId}.");

        await _payrollPeriodRepo.AddPeriodAsync(period, cancellationToken);
        await _payrollPeriodRepo.SaveChangesAsync(cancellationToken);

        return ApiResponse<PayrollPeriod>.CreateSuccess(period, "Payroll period created successfully.");
    }
}
