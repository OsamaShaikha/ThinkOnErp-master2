using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using System.Net;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Commands.DeactivateAttendancePolicy;

public sealed record DeactivateAttendancePolicyCommand(long Id, long CompanyId, string CurrentUser)
    : IRequest<ApiResponse<AttendancePolicy>>;

public sealed class DeactivateAttendancePolicyCommandValidator : AbstractValidator<DeactivateAttendancePolicyCommand>
{
    public DeactivateAttendancePolicyCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CompanyId).GreaterThan(0);
    }
}

public sealed class DeactivateAttendancePolicyCommandHandler
    : IRequestHandler<DeactivateAttendancePolicyCommand, ApiResponse<AttendancePolicy>>
{
    private readonly IPolicyRepository _policies;

    public DeactivateAttendancePolicyCommandHandler(IPolicyRepository policies)
    {
        _policies = policies;
    }

    public async Task<ApiResponse<AttendancePolicy>> Handle(DeactivateAttendancePolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = (await _policies.GetAttendancePoliciesAsync(request.CompanyId, cancellationToken))
            .SingleOrDefault(p => p.Id == request.Id);

        if (policy == null)
        {
            return ApiResponse<AttendancePolicy>.CreateFailure("Attendance policy not found.", statusCode: (int)HttpStatusCode.NotFound);
        }

        if (!policy.IsActive)
        {
            return ApiResponse<AttendancePolicy>.CreateFailure("Attendance policy is already inactive.", statusCode: (int)HttpStatusCode.Conflict);
        }

        var cutoff = policy.EffectiveFrom.Date >= DateTime.UtcNow.Date
            ? policy.EffectiveFrom.Date.AddDays(-1)
            : DateTime.UtcNow.Date.AddDays(-1);

        if (policy.EffectiveTo.HasValue && policy.EffectiveTo.Value.Date < cutoff)
        {
            cutoff = policy.EffectiveTo.Value.Date;
        }

        policy.EffectiveTo = cutoff;
        policy.IsActive = false;
        policy.UpdateUser = request.CurrentUser;
        policy.UpdateDate = DateTime.UtcNow;

        await _policies.SaveChangesAsync(cancellationToken);
        return ApiResponse<AttendancePolicy>.CreateSuccess(policy, "Attendance policy deactivated.");
    }
}
