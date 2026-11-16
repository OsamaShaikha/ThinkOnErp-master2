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

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.AddProrationPolicy;

public sealed record AddProrationPolicyCommand(
    ProrationPolicyDto Dto,
    string User
) : IRequest<ApiResponse<ProrationPolicy>>;

public sealed class AddProrationPolicyCommandValidator : AbstractValidator<AddProrationPolicyCommand>
{
    public AddProrationPolicyCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        When(x => x.Dto != null, () =>
        {
            RuleFor(x => x.Dto.CompanyId).GreaterThan(0);
            RuleFor(x => x.Dto.Code).NotEmpty();
            RuleFor(x => x.Dto.Method)
                .Must(m => m != null && (m.Trim().ToUpperInvariant() is "CALENDAR_DAYS" or "FIXED_30" or "WORKING_DAYS"))
                .WithMessage("Proration method must be CALENDAR_DAYS, FIXED_30, or WORKING_DAYS.");
        });
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class AddProrationPolicyCommandHandler : IRequestHandler<AddProrationPolicyCommand, ApiResponse<ProrationPolicy>>
{
    private readonly IPolicyRepository _policyRepo;
    private readonly IHrUnitOfWork? _unitOfWork;

    public AddProrationPolicyCommandHandler(
        IPolicyRepository policyRepo,
        IHrUnitOfWork? unitOfWork = null)
    {
        _policyRepo = policyRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<ProrationPolicy>> Handle(AddProrationPolicyCommand request, CancellationToken cancellationToken)
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
        var method = dto.Method?.Trim().ToUpperInvariant();
        var allocation = dto.SalarySegmentAllocation?.Trim().ToUpperInvariant() ?? "UNCONFIGURED";
        if (method is not ("CALENDAR_DAYS" or "FIXED_30" or "WORKING_DAYS"))
            throw new ArgumentException("Proration method must be CALENDAR_DAYS, FIXED_30, or WORKING_DAYS.");
        if (allocation is not ("UNCONFIGURED" or "CALENDAR_WEIGHTED" or "WORKING_WEIGHTED"))
            throw new ArgumentException("Salary segment allocation must be UNCONFIGURED, CALENDAR_WEIGHTED, or WORKING_WEIGHTED.");

        var policy = new ProrationPolicy
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            EffectiveFrom = dto.EffectiveFrom.Date,
            EffectiveTo = dto.EffectiveTo?.Date,
            Method = method,
            SalarySegmentAllocation = allocation,
            IsDefault = dto.IsDefault,
            CreationUser = request.User,
            CreationDate = DateTime.UtcNow
        };

        Func<Task> write = async () =>
        {
            await _policyRepo.AddProrationPolicyAsync(policy, cancellationToken);
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

        return ApiResponse<ProrationPolicy>.CreateSuccess(policy, "Proration policy added.");
    }
}
