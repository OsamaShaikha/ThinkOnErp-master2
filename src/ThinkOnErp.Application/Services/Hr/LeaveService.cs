using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class LeaveService : ILeaveService
{
    private readonly ILeaveRepository _leaveRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<LeaveService> _logger;

    public LeaveService(
        ILeaveRepository leaveRepository,
        IEmployeeRepository employeeRepository,
        ILogger<LeaveService> logger)
    {
        _leaveRepository = leaveRepository ?? throw new ArgumentNullException(nameof(leaveRepository));
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<LeaveTypeDto>> GetAllLeaveTypesAsync(bool activeOnly = true)
    {
        var types = await _leaveRepository.GetAllLeaveTypesAsync(activeOnly);
        return types.Select(MapToLeaveTypeDto).ToList();
    }

    public async Task<LeaveTypeDto?> GetLeaveTypeByCodeAsync(string code)
    {
        var type = await _leaveRepository.GetLeaveTypeByCodeAsync(code);
        return type == null ? null : MapToLeaveTypeDto(type);
    }

    public async Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var code = dto.LeaveTypeCode.Trim().ToUpperInvariant();

        if (await _leaveRepository.LeaveTypeCodeExistsAsync(code))
        {
            throw new HrConflictException($"نوع الإجازة ({code}) معرف مسبقاً.", "LEAVE_TYPE_DUPLICATE");
        }

        var leaveType = new LeaveType
        {
            LeaveTypeCode = code,
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            IsPaid = dto.IsPaid,
            IsStatutory = dto.IsStatutory,
            RequiresDocumentation = dto.RequiresDocumentation,
            MaxDaysPerYear = dto.MaxDaysPerYear,
            CarryForwardAllowed = dto.CarryForwardAllowed,
            CarryForwardCapDays = dto.CarryForwardCapDays,
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _leaveRepository.AddLeaveTypeAsync(leaveType);
        await _leaveRepository.SaveChangesAsync();

        _logger.LogInformation("Created leave type {Code} by {User}", code, currentUser);
        return MapToLeaveTypeDto(leaveType);
    }

    public async Task<List<LeavePolicyDto>> GetPoliciesByLeaveTypeAsync(string leaveTypeCode)
    {
        var policies = await _leaveRepository.GetPoliciesByLeaveTypeAsync(leaveTypeCode);
        return policies.Select(MapToPolicyDto).ToList();
    }

    public async Task<LeavePolicyDto> CreatePolicyAsync(CreateLeavePolicyDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var typeCode = dto.LeaveTypeCode.Trim().ToUpperInvariant();

        var type = await _leaveRepository.GetLeaveTypeByCodeAsync(typeCode);
        if (type == null)
        {
            throw new HrNotFoundException($"نوع الإجازة ({typeCode}) غير موجود.", "LEAVE_TYPE_NOT_FOUND");
        }

        var policy = new LeavePolicy
        {
            LeaveTypeCode = typeCode,
            PolicyName = dto.PolicyName.Trim(),
            ApplicableTo = string.IsNullOrWhiteSpace(dto.ApplicableTo) ? "ALL" : dto.ApplicableTo.Trim().ToUpperInvariant(),
            AccrualMethod = string.IsNullOrWhiteSpace(dto.AccrualMethod) ? "SERVICE_TIERED" : dto.AccrualMethod.Trim().ToUpperInvariant(),
            AccrualRate = dto.AccrualRate,
            MinServiceMonths = dto.MinServiceMonths,
            Tier1YearsThreshold = dto.Tier1YearsThreshold,
            Tier1Days = dto.Tier1Days,
            Tier2Days = dto.Tier2Days,
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _leaveRepository.AddPolicyAsync(policy);
        await _leaveRepository.SaveChangesAsync();

        _logger.LogInformation("Created leave policy for {Type} by {User}", typeCode, currentUser);
        return MapToPolicyDto(policy);
    }

    public async Task<List<LeaveBalanceDto>> GetEmployeeBalancesAsync(string employeeCode, int? year = null)
    {
        var targetYear = year ?? DateTime.UtcNow.Year;
        var balances = await _leaveRepository.GetBalancesByEmployeeAsync(employeeCode, targetYear);
        return balances.Select(MapToBalanceDto).ToList();
    }

    public async Task<RunAccrualResultDto> RunMonthlyAccrualAsync(int? year = null, string? currentUser = null)
    {
        var targetYear = year ?? DateTime.UtcNow.Year;
        var activeEmployees = await _employeeRepository.GetAllAsync(activeOnly: true);
        var policies = await _leaveRepository.GetAllActivePoliciesAsync();
        var user = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM_ACCRUAL" : currentUser;
        var now = DateTime.UtcNow;

        var updatedCount = 0;

        foreach (var emp in activeEmployees.Where(e => e.EmploymentStatus != "TERMINATED"))
        {
            var tenureYears = (now - emp.HireDate).TotalDays / 365.25;

            foreach (var policy in policies)
            {
                var balance = await _leaveRepository.GetBalanceAsync(emp.EmployeeCode, policy.LeaveTypeCode, targetYear);
                if (balance == null)
                {
                    balance = new LeaveBalance
                    {
                        EmployeeCode = emp.EmployeeCode,
                        LeaveTypeCode = policy.LeaveTypeCode,
                        Year = targetYear,
                        AccruedDays = 0m,
                        UsedDays = 0m,
                        CarriedForwardDays = 0m,
                        CreationUser = user,
                        CreationDate = now
                    };
                    await _leaveRepository.AddBalanceAsync(balance);
                }

                if (policy.AccrualMethod == "SERVICE_TIERED")
                {
                    // Tiered: 14 days baseline, 21 days if >= threshold (5 yrs)
                    var annualEntitlement = tenureYears >= policy.Tier1YearsThreshold ? policy.Tier2Days : policy.Tier1Days;
                    var monthlyRate = annualEntitlement / 12m;
                    balance.AccruedDays = Math.Min(annualEntitlement, Math.Round(balance.AccruedDays + monthlyRate, 2));
                }
                else if (policy.AccrualMethod == "MONTHLY_ACCRUAL")
                {
                    balance.AccruedDays = Math.Round(balance.AccruedDays + policy.AccrualRate, 2);
                }
                else if (policy.AccrualMethod == "ANNUAL_LUMP_SUM")
                {
                    if (balance.AccruedDays == 0)
                    {
                        balance.AccruedDays = policy.Tier1Days > 0 ? policy.Tier1Days : policy.AccrualRate;
                    }
                }

                balance.UpdateUser = user;
                balance.UpdateDate = now;
                _leaveRepository.UpdateBalance(balance);
                updatedCount++;
            }
        }

        await _leaveRepository.SaveChangesAsync();
        _logger.LogInformation("Completed monthly leave accrual for {Count} employees ({Updated} balances)", activeEmployees.Count, updatedCount);

        return new RunAccrualResultDto
        {
            ProcessedEmployees = activeEmployees.Count,
            UpdatedBalances = updatedCount,
            Year = targetYear,
            Message = $"Successfully executed leave accruals for year {targetYear} across {activeEmployees.Count} active employees."
        };
    }

    public async Task<List<LeaveRequestDto>> GetLeaveRequestsAsync(
        string? employeeCode = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var requests = await _leaveRepository.GetRequestsAsync(employeeCode, status, fromDate, toDate);
        return requests.Select(MapToRequestDto).ToList();
    }

    public async Task<LeaveRequestDto?> GetLeaveRequestByIdAsync(long id)
    {
        var req = await _leaveRepository.GetRequestByIdAsync(id);
        return req == null ? null : MapToRequestDto(req);
    }

    public async Task<LeaveRequestDto> SubmitRequestAsync(SubmitLeaveRequestDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var empCode = dto.EmployeeCode.Trim().ToUpperInvariant();
        var typeCode = dto.LeaveTypeCode.Trim().ToUpperInvariant();

        var emp = await _employeeRepository.GetByCodeAsync(empCode);
        if (emp == null)
        {
            throw new HrNotFoundException($"الموظف ({empCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var leaveType = await _leaveRepository.GetLeaveTypeByCodeAsync(typeCode);
        if (leaveType == null)
        {
            throw new HrNotFoundException($"نوع الإجازة ({typeCode}) غير موجود.", "LEAVE_TYPE_NOT_FOUND");
        }

        if (dto.EndDate < dto.StartDate)
        {
            throw new HrValidationException("تاريخ نهاية الإجازة لا يمكن أن يكون قبل تاريخ بدايتها.", "INVALID_DATE_RANGE");
        }

        var year = dto.StartDate.Year;
        var balance = await _leaveRepository.GetBalanceAsync(empCode, typeCode, year);

        // If paid statutory leave, check remaining balance
        if (leaveType.IsPaid)
        {
            var remaining = balance?.RemainingDays ?? 0m;
            if (dto.DaysRequested > remaining)
            {
                throw new HrValidationException($"الرصيد المتبقي ({remaining} يوم) غير كافٍ لطلب إجازة لمدة ({dto.DaysRequested} يوم).", "INSUFFICIENT_LEAVE_BALANCE");
            }
        }

        var request = new LeaveRequest
        {
            EmployeeCode = empCode,
            LeaveTypeCode = typeCode,
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate.Date,
            DaysRequested = dto.DaysRequested,
            Status = "PENDING",
            Reason = dto.Reason,
            AttachmentFileReference = dto.AttachmentFileReference,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _leaveRepository.AddRequestAsync(request);
        await _leaveRepository.SaveChangesAsync();

        _logger.LogInformation("Submitted leave request for {Emp} ({Days} days {Type})", empCode, dto.DaysRequested, typeCode);
        return MapToRequestDto(request);
    }

    public async Task<LeaveRequestDto> ApproveRequestAsync(long id, string approvedBy)
    {
        var req = await _leaveRepository.GetRequestByIdAsync(id);
        if (req == null)
        {
            throw new HrNotFoundException($"طلب الإجازة رقم ({id}) غير موجود.", "LEAVE_REQUEST_NOT_FOUND");
        }

        if (req.Status != "PENDING")
        {
            throw new HrValidationException($"لا يمكن اعتماد طلب إجازة بحالة ({req.Status}).", "INVALID_STATUS_TRANSITION");
        }

        var year = req.StartDate.Year;
        var balance = await _leaveRepository.GetBalanceAsync(req.EmployeeCode, req.LeaveTypeCode, year);
        if (balance == null)
        {
            balance = new LeaveBalance
            {
                EmployeeCode = req.EmployeeCode,
                LeaveTypeCode = req.LeaveTypeCode,
                Year = year,
                AccruedDays = 0m,
                UsedDays = 0m,
                CarriedForwardDays = 0m,
                CreationUser = approvedBy,
                CreationDate = DateTime.UtcNow
            };
            await _leaveRepository.AddBalanceAsync(balance);
        }

        // Deduct from balance
        balance.UsedDays += req.DaysRequested;
        balance.UpdateUser = approvedBy;
        balance.UpdateDate = DateTime.UtcNow;
        _leaveRepository.UpdateBalance(balance);

        req.Status = "APPROVED";
        req.ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? "SYSTEM" : approvedBy;
        req.ApprovalDate = DateTime.UtcNow;
        req.UpdateUser = req.ApprovedBy;
        req.UpdateDate = DateTime.UtcNow;

        _leaveRepository.UpdateRequest(req);
        await _leaveRepository.SaveChangesAsync();

        _logger.LogInformation("Approved leave request {Id} for {Emp}", id, req.EmployeeCode);
        return MapToRequestDto(req);
    }

    public async Task<LeaveRequestDto> RejectRequestAsync(long id, string rejectionReason, string rejectedBy)
    {
        var req = await _leaveRepository.GetRequestByIdAsync(id);
        if (req == null)
        {
            throw new HrNotFoundException($"طلب الإجازة رقم ({id}) غير موجود.", "LEAVE_REQUEST_NOT_FOUND");
        }

        if (req.Status != "PENDING")
        {
            throw new HrValidationException($"لا يمكن رفض طلب إجازة بحالة ({req.Status}).", "INVALID_STATUS_TRANSITION");
        }

        req.Status = "REJECTED";
        req.ApprovedBy = string.IsNullOrWhiteSpace(rejectedBy) ? "SYSTEM" : rejectedBy;
        req.ApprovalDate = DateTime.UtcNow;
        req.RejectionReason = rejectionReason;
        req.UpdateUser = req.ApprovedBy;
        req.UpdateDate = DateTime.UtcNow;

        _leaveRepository.UpdateRequest(req);
        await _leaveRepository.SaveChangesAsync();

        _logger.LogInformation("Rejected leave request {Id} for {Emp}", id, req.EmployeeCode);
        return MapToRequestDto(req);
    }

    private static LeaveTypeDto MapToLeaveTypeDto(LeaveType t)
    {
        return new LeaveTypeDto
        {
            LeaveTypeCode = t.LeaveTypeCode,
            NameAr = t.NameAr,
            NameEn = t.NameEn,
            IsPaid = t.IsPaid,
            IsStatutory = t.IsStatutory,
            RequiresDocumentation = t.RequiresDocumentation,
            MaxDaysPerYear = t.MaxDaysPerYear,
            CarryForwardAllowed = t.CarryForwardAllowed,
            CarryForwardCapDays = t.CarryForwardCapDays,
            IsActive = t.IsActive
        };
    }

    private static LeavePolicyDto MapToPolicyDto(LeavePolicy p)
    {
        return new LeavePolicyDto
        {
            Id = p.Id,
            LeaveTypeCode = p.LeaveTypeCode,
            PolicyName = p.PolicyName,
            ApplicableTo = p.ApplicableTo,
            AccrualMethod = p.AccrualMethod,
            AccrualRate = p.AccrualRate,
            MinServiceMonths = p.MinServiceMonths,
            Tier1YearsThreshold = p.Tier1YearsThreshold,
            Tier1Days = p.Tier1Days,
            Tier2Days = p.Tier2Days,
            IsActive = p.IsActive
        };
    }

    private static LeaveBalanceDto MapToBalanceDto(LeaveBalance b)
    {
        return new LeaveBalanceDto
        {
            Id = b.Id,
            EmployeeCode = b.EmployeeCode,
            LeaveTypeCode = b.LeaveTypeCode,
            LeaveTypeNameEn = b.LeaveType?.NameEn ?? string.Empty,
            Year = b.Year,
            AccruedDays = b.AccruedDays,
            UsedDays = b.UsedDays,
            CarriedForwardDays = b.CarriedForwardDays,
            RemainingDays = b.RemainingDays
        };
    }

    private static LeaveRequestDto MapToRequestDto(LeaveRequest r)
    {
        return new LeaveRequestDto
        {
            Id = r.Id,
            EmployeeCode = r.EmployeeCode,
            EmployeeNameAr = r.Employee?.NameAr ?? string.Empty,
            EmployeeNameEn = r.Employee?.NameEn ?? string.Empty,
            LeaveTypeCode = r.LeaveTypeCode,
            LeaveTypeNameEn = r.LeaveType?.NameEn ?? string.Empty,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            DaysRequested = r.DaysRequested,
            Status = r.Status,
            Reason = r.Reason,
            AttachmentFileReference = r.AttachmentFileReference,
            ApprovedBy = r.ApprovedBy,
            ApprovalDate = r.ApprovalDate,
            RejectionReason = r.RejectionReason,
            CreationDate = r.CreationDate
        };
    }
}
