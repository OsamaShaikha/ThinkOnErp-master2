using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.SubmitShortLeave;

public sealed record SubmitShortLeaveCommand(CreateShortLeaveDto Dto, string User) : IRequest<ShortLeaveDto>;

public sealed class SubmitShortLeaveCommandValidator : AbstractValidator<SubmitShortLeaveCommand>
{
    public SubmitShortLeaveCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.EmployeeCode).NotEmpty();
        RuleFor(x => x.Dto.Reason).NotEmpty();
        RuleFor(x => x.Dto.CompanyId).GreaterThan(0);
        RuleFor(x => x.Dto.EndTime).GreaterThan(x => x.Dto.StartTime);
    }
}

public sealed class SubmitShortLeaveCommandHandler : IRequestHandler<SubmitShortLeaveCommand, ShortLeaveDto>
{
    private readonly IShortLeaveRepository _repo;
    private readonly IEmployeeRepository _employeeRepo;

    public SubmitShortLeaveCommandHandler(
        IShortLeaveRepository repo,
        IEmployeeRepository employeeRepo)
    {
        _repo = repo;
        _employeeRepo = employeeRepo;
    }

    public async Task<ShortLeaveDto> Handle(SubmitShortLeaveCommand request, CancellationToken ct)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
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
            var excessAction = ShortLeaveCommon.NormalizeExcessAction(policy.ExcessAction, invalidOperation: true);
            if (excessAction == "REJECT")
                throw new InvalidOperationException($"Monthly short leave quota of {policy.MonthlyPaidMinutesQuota} minutes would be exceeded by this request.");

            compensationType = excessAction;
        }

        var entity = new ShortLeaveRequest
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
            CreationUser = request.User,
            CreationDate = DateTime.UtcNow
        };

        await _repo.AddRequestAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);

        return ShortLeaveCommon.MapToDto(entity, employee.NameLocal);
    }
}
