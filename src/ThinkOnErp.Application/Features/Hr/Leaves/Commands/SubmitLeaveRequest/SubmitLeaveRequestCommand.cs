using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.SubmitLeaveRequest;

public sealed record SubmitLeaveRequestCommand(
    SubmitLeaveRequestDto Dto,
    string User,
    long? CompanyId = null) : IRequest<ApiResponse<LeaveRequestDto>>;

public sealed class SubmitLeaveRequestCommandValidator : AbstractValidator<SubmitLeaveRequestCommand>
{
    public SubmitLeaveRequestCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.EmployeeCode).NotEmpty();
        RuleFor(x => x.Dto.LeaveTypeCode).NotEmpty();
        RuleFor(x => x.Dto.EndDate).GreaterThanOrEqualTo(x => x.Dto.StartDate);
    }
}

public sealed class SubmitLeaveRequestCommandHandler : IRequestHandler<SubmitLeaveRequestCommand, ApiResponse<LeaveRequestDto>>
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IHrUnitOfWork? _unitOfWork;
    private readonly IWorkCalendarService? _calendarService;

    public SubmitLeaveRequestCommandHandler(
        ILeaveRepository leaveRepo,
        IEmployeeRepository employeeRepo,
        IHrUnitOfWork? unitOfWork = null,
        IWorkCalendarService? calendarService = null)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
        _unitOfWork = unitOfWork;
        _calendarService = calendarService;
    }

    public async Task<ApiResponse<LeaveRequestDto>> Handle(SubmitLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (request.CompanyId.HasValue && request.CompanyId.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.CompanyId), "A valid tenant company is required to calculate leave days.");

        LeaveRequestDto result;
        if (_unitOfWork == null)
        {
            result = await _leaveRepo.ExecuteForLeaveEmployeeAsync(dto.EmployeeCode,
                () => SubmitCoreAsync(dto, request.User, request.CompanyId, cancellationToken), cancellationToken);
        }
        else
        {
            result = await _unitOfWork.ExecuteForEmployeeDatesAsync(dto.EmployeeCode, dto.StartDate, dto.EndDate,
                () => SubmitCoreAsync(dto, request.User, request.CompanyId, cancellationToken), cancellationToken);
        }

        return ApiResponse<LeaveRequestDto>.CreateSuccess(result, "Leave request submitted successfully.");
    }

    private async Task<LeaveRequestDto> SubmitCoreAsync(SubmitLeaveRequestDto dto, string user, long? companyId, CancellationToken cancellationToken)
    {
        if (dto.EndDate < dto.StartDate)
        {
            throw new InvalidOperationException("Leave end date cannot be earlier than start date.");
        }

        var emp = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Employee '{dto.EmployeeCode}' not found.");

        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(dto.LeaveTypeCode, cancellationToken);
        if (leaveType == null || !leaveType.IsActive)
        {
            throw new InvalidOperationException($"Leave type '{dto.LeaveTypeCode}' is invalid or inactive.");
        }

        var requestSettings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var (requestPolicy, requestCycle) = await LeaveCommon.ResolveLeavePolicyCycleAsync(_leaveRepo, emp, dto.LeaveTypeCode, dto.StartDate.Date, requestSettings, cancellationToken);
        if (leaveType.TracksBalance && requestPolicy == null && await LeaveCommon.HasLeavePolicyHistoryAsync(_leaveRepo, dto.LeaveTypeCode, cancellationToken))
            throw new InvalidOperationException("No leave policy is effective for this employee's cycle.");
        if (leaveType.TracksBalance && !LeaveCommon.IsApplicableToPolicy(emp, requestPolicy, requestSettings.EligibleCitizenshipCountryCode))
            throw new InvalidOperationException("The employee is not eligible under this leave policy.");
        if (dto.EndDate.Date > requestCycle.End)
            throw new InvalidOperationException("Split this leave request at the leave-year boundary before submission.");
        await _leaveRepo.EnsureLeaveCycleOpenAsync(dto.EmployeeCode, dto.LeaveTypeCode, requestCycle.Start, requestCycle.End, cancellationToken);

        var daysRequested = await LeaveCommon.CalculateRequestedWorkingDaysAsync(dto.StartDate.Date, dto.EndDate.Date, companyId, _calendarService, cancellationToken);
        if (daysRequested <= 0m)
            throw new InvalidOperationException("The requested date range contains no working days.");
        if (await _leaveRepo.HasConflictingLeaveAsync(dto.EmployeeCode, dto.StartDate.Date, dto.EndDate.Date,
                cancellationToken: cancellationToken))
            throw new InvalidOperationException("The employee already has a pending or approved leave or an approved short leave that overlaps these dates.");

        // Check the balance in the employee's configured leave cycle.
        if (leaveType.TracksBalance)
        {
            var balance = await _leaveRepo.GetLeaveBalanceByCycleAsync(dto.EmployeeCode, dto.LeaveTypeCode, requestCycle.Start, cancellationToken);
            var companyToday = LeaveCommon.GetLeaveCompanyToday(requestSettings);
            var asOfDate = companyToday < requestCycle.End ? companyToday : requestCycle.End;
            var accrued = balance?.AccruedDays ?? LeaveCommon.CalculateAccruedToDate(emp, leaveType, requestPolicy, asOfDate, requestSettings);
            var pending = await _leaveRepo.GetPendingLeaveDaysAsync(dto.EmployeeCode, dto.LeaveTypeCode, requestCycle.Start, requestCycle.End, cancellationToken: cancellationToken);
            decimal available = accrued + (balance?.CarriedForwardDays ?? 0m) - (balance?.UsedDays ?? 0m) - pending;

            if (daysRequested > available)
            {
                throw new InvalidOperationException($"Insufficient leave balance. Requested: {daysRequested} days, Available: {available} days.");
            }
        }

        var request = new LeaveRequest
        {
            EmployeeCode = dto.EmployeeCode,
            LeaveTypeCode = dto.LeaveTypeCode,
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate.Date,
            DaysRequested = daysRequested,
            Status = "PENDING",
            Reason = dto.Reason,
            AttachmentFileRef = dto.AttachmentFileRef,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        await _leaveRepo.AddLeaveRequestAsync(request, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return LeaveCommon.MapLeaveRequest(request, emp.NameLocal, leaveType.NameLocal);
    }
}
