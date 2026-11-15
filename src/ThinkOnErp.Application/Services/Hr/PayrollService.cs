using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.ApprovePayroll;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.CalculatePayroll;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.CancelPayroll;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.ClosePeriod;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.CorrectPayroll;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.ReopenPeriod;
using ThinkOnErp.Application.Features.Hr.Payroll.Commands.WithdrawPayrollHandoff;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.ComparePayrollRun;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetEmployeePayslip;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetMyPayslips;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollHandoffPaymentStatus;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollHandoffStatus;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollRunById;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPayrollRuns;
using ThinkOnErp.Application.Features.Hr.Payroll.Queries.GetPeriods;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IPayrollService
{
    Task<PayrollCalculationResultDto> CreateAndCalculatePayrollRunAsync(CreatePayrollRunDto dto, string user, CancellationToken cancellationToken = default);
    Task<PayrollCalculationResultDto> CreatePayrollCorrectionAsync(long payrollRunId, string reason, string user, CancellationToken cancellationToken = default);
    Task<PayrollRun> ApprovePayrollRunAsync(long payrollRunId, string user, CancellationToken cancellationToken = default);
    Task<PayrollCalculationResultDto> CancelPayrollRunAsync(long payrollRunId, CancelPayrollRunDto dto, string user, CancellationToken cancellationToken = default);
    Task<PayrollCalculationResultDto?> GetPayrollRunByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<PagedResultDto<PayrollRunListItemDto>> GetPayrollRunsAsync(
        long companyId, string? payPeriod, long? branchId, string? status, int pageIndex, int pageSize,
        CancellationToken cancellationToken = default);
    Task<PagedResultDto<PayslipSummaryDto>?> GetMyPayslipsAsync(
        long companyId, long userId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);
    Task<PayslipSummaryDto?> GetEmployeePayslipAsync(
        long companyId, long payrollRunId, string employeeCode, CancellationToken cancellationToken = default);
    Task<PayrollHandoffStatusDto?> GetPayrollHandoffStatusAsync(long id, CancellationToken cancellationToken = default);
    Task<PayrollHandoffPaymentStatusDto?> GetPayrollHandoffPaymentStatusAsync(long id, CancellationToken cancellationToken = default);
    Task<PayrollHandoffStatusDto?> WithdrawPayrollHandoffAsync(long id, string reason, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollPeriod>> GetPeriodsAsync(long companyId, int? year = null, CancellationToken cancellationToken = default);
    Task<PayrollPeriod> CreatePeriodAsync(PayrollPeriod period, CancellationToken cancellationToken = default);
    Task<PayrollPeriod> ClosePeriodAsync(long companyId, string periodCode, string reason, string user, CancellationToken cancellationToken = default);
    Task<PayrollPeriod> ReopenPeriodAsync(long companyId, string periodCode, string reason, string user, CancellationToken cancellationToken = default);
    Task<PayrollRunComparisonDto> ComparePayrollRunAsync(long payrollRunId, long? baselineRunId = null, CancellationToken cancellationToken = default);
}

[Obsolete("Inject IMediator and dispatch Features.Hr.Payroll commands/queries instead. This facade remains for backward-compatible consumers.")]
public sealed partial class PayrollService : IPayrollService
{
    private readonly IHrUnitOfWork? _unitOfWork;
    private readonly IPayrollInputSnapshotService? _inputSnapshots;
    private readonly IHrAccountingHandoffService? _accountingHandoffs;

    private readonly IPayrollPeriodRepository _payrollPeriodRepo;
    private readonly IPayrollCalculationEngine _calculationEngine;
    private readonly IPayrollValidationService _validationService;
    private readonly IEmployeeRepository? _employeeRepository;

    public PayrollService(
        IPayrollPeriodRepository payrollPeriodRepo,
        IPayrollCalculationEngine calculationEngine,
        IPayrollValidationService validationService,
        IHrUnitOfWork? unitOfWork = null,
        IPayrollInputSnapshotService? inputSnapshots = null,
        IHrAccountingHandoffService? accountingHandoffs = null,
        IEmployeeRepository? employeeRepository = null)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
        _calculationEngine = calculationEngine;
        _validationService = validationService;
        _unitOfWork = unitOfWork;
        _inputSnapshots = inputSnapshots;
        _accountingHandoffs = accountingHandoffs;
        _employeeRepository = employeeRepository;
    }

    public async Task<PayrollCalculationResultDto> CreateAndCalculatePayrollRunAsync(CreatePayrollRunDto dto, string user, CancellationToken cancellationToken = default)
    {
        var handler = new CalculatePayrollCommandHandler(_payrollPeriodRepo, _calculationEngine, _validationService, _unitOfWork, _inputSnapshots);
        var response = await handler.Handle(new CalculatePayrollCommand(dto, user), cancellationToken);
        return response.Data!;
    }

    public async Task<PayrollCalculationResultDto> CreatePayrollCorrectionAsync(long payrollRunId, string reason, string user, CancellationToken cancellationToken = default)
    {
        var handler = new CorrectPayrollCommandHandler(_payrollPeriodRepo, _calculationEngine, _validationService, _unitOfWork, _inputSnapshots, _accountingHandoffs);
        var response = await handler.Handle(new CorrectPayrollCommand(payrollRunId, reason, user), cancellationToken);
        return response.Data!;
    }

    public async Task<PayrollRun> ApprovePayrollRunAsync(long payrollRunId, string user, CancellationToken cancellationToken = default)
    {
        var handler = new ApprovePayrollCommandHandler(_payrollPeriodRepo, _validationService, _unitOfWork, _inputSnapshots, _accountingHandoffs);
        var response = await handler.Handle(new ApprovePayrollCommand(payrollRunId, user), cancellationToken);
        return response.Data!;
    }

    public async Task<PayrollCalculationResultDto> CancelPayrollRunAsync(long payrollRunId, CancelPayrollRunDto dto, string user, CancellationToken cancellationToken = default)
    {
        var handler = new CancelPayrollCommandHandler(_payrollPeriodRepo, _unitOfWork);
        var response = await handler.Handle(new CancelPayrollCommand(payrollRunId, dto, user), cancellationToken);
        return response.Data!;
    }

    public async Task<PayrollCalculationResultDto?> GetPayrollRunByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var handler = new GetPayrollRunByIdQueryHandler(_payrollPeriodRepo);
        var response = await handler.Handle(new GetPayrollRunByIdQuery(id), cancellationToken);
        return response.Success ? response.Data : null;
    }

    public async Task<PagedResultDto<PayrollRunListItemDto>> GetPayrollRunsAsync(
        long companyId, string? payPeriod, long? branchId, string? status, int pageIndex, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var handler = new GetPayrollRunsQueryHandler(_payrollPeriodRepo);
        var response = await handler.Handle(new GetPayrollRunsQuery(companyId, payPeriod, branchId, status, pageIndex, pageSize), cancellationToken);
        return response.Data!;
    }

    public async Task<PagedResultDto<PayslipSummaryDto>?> GetMyPayslipsAsync(
        long companyId, long userId, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        var handler = new GetMyPayslipsQueryHandler(_payrollPeriodRepo, _employeeRepository);
        var response = await handler.Handle(new GetMyPayslipsQuery(companyId, userId, pageIndex, pageSize), cancellationToken);
        return response.Success ? response.Data : null;
    }

    public async Task<PayslipSummaryDto?> GetEmployeePayslipAsync(
        long companyId, long payrollRunId, string employeeCode, CancellationToken cancellationToken = default)
    {
        var handler = new GetEmployeePayslipQueryHandler(_payrollPeriodRepo);
        var response = await handler.Handle(new GetEmployeePayslipQuery(companyId, payrollRunId, employeeCode), cancellationToken);
        return response.Success ? response.Data : null;
    }

    public async Task<PayrollHandoffStatusDto?> GetPayrollHandoffStatusAsync(long id, CancellationToken cancellationToken = default)
    {
        var handler = new GetPayrollHandoffStatusQueryHandler(_accountingHandoffs);
        var response = await handler.Handle(new GetPayrollHandoffStatusQuery(id), cancellationToken);
        return response.Success ? response.Data : null;
    }

    public async Task<PayrollHandoffPaymentStatusDto?> GetPayrollHandoffPaymentStatusAsync(long id, CancellationToken cancellationToken = default)
    {
        var handler = new GetPayrollHandoffPaymentStatusQueryHandler(_accountingHandoffs);
        var response = await handler.Handle(new GetPayrollHandoffPaymentStatusQuery(id), cancellationToken);
        return response.Success ? response.Data : null;
    }

    public async Task<PayrollHandoffStatusDto?> WithdrawPayrollHandoffAsync(long id, string reason, string user, CancellationToken cancellationToken = default)
    {
        var handler = new WithdrawPayrollHandoffCommandHandler(_accountingHandoffs);
        var response = await handler.Handle(new WithdrawPayrollHandoffCommand(id, reason, user), cancellationToken);
        return response.Success ? response.Data : null;
    }

    public async Task<IReadOnlyList<PayrollPeriod>> GetPeriodsAsync(long companyId, int? year = null, CancellationToken cancellationToken = default)
    {
        var handler = new GetPeriodsQueryHandler(_payrollPeriodRepo);
        var response = await handler.Handle(new GetPeriodsQuery(companyId, year), cancellationToken);
        return response.Data!;
    }

    public async Task<PayrollPeriod> CreatePeriodAsync(PayrollPeriod period, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(period);
        if (period.CompanyId <= 0 || string.IsNullOrWhiteSpace(period.PeriodCode))
            throw new ArgumentException("Company and period code are required.", nameof(period));
        var existing = await _payrollPeriodRepo.GetPeriodByCodeAsync(period.CompanyId, period.PeriodCode.Trim(), cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Payroll period '{period.PeriodCode}' already exists for company {period.CompanyId}.");
        period.PeriodCode = period.PeriodCode.Trim();
        await _payrollPeriodRepo.AddPeriodAsync(period, cancellationToken);
        await _payrollPeriodRepo.SaveChangesAsync(cancellationToken);
        return period;
    }

    public async Task<PayrollPeriod> ClosePeriodAsync(long companyId, string periodCode, string reason, string user, CancellationToken cancellationToken = default)
    {
        var handler = new ClosePeriodCommandHandler(_payrollPeriodRepo, _unitOfWork);
        var response = await handler.Handle(new ClosePeriodCommand(companyId, periodCode, reason, user), cancellationToken);
        return response.Data!;
    }

    public async Task<PayrollPeriod> ReopenPeriodAsync(long companyId, string periodCode, string reason, string user, CancellationToken cancellationToken = default)
    {
        var handler = new ReopenPeriodCommandHandler(_payrollPeriodRepo, _unitOfWork);
        var response = await handler.Handle(new ReopenPeriodCommand(companyId, periodCode, reason, user), cancellationToken);
        return response.Data!;
    }

    public async Task<PayrollRunComparisonDto> ComparePayrollRunAsync(long payrollRunId, long? baselineRunId = null, CancellationToken cancellationToken = default)
    {
        var handler = new ComparePayrollRunQueryHandler(_payrollPeriodRepo);
        var response = await handler.Handle(new ComparePayrollRunQuery(payrollRunId, baselineRunId), cancellationToken);
        return response.Data!;
    }
}
