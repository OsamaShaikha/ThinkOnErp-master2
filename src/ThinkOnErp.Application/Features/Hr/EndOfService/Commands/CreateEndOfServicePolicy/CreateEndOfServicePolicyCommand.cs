using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.EndOfService.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Commands.CreateEndOfServicePolicy;

public sealed record CreateEndOfServicePolicyCommand(
    CreateEndOfServicePolicyDto Dto,
    long CompanyId,
    string Username
) : IRequest<ApiResponse<EndOfServicePolicy>>;

public sealed class CreateEndOfServicePolicyCommandValidator : AbstractValidator<CreateEndOfServicePolicyCommand>
{
    public CreateEndOfServicePolicyCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.CompanyId).GreaterThan(0);
        RuleFor(x => x.Username).NotEmpty();
    }
}

public sealed class CreateEndOfServicePolicyCommandHandler : IRequestHandler<CreateEndOfServicePolicyCommand, ApiResponse<EndOfServicePolicy>>
{
    private readonly IEndOfServiceRepository _policies;

    public CreateEndOfServicePolicyCommandHandler(IEndOfServiceRepository policies)
    {
        _policies = policies;
    }

    public async Task<ApiResponse<EndOfServicePolicy>> Handle(CreateEndOfServicePolicyCommand request, CancellationToken ct)
    {
        if (!EndOfServicePolicyValidator.Valid(request.Dto, out var error))
            return ApiResponse<EndOfServicePolicy>.CreateFailure(error!, statusCode: 400);

        var existing = await _policies.GetPoliciesAsync(request.CompanyId, ct);
        foreach (var prior in existing.Where(p => p.IsActive && !p.EffectiveTo.HasValue &&
                     (!p.EffectiveFrom.HasValue || p.EffectiveFrom.Value.Date < request.Dto.EffectiveFrom.Date)))
        {
            prior.EffectiveTo = request.Dto.EffectiveFrom.Date.AddDays(-1);
            prior.UpdateUser = request.Username;
            prior.UpdateDate = DateTime.UtcNow;
        }

        if (existing.Any(p => EndOfServicePolicyValidator.Overlaps(p.EffectiveFrom, p.EffectiveTo, request.Dto.EffectiveFrom, request.Dto.EffectiveTo)))
            return ApiResponse<EndOfServicePolicy>.CreateFailure("An end of service policy overlaps this effective period; choose a non-overlapping period.", statusCode: 409);

        var policy = new EndOfServicePolicy
        {
            CompanyId = request.CompanyId,
            PolicyName = request.Dto.PolicyName.Trim(),
            EffectiveFrom = request.Dto.EffectiveFrom,
            EffectiveTo = request.Dto.EffectiveTo,
            CalculationBasis = request.Dto.CalculationBasis.Trim().ToUpperInvariant(),
            LeaveDailyRateDivisor = request.Dto.LeaveDailyRateDivisor,
            NoticePeriodDays = request.Dto.NoticePeriodDays,
            ProrateFinalMonthByWorkingDays = request.Dto.ProrateFinalMonthByWorkingDays,
            ResignationFactorUnderTwoYears = request.Dto.ResignationFactorUnderTwoYears,
            ResignationFactorTwoToFiveYears = request.Dto.ResignationFactorTwoToFiveYears,
            ResignationFactorFiveToTenYears = request.Dto.ResignationFactorFiveToTenYears,
            ResignationFactorTenYearsAndAbove = request.Dto.ResignationFactorTenYearsAndAbove,
            FirstFiveYearsRatePerYear = request.Dto.FirstFiveYearsRatePerYear,
            SubsequentYearsRatePerYear = request.Dto.SubsequentYearsRatePerYear,
            IsActive = true,
            CreationUser = request.Username,
            CreationDate = DateTime.UtcNow
        };

        await _policies.AddPolicyAsync(policy, ct);
        await _policies.SavePolicyChangesAsync(ct);

        return ApiResponse<EndOfServicePolicy>.CreateSuccess(policy, "End of service policy version created.");
    }
}
