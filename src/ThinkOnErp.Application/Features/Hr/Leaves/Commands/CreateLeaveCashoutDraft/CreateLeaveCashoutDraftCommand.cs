using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Leaves.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Leaves.Commands.CreateLeaveCashoutDraft;

public sealed record CreateLeaveCashoutDraftCommand(CreateLeaveCashoutSettlementDto Dto, string User) : IRequest<ApiResponse<LeaveCashoutSettlementDto>>;

public sealed class CreateLeaveCashoutDraftCommandValidator : AbstractValidator<CreateLeaveCashoutDraftCommand>
{
    public CreateLeaveCashoutDraftCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.LeaveYearCloseId).GreaterThan(0);
        RuleFor(x => x.Dto.SettlementKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dto.PayPeriod).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Dto.CurrencyCode).NotEmpty().MaximumLength(20);
    }
}

public sealed class CreateLeaveCashoutDraftCommandHandler : IRequestHandler<CreateLeaveCashoutDraftCommand, ApiResponse<LeaveCashoutSettlementDto>>
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IPolicyRepository? _policyRepository;

    public CreateLeaveCashoutDraftCommandHandler(
        ILeaveRepository leaveRepo,
        IEmployeeRepository employeeRepo,
        IPolicyRepository? policyRepository = null)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
        _policyRepository = policyRepository;
    }

    public async Task<ApiResponse<LeaveCashoutSettlementDto>> Handle(CreateLeaveCashoutDraftCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto ?? throw new ArgumentNullException(nameof(request.Dto));
        if (dto.LeaveYearCloseId <= 0) throw new ArgumentOutOfRangeException(nameof(dto.LeaveYearCloseId));
        var key = dto.SettlementKey?.Trim();
        var payPeriodCode = dto.PayPeriod?.Trim().ToUpperInvariant();
        var currencyCode = dto.CurrencyCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200) throw new ArgumentException("Settlement key is required and must not exceed 200 characters.", nameof(dto.SettlementKey));
        if (string.IsNullOrWhiteSpace(payPeriodCode) || payPeriodCode.Length > 40) throw new ArgumentException("Pay period is required.", nameof(dto.PayPeriod));
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length > 20 || currencyCode.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("Currency code must contain only ASCII letters or digits.", nameof(dto.CurrencyCode));

        var payload = $"{dto.LeaveYearCloseId}|{payPeriodCode.ToUpperInvariant()}|{currencyCode}";
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var existing = await _leaveRepo.GetLeaveCashoutSettlementByKeyAsync(key, cancellationToken);
        if (existing != null)
        {
            if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
                throw new InvalidOperationException("The settlement key is already used for a different close, pay period, or currency.");
            return ApiResponse<LeaveCashoutSettlementDto>.CreateSuccess(LeaveCommon.MapLeaveCashoutSettlement(existing), "Leave cashout settlement draft prepared for review.");
        }

        var settings = await _leaveRepo.GetHrLeaveSettingsAsync(cancellationToken);
        var basis = settings.LeaveCashoutWageBasisCode?.Trim().ToUpperInvariant();
        var divisor = settings.LeaveCashoutDayDivisor;
        var payComponent = settings.LeaveCashoutPayComponentCode?.Trim();
        var basisComponent = settings.LeaveCashoutBasisComponentCode?.Trim();
        if (basis is not ("BASIC" or "GROSS_EARNINGS" or "SALARY_COMPONENT") || divisor is null or <= 0m || string.IsNullOrWhiteSpace(payComponent))
            throw new InvalidOperationException("Configure the company leave cashout wage basis, day divisor, and payroll component before drafting a settlement.");
        var leaveClose = await _leaveRepo.GetLeaveYearCloseAsync(dto.LeaveYearCloseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Leave-year close '{dto.LeaveYearCloseId}' was not found in the current HR scope.");
        if (leaveClose.Status != "DRAFT") throw new InvalidOperationException("Prepare and approve the cashout settlement before posting the leave-year close.");
        var period = await _leaveRepo.GetPayrollPeriodForLeaveCashoutAsync(payPeriodCode, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll period was not found in the current company scope.");
        if (period.Status != "OPEN") throw new InvalidOperationException("Cashout settlement drafts require an open payroll period.");
        var configuredCurrencyDecimals = _policyRepository == null
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : await _policyRepository.GetConfiguredPayrollCurrencyDecimalsAsync(period.CompanyId, cancellationToken);
        var branchRoundingRules = new Dictionary<long, int?>();

        var settlement = new LeaveCashoutSettlement
        {
            SettlementKey = key,
            PayloadHash = payloadHash,
            LeaveYearCloseId = leaveClose.Id,
            PayPeriod = period.PeriodCode,
            WageBasisCode = basis,
            BasisComponentCode = basis == "SALARY_COMPONENT" ? basisComponent : null,
            DayDivisor = divisor.Value,
            PayComponentCode = payComponent,
            Status = "DRAFT",
            CreationUser = request.User,
            CreationDate = DateTime.UtcNow
        };

        foreach (var closeLine in leaveClose.Lines.Where(l => l.CashoutDays > 0m).OrderBy(l => l.EmployeeCode, StringComparer.Ordinal).ThenBy(l => l.LeaveTypeCode, StringComparer.Ordinal))
        {
            var structure = await _employeeRepo.GetActiveSalaryStructureAsync(closeLine.EmployeeCode, period.StartDate, cancellationToken);
            if (structure == null) throw new InvalidOperationException($"No active salary structure applies to {closeLine.EmployeeCode} at payroll period start.");
            var lineCurrency = structure.CurrencyCode.Trim().ToUpperInvariant();
            if (!string.Equals(lineCurrency, currencyCode, StringComparison.Ordinal)) continue;
            var employee = await _employeeRepo.GetEmployeeByCodeAsync(closeLine.EmployeeCode, cancellationToken)
                ?? throw new InvalidOperationException($"Employee '{closeLine.EmployeeCode}' no longer exists in the current HR scope.");

            int? roundingRule = null;
            if (_policyRepository != null && employee.BranchId.HasValue)
            {
                if (!branchRoundingRules.TryGetValue(employee.BranchId.Value, out roundingRule))
                {
                    roundingRule = await _policyRepository.GetBranchRoundingRulesAsync(employee.BranchId.Value, cancellationToken) ?? 1;
                    if (roundingRule is < 1 or > 6)
                        throw new InvalidOperationException($"Branch {employee.BranchId.Value} has an invalid payroll rounding rule.");
                    branchRoundingRules.Add(employee.BranchId.Value, roundingRule);
                }
            }
            configuredCurrencyDecimals.TryGetValue(lineCurrency, out var configuredDecimals);
            var currencyDecimals = PayrollCurrencyRules.GetCurrencyDecimals(lineCurrency,
                configuredCurrencyDecimals.ContainsKey(lineCurrency) ? configuredDecimals : null);

            var basic = structure.BasicSalary;
            var earnings = structure.Lines.Where(l => l.IsActive && (l.Component?.ComponentType?.Trim().ToUpperInvariant() ?? "ALLOWANCE") is "ALLOWANCE" or "EARNING")
                .Select(l => new
                {
                    l.ComponentCode,
                    Amount = PayrollCurrencyRules.Round(l.Percent is > 0m
                        ? basic * (l.Percent.Value / 100m)
                        : l.Amount, currencyDecimals, roundingRule)
                }).ToList();
            decimal wageBasisAmount;
            IReadOnlyList<object> basisSnapshot;
            if (basis == "BASIC")
            {
                wageBasisAmount = basic;
                basisSnapshot = [new { componentCode = "BASIC", amount = basic }];
            }
            else if (basis == "GROSS_EARNINGS")
            {
                wageBasisAmount = basic + earnings.Sum(e => e.Amount);
                basisSnapshot = new object[] { new { componentCode = "BASIC", amount = basic } }
                    .Concat(earnings.Select(e => (object)new { componentCode = e.ComponentCode, amount = e.Amount })).ToList();
            }
            else
            {
                var selected = earnings.FirstOrDefault(e => string.Equals(e.ComponentCode, basisComponent, StringComparison.OrdinalIgnoreCase));
                if (selected == null) throw new InvalidOperationException($"Salary component '{basisComponent}' has no active earning line for {closeLine.EmployeeCode}.");
                wageBasisAmount = selected.Amount;
                basisSnapshot = [new { componentCode = selected.ComponentCode, amount = selected.Amount }];
            }

            var cashoutAmount = PayrollCurrencyRules.Round(closeLine.CashoutDays * wageBasisAmount / divisor.Value,
                currencyDecimals, roundingRule);
            settlement.Lines.Add(new LeaveCashoutSettlementLine
            {
                LeaveYearCloseLineId = closeLine.Id,
                EmployeeCode = closeLine.EmployeeCode,
                LeaveTypeCode = closeLine.LeaveTypeCode,
                SalaryStructureId = structure.Id,
                SalaryEffectiveFrom = structure.EffectiveFrom,
                CashoutDays = closeLine.CashoutDays,
                WageBasisAmount = wageBasisAmount,
                DayDivisor = divisor.Value,
                CashoutAmount = cashoutAmount,
                CurrencyCode = lineCurrency,
                BasisSnapshotJson = JsonSerializer.Serialize(new { structureId = structure.Id, effectiveFrom = structure.EffectiveFrom, currency = lineCurrency, basis, components = basisSnapshot })
            });
        }
        if (settlement.Lines.Count == 0) throw new InvalidOperationException("The posted leave close has no cashout days for the selected currency.");

        var saved = await _leaveRepo.SaveLeaveCashoutSettlementDraftAsync(settlement, cancellationToken);
        var resultDto = LeaveCommon.MapLeaveCashoutSettlement(saved);
        return ApiResponse<LeaveCashoutSettlementDto>.CreateSuccess(resultDto, "Leave cashout settlement draft prepared for review.");
    }
}
