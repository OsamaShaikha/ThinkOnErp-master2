using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.ApproveAdvance;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.ApproveLoan;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.CancelAdvance;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.CancelLoan;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.CreateAdvanceRequest;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.CreateLoanApplication;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.SettleAdvanceEarly;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.SettleLoanEarly;
using ThinkOnErp.Application.Features.Hr.Loans.Commands.SkipInstallment;
using ThinkOnErp.Application.Features.Hr.Loans.Common;
using ThinkOnErp.Application.Features.Hr.Loans.Queries.GetAdvances;
using ThinkOnErp.Application.Features.Hr.Loans.Queries.GetLoans;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public record LoanDeductionCalculationResult(
    decimal TotalScheduledDeduction,
    decimal TotalActualDeduction,
    decimal TotalCarriedForward,
    List<LoanRepaymentSchedule> UpdatedSchedules,
    List<EmployeeAdvance> UpdatedAdvances
);

public interface ILoanDeductionService
{
    Task<LoanDeductionCalculationResult> CalculateAndApplyDeductionsAsync(
        long? runLineId,
        string employeeCode,
        string payPeriod,
        decimal netPayBeforeLoans,
        DeductionPolicy? policy,
        bool persistChanges = false,
        CancellationToken cancellationToken = default,
        string? payrollCurrencyCode = null);

    Task<EmployeeLoan> CreateLoanApplicationAsync(CreateLoanApplicationDto dto, string user, CancellationToken cancellationToken = default);
    Task<EmployeeLoan> ApproveLoanAsync(long loanId, string user, CancellationToken cancellationToken = default);
    Task<EmployeeAdvance> CreateAdvanceRequestAsync(CreateAdvanceRequestDto dto, string user, CancellationToken cancellationToken = default);
    Task<EmployeeAdvance> ApproveAdvanceAsync(long advanceId, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeLoan>> GetLoansAsync(string? employeeCode, string? status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeAdvance>> GetAdvancesAsync(string? employeeCode, string? targetPayPeriod, CancellationToken cancellationToken = default);
    Task<LoanRepaymentSchedule> SkipInstallmentAsync(long scheduleId, string reason, string user, CancellationToken cancellationToken = default);
    Task<EmployeeLoan> CancelLoanAsync(long loanId, string reason, string user, CancellationToken cancellationToken = default);
    Task<EmployeeAdvance> CancelAdvanceAsync(long advanceId, string reason, string user, CancellationToken cancellationToken = default);
    Task<EmployeeLoan> SettleLoanEarlyAsync(long loanId, SettleLoanEarlyDto dto, string user, CancellationToken cancellationToken = default);
    Task<EmployeeAdvance> SettleAdvanceEarlyAsync(long advanceId, SettleAdvanceEarlyDto dto, string user, CancellationToken cancellationToken = default);
}

[Obsolete("Use the MediatR requests in ThinkOnErp.Application.Features.Hr.Loans. Kept for backwards compatibility.")]
public sealed class LoanDeductionService : ILoanDeductionService
{
    private readonly ILoanRepository _loanRepository;
    private readonly IHrUnitOfWork? _unitOfWork;
    private readonly IEmployeeRepository? _employeeRepository;

    public LoanDeductionService(ILoanRepository loanRepository, IHrUnitOfWork? unitOfWork = null, IEmployeeRepository? employeeRepository = null)
    {
        _loanRepository = loanRepository;
        _unitOfWork = unitOfWork;
        _employeeRepository = employeeRepository;
    }

    public Task<LoanDeductionCalculationResult> CalculateAndApplyDeductionsAsync(
        long? runLineId,
        string employeeCode,
        string payPeriod,
        decimal netPayBeforeLoans,
        DeductionPolicy? policy,
        bool persistChanges = false,
        CancellationToken cancellationToken = default,
        string? payrollCurrencyCode = null)
        => LoanCommon.CalculateAndApplyDeductionsCoreAsync(
            _loanRepository,
            runLineId,
            employeeCode,
            payPeriod,
            netPayBeforeLoans,
            policy,
            persistChanges,
            cancellationToken,
            payrollCurrencyCode);

    public async Task<EmployeeLoan> CreateLoanApplicationAsync(CreateLoanApplicationDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new CreateLoanApplicationCommandHandler(_loanRepository, _unitOfWork)
            .Handle(new CreateLoanApplicationCommand(dto, user), cancellationToken));

    public async Task<EmployeeLoan> ApproveLoanAsync(long loanId, string user, CancellationToken cancellationToken = default)
        => Data(await new ApproveLoanCommandHandler(_loanRepository, _employeeRepository)
            .Handle(new ApproveLoanCommand(loanId, user), cancellationToken));

    public async Task<EmployeeAdvance> CreateAdvanceRequestAsync(CreateAdvanceRequestDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new CreateAdvanceRequestCommandHandler(_loanRepository, _unitOfWork)
            .Handle(new CreateAdvanceRequestCommand(dto, user), cancellationToken));

    public async Task<EmployeeAdvance> ApproveAdvanceAsync(long advanceId, string user, CancellationToken cancellationToken = default)
        => Data(await new ApproveAdvanceCommandHandler(_loanRepository, _employeeRepository)
            .Handle(new ApproveAdvanceCommand(advanceId, user), cancellationToken));

    public async Task<IReadOnlyList<EmployeeLoan>> GetLoansAsync(string? employeeCode, string? status, CancellationToken cancellationToken = default)
        => Data(await new GetLoansQueryHandler(_loanRepository)
            .Handle(new GetLoansQuery(employeeCode, status), cancellationToken));

    public async Task<IReadOnlyList<EmployeeAdvance>> GetAdvancesAsync(string? employeeCode, string? targetPayPeriod, CancellationToken cancellationToken = default)
        => Data(await new GetAdvancesQueryHandler(_loanRepository)
            .Handle(new GetAdvancesQuery(employeeCode, targetPayPeriod), cancellationToken));

    public async Task<LoanRepaymentSchedule> SkipInstallmentAsync(long scheduleId, string reason, string user, CancellationToken cancellationToken = default)
        => Data(await new SkipInstallmentCommandHandler(_loanRepository)
            .Handle(new SkipInstallmentCommand(scheduleId, reason, user), cancellationToken));

    public async Task<EmployeeLoan> CancelLoanAsync(long loanId, string reason, string user, CancellationToken cancellationToken = default)
        => Data(await new CancelLoanCommandHandler(_loanRepository)
            .Handle(new CancelLoanCommand(loanId, reason, user), cancellationToken));

    public async Task<EmployeeAdvance> CancelAdvanceAsync(long advanceId, string reason, string user, CancellationToken cancellationToken = default)
        => Data(await new CancelAdvanceCommandHandler(_loanRepository)
            .Handle(new CancelAdvanceCommand(advanceId, reason, user), cancellationToken));

    public async Task<EmployeeLoan> SettleLoanEarlyAsync(long loanId, SettleLoanEarlyDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new SettleLoanEarlyCommandHandler(_loanRepository)
            .Handle(new SettleLoanEarlyCommand(loanId, dto, user), cancellationToken));

    public async Task<EmployeeAdvance> SettleAdvanceEarlyAsync(long advanceId, SettleAdvanceEarlyDto dto, string user, CancellationToken cancellationToken = default)
        => Data(await new SettleAdvanceEarlyCommandHandler(_loanRepository)
            .Handle(new SettleAdvanceEarlyCommand(advanceId, dto, user), cancellationToken));

    private static T Data<T>(ApiResponse<T> response) => response.Data!;
}
