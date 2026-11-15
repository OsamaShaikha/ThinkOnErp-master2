using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.EndOfService.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Commands.CalculateSettlement;

public sealed record CalculateSettlementCommand(
    CreateEndOfServiceRequest Request,
    long CompanyId,
    string Username
) : IRequest<ApiResponse<EndOfServiceSettlementDto>>;

public sealed class CalculateSettlementCommandValidator : AbstractValidator<CalculateSettlementCommand>
{
    public CalculateSettlementCommandValidator()
    {
        RuleFor(x => x.Request).NotNull().WithMessage("Request body cannot be null");
        RuleFor(x => x.CompanyId).GreaterThan(0).WithMessage("A valid company ID is required for an end of service calculation.");
        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.EmployeeCode).NotEmpty().WithMessage("Employee code is required.");
        });
    }
}

public sealed class CalculateSettlementCommandHandler : IRequestHandler<CalculateSettlementCommand, ApiResponse<EndOfServiceSettlementDto>>
{
    private readonly IEndOfServiceRepository _eosRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IEndOfServiceCalculationEngine _engine;
    private readonly IPolicyRepository _policyRepository;

    public CalculateSettlementCommandHandler(
        IEndOfServiceRepository eosRepo,
        IEmployeeRepository employeeRepo,
        IEndOfServiceCalculationEngine engine,
        IPolicyRepository policyRepository)
    {
        _eosRepo = eosRepo;
        _employeeRepo = employeeRepo;
        _engine = engine;
        _policyRepository = policyRepository;
    }

    public async Task<ApiResponse<EndOfServiceSettlementDto>> Handle(CalculateSettlementCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request.Request);
        if (request.CompanyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.CompanyId), "A valid company ID is required for an end of service calculation.");
        if (string.IsNullOrWhiteSpace(request.Request.EmployeeCode))
            throw new ArgumentException("Employee code is required.", nameof(request.Request.EmployeeCode));

        var req = request.Request;
        var employee = await _employeeRepo.GetEmployeeByCodeAsync(req.EmployeeCode, ct)
            ?? throw new KeyNotFoundException($"Employee '{req.EmployeeCode}' not found.");

        var activeStructure = await _employeeRepo.GetActiveSalaryStructureAsync(employee.EmployeeCode, req.LastWorkingDate, ct)
            ?? throw new InvalidOperationException($"Employee '{req.EmployeeCode}' does not have an active salary structure to calculate end of service.");

        var policy = await _eosRepo.GetActivePolicyAsync(request.CompanyId, req.LastWorkingDate, ct)
            ?? await _eosRepo.GetActivePolicyAsync(request.CompanyId, ct)
            ?? new EndOfServicePolicy { CompanyId = request.CompanyId };
        if (policy.CompanyId != request.CompanyId)
            throw new InvalidOperationException("The end of service policy does not belong to the resolved tenant.");

        decimal leaveBalance = await _eosRepo.GetRemainingLeaveBalanceDaysAsync(employee.EmployeeCode, ct);
        decimal outstandingLoans = await _eosRepo.GetOutstandingLoanBalanceAsync(employee.EmployeeCode, ct);
        decimal outstandingAdvances = await _eosRepo.GetOutstandingAdvanceBalanceAsync(employee.EmployeeCode, ct);

        int? currencyDecimals = null;
        var roundingRule = 1;
        var configuredDecimals = await _policyRepository.GetConfiguredPayrollCurrencyDecimalsAsync(request.CompanyId, ct);
        if (configuredDecimals.TryGetValue(SalaryPaymentMetadata.Normalize(activeStructure.CurrencyCode), out var configured))
            currencyDecimals = configured;
        if (employee.BranchId.HasValue)
        {
            roundingRule = await _policyRepository.GetBranchRoundingRulesAsync(employee.BranchId.Value, ct) ?? 1;
            if (roundingRule is < 1 or > 6)
                throw new InvalidOperationException($"Branch {employee.BranchId.Value} has an invalid payroll rounding rule.");
        }

        var settlement = _engine.Calculate(
            employee,
            activeStructure,
            policy,
            req.TerminationType,
            req.LastWorkingDate,
            req.NoticeDate,
            req.NoticeCompensationDays,
            req.OtherEntitlements,
            req.OtherDeductions,
            leaveBalance,
            outstandingLoans,
            outstandingAdvances,
            req.CalculationNotes,
            currencyDecimals,
            roundingRule
        );
        settlement.CompanyId = request.CompanyId;
        settlement.CreationUser = request.Username;
        settlement.CreationDate = DateTime.UtcNow;

        await _eosRepo.AddAsync(settlement, ct);
        await _eosRepo.SaveChangesAsync(ct);

        return ApiResponse<EndOfServiceSettlementDto>.CreateSuccess(
            EndOfServiceMappings.ToDto(settlement),
            "End of service settlement calculated successfully in DRAFT status.");
    }
}
