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

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.AddDeductionPolicy;

public sealed record AddDeductionPolicyCommand(
    DeductionPolicyDto Dto,
    string User
) : IRequest<ApiResponse<DeductionPolicy>>;

public sealed class AddDeductionPolicyCommandValidator : AbstractValidator<AddDeductionPolicyCommand>
{
    public AddDeductionPolicyCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        When(x => x.Dto != null, () =>
        {
            RuleFor(x => x.Dto.CompanyId).GreaterThan(0);
            RuleFor(x => x.Dto.Code).NotEmpty();
            RuleFor(x => x.Dto.MaxDeductionPercentage).InclusiveBetween(0, 100);
            RuleFor(x => x.Dto.MinNetPayGuarantee).GreaterThanOrEqualTo(0);
        });
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class AddDeductionPolicyCommandHandler : IRequestHandler<AddDeductionPolicyCommand, ApiResponse<DeductionPolicy>>
{
    private readonly IPolicyRepository _policyRepo;
    private readonly IHrUnitOfWork? _unitOfWork;

    public AddDeductionPolicyCommandHandler(
        IPolicyRepository policyRepo,
        IHrUnitOfWork? unitOfWork = null)
    {
        _policyRepo = policyRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<DeductionPolicy>> Handle(AddDeductionPolicyCommand request, CancellationToken cancellationToken)
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
        if (dto.MaxDeductionPercentage < 0 || dto.MaxDeductionPercentage > 100)
            throw new ArgumentException("Max deduction percentage must be between 0 and 100.");
        if (dto.MinNetPayGuarantee < 0)
            throw new ArgumentException("Min net pay guarantee must be non-negative.");

        var policy = new DeductionPolicy
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            EffectiveFrom = dto.EffectiveFrom.Date,
            EffectiveTo = dto.EffectiveTo?.Date,
            MaxDeductionPercentage = dto.MaxDeductionPercentage,
            MinNetPayGuarantee = dto.MinNetPayGuarantee,
            AutoCapAndCarryForward = dto.AutoCapAndCarryForward,
            AllowNegativeNetPay = dto.AllowNegativeNetPay,
            IsDefault = dto.IsDefault,
            CreationUser = request.User,
            CreationDate = DateTime.UtcNow
        };

        Func<Task> write = async () =>
        {
            await _policyRepo.AddDeductionPolicyAsync(policy, cancellationToken);
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

        return ApiResponse<DeductionPolicy>.CreateSuccess(policy, "Deduction policy added.");
    }
}
