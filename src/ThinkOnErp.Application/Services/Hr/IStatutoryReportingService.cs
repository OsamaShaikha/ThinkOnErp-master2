using System;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IStatutoryReportingService
{
    Task<SscMonthlyReturnDto> GenerateSscMonthlyReturnAsync(string payPeriod, long? branchId = null);
    Task<IstdMonthlyTaxStatementDto> GenerateIstdMonthlyTaxStatementAsync(string payPeriod, long? branchId = null);
    Task<BankWpsFileDto> GenerateBankWpsPayrollFileAsync(string payPeriod, string companyAccountIban, string companyId, long? branchId = null);
    Task<AnnualTaxCertificateDto> GenerateAnnualTaxCertificateAsync(string employeeCode, int taxYear);
}
