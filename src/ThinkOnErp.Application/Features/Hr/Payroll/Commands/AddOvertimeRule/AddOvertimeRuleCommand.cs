using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.AddOvertimeRule;

public sealed record AddOvertimeRuleCommand(
    OvertimeRuleDto Dto,
    string User
) : IRequest<ApiResponse<OvertimeRule>>;

public sealed class AddOvertimeRuleCommandValidator : AbstractValidator<AddOvertimeRuleCommand>
{
    public AddOvertimeRuleCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        When(x => x.Dto != null, () =>
        {
            RuleFor(x => x.Dto.CompanyId).GreaterThan(0);
            RuleFor(x => x.Dto.Code).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Dto.NameAr).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Dto.NameEn).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Dto.DayType)
                .Must(dt => dt != null && (dt.Trim().ToUpperInvariant() is "REGULAR" or "WEEKEND" or "HOLIDAY"))
                .WithMessage("DayType must be REGULAR, WEEKEND, or HOLIDAY.");
            RuleFor(x => x.Dto.Multiplier).InclusiveBetween(0.01m, 999.99m);
            RuleFor(x => x.Dto.MinimumMinutes).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Dto.MaximumMinutes).GreaterThan(0);
        });
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class AddOvertimeRuleCommandHandler : IRequestHandler<AddOvertimeRuleCommand, ApiResponse<OvertimeRule>>
{
    private readonly IPolicyRepository _policyRepo;
    private readonly IHrUnitOfWork? _unitOfWork;

    public AddOvertimeRuleCommandHandler(
        IPolicyRepository policyRepo,
        IHrUnitOfWork? unitOfWork = null)
    {
        _policyRepo = policyRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<OvertimeRule>> Handle(AddOvertimeRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Dto);

        var dto = request.Dto;
        if (dto.CompanyId <= 0) throw new ArgumentException("CompanyId must be positive.");
        if (string.IsNullOrWhiteSpace(dto.Code)) throw new ArgumentException("Overtime rule code is required.");
        if (dto.Code.Trim().Length > 50) throw new ArgumentException("Overtime rule code cannot exceed 50 characters.");
        if (string.IsNullOrWhiteSpace(dto.NameAr) || dto.NameAr.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(dto.NameEn) || dto.NameEn.Trim().Length > 200)
            throw new ArgumentException("Overtime rule names are required and cannot exceed 200 characters.");
        if (dto.EffectiveTo.HasValue && dto.EffectiveTo.Value.Date < dto.EffectiveFrom.Date)
            throw new ArgumentException("EffectiveTo cannot be earlier than EffectiveFrom.");

        var dayType = dto.DayType?.Trim().ToUpperInvariant();
        if (dayType is not ("REGULAR" or "WEEKEND" or "HOLIDAY"))
            throw new ArgumentException("DayType must be REGULAR, WEEKEND, or HOLIDAY.");
        if (dto.Multiplier <= 0m || dto.Multiplier > 999.99m)
            throw new ArgumentException("Overtime multiplier must be positive and fit the configured 5,2 precision.");
        if (dto.MinimumMinutes < 0 || dto.MaximumMinutes <= 0 || dto.MaximumMinutes < dto.MinimumMinutes)
            throw new ArgumentException("Overtime minute range is invalid.");
        if (!OvertimeCalculationService.TryParseMonthlyHoursDivisor(dto.HourlyDivisorFormula, out var hourlyDivisor))
            throw new ArgumentException("HourlyDivisorFormula must use MONTHLY_SALARY / a positive numeric divisor.");
        var normalizedFormula = $"MONTHLY_SALARY / {hourlyDivisor.ToString(CultureInfo.InvariantCulture)}";
        if (normalizedFormula.Length > 50)
            throw new ArgumentException("HourlyDivisorFormula cannot exceed 50 characters.");

        var rule = new OvertimeRule
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code.Trim(),
            NameAr = dto.NameAr?.Trim() ?? string.Empty,
            NameEn = dto.NameEn?.Trim() ?? string.Empty,
            EffectiveFrom = dto.EffectiveFrom.Date,
            EffectiveTo = dto.EffectiveTo?.Date,
            DayType = dayType,
            Multiplier = dto.Multiplier,
            MinimumMinutes = dto.MinimumMinutes,
            MaximumMinutes = dto.MaximumMinutes,
            HourlyDivisorFormula = normalizedFormula,
            RequiresApproval = dto.RequiresApproval,
            IsActive = true,
            CreationUser = request.User,
            CreationDate = DateTime.UtcNow
        };

        Func<Task> write = async () =>
        {
            await _policyRepo.AddOvertimeRuleAsync(rule, cancellationToken);
            await _policyRepo.SaveChangesAsync(cancellationToken);
        };

        if (_unitOfWork == null)
        {
            await write();
        }
        else
        {
            await _unitOfWork.ExecuteForCompanyPayrollDatesAsync(dto.CompanyId, dto.EffectiveFrom.Date, dto.EffectiveTo?.Date ?? DateTime.MaxValue.Date, async () =>
            {
                await write();
                return true;
            }, cancellationToken);
        }

        return ApiResponse<OvertimeRule>.CreateSuccess(rule, "Overtime rule added.");
    }
}
