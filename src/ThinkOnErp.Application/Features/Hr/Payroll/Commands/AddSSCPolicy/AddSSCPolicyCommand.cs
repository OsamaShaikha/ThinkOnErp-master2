using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.AddSSCPolicy;

public sealed record AddSSCPolicyCommand(
    SSCPolicyDto Dto,
    string User
) : IRequest<ApiResponse<SSCPolicy>>;

public sealed class AddSSCPolicyCommandValidator : AbstractValidator<AddSSCPolicyCommand>
{
    public AddSSCPolicyCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        When(x => x.Dto != null, () =>
        {
            RuleFor(x => x.Dto.CompanyId).GreaterThan(0);
            RuleFor(x => x.Dto.Code).NotEmpty();
            RuleFor(x => x.Dto.EmployeeContribRate).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Dto.EmployerContribRate).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Dto.HighRiskSurchargeRate).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Dto.MonthlyCeilingCap).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Dto.MinimumWageFloor).GreaterThanOrEqualTo(0);
        });
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class AddSSCPolicyCommandHandler : IRequestHandler<AddSSCPolicyCommand, ApiResponse<SSCPolicy>>
{
    private readonly IPolicyRepository _policyRepo;
    private readonly IHrUnitOfWork? _unitOfWork;

    public AddSSCPolicyCommandHandler(
        IPolicyRepository policyRepo,
        IHrUnitOfWork? unitOfWork = null)
    {
        _policyRepo = policyRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<SSCPolicy>> Handle(AddSSCPolicyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Dto);

        var dto = request.Dto;
        if (dto.CompanyId <= 0)
            throw new ArgumentException("CompanyId must be positive.");
        if (string.IsNullOrWhiteSpace(dto.Code))
            throw new ArgumentException("Policy code is required.");
        if (dto.EffectiveTo.HasValue && dto.EffectiveTo.Value.Date < dto.EffectiveFrom.Date)
            throw new ArgumentException("EffectiveTo cannot be earlier than EffectiveFrom.");
        if (dto.EmployeeContribRate < 0 || dto.EmployerContribRate < 0)
            throw new ArgumentException("Contribution rates must be non-negative.");
        if (dto.HighRiskSurchargeRate < 0)
            throw new ArgumentException("High risk surcharge rate must be non-negative.");
        if (dto.MonthlyCeilingCap < 0)
            throw new ArgumentException("Monthly ceiling cap must be non-negative.");
        if (dto.MinimumWageFloor < 0)
            throw new ArgumentException("Minimum wage floor must be non-negative.");

        var policy = new SSCPolicy
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            EffectiveFrom = dto.EffectiveFrom.Date,
            EffectiveTo = dto.EffectiveTo?.Date,
            EmployeeContribRate = dto.EmployeeContribRate,
            EmployerContribRate = dto.EmployerContribRate,
            HighRiskSurchargeRate = dto.HighRiskSurchargeRate,
            MonthlyCeilingCap = dto.MonthlyCeilingCap,
            MinimumWageFloor = dto.MinimumWageFloor,
            CreationUser = request.User,
            CreationDate = DateTime.UtcNow
        };

        Func<Task> write = async () =>
        {
            await _policyRepo.AddSSCPolicyAsync(policy, cancellationToken);
            await _policyRepo.SaveChangesAsync(cancellationToken);
        };

        if (_unitOfWork == null)
        {
            await write();
        }
        else
        {
            await _unitOfWork.ExecuteForCompanyPayrollDatesAsync(dto.CompanyId, dto.EffectiveFrom.Date, dto.EffectiveTo?.Date ?? DateTime.MaxValue.Date, async () =>
            {
                await write();
                return true;
            }, cancellationToken);
        }

        return ApiResponse<SSCPolicy>.CreateSuccess(policy, "SSC policy added.");
    }
}
