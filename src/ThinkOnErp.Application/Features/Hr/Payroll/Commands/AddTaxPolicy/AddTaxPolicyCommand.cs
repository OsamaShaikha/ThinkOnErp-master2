using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Commands.AddTaxPolicy;

public sealed record AddTaxPolicyCommand(
    TaxPolicyDto Dto,
    string User
) : IRequest<ApiResponse<TaxPolicy>>;

public sealed class AddTaxPolicyCommandValidator : AbstractValidator<AddTaxPolicyCommand>
{
    public AddTaxPolicyCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        When(x => x.Dto != null, () =>
        {
            RuleFor(x => x.Dto.CompanyId).GreaterThan(0);
            RuleFor(x => x.Dto.Code).NotEmpty();
            RuleFor(x => x.Dto.PersonalExemptionSelf).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Dto.PersonalExemptionDependent).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Dto.NationalContribRate).GreaterThanOrEqualTo(0);
        });
        RuleFor(x => x.User).NotEmpty();
    }
}

public sealed class AddTaxPolicyCommandHandler : IRequestHandler<AddTaxPolicyCommand, ApiResponse<TaxPolicy>>
{
    private readonly IPolicyRepository _policyRepo;
    private readonly IHrUnitOfWork? _unitOfWork;

    public AddTaxPolicyCommandHandler(
        IPolicyRepository policyRepo,
        IHrUnitOfWork? unitOfWork = null)
    {
        _policyRepo = policyRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<TaxPolicy>> Handle(AddTaxPolicyCommand request, CancellationToken cancellationToken)
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
        if (dto.PersonalExemptionSelf < 0 || dto.PersonalExemptionDependent < 0)
            throw new ArgumentException("Personal exemptions must be non-negative.");
        if (dto.NationalContribRate < 0)
            throw new ArgumentException("National contribution rate must be non-negative.");
        if (dto.Brackets != null)
        {
            foreach (var b in dto.Brackets)
            {
                if (b.LowerLimit < 0)
                    throw new ArgumentException("Bracket lower limit must be non-negative.");
                if (b.UpperLimit.HasValue && b.UpperLimit.Value < b.LowerLimit)
                    throw new ArgumentException("Bracket upper limit cannot be less than lower limit.");
                if (b.RatePercent < 0)
                    throw new ArgumentException("Bracket rate percentage must be non-negative.");
            }
        }

        var policy = new TaxPolicy
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            EffectiveFrom = dto.EffectiveFrom.Date,
            EffectiveTo = dto.EffectiveTo?.Date,
            PersonalExemptionSelf = dto.PersonalExemptionSelf,
            PersonalExemptionDependent = dto.PersonalExemptionDependent,
            NationalContribThreshold = dto.NationalContribThreshold,
            NationalContribRate = dto.NationalContribRate,
            IsSscTaxDeductible = dto.IsSscTaxDeductible,
            CalculationFrequency = dto.CalculationFrequency,
            CreationUser = request.User,
            CreationDate = DateTime.UtcNow
        };

        if (dto.Brackets != null)
        {
            foreach (var b in dto.Brackets)
            {
                policy.Brackets.Add(new TaxBracket
                {
                    BracketOrder = b.BracketOrder,
                    LowerLimit = b.LowerLimit,
                    UpperLimit = b.UpperLimit,
                    RatePercent = b.RatePercent,
                    Description = b.Description
                });
            }
        }

        Func<Task> write = async () =>
        {
            await _policyRepo.AddTaxPolicyAsync(policy, cancellationToken);
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

        return ApiResponse<TaxPolicy>.CreateSuccess(policy, "Tax policy added.");
    }
}
