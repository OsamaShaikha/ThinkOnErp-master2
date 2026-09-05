using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class StatutoryReportingService : IStatutoryReportingService
{
    private readonly IPayrollRepository _payrollRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<StatutoryReportingService> _logger;

    public StatutoryReportingService(
        IPayrollRepository payrollRepository,
        IEmployeeRepository employeeRepository,
        ILogger<StatutoryReportingService> logger)
    {
        _payrollRepository = payrollRepository ?? throw new ArgumentNullException(nameof(payrollRepository));
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SscMonthlyReturnDto> GenerateSscMonthlyReturnAsync(string payPeriod, long? branchId = null)
    {
        var runs = await _payrollRepository.GetAllRunsAsync(payPeriod, branchId);
        var activeRun = runs.FirstOrDefault(r => r.Status != "DRAFT");
        if (activeRun == null)
        {
            throw new HrNotFoundException($"لا يوجد مسير رواتب محتسب لشهر ({payPeriod}).", "PAYROLL_RUN_NOT_FOUND");
        }

        var fullRun = await _payrollRepository.GetRunByIdAsync(activeRun.Id, includeLines: true);
        var lines = fullRun?.Lines ?? new();

        var result = new SscMonthlyReturnDto
        {
            PayPeriod = payPeriod,
            CompanyRegistrationNumber = "JO-SSC-998877",
            CompanyName = "ThinkOn ERP Tenant Company",
            TotalEmployees = lines.Count,
            TotalEligibleGross = lines.Sum(l => l.SscEligibleSalary),
            TotalEmployeeContribution = lines.Sum(l => l.SscEmployeeContribution),
            TotalEmployerContribution = lines.Sum(l => l.SscEmployerContribution),
            TotalPayableToSsc = lines.Sum(l => l.SscEmployeeContribution + l.SscEmployerContribution),
            Lines = lines.Select(l => new SscEmployeeReturnLineDto
            {
                EmployeeCode = l.EmployeeCode,
                NationalId = l.Employee?.NationalId ?? string.Empty,
                SscNumber = l.Employee?.SscNumber ?? string.Empty,
                EmployeeNameAr = l.Employee?.NameAr ?? string.Empty,
                GrossSalary = l.GrossSalary,
                SscEligibleSalary = l.SscEligibleSalary,
                EmployeeContribution = l.SscEmployeeContribution,
                EmployerContribution = l.SscEmployerContribution,
                TotalContribution = l.SscEmployeeContribution + l.SscEmployerContribution,
                IsHighRisk = l.Employee?.IsHighRiskRole ?? false
            }).ToList()
        };

        _logger.LogInformation("Generated SSC Form 1 Return for period {Period}: {Count} employees, total {Total} JOD", payPeriod, result.TotalEmployees, result.TotalPayableToSsc);
        return result;
    }

    public async Task<IstdMonthlyTaxStatementDto> GenerateIstdMonthlyTaxStatementAsync(string payPeriod, long? branchId = null)
    {
        var runs = await _payrollRepository.GetAllRunsAsync(payPeriod, branchId);
        var activeRun = runs.FirstOrDefault(r => r.Status != "DRAFT");
        if (activeRun == null)
        {
            throw new HrNotFoundException($"لا يوجد مسير رواتب محتسب لشهر ({payPeriod}).", "PAYROLL_RUN_NOT_FOUND");
        }

        var fullRun = await _payrollRepository.GetRunByIdAsync(activeRun.Id, includeLines: true);
        var lines = fullRun?.Lines ?? new();

        var result = new IstdMonthlyTaxStatementDto
        {
            PayPeriod = payPeriod,
            TaxNumber = "JO-ISTD-11223344",
            CompanyName = "ThinkOn ERP Tenant Company",
            TotalTaxableEmployees = lines.Count(l => l.TaxableGross > 0),
            TotalTaxableGross = lines.Sum(l => l.TaxableGross),
            TotalTaxWithheld = lines.Sum(l => l.IncomeTaxWithheld),
            TotalNationalSurchargeWithheld = lines.Sum(l => l.NationalContributionWithheld),
            TotalRemittancePayable = lines.Sum(l => l.IncomeTaxWithheld + l.NationalContributionWithheld),
            Lines = lines.Select(l => new IstdTaxEmployeeLineDto
            {
                EmployeeCode = l.EmployeeCode,
                NationalId = l.Employee?.NationalId ?? string.Empty,
                EmployeeNameAr = l.Employee?.NameAr ?? string.Empty,
                MonthlyGrossSalary = l.GrossSalary,
                MonthlyTaxableGross = l.TaxableGross,
                AnnualExemptions = l.AnnualExemptions,
                MonthlyTaxWithheld = l.IncomeTaxWithheld,
                MonthlyNationalContributionWithheld = l.NationalContributionWithheld
            }).ToList()
        };

        _logger.LogInformation("Generated ISTD Monthly Tax Statement for {Period}: Tax {Tax}, National Surcharge {Nat}", payPeriod, result.TotalTaxWithheld, result.TotalNationalSurchargeWithheld);
        return result;
    }

    public async Task<BankWpsFileDto> GenerateBankWpsPayrollFileAsync(string payPeriod, string companyAccountIban, string companyId, long? branchId = null)
    {
        var runs = await _payrollRepository.GetAllRunsAsync(payPeriod, branchId);
        var activeRun = runs.FirstOrDefault(r => r.Status != "DRAFT");
        if (activeRun == null)
        {
            throw new HrNotFoundException($"لا يوجد مسير رواتب محتسب لشهر ({payPeriod}).", "PAYROLL_RUN_NOT_FOUND");
        }

        var fullRun = await _payrollRepository.GetRunByIdAsync(activeRun.Id, includeLines: true);
        var lines = fullRun?.Lines.Where(l => l.NetPay > 0).ToList() ?? new();

        var valueDate = DateTime.UtcNow.ToString("yyyyMMdd");
        var totalAmount = lines.Sum(l => l.NetPay);

        // Standard CBJ / ACH WPS formatted salary dispatch text file
        var sb = new StringBuilder();
        // Header record: H,CompanyId,CompanyIBAN,ValueDate,PayPeriod,RecordCount,TotalAmount
        sb.AppendLine($"H,{companyId.Trim()},{companyAccountIban.Trim()},{valueDate},{payPeriod},{lines.Count},{totalAmount:F3}");

        // Detail records: D,EmployeeCode,NationalId,BeneficiaryName,BeneficiaryIBAN,BankCode,NetAmount,Currency
        foreach (var l in lines)
        {
            var iban = string.IsNullOrWhiteSpace(l.Iban) ? "NO_IBAN_PROVIDED" : l.Iban.Trim();
            var bankCode = string.IsNullOrWhiteSpace(l.BankCode) ? "CBJ" : l.BankCode.Trim();
            var name = string.IsNullOrWhiteSpace(l.Employee?.NameEn) ? l.EmployeeCode : l.Employee.NameEn;

            sb.AppendLine($"D,{l.EmployeeCode},{l.Employee?.NationalId ?? ""},{name},{iban},{bankCode},{l.NetPay:F3},JOD");
        }

        // Trailer record: T,RecordCount,TotalAmount
        sb.AppendLine($"T,{lines.Count},{totalAmount:F3}");

        return new BankWpsFileDto
        {
            PayPeriod = payPeriod,
            CompanyId = companyId,
            CompanyAccountIban = companyAccountIban,
            ValueDate = valueDate,
            TotalRecordCount = lines.Count,
            TotalDisbursementAmount = totalAmount,
            RawFileContent = sb.ToString(),
            SuggestedFileName = $"WPS_PAYROLL_{payPeriod}_{DateTime.UtcNow:yyyyMMddHHmm}.txt"
        };
    }

    public async Task<AnnualTaxCertificateDto> GenerateAnnualTaxCertificateAsync(string employeeCode, int taxYear)
    {
        var employee = await _employeeRepository.GetByCodeAsync(employeeCode);
        if (employee == null)
        {
            throw new HrNotFoundException($"الموظف ({employeeCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var payslips = await _payrollRepository.GetEmployeePayslipsAsync(employeeCode);
        var yearPayslips = payslips.Where(p => p.PayrollRun != null && p.PayrollRun.PayPeriod.StartsWith(taxYear.ToString())).ToList();

        var totalGross = yearPayslips.Sum(p => p.GrossSalary);
        var totalSsc = yearPayslips.Sum(p => p.SscEmployeeContribution);
        var totalTaxable = yearPayslips.Sum(p => p.TaxableGross);
        var totalTax = yearPayslips.Sum(p => p.IncomeTaxWithheld);
        var totalNational = yearPayslips.Sum(p => p.NationalContributionWithheld);
        var exemptions = yearPayslips.FirstOrDefault()?.AnnualExemptions ?? 9000m;

        return new AnnualTaxCertificateDto
        {
            TaxYear = taxYear,
            EmployeeCode = employeeCode,
            EmployeeNameAr = employee.NameAr,
            EmployeeNameEn = employee.NameEn,
            NationalId = employee.NationalId,
            SscNumber = employee.SscNumber ?? string.Empty,
            TotalAnnualGrossEarnings = totalGross,
            TotalAnnualSscDeducted = totalSsc,
            TotalAnnualTaxableIncome = totalTaxable,
            TotalPersonalExemptionsClaimed = exemptions,
            TotalAnnualIncomeTaxPaid = totalTax,
            TotalAnnualNationalContributionPaid = totalNational
        };
    }
}
