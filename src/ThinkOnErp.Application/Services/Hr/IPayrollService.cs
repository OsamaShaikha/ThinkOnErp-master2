using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IPayrollService
{
    Task<List<PayrollRunDto>> GetAllRunsAsync(string? payPeriod = null, long? branchId = null, string? status = null);
    Task<PayrollRunDto?> GetRunByIdAsync(long id);
    Task<PayrollRunDto> CreateRunAsync(CreatePayrollRunDto dto, string currentUser);
    Task<PayrollRunDto> CalculateRunAsync(long id, string currentUser);
    Task<PayrollValidationResultDto> ValidateRunAsync(long id);
    Task<PayrollRunDto> SubmitForApprovalAsync(long id, string currentUser);
    Task<PayrollRunDto> ApproveRunAsync(long id, string currentUser);
    Task<PayrollRunDto> PostRunToGlAsync(long id, string currentUser);
    Task<PayrollRunDto> LockRunAsync(long id, string currentUser);
    Task<PayrollRunDto> ReverseRunAsync(long id, string reason, string currentUser);
    Task<PayrollRunDto> DisbursePayrollAsync(long id, string currentUser);

    Task<List<PayslipDto>> GetEmployeePayslipsAsync(string employeeCode, string? payPeriod = null);
    Task<PayslipDto?> GetEmployeePayslipForPeriodAsync(string employeeCode, string payPeriod);
}
