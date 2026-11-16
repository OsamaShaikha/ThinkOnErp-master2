using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.AddDeductionPolicy;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.AddOvertimeRule;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.AddProrationPolicy;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.AddSSCPolicy;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.AddTaxPolicy;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.DeleteCurrencyPrecision;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.SetCurrencyPrecision;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetCurrencyPrecision;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetDeductionPolicies;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetOvertimeRules;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetProrationPolicies;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetSSCPolicies;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetTaxPolicies;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.API.Controllers.Hr;

[ApiController]
[Route("api/hr/payroll-policies")]
[ApiExplorerSettings(GroupName = ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class PayrollPoliciesController : HrControllerBase
{
    private readonly IMediator? _mediator;
    private readonly IPolicyRepository? _policyRepo;
    private readonly ILogger<PayrollPoliciesController>? _logger;
    private readonly IHrUnitOfWork? _unitOfWork;

    [ActivatorUtilitiesConstructor]
    public PayrollPoliciesController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [Obsolete("Transitional constructor for existing test suites. Production uses the IMediator constructor.")]
    public PayrollPoliciesController(
        IPolicyRepository policyRepo,
        ILogger<PayrollPoliciesController> logger,
        IHrUnitOfWork? unitOfWork = null)
    {
        _policyRepo = policyRepo ?? throw new ArgumentNullException(nameof(policyRepo));
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    [HttpGet("currency-precision")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PayrollCurrencyPrecisionDto>>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-policies", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PayrollCurrencyPrecisionDto>>>> GetCurrencyPrecision(
        [FromQuery] long companyId,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetCurrencyPrecisionQuery(companyId), cancellationToken));

        if (companyId <= 0) throw new ArgumentException("CompanyId must be positive.");
        var settings = await _policyRepo!.GetPayrollCurrencyPrecisionsAsync(companyId, cancellationToken);
        var result = settings.Select(x => new PayrollCurrencyPrecisionDto
        {
            CompanyId = x.CompanyId,
            CurrencyCode = x.CurrencyCode,
            DecimalPlaces = x.DecimalPlaces
        }).ToArray();
        return Ok(ApiResponse<IReadOnlyList<PayrollCurrencyPrecisionDto>>.CreateSuccess(result, "Payroll currency precision retrieved."));
    }

    [HttpPut("currency-precision")]
    [ProducesResponseType(typeof(ApiResponse<PayrollCurrencyPrecisionDto>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-policies", "edit")]
    public async Task<ActionResult<ApiResponse<PayrollCurrencyPrecisionDto>>> SetCurrencyPrecision(
        [FromBody] PayrollCurrencyPrecisionDto dto,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new SetCurrencyPrecisionCommand(dto, CurrentUsername), cancellationToken));

        var code = dto.CurrencyCode?.Trim().ToUpperInvariant();
        if (dto.CompanyId <= 0) throw new ArgumentException("CompanyId must be positive.");
        if (string.IsNullOrWhiteSpace(code) || code.Length > 10) throw new ArgumentException("CurrencyCode is required and cannot exceed 10 characters.");
        if (dto.DecimalPlaces is < 0 or > 4) throw new ArgumentException("DecimalPlaces must be between 0 and 4.");

        var now = DateTime.UtcNow;
        var precision = new PayrollCurrencyPrecision
        {
            CompanyId = dto.CompanyId,
            CurrencyCode = code,
            DecimalPlaces = dto.DecimalPlaces,
            CreationUser = CurrentUsername,
            CreationDate = now
        };
        await ExecutePolicyWriteAsync(dto.CompanyId, DateTime.MinValue.Date, DateTime.MaxValue.Date, async () =>
        {
            await _policyRepo!.SavePayrollCurrencyPrecisionAsync(precision, cancellationToken);
            await _policyRepo.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        dto.CurrencyCode = code;
        return Ok(ApiResponse<PayrollCurrencyPrecisionDto>.CreateSuccess(dto, "Payroll currency precision saved."));
    }

    [HttpDelete("currency-precision/{companyId:long}/{currencyCode}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-policies", "delete")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteCurrencyPrecision(
        long companyId,
        string currencyCode,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new DeleteCurrencyPrecisionCommand(companyId, currencyCode), cancellationToken));

        if (companyId <= 0) throw new ArgumentException("CompanyId must be positive.");
        var code = currencyCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 10) throw new ArgumentException("CurrencyCode is required and cannot exceed 10 characters.");
        var deleted = false;
        await ExecutePolicyWriteAsync(companyId, DateTime.MinValue.Date, DateTime.MaxValue.Date, async () =>
        {
            deleted = await _policyRepo!.DeletePayrollCurrencyPrecisionAsync(companyId, code, cancellationToken);
            if (deleted) await _policyRepo.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        return Ok(ApiResponse<bool>.CreateSuccess(deleted, deleted ? "Payroll currency precision removed." : "No payroll currency precision override exists."));
    }

    [HttpGet("overtime")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OvertimeRule>>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-policies", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OvertimeRule>>>> GetOvertimeRules(
        [FromQuery] long companyId,
        [FromQuery] DateTime? effectiveDate,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetOvertimeRulesQuery(companyId, effectiveDate), cancellationToken));

        if (companyId <= 0) throw new ArgumentException("CompanyId must be positive.");
        var result = await _policyRepo!.GetActiveOvertimeRulesAsync(companyId, effectiveDate?.Date ?? DateTime.UtcNow.Date, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OvertimeRule>>.CreateSuccess(result, "Overtime rules retrieved."));
    }

    [HttpPost("overtime")]
    [ProducesResponseType(typeof(ApiResponse<OvertimeRule>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-policies", "create")]
    public async Task<ActionResult<ApiResponse<OvertimeRule>>> AddOvertimeRule(
        [FromBody] OvertimeRuleDto dto,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new AddOvertimeRuleCommand(dto, CurrentUsername), cancellationToken));

        if (dto.CompanyId <= 0) throw new ArgumentException("CompanyId must be positive.");
        if (string.IsNullOrWhiteSpace(dto.Code)) throw new ArgumentException("Overtime rule code is required.");
        if (dto.Code.Trim().Length > 50) throw new ArgumentException("Overtime rule code cannot exceed 50 characters.");
        if (string.IsNullOrWhiteSpace(dto.NameAr) || dto.NameAr.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(dto.NameEn) || dto.NameEn.Trim().Length > 200)
            throw new ArgumentException("Overtime rule names are required and cannot exceed 200 characters.");
        if (dto.EffectiveTo.HasValue && dto.EffectiveTo.Value.Date < dto.EffectiveFrom.Date)
            throw new ArgumentException("EffectiveTo cannot be earlier than EffectiveFrom.");

        var dayType = dto.DayType?.Trim().ToUpperInvariant();
        if (dayType is not ("REGULAR" or "WEEKEND" or "HOLIDAY"))
            throw new ArgumentException("DayType must be REGULAR, WEEKEND, or HOLIDAY.");
        if (dto.Multiplier <= 0m || dto.Multiplier > 999.99m)
            throw new ArgumentException("Overtime multiplier must be positive and fit the configured 5,2 precision.");
        if (dto.MinimumMinutes < 0 || dto.MaximumMinutes <= 0 || dto.MaximumMinutes < dto.MinimumMinutes)
            throw new ArgumentException("Overtime minute range is invalid.");
        if (!OvertimeCalculationService.TryParseMonthlyHoursDivisor(dto.HourlyDivisorFormula, out var hourlyDivisor))
            throw new ArgumentException("HourlyDivisorFormula must use MONTHLY_SALARY / a positive numeric divisor.");
        var normalizedFormula = $"MONTHLY_SALARY / {hourlyDivisor.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (normalizedFormula.Length > 50)
            throw new ArgumentException("HourlyDivisorFormula cannot exceed 50 characters.");

        var rule = new OvertimeRule
        {
            CompanyId = dto.CompanyId,
            Code = dto.Code.Trim(),
            NameAr = dto.NameAr?.Trim() ?? string.Empty,
            NameEn = dto.NameEn?.Trim() ?? string.Empty,
            EffectiveFrom = dto.EffectiveFrom.Date,
            EffectiveTo = dto.EffectiveTo?.Date,
            DayType = dayType,
            Multiplier = dto.Multiplier,
            MinimumMinutes = dto.MinimumMinutes,
            MaximumMinutes = dto.MaximumMinutes,
            HourlyDivisorFormula = normalizedFormula,
            RequiresApproval = dto.RequiresApproval,
            IsActive = true,
            CreationUser = CurrentUsername,
            CreationDate = DateTime.UtcNow
        };
        await ExecutePolicyWriteAsync(dto.CompanyId, dto.EffectiveFrom, dto.EffectiveTo, async () =>
        {
            await _policyRepo!.AddOvertimeRuleAsync(rule, cancellationToken);
            await _policyRepo.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        return Ok(ApiResponse<OvertimeRule>.CreateSuccess(rule, "Overtime rule added."));
    }

    [HttpGet("proration")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProrationPolicy>>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-policies", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProrationPolicy>>>> GetProrationPolicies(
        [FromQuery] long companyId,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetProrationPoliciesQuery(companyId), cancellationToken));

        var result = await _policyRepo!.GetProrationPoliciesAsync(companyId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProrationPolicy>>.CreateSuccess(result, "Proration policies retrieved."));
    }

    [HttpPost("proration")]
    [ProducesResponseType(typeof(ApiResponse<ProrationPolicy>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-policies", "create")]
    public async Task<ActionResult<ApiResponse<ProrationPolicy>>> AddProrationPolicy(
        [FromBody] ProrationPolicyDto dto,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new AddProrationPolicyCommand(dto, CurrentUsername), cancellationToken));

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
            CreationUser = CurrentUsername,
            CreationDate = DateTime.UtcNow
        };
        await ExecutePolicyWriteAsync(dto.CompanyId, dto.EffectiveFrom, dto.EffectiveTo, async () =>
        {
            await _policyRepo!.AddProrationPolicyAsync(policy, cancellationToken);
            await _policyRepo.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        return Ok(ApiResponse<ProrationPolicy>.CreateSuccess(policy, "Proration policy added."));
    }

    [HttpGet("ssc")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SSCPolicy>>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-policies", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SSCPolicy>>>> GetSSCPolicies(
        [FromQuery] long companyId,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetSSCPoliciesQuery(companyId), cancellationToken));

        var result = await _policyRepo!.GetSSCPoliciesAsync(companyId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SSCPolicy>>.CreateSuccess(result, "SSC policies retrieved."));
    }

    [HttpPost("ssc")]
    [ProducesResponseType(typeof(ApiResponse<SSCPolicy>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-policies", "create")]
    public async Task<ActionResult<ApiResponse<SSCPolicy>>> AddSSCPolicy(
        [FromBody] SSCPolicyDto dto,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new AddSSCPolicyCommand(dto, CurrentUsername), cancellationToken));

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
            CreationUser = CurrentUsername,
            CreationDate = DateTime.UtcNow
        };
        await ExecutePolicyWriteAsync(dto.CompanyId, dto.EffectiveFrom, dto.EffectiveTo, async () =>
        {
            await _policyRepo!.AddSSCPolicyAsync(policy, cancellationToken);
            await _policyRepo.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        return Ok(ApiResponse<SSCPolicy>.CreateSuccess(policy, "SSC policy added."));
    }

    [HttpGet("tax")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TaxPolicy>>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-policies", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TaxPolicy>>>> GetTaxPolicies(
        [FromQuery] long companyId,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetTaxPoliciesQuery(companyId), cancellationToken));

        var result = await _policyRepo!.GetTaxPoliciesAsync(companyId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TaxPolicy>>.CreateSuccess(result, "Tax policies retrieved."));
    }

    [HttpPost("tax")]
    [ProducesResponseType(typeof(ApiResponse<TaxPolicy>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-policies", "create")]
    public async Task<ActionResult<ApiResponse<TaxPolicy>>> AddTaxPolicy(
        [FromBody] TaxPolicyDto dto,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new AddTaxPolicyCommand(dto, CurrentUsername), cancellationToken));

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
            CreationUser = CurrentUsername,
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

        await ExecutePolicyWriteAsync(dto.CompanyId, dto.EffectiveFrom, dto.EffectiveTo, async () =>
        {
            await _policyRepo!.AddTaxPolicyAsync(policy, cancellationToken);
            await _policyRepo.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        return Ok(ApiResponse<TaxPolicy>.CreateSuccess(policy, "Tax policy added."));
    }

    [HttpGet("deductions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DeductionPolicy>>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-policies", "view")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DeductionPolicy>>>> GetDeductionPolicies(
        [FromQuery] long companyId,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new GetDeductionPoliciesQuery(companyId), cancellationToken));

        var result = await _policyRepo!.GetDeductionPoliciesAsync(companyId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DeductionPolicy>>.CreateSuccess(result, "Deduction policies retrieved."));
    }

    [HttpPost("deductions")]
    [ProducesResponseType(typeof(ApiResponse<DeductionPolicy>), StatusCodes.Status200OK)]
    [HrPermission("hr-payroll-policies", "create")]
    public async Task<ActionResult<ApiResponse<DeductionPolicy>>> AddDeductionPolicy(
        [FromBody] DeductionPolicyDto dto,
        CancellationToken cancellationToken)
    {
        if (_mediator != null)
            return FromResponse(await _mediator.Send(new AddDeductionPolicyCommand(dto, CurrentUsername), cancellationToken));

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
            CreationUser = CurrentUsername,
            CreationDate = DateTime.UtcNow
        };
        await ExecutePolicyWriteAsync(dto.CompanyId, dto.EffectiveFrom, dto.EffectiveTo, async () =>
        {
            await _policyRepo!.AddDeductionPolicyAsync(policy, cancellationToken);
            await _policyRepo.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        return Ok(ApiResponse<DeductionPolicy>.CreateSuccess(policy, "Deduction policy added."));
    }

    private Task ExecutePolicyWriteAsync(long companyId, DateTime effectiveFrom, DateTime? effectiveTo,
        Func<Task> write, CancellationToken cancellationToken)
    {
        if (_unitOfWork == null) return write();
        return _unitOfWork.ExecuteForCompanyPayrollDatesAsync(companyId, effectiveFrom.Date,
            effectiveTo?.Date ?? DateTime.MaxValue.Date, async () =>
            {
                await write();
                return true;
            }, cancellationToken);
    }
}
