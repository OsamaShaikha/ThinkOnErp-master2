using System;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IPayrollValidationService
{
    Task<PayrollValidationResultDto> ValidatePayrollRunAsync(long payrollRunId);
}
