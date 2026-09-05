using System;
using System.Threading.Tasks;

namespace ThinkOnErp.Application.Services.Hr;

public record ProrationResult(
    decimal ProrationFactor,
    decimal EligibleDays,
    decimal TotalBaseDays,
    string MethodUsed,
    string PolicyCode);

public interface IPayrollProrationService
{
    Task<ProrationResult> CalculateProrationFactorAsync(
        string employeeCode,
        DateTime hireDate,
        DateTime? terminationDate,
        DateTime periodStart,
        DateTime periodEnd,
        long companyId);
}
