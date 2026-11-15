using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateOrUpdateLeavePolicy;

public sealed record CreateOrUpdateLeavePolicyCommand(CreateLeavePolicyDto Dto, string User) : IRequest<ApiResponse<LeavePolicyDetailsDto>>;

public sealed class CreateOrUpdateLeavePolicyCommandValidator : AbstractValidator<CreateOrUpdateLeavePolicyCommand>
{
    public CreateOrUpdateLeavePolicyCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.LeaveTypeCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Dto.PolicyName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dto.ApplicableTo).NotEmpty();
        RuleFor(x => x.Dto.AccrualMethod).NotEmpty();
        RuleFor(x => x.Dto.CycleBasis).NotEmpty();
    }
}

public sealed class CreateOrUpdateLeavePolicyCommandHandler : IRequestHandler<CreateOrUpdateLeavePolicyCommand, ApiResponse<LeavePolicyDetailsDto>>
{
    private readonly ILeaveRepository _leaveRepo;

    public CreateOrUpdateLeavePolicyCommandHandler(ILeaveRepository leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<ApiResponse<LeavePolicyDetailsDto>> Handle(CreateOrUpdateLeavePolicyCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (string.IsNullOrWhiteSpace(dto.LeaveTypeCode))
            throw new ArgumentException("Leave type code is required.", nameof(dto.LeaveTypeCode));
        if (dto.MinServiceMonths < 0)
            throw new ArgumentOutOfRangeException(nameof(dto.MinServiceMonths), "Minimum service months cannot be negative.");
        if (dto.AnnualEntitlementDays is <= 0m)
            throw new ArgumentOutOfRangeException(nameof(dto.AnnualEntitlementDays), "Annual entitlement must be greater than zero when provided.");
        if (dto.Tier1YearsThreshold < 0 || dto.Tier1Days < 0m || dto.Tier2Days < 0m || dto.AccrualRate < 0m)
            throw new ArgumentOutOfRangeException(nameof(dto), "Service thresholds, quotas, and accrual rate cannot be negative.");
        if (string.IsNullOrWhiteSpace(dto.ApplicableTo))
            throw new ArgumentException("Policy applicability is required.", nameof(dto.ApplicableTo));
        var applicableTo = dto.ApplicableTo.Trim().ToUpperInvariant();
        if (applicableTo is not ("ALL" or "GENDER_F" or "GENDER_M" or "CITIZENS"))
            throw new ArgumentException("ApplicableTo must be ALL, GENDER_F, GENDER_M, or CITIZENS.", nameof(dto.ApplicableTo));

        if (string.IsNullOrWhiteSpace(dto.AccrualMethod))
            throw new ArgumentException("Accrual method is required.", nameof(dto.AccrualMethod));
        if (string.IsNullOrWhiteSpace(dto.CycleBasis))
            throw new ArgumentException("Cycle basis is required.", nameof(dto.CycleBasis));
        var accrualMethod = dto.AccrualMethod.Trim().ToUpperInvariant();
        var prorateFirstCycle = dto.ProrateFirstCycle || accrualMethod == "PRO_RATA";
        if (accrualMethod == "PRO_RATA")
            accrualMethod = "MONTHLY";
        if (accrualMethod is not ("YEARLY" or "MONTHLY" or "DAILY" or "PRO_RATA"))
            throw new ArgumentException("Accrual method must be YEARLY, MONTHLY, or DAILY.", nameof(dto.AccrualMethod));

        var cycleBasis = dto.CycleBasis.Trim().ToUpperInvariant();
        if (cycleBasis is not ("CALENDAR_YEAR" or "COMPANY_LEAVE_YEAR" or "EMPLOYMENT_ANNIVERSARY"))
            throw new ArgumentException("Cycle basis must be CALENDAR_YEAR, COMPANY_LEAVE_YEAR, or EMPLOYMENT_ANNIVERSARY.", nameof(dto.CycleBasis));

        var normalizedLeaveTypeCode = dto.LeaveTypeCode.Trim().ToUpperInvariant();
        var leaveType = await _leaveRepo.GetLeaveTypeByCodeAsync(normalizedLeaveTypeCode, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave type '{normalizedLeaveTypeCode}' not found.");
        if (!leaveType.IsActive && dto.IsActive)
            throw new InvalidOperationException("An active leave policy cannot be assigned to an inactive leave type.");

        var policySettings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var companyToday = LeaveCommon.GetLeaveCompanyToday(policySettings);
        var effectiveFrom = dto.EffectiveFrom?.Date ?? companyToday;
        if (effectiveFrom < companyToday)
            throw new ArgumentException("A policy version cannot be backdated; use a prospective effective date.", nameof(dto.EffectiveFrom));

        var result = await _leaveRepo.ExecuteForLeavePolicyAsync(normalizedLeaveTypeCode, async () =>
        {
            var versions = await _leaveRepo.GetLeavePolicyVersionsAsync(normalizedLeaveTypeCode, cancellationToken);
            var latest = versions.OrderByDescending(p => p.EffectiveFrom).ThenByDescending(p => p.Id).FirstOrDefault();
            var today = companyToday;
            var defaultStart = latest == null ? today :
                latest.EffectiveTo.HasValue && latest.EffectiveTo.Value.Date.AddDays(1) > today.AddDays(1)
                    ? latest.EffectiveTo.Value.Date.AddDays(1)
                    : today.AddDays(1);
            var start = dto.EffectiveFrom?.Date ?? defaultStart;
            if (latest != null && start <= latest.EffectiveFrom.Date)
                throw new InvalidOperationException("A new policy version must start after the latest existing version.");
            if (latest != null && start <= today)
                throw new InvalidOperationException("A replacement policy version must take effect after today to protect earned balance.");
            if (latest?.EffectiveTo is DateTime latestEnd && latestEnd.Date >= start)
                throw new InvalidOperationException("The requested policy version overlaps the latest policy version.");
            if (versions.Where(version => latest == null || version.Id != latest.Id).Any(version => version.EffectiveFrom.Date <= start &&
                    (!version.EffectiveTo.HasValue || version.EffectiveTo.Value.Date >= start)))
                throw new InvalidOperationException("The requested policy version overlaps an existing version.");

            var policy = new LeavePolicy
            {
                PolicyName = dto.PolicyName,
                LeaveTypeCode = normalizedLeaveTypeCode,
                EffectiveFrom = start,
                AccrualMethod = accrualMethod,
                AccrualRate = dto.AccrualRate,
                AnnualEntitlementDays = dto.AnnualEntitlementDays,
                CycleBasis = cycleBasis,
                ProrateFirstCycle = prorateFirstCycle,
                ApplicableTo = applicableTo,
                MinServiceMonths = dto.MinServiceMonths,
                Tier1YearsThreshold = dto.Tier1YearsThreshold,
                Tier1Days = dto.Tier1Days,
                Tier2Days = dto.Tier2Days,
                IsActive = dto.IsActive,
                CreationUser = request.User,
                CreationDate = DateTime.UtcNow
            };

            if (latest != null)
            {
                if (!latest.EffectiveTo.HasValue)
                {
                    latest.EffectiveTo = start.AddDays(-1);
                    latest.UpdateUser = request.User;
                    latest.UpdateDate = DateTime.UtcNow;
                    await _leaveRepo.UpdateLeavePolicyAsync(latest, cancellationToken);
                }
            }
            await _leaveRepo.AddLeavePolicyAsync(policy, cancellationToken);
            await _leaveRepo.SaveChangesAsync(cancellationToken);
            return new LeavePolicyDetailsDto(policy.Id, policy.PolicyName, policy.LeaveTypeCode, policy.AccrualMethod,
                policy.AccrualRate, policy.ApplicableTo, policy.MinServiceMonths, policy.Tier1YearsThreshold,
                policy.Tier1Days, policy.Tier2Days, policy.IsActive, policy.AnnualEntitlementDays,
                policy.CycleBasis, policy.ProrateFirstCycle, policy.EffectiveFrom, policy.EffectiveTo);
        }, cancellationToken);

        return ApiResponse<LeavePolicyDetailsDto>.CreateSuccess(result, "Leave policy saved successfully.");
    }
}
