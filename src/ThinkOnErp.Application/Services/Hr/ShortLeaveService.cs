using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IShortLeaveService
{
    Task<ShortLeaveDto> SubmitRequestAsync(CreateShortLeaveDto dto, string user, CancellationToken ct = default);
    Task<ShortLeaveDto> ApproveRequestAsync(long requestId, string user, CancellationToken ct = default);
    Task<ShortLeaveDto> RejectRequestAsync(long requestId, string reason, string user, CancellationToken ct = default);
    Task<ShortLeaveDto> CancelRequestAsync(long requestId, string reason, string user, CancellationToken ct = default);
    Task<ShortLeaveDto?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<ShortLeaveDto>> GetByEmployeeAsync(string employeeCode, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default);
    Task<IReadOnlyList<ShortLeaveDto>> GetRequestsAsync(long companyId, long? branchId = null, string? status = null, DateTime? date = null, CancellationToken ct = default);
    Task<ShortLeavePolicyDto> GetPolicyAsync(long companyId, CancellationToken ct = default);
    Task<ShortLeavePolicyDto> SavePolicyAsync(ShortLeavePolicyDto dto, string user, CancellationToken ct = default);
    (int ExcusedLateMinutes, int ExcusedEarlyMinutes, int ExcusedMiddayMinutes) CalculateExcusedMinutes(IEnumerable<ShortLeaveRequest> approvedRequests);
}

public sealed class ShortLeaveService : IShortLeaveService
{
    private readonly IShortLeaveRepository _repo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IPolicyRepository? _policyRepository;
    private readonly IHrUnitOfWork? _unitOfWork;

    public ShortLeaveService(IShortLeaveRepository repo, IEmployeeRepository employeeRepo, IPolicyRepository? policyRepository = null,
        IHrUnitOfWork? unitOfWork = null)
    {
        _repo = repo;
        _employeeRepo = employeeRepo;
        _policyRepository = policyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ShortLeaveDto> SubmitRequestAsync(CreateShortLeaveDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.EmployeeCode))
            throw new ArgumentException("Employee code is required.", nameof(dto.EmployeeCode));

        if (!ShortLeaveTypes.All.Contains(dto.PermissionType))
            throw new ArgumentException($"Invalid permission type: '{dto.PermissionType}'.", nameof(dto.PermissionType));

        if (dto.EndTime <= dto.StartTime)
            throw new ArgumentException("End time must be later than start time.", nameof(dto.EndTime));

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new ArgumentException("A reason is required for the short leave request.", nameof(dto.Reason));

        if (dto.CompanyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(dto.CompanyId), "A resolved company is required for a short leave request.");

        var employee = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, ct)
            ?? throw new KeyNotFoundException($"Employee '{dto.EmployeeCode}' not found.");

        if (!employee.IsActive || employee.EmploymentStatus != "ACTIVE")
            throw new InvalidOperationException($"Cannot submit short leave request for inactive employee '{dto.EmployeeCode}'.");

        long companyId = dto.CompanyId;
        long? branchId = dto.BranchId ?? employee.BranchId;

        var policy = await _repo.GetActivePolicyAsync(companyId, ct)
            ?? new ShortLeavePolicy { CompanyId = companyId };

        int duration = (int)(dto.EndTime - dto.StartTime).TotalMinutes;
        if (duration <= 0)
            throw new ArgumentException("Permission duration must be greater than zero minutes.");

        if (duration > policy.MaxMinutesPerRequest)
            throw new InvalidOperationException($"Requested duration of {duration} minutes exceeds the maximum allowed limit of {policy.MaxMinutesPerRequest} minutes per request.");

        if (string.Equals(dto.PermissionType, ShortLeaveTypes.MiddayExit, StringComparison.OrdinalIgnoreCase) && !policy.AllowMiddayExit)
            throw new InvalidOperationException("Midday exit permissions are not allowed according to company policy.");

        bool overlaps = await _repo.HasOverlappingRequestAsync(dto.EmployeeCode, dto.PermissionDate.Date, dto.StartTime, dto.EndTime, null, ct);
        if (overlaps)
            throw new InvalidOperationException($"An active short leave request already overlaps with {dto.StartTime:hh\\:mm} - {dto.EndTime:hh\\:mm} on {dto.PermissionDate:yyyy-MM-dd}.");

        // Quota projection
        int usedMinutesThisMonth = await _repo.GetMonthlyApprovedMinutesAsync(dto.EmployeeCode, dto.PermissionDate.Year, dto.PermissionDate.Month, ct);
        string compensationType;

        if (usedMinutesThisMonth + duration <= policy.MonthlyPaidMinutesQuota)
        {
            compensationType = ShortLeaveCompensationTypes.Paid;
        }
        else
        {
            var excessAction = NormalizeExcessAction(policy.ExcessAction, invalidOperation: true);
            if (excessAction == "REJECT")
                throw new InvalidOperationException($"Monthly short leave quota of {policy.MonthlyPaidMinutesQuota} minutes would be exceeded by this request.");

            compensationType = excessAction;
        }

        var request = new ShortLeaveRequest
        {
            CompanyId = companyId,
            BranchId = branchId,
            EmployeeCode = dto.EmployeeCode,
            PermissionType = dto.PermissionType,
            PermissionDate = dto.PermissionDate.Date,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            DurationMinutes = duration,
            Reason = dto.Reason.Trim(),
            Status = ShortLeaveStatus.PendingApproval,
            CompensationType = compensationType,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        await _repo.AddRequestAsync(request, ct);
        await _repo.SaveChangesAsync(ct);

        return MapToDto(request, employee.NameLocal);
    }

    public async Task<ShortLeaveDto> ApproveRequestAsync(long requestId, string user, CancellationToken ct = default)
    {
        var snapshot = await _repo.GetByIdAsync(requestId, ct)
            ?? throw new KeyNotFoundException($"Short leave request {requestId} not found.");
        if (_unitOfWork == null)
            return await ApproveRequestCoreAsync(requestId, user, ct);
        return await _unitOfWork.ExecuteForEmployeeDatesAsync(snapshot.EmployeeCode, snapshot.PermissionDate, snapshot.PermissionDate,
            () => ApproveRequestCoreAsync(requestId, user, ct), ct);
    }

    private async Task<ShortLeaveDto> ApproveRequestCoreAsync(long requestId, string user, CancellationToken ct)
    {
        var request = await _repo.GetByIdAsync(requestId, ct)
            ?? throw new KeyNotFoundException($"Short leave request {requestId} not found.");

        if (request.Status != ShortLeaveStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot approve request in status '{request.Status}'. Only PENDING_APPROVAL requests can be approved.");

        var employeeUserName = await _employeeRepo.GetLinkedUserNameByEmployeeCodeAsync(request.EmployeeCode, ct);
        HrApprovalGuard.EnsureNotSelfApproved(user, request.CreationUser, employeeUserName, "short leave request");
        if (await _repo.HasConflictingLeaveRequestAsync(request.EmployeeCode, request.PermissionDate, ct))
            throw new InvalidOperationException("The employee already has a pending or approved leave request on this date.");

        var policy = await _repo.GetActivePolicyAsync(request.CompanyId, ct)
            ?? new ShortLeavePolicy { CompanyId = request.CompanyId };

        // Re-evaluate quota at approval time
        int usedMinutes = await _repo.GetMonthlyApprovedMinutesAsync(request.EmployeeCode, request.PermissionDate.Year, request.PermissionDate.Month, ct);
        if (usedMinutes + request.DurationMinutes <= policy.MonthlyPaidMinutesQuota)
        {
            request.CompensationType = ShortLeaveCompensationTypes.Paid;
            request.DeductedSalaryAmount = 0m;
            request.ConvertedLeaveDays = 0m;
        }
        else
        {
            var excessAction = NormalizeExcessAction(policy.ExcessAction, invalidOperation: true);
            if (excessAction == "REJECT")
                throw new InvalidOperationException("The company short leave quota is exceeded and its current policy does not allow approval.");
            if (policy.StandardWorkdayHours <= 0)
                throw new InvalidOperationException("A positive standard workday length is required to calculate excess short leave compensation.");

            request.CompensationType = excessAction;

            // The request repository may include Employee without its salary structures.
            // Reload through the employee repository so a salary deduction is never silently zeroed.
            if (string.Equals(request.CompensationType, ShortLeaveCompensationTypes.DeductSalary, StringComparison.OrdinalIgnoreCase))
            {
                var employee = await _employeeRepo.GetEmployeeByCodeAsync(request.EmployeeCode, ct) ?? request.Employee;
                var activeStructure = await _employeeRepo.GetActiveSalaryStructureAsync(request.EmployeeCode, request.PermissionDate, ct);
                if (activeStructure == null)
                    throw new InvalidOperationException("A salary structure effective on the short leave date is required to calculate the salary deduction.");
                decimal dailyRate = activeStructure.BasicSalary / 30m;
                decimal hourlyRate = dailyRate / policy.StandardWorkdayHours;
                int? configuredDecimals = null;
                var roundingRule = 1;
                if (_policyRepository != null)
                {
                    var configuredPrecisions = await _policyRepository.GetConfiguredPayrollCurrencyDecimalsAsync(request.CompanyId, ct);
                    if (configuredPrecisions.TryGetValue(SalaryPaymentMetadata.Normalize(activeStructure.CurrencyCode), out var configured))
                        configuredDecimals = configured;
                    var branchId = request.BranchId ?? employee?.BranchId;
                    if (branchId.HasValue)
                    {
                        roundingRule = await _policyRepository.GetBranchRoundingRulesAsync(branchId.Value, ct) ?? 1;
                        if (roundingRule is < 1 or > 6)
                            throw new InvalidOperationException($"Branch {branchId.Value} has an invalid payroll rounding rule.");
                    }
                }
                var decimals = PayrollCurrencyRules.GetCurrencyDecimals(activeStructure.CurrencyCode, configuredDecimals);
                request.DeductedSalaryAmount = PayrollCurrencyRules.Round((request.DurationMinutes / 60m) * hourlyRate, decimals, roundingRule);
            }
            else if (string.Equals(request.CompensationType, ShortLeaveCompensationTypes.DeductLeaveBalance, StringComparison.OrdinalIgnoreCase))
            {
                decimal workdayMinutes = policy.StandardWorkdayHours * 60m;
                request.ConvertedLeaveDays = Math.Round((decimal)request.DurationMinutes / workdayMinutes, 4);
            }
        }

        request.Status = ShortLeaveStatus.Approved;
        request.ApprovedBy = user;
        request.ApprovedAtUtc = DateTime.UtcNow;
        request.UpdateUser = user;
        request.UpdateDate = DateTime.UtcNow;

        await _repo.UpdateRequestAsync(request, ct);
        await _repo.SaveChangesAsync(ct);

        return MapToDto(request, request.Employee?.NameLocal);
    }

    public async Task<ShortLeaveDto> RejectRequestAsync(long requestId, string reason, string user, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A rejection reason is required.", nameof(reason));

        var request = await _repo.GetByIdAsync(requestId, ct)
            ?? throw new KeyNotFoundException($"Short leave request {requestId} not found.");

        if (request.Status != ShortLeaveStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot reject request in status '{request.Status}'. Only PENDING_APPROVAL requests can be rejected.");

        request.Status = ShortLeaveStatus.Rejected;
        request.RejectionReason = reason.Trim();
        request.RejectedBy = user;
        request.RejectedAtUtc = DateTime.UtcNow;
        request.UpdateUser = user;
        request.UpdateDate = DateTime.UtcNow;

        await _repo.UpdateRequestAsync(request, ct);
        await _repo.SaveChangesAsync(ct);

        return MapToDto(request, request.Employee?.NameLocal);
    }

    public async Task<ShortLeaveDto> CancelRequestAsync(long requestId, string reason, string user, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A cancellation reason is required.", nameof(reason));

        var request = await _repo.GetByIdAsync(requestId, ct)
            ?? throw new KeyNotFoundException($"Short leave request {requestId} not found.");

        if (request.Status == ShortLeaveStatus.Cancelled)
            throw new InvalidOperationException("Short leave request is already cancelled.");

        if (request.Status == ShortLeaveStatus.Rejected)
            throw new InvalidOperationException("Cannot cancel an already rejected short leave request.");

        request.Status = ShortLeaveStatus.Cancelled;
        request.CancellationReason = reason.Trim();
        request.CancelledBy = user;
        request.CancelledAtUtc = DateTime.UtcNow;
        request.UpdateUser = user;
        request.UpdateDate = DateTime.UtcNow;

        await _repo.UpdateRequestAsync(request, ct);
        await _repo.SaveChangesAsync(ct);

        return MapToDto(request, request.Employee?.NameLocal);
    }

    public async Task<ShortLeaveDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var request = await _repo.GetByIdAsync(id, ct);
        return request == null ? null : MapToDto(request, request.Employee?.NameLocal);
    }

    public async Task<IReadOnlyList<ShortLeaveDto>> GetByEmployeeAsync(string employeeCode, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default)
    {
        var items = await _repo.GetByEmployeeAsync(employeeCode, fromDate, toDate, ct);
        return items.Select(r => MapToDto(r, r.Employee?.NameLocal)).ToList();
    }

    public async Task<IReadOnlyList<ShortLeaveDto>> GetRequestsAsync(long companyId, long? branchId = null, string? status = null, DateTime? date = null, CancellationToken ct = default)
    {
        var items = await _repo.GetRequestsAsync(companyId, branchId, status, date, ct);
        return items.Select(r => MapToDto(r, r.Employee?.NameLocal)).ToList();
    }

    public async Task<ShortLeavePolicyDto> GetPolicyAsync(long companyId, CancellationToken ct = default)
    {
        var policy = await _repo.GetActivePolicyAsync(companyId, ct)
            ?? new ShortLeavePolicy { CompanyId = companyId };

        return MapPolicyToDto(policy);
    }

    public async Task<ShortLeavePolicyDto> SavePolicyAsync(ShortLeavePolicyDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.MonthlyPaidMinutesQuota < 0)
            throw new ArgumentException("Monthly paid quota minutes cannot be negative.");
        if (dto.MaxMinutesPerRequest <= 0)
            throw new ArgumentException("Max minutes per request must be greater than zero.");
        if (dto.StandardWorkdayHours <= 0)
            throw new ArgumentException("Standard workday hours must be greater than zero.");
        var excessAction = NormalizeExcessAction(dto.ExcessAction, invalidOperation: false);

        var policy = await _repo.GetActivePolicyAsync(dto.CompanyId, ct)
            ?? new ShortLeavePolicy { CompanyId = dto.CompanyId, CreationUser = user, CreationDate = DateTime.UtcNow };

        policy.MonthlyPaidMinutesQuota = dto.MonthlyPaidMinutesQuota;
        policy.MaxMinutesPerRequest = dto.MaxMinutesPerRequest;
        policy.AllowMiddayExit = dto.AllowMiddayExit;
        policy.ExcessAction = excessAction;
        policy.LeaveTypeCodeForDeduction = dto.LeaveTypeCodeForDeduction;
        policy.StandardWorkdayHours = dto.StandardWorkdayHours;
        policy.IsActive = dto.IsActive;
        policy.UpdateUser = user;
        policy.UpdateDate = DateTime.UtcNow;

        await _repo.SavePolicyAsync(policy, ct);
        return MapPolicyToDto(policy);
    }

    private static string NormalizeExcessAction(string? action, bool invalidOperation)
    {
        var normalized = action?.Trim();
        if (string.Equals(normalized, ShortLeaveCompensationTypes.DeductSalary, StringComparison.OrdinalIgnoreCase))
            return ShortLeaveCompensationTypes.DeductSalary;
        if (string.Equals(normalized, ShortLeaveCompensationTypes.DeductLeaveBalance, StringComparison.OrdinalIgnoreCase))
            return ShortLeaveCompensationTypes.DeductLeaveBalance;
        if (string.Equals(normalized, "REJECT", StringComparison.OrdinalIgnoreCase))
            return "REJECT";
        if (invalidOperation)
            throw new InvalidOperationException("The company short leave excess action is not supported.");
        throw new ArgumentException("Excess action must be DEDUCT_SALARY, DEDUCT_LEAVE_BALANCE, or REJECT.", nameof(action));
    }

    public (int ExcusedLateMinutes, int ExcusedEarlyMinutes, int ExcusedMiddayMinutes) CalculateExcusedMinutes(IEnumerable<ShortLeaveRequest> approvedRequests)
    {
        int late = 0;
        int early = 0;
        int midday = 0;

        foreach (var req in approvedRequests)
        {
            if (req.Status != ShortLeaveStatus.Approved)
                continue;

            if (string.Equals(req.PermissionType, ShortLeaveTypes.LateArrival, StringComparison.OrdinalIgnoreCase))
                late += req.DurationMinutes;
            else if (string.Equals(req.PermissionType, ShortLeaveTypes.EarlyDeparture, StringComparison.OrdinalIgnoreCase))
                early += req.DurationMinutes;
            else if (string.Equals(req.PermissionType, ShortLeaveTypes.MiddayExit, StringComparison.OrdinalIgnoreCase))
                midday += req.DurationMinutes;
        }

        return (late, early, midday);
    }

    private static ShortLeaveDto MapToDto(ShortLeaveRequest r, string? employeeName) =>
        new(
            r.Id,
            r.CompanyId,
            r.BranchId,
            r.EmployeeCode,
            employeeName,
            r.PermissionType,
            r.PermissionDate,
            r.StartTime,
            r.EndTime,
            r.DurationMinutes,
            r.Reason,
            r.Status,
            r.CompensationType,
            r.DeductedSalaryAmount,
            r.ConvertedLeaveDays,
            r.ApprovedBy,
            r.ApprovedAtUtc,
            r.RejectedBy,
            r.RejectedAtUtc,
            r.RejectionReason,
            r.CancelledBy,
            r.CancelledAtUtc,
            r.CancellationReason,
            r.CreationDate
        );

    private static ShortLeavePolicyDto MapPolicyToDto(ShortLeavePolicy p) =>
        new(
            p.Id,
            p.CompanyId,
            p.MonthlyPaidMinutesQuota,
            p.MaxMinutesPerRequest,
            p.AllowMiddayExit,
            p.ExcessAction,
            p.LeaveTypeCodeForDeduction,
            p.StandardWorkdayHours,
            p.IsActive
        );
}

