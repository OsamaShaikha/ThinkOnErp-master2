using System;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IPayrollCalculationEngine
{
    Task<PayrollRunLine> CalculateEmployeePayrollAsync(
        string employeeCode,
        string payPeriod,
        long payrollRunId,
        string currentUser);
}
