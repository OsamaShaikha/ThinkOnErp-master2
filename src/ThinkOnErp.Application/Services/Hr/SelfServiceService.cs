using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class SelfServiceService : ISelfServiceService
{
    private readonly IEmployeeService _employeeService;
    private readonly IPayrollService _payrollService;
    private readonly ILeaveService _leaveService;
    private readonly IAttendanceService _attendanceService;
    private readonly IAssetAssignmentService _assetService;
    private readonly IExpenseClaimService _expenseService;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILeaveRepository _leaveRepository;
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly ILogger<SelfServiceService> _logger;

    public SelfServiceService(
        IEmployeeService employeeService,
        IPayrollService payrollService,
        ILeaveService leaveService,
        IAttendanceService attendanceService,
        IAssetAssignmentService assetService,
        IExpenseClaimService expenseService,
        IEmployeeRepository employeeRepository,
        ILeaveRepository leaveRepository,
        IAttendanceRepository attendanceRepository,
        ILogger<SelfServiceService> logger)
    {
        _employeeService = employeeService ?? throw new ArgumentNullException(nameof(employeeService));
        _payrollService = payrollService ?? throw new ArgumentNullException(nameof(payrollService));
        _leaveService = leaveService ?? throw new ArgumentNullException(nameof(leaveService));
        _attendanceService = attendanceService ?? throw new ArgumentNullException(nameof(attendanceService));
        _assetService = assetService ?? throw new ArgumentNullException(nameof(assetService));
        _expenseService = expenseService ?? throw new ArgumentNullException(nameof(expenseService));
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _leaveRepository = leaveRepository ?? throw new ArgumentNullException(nameof(leaveRepository));
        _attendanceRepository = attendanceRepository ?? throw new ArgumentNullException(nameof(attendanceRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<EmployeeDto?> GetMyProfileAsync(string employeeCode)
    {
        return _employeeService.GetByCodeAsync(employeeCode);
    }

    public Task<List<PayslipDto>> GetMyPayslipsAsync(string employeeCode)
    {
        return _payrollService.GetEmployeePayslipsAsync(employeeCode);
    }

    public Task<List<LeaveBalanceDto>> GetMyLeaveBalancesAsync(string employeeCode, int? year = null)
    {
        return _leaveService.GetEmployeeBalancesAsync(employeeCode, year);
    }

    public Task<List<LeaveRequestDto>> GetMyLeaveRequestsAsync(string employeeCode)
    {
        return _leaveService.GetLeaveRequestsAsync(employeeCode: employeeCode);
    }

    public Task<LeaveRequestDto> SubmitMyLeaveRequestAsync(string employeeCode, SubmitLeaveRequestDto dto)
    {
        dto.EmployeeCode = employeeCode;
        return _leaveService.SubmitRequestAsync(dto, employeeCode);
    }

    public Task<List<AttendanceRecordDto>> GetMyAttendanceHistoryAsync(string employeeCode, DateTime fromDate, DateTime toDate)
    {
        return _attendanceService.GetAttendanceRecordsAsync(employeeCode: employeeCode, fromDate: fromDate, toDate: toDate);
    }

    public Task<List<AssetAssignmentDto>> GetMyAssignedAssetsAsync(string employeeCode)
    {
        return _assetService.GetAllAssignmentsAsync(employeeCode: employeeCode, status: "ASSIGNED");
    }

    public Task<ExpenseClaimDto> SubmitMyExpenseClaimAsync(string employeeCode, SubmitExpenseClaimDto dto)
    {
        dto.EmployeeCode = employeeCode;
        return _expenseService.SubmitClaimAsync(dto, employeeCode);
    }

    public async Task<List<EmployeeDto>> GetMyTeamMembersAsync(string managerEmployeeCode)
    {
        var all = await _employeeService.GetAllAsync();
        return all.Where(e => !string.IsNullOrWhiteSpace(e.ManagerEmployeeCode) && e.ManagerEmployeeCode.Equals(managerEmployeeCode, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<List<LeaveRequestDto>> GetMyTeamPendingLeavesAsync(string managerEmployeeCode)
    {
        var team = await GetMyTeamMembersAsync(managerEmployeeCode);
        var teamCodes = team.Select(t => t.EmployeeCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var pending = await _leaveService.GetLeaveRequestsAsync(status: "PENDING");
        return pending.Where(r => teamCodes.Contains(r.EmployeeCode)).ToList();
    }

    public async Task<LeaveRequestDto> ApproveTeamLeaveAsync(string managerEmployeeCode, long leaveRequestId)
    {
        var req = await _leaveRepository.GetRequestByIdAsync(leaveRequestId);
        if (req == null)
        {
            throw new HrNotFoundException($"طلب الإجازة رقم ({leaveRequestId}) غير موجود.", "LEAVE_REQUEST_NOT_FOUND");
        }

        var team = await GetMyTeamMembersAsync(managerEmployeeCode);
        if (!team.Any(t => t.EmployeeCode.Equals(req.EmployeeCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new HrValidationException("لا يمكنك اعتماد إجازة لموظف ليس تحت إدارتك المباشرة.", "NOT_AUTHORIZED_MANAGER");
        }

        return await _leaveService.ApproveRequestAsync(leaveRequestId, managerEmployeeCode);
    }

    public async Task<LeaveRequestDto> RejectTeamLeaveAsync(string managerEmployeeCode, long leaveRequestId, string rejectionReason)
    {
        var req = await _leaveRepository.GetRequestByIdAsync(leaveRequestId);
        if (req == null)
        {
            throw new HrNotFoundException($"طلب الإجازة رقم ({leaveRequestId}) غير موجود.", "LEAVE_REQUEST_NOT_FOUND");
        }

        var team = await GetMyTeamMembersAsync(managerEmployeeCode);
        if (!team.Any(t => t.EmployeeCode.Equals(req.EmployeeCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new HrValidationException("لا يمكنك رفض إجازة لموظف ليس تحت إدارتك المباشرة.", "NOT_AUTHORIZED_MANAGER");
        }

        return await _leaveService.RejectRequestAsync(leaveRequestId, rejectionReason, managerEmployeeCode);
    }

    public async Task<List<OvertimeRecordDto>> GetMyTeamPendingOvertimeAsync(string managerEmployeeCode)
    {
        var team = await GetMyTeamMembersAsync(managerEmployeeCode);
        var teamCodes = team.Select(t => t.EmployeeCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var allOvertime = await _attendanceService.GetOvertimeRecordsAsync(status: "PENDING", fromDate: startOfMonth.AddMonths(-1));

        return allOvertime.Where(o => teamCodes.Contains(o.EmployeeCode)).ToList();
    }

    public async Task<OvertimeRecordDto> ApproveTeamOvertimeAsync(string managerEmployeeCode, long overtimeId)
    {
        return await _attendanceService.ApproveOvertimeAsync(overtimeId, managerEmployeeCode);
    }
}
