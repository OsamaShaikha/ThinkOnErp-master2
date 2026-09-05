using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public record LoanDeductionResult(
    decimal TotalDeductedAmount,
    decimal TotalCarriedForwardAmount,
    List<DeductedLoanItem> Items);

public record DeductedLoanItem(
    long LoanId,
    long? ScheduleId,
    string LoanType,
    decimal ScheduledAmount,
    decimal DeductedAmount,
    decimal CarriedForwardAmount);

public interface ILoanDeductionService
{
    Task<LoanDeductionResult> ProcessPeriodLoanDeductionsAsync(
        string employeeCode,
        string payPeriod,
        decimal disposableSalary,
        long companyId,
        DateTime calculationDate);

    Task<EmployeeLoanDto> CreateLoanAsync(CreateEmployeeLoanDto dto, string currentUser);
    Task<EmployeeLoanDto> ApproveLoanAsync(long id, string approvedBy);
    Task<EmployeeAdvanceDto> CreateAdvanceAsync(CreateEmployeeAdvanceDto dto, string currentUser);
    Task<EmployeeAdvanceDto> ApproveAdvanceAsync(long id, string approvedBy);
    Task<List<EmployeeLoanDto>> GetLoansAsync(string? employeeCode = null, string? status = null);
    Task<List<EmployeeAdvanceDto>> GetAdvancesAsync(string? employeeCode = null, string? payPeriod = null, string? status = null);
}
