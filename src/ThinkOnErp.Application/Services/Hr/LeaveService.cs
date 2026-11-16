using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CancelLeaveRequest;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateBalanceMigrationDraft;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateLeaveCashoutDraft;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateLeaveType;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateOrUpdateLeavePolicy;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateYearEndCloseDraft;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.DeleteLeaveType;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.GrantOrAdjustBalance;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.PostBalanceMigration;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.PostLeaveCashout;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.PostYearEndClose;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.ProcessLeaveRequest;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.ReverseBalanceMigration;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.ReverseLeaveCashout;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.ReverseManualBalanceMovement;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.RunDailyAccrual;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.RunMonthlyAccrual;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.RunYearlyAllocation;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.SubmitLeaveRequest;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.UnpostYearEndClose;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.UpdateLeaveSettings;
using ThinkOnErp.Application.Features.Hr.Leaves.Commands.UpdateLeaveType;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.CalculateLeaveDays;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetBalanceMigration;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetEmployeeBalances;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveCashoutDraft;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveMovements;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeavePolicies;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveRequests;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveSettings;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveTypeByCode;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetLeaveTypes;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetUnpaidLeaveDays;
using ThinkOnErp.Application.Features.Hr.Leaves.Queries.GetYearEndClose;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

[Obsolete("Use the MediatR requests in ThinkOnErp.Application.Features.Hr.Leaves.")]
public interface ILeaveService
{
    Task<LeaveMovementPageDto> GetMovementsAsync(string employeeCode, string? leaveTypeCode, DateTime? cycleStart, int page, int size, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<LeaveTypeDto?> GetLeaveTypeByCodeAsync(string leaveTypeCode, CancellationToken cancellationToken = default);
    Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveTypeDto> UpdateLeaveTypeAsync(string leaveTypeCode, UpdateLeaveTypeDto dto, string user, CancellationToken cancellationToken = default);
    Task<bool> DeleteLeaveTypeAsync(string leaveTypeCode, string user, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<LeaveRequestDto> Items, int TotalCount)> GetLeaveRequestsPagedAsync(
        string? employeeCode = null,
        string? leaveTypeCode = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<LeaveRequestDto> SubmitLeaveRequestAsync(SubmitLeaveRequestDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveRequestDto> SubmitLeaveRequestAsync(SubmitLeaveRequestDto dto, string user, long companyId, CancellationToken cancellationToken = default);
    Task<LeaveDaysCalculationDto> CalculateLeaveDaysAsync(string employeeCode, string leaveTypeCode, DateTime startDate, DateTime endDate, long companyId, CancellationToken cancellationToken = default);
    Task<LeaveRequestDto> ProcessLeaveRequestAsync(long requestId, ProcessLeaveRequestDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveRequestDto> CancelLeaveRequestAsync(string employeeCode, long requestId, CancelLeaveRequestDto dto, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveBalanceDto>> GetEmployeeBalancesAsync(string employeeCode, int? year = null, CancellationToken cancellationToken = default);
    Task<HrLeaveSettingsDto> GetHrLeaveSettingsAsync(CancellationToken cancellationToken = default);
    Task<HrLeaveSettingsDto> UpdateHrLeaveSettingsAsync(UpdateHrLeaveSettingsDto dto, string user, CancellationToken cancellationToken = default);

    Task<decimal> GetUnpaidLeaveDaysInPeriodAsync(string employeeCode, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default);

    Task<LeaveBalanceDto> GrantOrAdjustBalanceAsync(GrantLeaveBalanceDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveBalanceDto> ReverseManualBalanceMovementAsync(string employeeCode, long movementId, ReverseLeaveBalanceMovementDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveAccrualResultDto> RunDailyAccrualAsync(DailyLeaveAccrualDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveAccrualResultDto> RunMonthlyAccrualAsync(MonthlyLeaveAccrualDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveAccrualResultDto> RunYearlyAllocationAsync(YearlyLeaveAllocationDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveRolloverResultDto> RunYearEndRolloverAsync(YearEndRolloverDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveYearCloseDto> CreateLeaveYearCloseDraftAsync(YearEndRolloverDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveYearCloseDto> GetLeaveYearCloseAsync(long id, CancellationToken cancellationToken = default);
    Task<LeaveYearCloseDto> PostLeaveYearCloseAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<LeaveYearCloseDto> UnpostLeaveYearCloseAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<LeaveCashoutSettlementDto> CreateLeaveCashoutSettlementDraftAsync(CreateLeaveCashoutSettlementDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveCashoutSettlementDto> GetLeaveCashoutSettlementAsync(long id, CancellationToken cancellationToken = default);
    Task<LeaveCashoutSettlementDto> PostLeaveCashoutSettlementAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<LeaveCashoutSettlementDto> ReverseLeaveCashoutSettlementAsync(long id, ReverseLeaveCashoutSettlementDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveBalanceMigrationDto> CreateLeaveBalanceMigrationDraftAsync(CreateLeaveBalanceMigrationDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveBalanceMigrationDto> GetLeaveBalanceMigrationAsync(long id, CancellationToken cancellationToken = default);
    Task<LeaveBalanceMigrationDto> PostLeaveBalanceMigrationAsync(long id, string user, CancellationToken cancellationToken = default);
    Task<LeaveBalanceMigrationDto> ReverseLeaveBalanceMigrationAsync(long id, string user, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeavePolicyDetailsDto>> GetLeavePoliciesAsync(CancellationToken cancellationToken = default);
    Task<LeavePolicyDetailsDto> CreateOrUpdateLeavePolicyAsync(CreateLeavePolicyDto dto, string user, CancellationToken cancellationToken = default);
}

[Obsolete("Use the MediatR requests in ThinkOnErp.Application.Features.Hr.Leaves. Kept for backwards compatibility.")]
public sealed class LeaveService : ILeaveService
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IHrUnitOfWork? _unitOfWork;
    private readonly IWorkCalendarService? _calendarService;
    private readonly IPolicyRepository? _policyRepository;

    public LeaveService(
        ILeaveRepository leaveRepo,
        IEmployeeRepository employeeRepo,
        IHrUnitOfWork? unitOfWork = null,
        IWorkCalendarService? calendarService = null,
        IPolicyRepository? policyRepository = null)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
        _unitOfWork = unitOfWork;
        _calendarService = calendarService;
        _policyRepository = policyRepository;
    }

    public async Task<LeaveMovementPageDto> GetMovementsAsync(string employeeCode, string? leaveTypeCode, DateTime? cycleStart, int page, int size, CancellationToken cancellationToken = default)
        => Data(await new GetLeaveMovementsQueryHandler(_leaveRepo, _employeeRepo)
            .Handle(new GetLeaveMovementsQuery(employeeCode, leaveTypeCode, cycleStart, page, size), cancellationToken));

    public async Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
        => Data(await new GetLeaveTypesQueryHandler(_leaveRepo)
            .Handle(new GetLeaveTypesQuery(activeOnly), cancellationToken));

    public async Task<LeaveTypeDto?> GetLeaveTypeByCodeAsync(string leaveTypeCode, CancellationToken cancellationToken = default)
        => DataOrNull(await new GetLeaveTypeByCodeQueryHandler(_leaveRepo)
            .Handle(new GetLeaveTypeByCodeQuery(leaveTypeCode), cancellationToken));

    public async Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new CreateLeaveTypeCommandHandler(_leaveRepo)
            .Handle(new CreateLeaveTypeCommand(dto, user), cancellationToken));

    public async Task<LeaveTypeDto> UpdateLeaveTypeAsync(string leaveTypeCode, UpdateLeaveTypeDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new UpdateLeaveTypeCommandHandler(_leaveRepo)
            .Handle(new UpdateLeaveTypeCommand(leaveTypeCode, dto, user), cancellationToken));

    public async Task<bool> DeleteLeaveTypeAsync(string leaveTypeCode, string user, CancellationToken cancellationToken = default)
    {
        var response = await new DeleteLeaveTypeCommandHandler(_leaveRepo)
            .Handle(new DeleteLeaveTypeCommand(leaveTypeCode, user), cancellationToken);
        return response.Success && response.Data;
    }

    public async Task<(IReadOnlyList<LeaveRequestDto> Items, int TotalCount)> GetLeaveRequestsPagedAsync(
        string? employeeCode = null,
        string? leaveTypeCode = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var page = Data(await new GetLeaveRequestsQueryHandler(_leaveRepo)
            .Handle(new GetLeaveRequestsQuery(employeeCode, leaveTypeCode, status, fromDate, toDate, pageIndex, pageSize), cancellationToken));
        return (page.Items, (int)page.TotalCount);
    }

    public async Task<LeaveRequestDto> SubmitLeaveRequestAsync(SubmitLeaveRequestDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new SubmitLeaveRequestCommandHandler(_leaveRepo, _employeeRepo, _unitOfWork, _calendarService)
            .Handle(new SubmitLeaveRequestCommand(dto, user), cancellationToken));

    public async Task<LeaveRequestDto> SubmitLeaveRequestAsync(SubmitLeaveRequestDto dto, string user, long companyId, CancellationToken cancellationToken = default)
        => Data(await new SubmitLeaveRequestCommandHandler(_leaveRepo, _employeeRepo, _unitOfWork, _calendarService)
            .Handle(new SubmitLeaveRequestCommand(dto, user, companyId), cancellationToken));

    public async Task<LeaveDaysCalculationDto> CalculateLeaveDaysAsync(string employeeCode, string leaveTypeCode, DateTime startDate, DateTime endDate, long companyId, CancellationToken cancellationToken = default)
        => Data(await new CalculateLeaveDaysQueryHandler(_leaveRepo, _employeeRepo, _calendarService)
            .Handle(new CalculateLeaveDaysQuery(employeeCode, leaveTypeCode, startDate, endDate, companyId), cancellationToken));

    public async Task<LeaveRequestDto> ProcessLeaveRequestAsync(long requestId, ProcessLeaveRequestDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new ProcessLeaveRequestCommandHandler(_leaveRepo, _employeeRepo, _unitOfWork)
            .Handle(new ProcessLeaveRequestCommand(requestId, dto, user), cancellationToken));

    public async Task<LeaveRequestDto> CancelLeaveRequestAsync(string employeeCode, long requestId, CancelLeaveRequestDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new CancelLeaveRequestCommandHandler(_leaveRepo, _employeeRepo, _unitOfWork)
            .Handle(new CancelLeaveRequestCommand(employeeCode, requestId, dto, user), cancellationToken));

    public async Task<IReadOnlyList<LeaveBalanceDto>> GetEmployeeBalancesAsync(string employeeCode, int? year = null, CancellationToken cancellationToken = default)
        => Data(await new GetEmployeeBalancesQueryHandler(_leaveRepo, _employeeRepo)
            .Handle(new GetEmployeeBalancesQuery(employeeCode, year), cancellationToken));

    public async Task<HrLeaveSettingsDto> GetHrLeaveSettingsAsync(CancellationToken cancellationToken = default)
        => Data(await new GetLeaveSettingsQueryHandler(_leaveRepo)
            .Handle(new GetLeaveSettingsQuery(), cancellationToken));

    public async Task<HrLeaveSettingsDto> UpdateHrLeaveSettingsAsync(UpdateHrLeaveSettingsDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new UpdateLeaveSettingsCommandHandler(_leaveRepo, _employeeRepo)
            .Handle(new UpdateLeaveSettingsCommand(dto, user), cancellationToken));

    public Task<decimal> GetUnpaidLeaveDaysInPeriodAsync(string employeeCode, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default)
        => new GetUnpaidLeaveDaysQueryHandler(_leaveRepo)
            .Handle(new GetUnpaidLeaveDaysQuery(employeeCode, periodStart, periodEnd), cancellationToken);

    public async Task<LeaveBalanceDto> GrantOrAdjustBalanceAsync(GrantLeaveBalanceDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new GrantOrAdjustBalanceCommandHandler(_leaveRepo, _employeeRepo)
            .Handle(new GrantOrAdjustBalanceCommand(dto, user), cancellationToken));

    public async Task<LeaveBalanceDto> ReverseManualBalanceMovementAsync(string employeeCode, long movementId, ReverseLeaveBalanceMovementDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new ReverseManualBalanceMovementCommandHandler(_leaveRepo)
            .Handle(new ReverseManualBalanceMovementCommand(employeeCode, movementId, dto, user), cancellationToken));

    public async Task<LeaveAccrualResultDto> RunDailyAccrualAsync(DailyLeaveAccrualDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new RunDailyAccrualCommandHandler(_leaveRepo, _employeeRepo)
            .Handle(new RunDailyAccrualCommand(dto, user), cancellationToken));

    public async Task<LeaveAccrualResultDto> RunMonthlyAccrualAsync(MonthlyLeaveAccrualDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new RunMonthlyAccrualCommandHandler(_leaveRepo, _employeeRepo)
            .Handle(new RunMonthlyAccrualCommand(dto, user), cancellationToken));

    public async Task<LeaveAccrualResultDto> RunYearlyAllocationAsync(YearlyLeaveAllocationDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new RunYearlyAllocationCommandHandler(_leaveRepo, _employeeRepo)
            .Handle(new RunYearlyAllocationCommand(dto, user), cancellationToken));

    public async Task<LeaveRolloverResultDto> RunYearEndRolloverAsync(YearEndRolloverDto dto, string user, CancellationToken cancellationToken = default)
    {
        var close = await CreateLeaveYearCloseDraftAsync(dto, user, cancellationToken);
        if (!string.Equals(close.Status, "POSTED", StringComparison.OrdinalIgnoreCase))
            close = await PostLeaveYearCloseAsync(close.Id, user, cancellationToken);

        return new LeaveRolloverResultDto(
            close.Lines.Count,
            close.Lines.Count,
            close.TotalCarriedDays,
            close.TotalExpiredDays,
            close.Lines.Select(l => $"{l.EmployeeCode} [{l.LeaveTypeCode}]: Unused {l.UnusedDays:F4} => carried {l.CarriedDays:F4}, expired {l.ExpiredDays:F4}").ToList());
    }

    public async Task<LeaveYearCloseDto> CreateLeaveYearCloseDraftAsync(YearEndRolloverDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new CreateYearEndCloseDraftCommandHandler(_leaveRepo)
            .Handle(new CreateYearEndCloseDraftCommand(dto, user), cancellationToken));

    public async Task<LeaveYearCloseDto> GetLeaveYearCloseAsync(long id, CancellationToken cancellationToken = default)
        => Data(await new GetYearEndCloseQueryHandler(_leaveRepo)
            .Handle(new GetYearEndCloseQuery(id), cancellationToken));

    public async Task<LeaveYearCloseDto> PostLeaveYearCloseAsync(long id, string user, CancellationToken cancellationToken = default)
        => Data(await new PostYearEndCloseCommandHandler(_leaveRepo)
            .Handle(new PostYearEndCloseCommand(id, user), cancellationToken));

    public async Task<LeaveYearCloseDto> UnpostLeaveYearCloseAsync(long id, string user, CancellationToken cancellationToken = default)
        => Data(await new UnpostYearEndCloseCommandHandler(_leaveRepo)
            .Handle(new UnpostYearEndCloseCommand(id, user), cancellationToken));

    public async Task<LeaveCashoutSettlementDto> CreateLeaveCashoutSettlementDraftAsync(CreateLeaveCashoutSettlementDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new CreateLeaveCashoutDraftCommandHandler(_leaveRepo, _employeeRepo, _policyRepository)
            .Handle(new CreateLeaveCashoutDraftCommand(dto, user), cancellationToken));

    public async Task<LeaveCashoutSettlementDto> GetLeaveCashoutSettlementAsync(long id, CancellationToken cancellationToken = default)
        => Data(await new GetLeaveCashoutDraftQueryHandler(_leaveRepo)
            .Handle(new GetLeaveCashoutDraftQuery(id), cancellationToken));

    public async Task<LeaveCashoutSettlementDto> PostLeaveCashoutSettlementAsync(long id, string user, CancellationToken cancellationToken = default)
        => Data(await new PostLeaveCashoutCommandHandler(_leaveRepo)
            .Handle(new PostLeaveCashoutCommand(id, user), cancellationToken));

    public async Task<LeaveCashoutSettlementDto> ReverseLeaveCashoutSettlementAsync(long id, ReverseLeaveCashoutSettlementDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new ReverseLeaveCashoutCommandHandler(_leaveRepo)
            .Handle(new ReverseLeaveCashoutCommand(id, dto, user), cancellationToken));

    public async Task<LeaveBalanceMigrationDto> CreateLeaveBalanceMigrationDraftAsync(CreateLeaveBalanceMigrationDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new CreateBalanceMigrationDraftCommandHandler(_leaveRepo)
            .Handle(new CreateBalanceMigrationDraftCommand(dto, user), cancellationToken));

    public async Task<LeaveBalanceMigrationDto> GetLeaveBalanceMigrationAsync(long id, CancellationToken cancellationToken = default)
        => Data(await new GetBalanceMigrationQueryHandler(_leaveRepo)
            .Handle(new GetBalanceMigrationQuery(id), cancellationToken));

    public async Task<LeaveBalanceMigrationDto> PostLeaveBalanceMigrationAsync(long id, string user, CancellationToken cancellationToken = default)
        => Data(await new PostBalanceMigrationCommandHandler(_leaveRepo)
            .Handle(new PostBalanceMigrationCommand(id, user), cancellationToken));

    public async Task<LeaveBalanceMigrationDto> ReverseLeaveBalanceMigrationAsync(long id, string user, CancellationToken cancellationToken = default)
        => Data(await new ReverseBalanceMigrationCommandHandler(_leaveRepo)
            .Handle(new ReverseBalanceMigrationCommand(id, user), cancellationToken));

    public async Task<IReadOnlyList<LeavePolicyDetailsDto>> GetLeavePoliciesAsync(CancellationToken cancellationToken = default)
        => Data(await new GetLeavePoliciesQueryHandler(_leaveRepo)
            .Handle(new GetLeavePoliciesQuery(), cancellationToken));

    public async Task<LeavePolicyDetailsDto> CreateOrUpdateLeavePolicyAsync(CreateLeavePolicyDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new CreateOrUpdateLeavePolicyCommandHandler(_leaveRepo)
            .Handle(new CreateOrUpdateLeavePolicyCommand(dto, user), cancellationToken));

    private static T Data<T>(ApiResponse<T> response) => response.Data!;

    private static T? DataOrNull<T>(ApiResponse<T> response) where T : class => response.Success ? response.Data : null;
}
