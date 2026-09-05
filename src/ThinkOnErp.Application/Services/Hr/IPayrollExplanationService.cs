using System;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IPayrollExplanationService
{
    Task<PayrollExplanationDto> GetPayrollExplanationAsync(string employeeCode, string payPeriod);
}
