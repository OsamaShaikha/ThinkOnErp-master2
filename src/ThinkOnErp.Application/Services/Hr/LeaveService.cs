using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface ILeaveService
{
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
    Task<LeaveRequestDto> ProcessLeaveRequestAsync(long requestId, ProcessLeaveRequestDto dto, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveBalanceDto>> GetEmployeeBalancesAsync(string employeeCode, int? year = null, CancellationToken cancellationToken = default);

    Task<decimal> GetUnpaidLeaveDaysInPeriodAsync(string employeeCode, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default);

    Task<LeaveBalanceDto> GrantOrAdjustBalanceAsync(GrantLeaveBalanceDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveAccrualResultDto> RunMonthlyAccrualAsync(MonthlyLeaveAccrualDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveAccrualResultDto> RunYearlyAllocationAsync(YearlyLeaveAllocationDto dto, string user, CancellationToken cancellationToken = default);
    Task<LeaveRolloverResultDto> RunYearEndRolloverAsync(YearEndRolloverDto dto, string user, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeavePolicyDetailsDto>> GetLeavePoliciesAsync(CancellationToken cancellationToken = default);
    Task<LeavePolicyDetailsDto> CreateOrUpdateLeavePolicyAsync(CreateLeavePolicyDto dto, string user, CancellationToken cancellationToken = default);
}

public sealed class LeaveService : ILeaveService
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;

    public LeaveService(ILeaveRepository leaveRepo, IEmployeeRepository employeeRepo)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
    }

    public async Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var types = await _leaveRepo.GetLeaveTypesAsync(activeOnly, cancellationToken);
        return types.Select(t => new LeaveTypeDto(
            t.LeaveTypeCode,
            t.NameLocal,
            t.NameEn,
            t.IsPaid,
            t.IsStatutory,
            t.MaxDaysPerYear,
            t.CarryForwardAllowed,
            t.CarryForwardCapDays,
            t.RequiresDocumentation,
            t.IsActive
        )).ToList();
    }

    public async Task<LeaveTypeDto?> GetLeaveTypeByCodeAsync(string leaveTypeCode, CancellationToken cancellationToken = default)
    {
        var t = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken);
        if (t == null) return null;

        return new LeaveTypeDto(
            t.LeaveTypeCode,
            t.NameLocal,
            t.NameEn,
            t.IsPaid,
            t.IsStatutory,
            t.MaxDaysPerYear,
            t.CarryForwardAllowed,
            t.CarryForwardCapDays,
            t.RequiresDocumentation,
            t.IsActive
        );
    }

    public async Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto dto, string user, CancellationToken cancellationToken = default)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (string.IsNullOrWhiteSpace(dto.LeaveTypeCode))
            throw new ArgumentException("Leave type code is required.", nameof(dto.LeaveTypeCode));

        var existing = await _leaveRepo.GetLeaveTypeByCodeAsync(dto.LeaveTypeCode, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Leave type with code '{dto.LeaveTypeCode}' already exists.");

        var entity = new LeaveType
        {
            LeaveTypeCode = dto.LeaveTypeCode.Trim().ToUpperInvariant(),
            NameLocal = dto.NameLocal,
            NameEn = dto.NameEn,
            IsPaid = dto.IsPaid,
            IsStatutory = dto.IsStatutory,
            MaxDaysPerYear = dto.MaxDaysPerYear,
            CarryForwardAllowed = dto.CarryForwardAllowed,
            CarryForwardCapDays = dto.CarryForwardCapDays,
            RequiresDocumentation = dto.RequiresDocumentation,
            IsActive = dto.IsActive,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        await _leaveRepo.AddLeaveTypeAsync(entity, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return new LeaveTypeDto(
            entity.LeaveTypeCode,
            entity.NameLocal,
            entity.NameEn,
            entity.IsPaid,
            entity.IsStatutory,
            entity.MaxDaysPerYear,
            entity.CarryForwardAllowed,
            entity.CarryForwardCapDays,
            entity.RequiresDocumentation,
            entity.IsActive
        );
    }

    public async Task<LeaveTypeDto> UpdateLeaveTypeAsync(string leaveTypeCode, UpdateLeaveTypeDto dto, string user, CancellationToken cancellationToken = default)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        var entity = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken);
        if (entity == null)
            throw new KeyNotFoundException($"Leave type with code '{leaveTypeCode}' not found.");

        entity.NameLocal = dto.NameLocal;
        entity.NameEn = dto.NameEn;
        entity.IsPaid = dto.IsPaid;
        entity.IsStatutory = dto.IsStatutory;
        entity.MaxDaysPerYear = dto.MaxDaysPerYear;
        entity.CarryForwardAllowed = dto.CarryForwardAllowed;
        entity.CarryForwardCapDays = dto.CarryForwardCapDays;
        entity.RequiresDocumentation = dto.RequiresDocumentation;
        entity.IsActive = dto.IsActive;
        entity.UpdateUser = user;
        entity.UpdateDate = DateTime.UtcNow;

        await _leaveRepo.UpdateLeaveTypeAsync(entity, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return new LeaveTypeDto(
            entity.LeaveTypeCode,
            entity.NameLocal,
            entity.NameEn,
            entity.IsPaid,
            entity.IsStatutory,
            entity.MaxDaysPerYear,
            entity.CarryForwardAllowed,
            entity.CarryForwardCapDays,
            entity.RequiresDocumentation,
            entity.IsActive
        );
    }

    public async Task<bool> DeleteLeaveTypeAsync(string leaveTypeCode, string user, CancellationToken cancellationToken = default)
    {
        var entity = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken);
        if (entity == null) return false;

        entity.IsActive = false;
        entity.UpdateUser = user;
        entity.UpdateDate = DateTime.UtcNow;

        await _leaveRepo.UpdateLeaveTypeAsync(entity, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);
        return true;
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
        var (items, totalCount) = await _leaveRepo.GetLeaveRequestsPagedAsync(
            employeeCode, leaveTypeCode, status, fromDate, toDate, pageIndex, pageSize, cancellationToken);

        var dtos = items.Select(r => new LeaveRequestDto(
            r.Id,
            r.EmployeeCode,
            r.Employee?.NameLocal ?? r.EmployeeCode,
            r.LeaveTypeCode,
            r.LeaveType?.NameLocal ?? r.LeaveTypeCode,
            r.StartDate,
            r.EndDate,
            r.DaysRequested,
            r.Status,
            r.Reason,
            r.ApprovedBy,
            r.ApprovalDate,
            r.RejectionReason,
            r.CreationDate
        )).ToList();

        return (dtos, totalCount);
    }

    public async Task<LeaveRequestDto> SubmitLeaveRequestAsync(SubmitLeaveRequestDto dto, string user, CancellationToken cancellationToken = default)
    {
        if (dto.EndDate < dto.StartDate)
        {
            throw new InvalidOperationException("Leave end date cannot be earlier than start date.");
        }

        var emp = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken);
        if (emp == null)
        {
            throw new InvalidOperationException($"Employee '{dto.EmployeeCode}' not found.");
        }

        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(dto.LeaveTypeCode, cancellationToken);
        if (leaveType == null || !leaveType.IsActive)
        {
            throw new InvalidOperationException($"Leave type '{dto.LeaveTypeCode}' is invalid or inactive.");
        }

        int year = dto.StartDate.Year;

        // If paid leave, check available balance
        if (leaveType.IsPaid)
        {
            var balance = await _leaveRepo.GetLeaveBalanceAsync(dto.EmployeeCode, dto.LeaveTypeCode, year, cancellationToken);
            decimal available = (balance?.AccruedDays ?? leaveType.MaxDaysPerYear) + (balance?.CarriedForwardDays ?? 0m) - (balance?.UsedDays ?? 0m);

            if (dto.DaysRequested > available)
            {
                throw new InvalidOperationException($"Insufficient leave balance. Requested: {dto.DaysRequested} days, Available: {available} days.");
            }
        }

        var request = new LeaveRequest
        {
            EmployeeCode = dto.EmployeeCode,
            LeaveTypeCode = dto.LeaveTypeCode,
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate.Date,
            DaysRequested = dto.DaysRequested,
            Status = "PENDING",
            Reason = dto.Reason,
            AttachmentFileRef = dto.AttachmentFileRef,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        await _leaveRepo.AddLeaveRequestAsync(request, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return new LeaveRequestDto(
            request.Id,
            request.EmployeeCode,
            emp.NameLocal,
            request.LeaveTypeCode,
            leaveType.NameLocal,
            request.StartDate,
            request.EndDate,
            request.DaysRequested,
            request.Status,
            request.Reason,
            null,
            null,
            null,
            request.CreationDate
        );
    }

    public async Task<LeaveRequestDto> ProcessLeaveRequestAsync(long requestId, ProcessLeaveRequestDto dto, string user, CancellationToken cancellationToken = default)
    {
        var request = await _leaveRepo.GetLeaveRequestByIdAsync(requestId, cancellationToken);
        if (request == null)
        {
            throw new InvalidOperationException($"Leave request with ID {requestId} not found.");
        }

        if (request.Status != "PENDING")
        {
            throw new InvalidOperationException($"Leave request {requestId} has already been processed with status '{request.Status}'.");
        }

        request.Status = dto.Approved ? "APPROVED" : "REJECTED";
        request.ApprovedBy = user;
        request.ApprovalDate = DateTime.UtcNow;
        request.RejectionReason = dto.RejectionReason;
        request.UpdateUser = user;
        request.UpdateDate = DateTime.UtcNow;

        if (dto.Approved && request.LeaveType != null && request.LeaveType.IsPaid)
        {
            int year = request.StartDate.Year;
            var balance = await _leaveRepo.GetLeaveBalanceAsync(request.EmployeeCode, request.LeaveTypeCode, year, cancellationToken);
            if (balance == null)
            {
                balance = new LeaveBalance
                {
                    EmployeeCode = request.EmployeeCode,
                    LeaveTypeCode = request.LeaveTypeCode,
                    YearNo = year,
                    AccruedDays = request.LeaveType.MaxDaysPerYear,
                    UsedDays = request.DaysRequested,
                    CarriedForwardDays = 0,
                    CreationUser = user,
                    CreationDate = DateTime.UtcNow
                };
                await _leaveRepo.AddLeaveBalanceAsync(balance, cancellationToken);
            }
            else
            {
                balance.UsedDays += request.DaysRequested;
                balance.UpdateUser = user;
                balance.UpdateDate = DateTime.UtcNow;
                await _leaveRepo.UpdateLeaveBalanceAsync(balance, cancellationToken);
            }
        }

        await _leaveRepo.UpdateLeaveRequestAsync(request, cancellationToken);
        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return new LeaveRequestDto(
            request.Id,
            request.EmployeeCode,
            request.Employee?.NameLocal ?? request.EmployeeCode,
            request.LeaveTypeCode,
            request.LeaveType?.NameLocal ?? request.LeaveTypeCode,
            request.StartDate,
            request.EndDate,
            request.DaysRequested,
            request.Status,
            request.Reason,
            request.ApprovedBy,
            request.ApprovalDate,
            request.RejectionReason,
            request.CreationDate
        );
    }

    public async Task<IReadOnlyList<LeaveBalanceDto>> GetEmployeeBalancesAsync(string employeeCode, int? year = null, CancellationToken cancellationToken = default)
    {
        int targetYear = year ?? DateTime.UtcNow.Year;
        var balances = await _leaveRepo.GetEmployeeBalancesAsync(employeeCode, targetYear, cancellationToken);
        var allTypes = await _leaveRepo.GetLeaveTypesAsync(true, cancellationToken);

        var result = new List<LeaveBalanceDto>();

        foreach (var type in allTypes.Where(t => t.IsPaid))
        {
            var bal = balances.FirstOrDefault(b => b.LeaveTypeCode == type.LeaveTypeCode);
            decimal accrued = bal?.AccruedDays ?? type.MaxDaysPerYear;
            decimal carried = bal?.CarriedForwardDays ?? 0m;
            decimal used = bal?.UsedDays ?? 0m;
            decimal remaining = accrued + carried - used;

            result.Add(new LeaveBalanceDto(
                employeeCode,
                type.LeaveTypeCode,
                type.NameLocal,
                targetYear,
                accrued,
                used,
                carried,
                remaining
            ));
        }

        return result;
    }

    public async Task<decimal> GetUnpaidLeaveDaysInPeriodAsync(string employeeCode, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default)
    {
        var unpaidRequests = await _leaveRepo.GetApprovedUnpaidLeavesInPeriodAsync(employeeCode, periodStart, periodEnd, cancellationToken);
        decimal totalUnpaidDays = 0m;

        foreach (var req in unpaidRequests)
        {
            var effectiveStart = req.StartDate > periodStart ? req.StartDate.Date : periodStart.Date;
            var effectiveEnd = req.EndDate < periodEnd ? req.EndDate.Date : periodEnd.Date;

            if (effectiveEnd >= effectiveStart)
            {
                var days = (effectiveEnd - effectiveStart).Days + 1;
                totalUnpaidDays += days;
            }
        }

        return totalUnpaidDays;
    }

    public async Task<LeaveBalanceDto> GrantOrAdjustBalanceAsync(GrantLeaveBalanceDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var emp = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Employee '{dto.EmployeeCode}' not found.");

        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(dto.LeaveTypeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Leave type '{dto.LeaveTypeCode}' not found.");

        var balance = await _leaveRepo.GetLeaveBalanceAsync(dto.EmployeeCode, dto.LeaveTypeCode, dto.YearNo, cancellationToken);
        if (balance == null)
        {
            balance = new LeaveBalance
            {
                EmployeeCode = dto.EmployeeCode,
                LeaveTypeCode = dto.LeaveTypeCode,
                YearNo = dto.YearNo,
                AccruedDays = dto.Days,
                UsedDays = 0,
                CarriedForwardDays = 0,
                CreationUser = user,
                CreationDate = DateTime.UtcNow
            };
            await _leaveRepo.AddLeaveBalanceAsync(balance, cancellationToken);
        }
        else
        {
            if (string.Equals(dto.AdjustmentType, "OPENING_BALANCE", StringComparison.OrdinalIgnoreCase))
            {
                balance.AccruedDays = dto.Days;
            }
            else
            {
                balance.AccruedDays += dto.Days;
                if (balance.AccruedDays < 0) balance.AccruedDays = 0;
            }
            balance.UpdateUser = user;
            balance.UpdateDate = DateTime.UtcNow;
            await _leaveRepo.UpdateLeaveBalanceAsync(balance, cancellationToken);
        }

        await _leaveRepo.SaveChangesAsync(cancellationToken);

        decimal remaining = (balance.AccruedDays + balance.CarriedForwardDays) - balance.UsedDays;
        return new LeaveBalanceDto(
            balance.EmployeeCode,
            balance.LeaveTypeCode,
            leaveType.NameLocal,
            balance.YearNo,
            balance.AccruedDays,
            balance.UsedDays,
            balance.CarriedForwardDays,
            remaining
        );
    }

    public async Task<LeaveAccrualResultDto> RunMonthlyAccrualAsync(MonthlyLeaveAccrualDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.MonthNo < 1 || dto.MonthNo > 12)
            throw new ArgumentException("Month must be between 1 and 12.", nameof(dto.MonthNo));

        var leaveTypeCode = string.IsNullOrWhiteSpace(dto.LeaveTypeCode) ? "ANNUAL" : dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Leave type '{leaveTypeCode}' not found.");

        var policy = await _leaveRepo.GetLeavePolicyByTypeCodeAsync(leaveTypeCode, cancellationToken);

        var employees = new List<Employee>();
        if (!string.IsNullOrWhiteSpace(dto.EmployeeCode))
        {
            var emp = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken);
            if (emp != null && emp.IsActive && emp.EmploymentStatus == "ACTIVE")
                employees.Add(emp);
        }
        else
        {
            var all = await _employeeRepo.GetAllEmployeesForExportAsync(status: "ACTIVE", cancellationToken: cancellationToken);
            employees.AddRange(all);
        }

        var accrualDate = new DateTime(dto.YearNo, dto.MonthNo, DateTime.DaysInMonth(dto.YearNo, dto.MonthNo));
        var details = new List<string>();
        int updatedCount = 0;
        decimal totalAccrued = 0m;

        foreach (var emp in employees)
        {
            if (emp.HireDate > accrualDate)
            {
                // Not yet hired
                continue;
            }

            // Check tenure in years
            var tenureYears = (accrualDate - emp.HireDate).TotalDays / 365.25;
            var thresholdYears = policy?.Tier1YearsThreshold ?? 5;
            var annualQuota = tenureYears >= thresholdYears ? (policy?.Tier2Days ?? 21m) : (policy?.Tier1Days ?? leaveType.MaxDaysPerYear);

            // Monthly base earned rate
            var monthlyRate = Math.Round(annualQuota / 12m, 2);

            // Check if hired during this accrual month (pro-rate)
            if (emp.HireDate.Year == dto.YearNo && emp.HireDate.Month == dto.MonthNo)
            {
                int daysInMonth = DateTime.DaysInMonth(dto.YearNo, dto.MonthNo);
                int daysWorked = daysInMonth - emp.HireDate.Day + 1;
                monthlyRate = Math.Round(monthlyRate * ((decimal)daysWorked / daysInMonth), 2);
            }

            var balance = await _leaveRepo.GetLeaveBalanceAsync(emp.EmployeeCode, leaveTypeCode, dto.YearNo, cancellationToken);
            if (balance == null)
            {
                balance = new LeaveBalance
                {
                    EmployeeCode = emp.EmployeeCode,
                    LeaveTypeCode = leaveTypeCode,
                    YearNo = dto.YearNo,
                    AccruedDays = 0m,
                    UsedDays = 0m,
                    CarriedForwardDays = 0m,
                    CreationUser = user,
                    CreationDate = DateTime.UtcNow
                };
                await _leaveRepo.AddLeaveBalanceAsync(balance, cancellationToken);
            }

            // Cap at annual quota
            decimal daysToAdd = monthlyRate;
            if (balance.AccruedDays + daysToAdd > annualQuota)
            {
                daysToAdd = Math.Max(0m, annualQuota - balance.AccruedDays);
            }

            if (daysToAdd > 0)
            {
                balance.AccruedDays += daysToAdd;
                balance.UpdateUser = user;
                balance.UpdateDate = DateTime.UtcNow;
                await _leaveRepo.UpdateLeaveBalanceAsync(balance, cancellationToken);

                totalAccrued += daysToAdd;
                updatedCount++;
                details.Add($"{emp.EmployeeCode} ({emp.NameLocal}): +{daysToAdd:F2} days (Accrued: {balance.AccruedDays:F2}/{annualQuota:F2})");
            }
        }

        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return new LeaveAccrualResultDto(
            employees.Count,
            updatedCount,
            totalAccrued,
            details
        );
    }

    public async Task<LeaveAccrualResultDto> RunYearlyAllocationAsync(YearlyLeaveAllocationDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var leaveTypeCode = string.IsNullOrWhiteSpace(dto.LeaveTypeCode) ? "ANNUAL" : dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(leaveTypeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Leave type '{leaveTypeCode}' not found.");

        var policy = await _leaveRepo.GetLeavePolicyByTypeCodeAsync(leaveTypeCode, cancellationToken);

        var employees = new List<Employee>();
        if (!string.IsNullOrWhiteSpace(dto.EmployeeCode))
        {
            var emp = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken);
            if (emp != null && emp.IsActive && emp.EmploymentStatus == "ACTIVE")
                employees.Add(emp);
        }
        else
        {
            var all = await _employeeRepo.GetAllEmployeesForExportAsync(
                departmentCode: dto.DepartmentCode, status: "ACTIVE", cancellationToken: cancellationToken);
            employees.AddRange(all);
        }

        var yearStart = new DateTime(dto.YearNo, 1, 1);
        var details = new List<string>();
        int updatedCount = 0;
        decimal totalAllocated = 0m;

        foreach (var emp in employees)
        {
            if (emp.HireDate.Year > dto.YearNo)
            {
                continue;
            }

            var tenureYears = (yearStart - emp.HireDate).TotalDays / 365.25;
            var thresholdYears = policy?.Tier1YearsThreshold ?? 5;
            var quota = tenureYears >= thresholdYears ? (policy?.Tier2Days ?? 21m) : (policy?.Tier1Days ?? leaveType.MaxDaysPerYear);

            if (dto.ProrateForMidYearHires && emp.HireDate.Year == dto.YearNo)
            {
                int monthsRemaining = 12 - emp.HireDate.Month + 1;
                quota = Math.Round((quota / 12m) * monthsRemaining, 2);
            }

            var balance = await _leaveRepo.GetLeaveBalanceAsync(emp.EmployeeCode, leaveTypeCode, dto.YearNo, cancellationToken);
            if (balance == null)
            {
                balance = new LeaveBalance
                {
                    EmployeeCode = emp.EmployeeCode,
                    LeaveTypeCode = leaveTypeCode,
                    YearNo = dto.YearNo,
                    AccruedDays = quota,
                    UsedDays = 0m,
                    CarriedForwardDays = 0m,
                    CreationUser = user,
                    CreationDate = DateTime.UtcNow
                };
                await _leaveRepo.AddLeaveBalanceAsync(balance, cancellationToken);
            }
            else
            {
                balance.AccruedDays = quota;
                balance.UpdateUser = user;
                balance.UpdateDate = DateTime.UtcNow;
                await _leaveRepo.UpdateLeaveBalanceAsync(balance, cancellationToken);
            }

            totalAllocated += quota;
            updatedCount++;
            details.Add($"{emp.EmployeeCode} ({emp.NameLocal}): Allocated {quota:F2} days for year {dto.YearNo}");
        }

        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return new LeaveAccrualResultDto(
            employees.Count,
            updatedCount,
            totalAllocated,
            details
        );
    }

    public async Task<LeaveRolloverResultDto> RunYearEndRolloverAsync(YearEndRolloverDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.ToYear <= dto.FromYear)
            throw new ArgumentException("ToYear must be greater than FromYear.");

        var leaveTypeCode = string.IsNullOrWhiteSpace(dto.LeaveTypeCode) ? null : dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var balances = await _leaveRepo.GetLeaveBalancesByYearAsync(dto.FromYear, leaveTypeCode, cancellationToken);

        var details = new List<string>();
        int rolloverCount = 0;
        decimal totalCarried = 0m;
        decimal totalExpired = 0m;

        foreach (var bal in balances)
        {
            var leaveType = bal.LeaveType ?? await _leaveRepo.GetLeaveTypeByCodeAsync(bal.LeaveTypeCode, cancellationToken);
            if (leaveType == null || !leaveType.IsPaid) continue;

            decimal unused = (bal.AccruedDays + bal.CarriedForwardDays) - bal.UsedDays;
            if (unused <= 0) continue;

            decimal allowedCarry = 0m;
            decimal expired = 0m;

            if (leaveType.CarryForwardAllowed)
            {
                decimal cap = leaveType.CarryForwardCapDays > 0 ? leaveType.CarryForwardCapDays : unused;
                allowedCarry = Math.Min(unused, cap);
                expired = unused - allowedCarry;
            }
            else
            {
                expired = unused;
            }

            var nextBalance = await _leaveRepo.GetLeaveBalanceAsync(bal.EmployeeCode, bal.LeaveTypeCode, dto.ToYear, cancellationToken);
            if (nextBalance == null)
            {
                nextBalance = new LeaveBalance
                {
                    EmployeeCode = bal.EmployeeCode,
                    LeaveTypeCode = bal.LeaveTypeCode,
                    YearNo = dto.ToYear,
                    AccruedDays = 0m,
                    UsedDays = 0m,
                    CarriedForwardDays = allowedCarry,
                    CreationUser = user,
                    CreationDate = DateTime.UtcNow
                };
                await _leaveRepo.AddLeaveBalanceAsync(nextBalance, cancellationToken);
            }
            else
            {
                nextBalance.CarriedForwardDays = allowedCarry;
                nextBalance.UpdateUser = user;
                nextBalance.UpdateDate = DateTime.UtcNow;
                await _leaveRepo.UpdateLeaveBalanceAsync(nextBalance, cancellationToken);
            }

            totalCarried += allowedCarry;
            totalExpired += expired;
            rolloverCount++;

            details.Add($"{bal.EmployeeCode} [{bal.LeaveTypeCode}]: Unused: {unused:F2} => Carried: {allowedCarry:F2}, Expired: {expired:F2}");
        }

        await _leaveRepo.SaveChangesAsync(cancellationToken);

        return new LeaveRolloverResultDto(
            balances.Count,
            rolloverCount,
            totalCarried,
            totalExpired,
            details
        );
    }

    public async Task<IReadOnlyList<LeavePolicyDetailsDto>> GetLeavePoliciesAsync(CancellationToken cancellationToken = default)
    {
        var policies = await _leaveRepo.GetActiveLeavePoliciesAsync(cancellationToken);
        return policies.Select(p => new LeavePolicyDetailsDto(
            p.Id,
            p.PolicyName,
            p.LeaveTypeCode,
            p.AccrualMethod,
            p.AccrualRate,
            p.ApplicableTo,
            p.MinServiceMonths,
            p.Tier1YearsThreshold,
            p.Tier1Days,
            p.Tier2Days,
            p.IsActive
        )).ToList();
    }

    public async Task<LeavePolicyDetailsDto> CreateOrUpdateLeavePolicyAsync(CreateLeavePolicyDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var existing = await _leaveRepo.GetLeavePolicyByTypeCodeAsync(dto.LeaveTypeCode, cancellationToken);
        if (existing == null)
        {
            var policy = new LeavePolicy
            {
                PolicyName = dto.PolicyName,
                LeaveTypeCode = dto.LeaveTypeCode,
                AccrualMethod = dto.AccrualMethod,
                AccrualRate = dto.AccrualRate,
                ApplicableTo = dto.ApplicableTo,
                MinServiceMonths = dto.MinServiceMonths,
                Tier1YearsThreshold = dto.Tier1YearsThreshold,
                Tier1Days = dto.Tier1Days,
                Tier2Days = dto.Tier2Days,
                IsActive = dto.IsActive,
                CreationUser = user,
                CreationDate = DateTime.UtcNow
            };
            await _leaveRepo.AddLeavePolicyAsync(policy, cancellationToken);
            await _leaveRepo.SaveChangesAsync(cancellationToken);

            return new LeavePolicyDetailsDto(
                policy.Id, policy.PolicyName, policy.LeaveTypeCode, policy.AccrualMethod,
                policy.AccrualRate, policy.ApplicableTo, policy.MinServiceMonths,
                policy.Tier1YearsThreshold, policy.Tier1Days, policy.Tier2Days, policy.IsActive);
        }
        else
        {
            existing.PolicyName = dto.PolicyName;
            existing.AccrualMethod = dto.AccrualMethod;
            existing.AccrualRate = dto.AccrualRate;
            existing.ApplicableTo = dto.ApplicableTo;
            existing.MinServiceMonths = dto.MinServiceMonths;
            existing.Tier1YearsThreshold = dto.Tier1YearsThreshold;
            existing.Tier1Days = dto.Tier1Days;
            existing.Tier2Days = dto.Tier2Days;
            existing.IsActive = dto.IsActive;
            existing.UpdateUser = user;
            existing.UpdateDate = DateTime.UtcNow;

            await _leaveRepo.UpdateLeavePolicyAsync(existing, cancellationToken);
            await _leaveRepo.SaveChangesAsync(cancellationToken);

            return new LeavePolicyDetailsDto(
                existing.Id, existing.PolicyName, existing.LeaveTypeCode, existing.AccrualMethod,
                existing.AccrualRate, existing.ApplicableTo, existing.MinServiceMonths,
                existing.Tier1YearsThreshold, existing.Tier1Days, existing.Tier2Days, existing.IsActive);
        }
    }
}

