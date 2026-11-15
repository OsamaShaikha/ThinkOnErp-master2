using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.SaveShortLeavePolicy;

public sealed record SaveShortLeavePolicyCommand(ShortLeavePolicyDto Dto, string User) : IRequest<ShortLeavePolicyDto>;

public sealed class SaveShortLeavePolicyCommandValidator : AbstractValidator<SaveShortLeavePolicyCommand>
{
    public SaveShortLeavePolicyCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.CompanyId).GreaterThan(0);
        RuleFor(x => x.Dto.MonthlyPaidMinutesQuota).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Dto.MaxMinutesPerRequest).GreaterThan(0);
        RuleFor(x => x.Dto.StandardWorkdayHours).GreaterThan(0m);
    }
}

public sealed class SaveShortLeavePolicyCommandHandler : IRequestHandler<SaveShortLeavePolicyCommand, ShortLeavePolicyDto>
{
    private readonly IShortLeaveRepository _repo;

    public SaveShortLeavePolicyCommandHandler(IShortLeaveRepository repo)
    {
        _repo = repo;
    }

    public async Task<ShortLeavePolicyDto> Handle(SaveShortLeavePolicyCommand request, CancellationToken ct)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (dto.MonthlyPaidMinutesQuota < 0)
            throw new ArgumentException("Monthly paid quota minutes cannot be negative.");
        if (dto.MaxMinutesPerRequest <= 0)
            throw new ArgumentException("Max minutes per request must be greater than zero.");
        if (dto.StandardWorkdayHours <= 0)
            throw new ArgumentException("Standard workday hours must be greater than zero.");
        var excessAction = ShortLeaveCommon.NormalizeExcessAction(dto.ExcessAction, invalidOperation: false);

        var policy = await _repo.GetActivePolicyAsync(dto.CompanyId, ct)
            ?? new ShortLeavePolicy { CompanyId = dto.CompanyId, CreationUser = request.User, CreationDate = DateTime.UtcNow };

        policy.MonthlyPaidMinutesQuota = dto.MonthlyPaidMinutesQuota;
        policy.MaxMinutesPerRequest = dto.MaxMinutesPerRequest;
        policy.AllowMiddayExit = dto.AllowMiddayExit;
        policy.ExcessAction = excessAction;
        policy.LeaveTypeCodeForDeduction = dto.LeaveTypeCodeForDeduction;
        policy.StandardWorkdayHours = dto.StandardWorkdayHours;
        policy.IsActive = dto.IsActive;
        policy.UpdateUser = request.User;
        policy.UpdateDate = DateTime.UtcNow;

        await _repo.SavePolicyAsync(policy, ct);
        return ShortLeaveCommon.MapPolicyToDto(policy);
    }
}
