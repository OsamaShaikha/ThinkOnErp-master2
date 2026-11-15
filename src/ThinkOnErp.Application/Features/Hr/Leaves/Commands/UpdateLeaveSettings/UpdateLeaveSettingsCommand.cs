using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.UpdateLeaveSettings;

public sealed record UpdateLeaveSettingsCommand(UpdateHrLeaveSettingsDto Dto, string User) : IRequest<ApiResponse<HrLeaveSettingsDto>>;

public sealed class UpdateLeaveSettingsCommandValidator : AbstractValidator<UpdateLeaveSettingsCommand>
{
    public UpdateLeaveSettingsCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.LeaveYearStartMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.Dto.LeaveYearStartDay).InclusiveBetween(1, 31);
    }
}

public sealed class UpdateLeaveSettingsCommandHandler : IRequestHandler<UpdateLeaveSettingsCommand, ApiResponse<HrLeaveSettingsDto>>
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;

    public UpdateLeaveSettingsCommandHandler(
        ILeaveRepository leaveRepo,
        IEmployeeRepository employeeRepo)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
    }

    public async Task<ApiResponse<HrLeaveSettingsDto>> Handle(UpdateLeaveSettingsCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (dto.LeaveYearStartMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(dto.LeaveYearStartMonth), "Leave year start month must be between 1 and 12.");
        if (dto.LeaveYearStartDay < 1 || dto.LeaveYearStartDay > DateTime.DaysInMonth(2000, dto.LeaveYearStartMonth))
            throw new ArgumentOutOfRangeException(nameof(dto.LeaveYearStartDay), "Leave year start day is invalid for the selected month.");
        var displayFormat = dto.BalanceDisplayFormat?.Trim().ToUpperInvariant();
        if (displayFormat is not ("DECIMAL" or "DHM"))
            throw new ArgumentException("Balance display format must be DECIMAL or DHM.", nameof(dto.BalanceDisplayFormat));
        if (displayFormat == "DHM" && (dto.StandardWorkdayHours is null or < 0.25m or > 24m))
            throw new ArgumentException("Standard workday hours must be between 0.25 and 24 for DHM display.", nameof(dto.StandardWorkdayHours));

        var current = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var cashoutBasis = dto.LeaveCashoutWageBasisCode == null
            ? current.LeaveCashoutWageBasisCode
            : string.IsNullOrWhiteSpace(dto.LeaveCashoutWageBasisCode) ? null : dto.LeaveCashoutWageBasisCode.Trim().ToUpperInvariant();
        var basisComponentCode = dto.LeaveCashoutBasisComponentCode == null
            ? current.LeaveCashoutBasisComponentCode
            : string.IsNullOrWhiteSpace(dto.LeaveCashoutBasisComponentCode) ? null : dto.LeaveCashoutBasisComponentCode.Trim().ToUpperInvariant();
        var cashoutDivisor = dto.LeaveCashoutDayDivisor ?? current.LeaveCashoutDayDivisor;
        var payComponentCode = dto.LeaveCashoutPayComponentCode == null
            ? current.LeaveCashoutPayComponentCode
            : string.IsNullOrWhiteSpace(dto.LeaveCashoutPayComponentCode) ? null : dto.LeaveCashoutPayComponentCode.Trim().ToUpperInvariant();
        if (cashoutBasis == null)
        {
            basisComponentCode = null;
            cashoutDivisor = null;
            payComponentCode = null;
        }
        else
        {
            if (cashoutBasis is not ("BASIC" or "GROSS_EARNINGS" or "SALARY_COMPONENT"))
                throw new ArgumentException("Leave cashout wage basis must be BASIC, GROSS_EARNINGS, or SALARY_COMPONENT.", nameof(dto.LeaveCashoutWageBasisCode));
            if (cashoutDivisor is null or <= 0m or > 999999.9999m)
                throw new ArgumentException("A positive company-configured leave cashout day divisor is required.", nameof(dto.LeaveCashoutDayDivisor));
            if (cashoutBasis == "SALARY_COMPONENT" && string.IsNullOrWhiteSpace(basisComponentCode))
                throw new ArgumentException("A salary component code is required for the SALARY_COMPONENT wage basis.", nameof(dto.LeaveCashoutBasisComponentCode));
            if (cashoutBasis != "SALARY_COMPONENT") basisComponentCode = null;
            if (string.IsNullOrWhiteSpace(payComponentCode))
                throw new ArgumentException("A payroll earning component code is required for leave cashout.", nameof(dto.LeaveCashoutPayComponentCode));

            var payComponent = await _employeeRepo.GetSalaryComponentByCodeAsync(payComponentCode, cancellationToken);
            var payComponentType = payComponent?.ComponentType?.Trim().ToUpperInvariant();
            if (payComponent == null || !payComponent.IsActive || payComponentType is "DEDUCTION" or "EMPLOYER_CONTRIB")
                throw new ArgumentException("The configured leave cashout payroll component must exist, be active, and represent an earning.", nameof(dto.LeaveCashoutPayComponentCode));
            if (cashoutBasis == "SALARY_COMPONENT")
            {
                var basisComponent = await _employeeRepo.GetSalaryComponentByCodeAsync(basisComponentCode!, cancellationToken);
                var basisComponentType = basisComponent?.ComponentType?.Trim().ToUpperInvariant();
                if (basisComponent == null || !basisComponent.IsActive || basisComponentType is "DEDUCTION" or "EMPLOYER_CONTRIB")
                    throw new ArgumentException("The configured wage basis component must exist, be active, and represent an earning.", nameof(dto.LeaveCashoutBasisComponentCode));
            }
        }
        var eligibleCitizenshipCountryCode = dto.EligibleCitizenshipCountryCode == null
            ? current.EligibleCitizenshipCountryCode
            : CountryCodeRules.NormalizeOptional(dto.EligibleCitizenshipCountryCode);
        if (string.IsNullOrEmpty(eligibleCitizenshipCountryCode)) eligibleCitizenshipCountryCode = null;
        var accrualEnabled = dto.AutomaticAccrualEnabled ?? current.AutomaticAccrualEnabled;
        var accrualTimeZoneId = dto.AccrualTimeZoneId == null
            ? current.AccrualTimeZoneId
            : string.IsNullOrWhiteSpace(dto.AccrualTimeZoneId) ? null : dto.AccrualTimeZoneId.Trim();
        if (accrualEnabled && string.IsNullOrWhiteSpace(accrualTimeZoneId))
            throw new ArgumentException("An IANA or Windows time-zone ID is required when automatic leave accrual is enabled.", nameof(dto.AccrualTimeZoneId));
        if (accrualTimeZoneId != null)
        {
            try { _ = TimeZoneInfo.FindSystemTimeZoneById(accrualTimeZoneId); }
            catch (TimeZoneNotFoundException ex) { throw new ArgumentException("The leave accrual time-zone ID is not recognized.", nameof(dto.AccrualTimeZoneId), ex); }
            catch (InvalidTimeZoneException ex) { throw new ArgumentException("The leave accrual time-zone definition is invalid.", nameof(dto.AccrualTimeZoneId), ex); }
        }
        var leaveYearChanged = current.LeaveYearStartMonth != dto.LeaveYearStartMonth || current.LeaveYearStartDay != dto.LeaveYearStartDay;
        if (leaveYearChanged && await _leaveRepo.HasLeaveBalancesAsync(cancellationToken))
            throw new InvalidOperationException("The leave-year start cannot be changed after balances exist. Migrate current opening balances first so accrued and used days are not assigned to the wrong cycle.");

        var settings = new HrLeaveSettings
        {
            Id = 1,
            LeaveYearStartMonth = dto.LeaveYearStartMonth,
            LeaveYearStartDay = dto.LeaveYearStartDay,
            BalanceDisplayFormat = displayFormat,
            StandardWorkdayHours = dto.StandardWorkdayHours,
            AutomaticAccrualEnabled = accrualEnabled,
            AccrualTimeZoneId = accrualTimeZoneId,
            EligibleCitizenshipCountryCode = eligibleCitizenshipCountryCode,
            LeaveCashoutWageBasisCode = cashoutBasis,
            LeaveCashoutBasisComponentCode = basisComponentCode,
            LeaveCashoutDayDivisor = cashoutDivisor,
            LeaveCashoutPayComponentCode = payComponentCode,
            UpdateUser = request.User,
            UpdateDate = DateTime.UtcNow
        };
        await _leaveRepo.SaveHrLeaveSettingsAsync(settings, current.LeaveYearStartMonth, current.LeaveYearStartDay, cancellationToken);
        var resultDto = new HrLeaveSettingsDto(
            settings.LeaveYearStartMonth,
            settings.LeaveYearStartDay,
            settings.BalanceDisplayFormat,
            settings.StandardWorkdayHours,
            settings.AutomaticAccrualEnabled,
            settings.AccrualTimeZoneId,
            settings.EligibleCitizenshipCountryCode,
            settings.LeaveCashoutWageBasisCode,
            settings.LeaveCashoutBasisComponentCode,
            settings.LeaveCashoutDayDivisor,
            settings.LeaveCashoutPayComponentCode);

        return ApiResponse<HrLeaveSettingsDto>.CreateSuccess(resultDto, "HR leave settings updated successfully.");
    }
}
