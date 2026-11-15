using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Commands.DeactivateEndOfServicePolicy;

public sealed record DeactivateEndOfServicePolicyCommand(
    long Id,
    long CompanyId,
    string Username
) : IRequest<ApiResponse<EndOfServicePolicy>>;

public sealed class DeactivateEndOfServicePolicyCommandValidator : AbstractValidator<DeactivateEndOfServicePolicyCommand>
{
    public DeactivateEndOfServicePolicyCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CompanyId).GreaterThan(0);
        RuleFor(x => x.Username).NotEmpty();
    }
}

public sealed class DeactivateEndOfServicePolicyCommandHandler : IRequestHandler<DeactivateEndOfServicePolicyCommand, ApiResponse<EndOfServicePolicy>>
{
    private readonly IEndOfServiceRepository _policies;

    public DeactivateEndOfServicePolicyCommandHandler(IEndOfServiceRepository policies)
    {
        _policies = policies;
    }

    public async Task<ApiResponse<EndOfServicePolicy>> Handle(DeactivateEndOfServicePolicyCommand request, CancellationToken ct)
    {
        var policy = (await _policies.GetPoliciesAsync(request.CompanyId, ct)).SingleOrDefault(p => p.Id == request.Id);
        if (policy == null)
            return ApiResponse<EndOfServicePolicy>.CreateFailure("End of service policy not found.", statusCode: 404);

        if (!policy.IsActive)
            return ApiResponse<EndOfServicePolicy>.CreateFailure("End of service policy is already inactive.", statusCode: 409);

        var cutoff = policy.EffectiveFrom.HasValue && policy.EffectiveFrom.Value.Date >= DateTime.UtcNow.Date
            ? policy.EffectiveFrom.Value.Date.AddDays(-1)
            : DateTime.UtcNow.Date.AddDays(-1);

        if (policy.EffectiveTo.HasValue && policy.EffectiveTo.Value.Date < cutoff)
            cutoff = policy.EffectiveTo.Value.Date;

        policy.EffectiveTo = cutoff;
        policy.IsActive = false;
        policy.UpdateUser = request.Username;
        policy.UpdateDate = DateTime.UtcNow;

        await _policies.SavePolicyChangesAsync(ct);

        return ApiResponse<EndOfServicePolicy>.CreateSuccess(policy, "End of service policy deactivated.");
    }
}
