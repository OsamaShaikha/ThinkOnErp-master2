using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.ApproveShortLeave;

public sealed record ApproveShortLeaveCommand(long RequestId, string User) : IRequest<ShortLeaveDto>;

public sealed class ApproveShortLeaveCommandValidator : AbstractValidator<ApproveShortLeaveCommand>
{
    public ApproveShortLeaveCommandValidator()
    {
        RuleFor(x => x.RequestId).GreaterThan(0);
    }
}

public sealed class ApproveShortLeaveCommandHandler : IRequestHandler<ApproveShortLeaveCommand, ShortLeaveDto>
{
    private readonly IShortLeaveRepository _repo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IPolicyRepository? _policyRepository;
    private readonly IHrUnitOfWork? _unitOfWork;

    public ApproveShortLeaveCommandHandler(
        IShortLeaveRepository repo,
        IEmployeeRepository employeeRepo,
        IPolicyRepository? policyRepository = null,
        IHrUnitOfWork? unitOfWork = null)
    {
        _repo = repo;
        _employeeRepo = employeeRepo;
        _policyRepository = policyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ShortLeaveDto> Handle(ApproveShortLeaveCommand request, CancellationToken ct)
    {
        var snapshot = await _repo.GetByIdAsync(request.RequestId, ct)
            ?? throw new KeyNotFoundException($"Short leave request {request.RequestId} not found.");

        if (_unitOfWork == null)
            return await ApproveCoreAsync(request.RequestId, request.User, ct);

        return await _unitOfWork.ExecuteForEmployeeDatesAsync(snapshot.EmployeeCode, snapshot.PermissionDate, snapshot.PermissionDate,
            () => ApproveCoreAsync(request.RequestId, request.User, ct), ct);
    }

    private async Task<ShortLeaveDto> ApproveCoreAsync(long requestId, string user, CancellationToken ct)
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
            var excessAction = ShortLeaveCommon.NormalizeExcessAction(policy.ExcessAction, invalidOperation: true);
            if (excessAction == "REJECT")
                throw new InvalidOperationException("The company short leave quota is exceeded and its current policy does not allow approval.");
            if (policy.StandardWorkdayHours <= 0)
                throw new InvalidOperationException("A positive standard workday length is required to calculate excess short leave compensation.");

            request.CompensationType = excessAction;

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

        return ShortLeaveCommon.MapToDto(request, request.Employee?.NameLocal);
    }
}
