using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class SscMonthlyReturnDto
{
    public string PayPeriod { get; set; } = string.Empty;
    public string CompanyRegistrationNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public int TotalEmployees { get; set; }
    public decimal TotalEligibleGross { get; set; }
    public decimal TotalEmployeeContribution { get; set; }
    public decimal TotalEmployerContribution { get; set; }
    public decimal TotalPayableToSsc { get; set; }
    public List<SscEmployeeReturnLineDto> Lines { get; set; } = new();
}

public sealed class SscEmployeeReturnLineDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string SscNumber { get; set; } = string.Empty;
    public string EmployeeNameAr { get; set; } = string.Empty;
    public decimal GrossSalary { get; set; }
    public decimal SscEligibleSalary { get; set; }
    public decimal EmployeeContribution { get; set; }
    public decimal EmployerContribution { get; set; }
    public decimal TotalContribution { get; set; }
    public bool IsHighRisk { get; set; }
}

public sealed class IstdMonthlyTaxStatementDto
{
    public string PayPeriod { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public int TotalTaxableEmployees { get; set; }
    public decimal TotalTaxableGross { get; set; }
    public decimal TotalTaxWithheld { get; set; }
    public decimal TotalNationalSurchargeWithheld { get; set; }
    public decimal TotalRemittancePayable { get; set; }
    public List<IstdTaxEmployeeLineDto> Lines { get; set; } = new();
}

public sealed class IstdTaxEmployeeLineDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string EmployeeNameAr { get; set; } = string.Empty;
    public decimal MonthlyGrossSalary { get; set; }
    public decimal MonthlyTaxableGross { get; set; }
    public decimal AnnualExemptions { get; set; }
    public decimal MonthlyTaxWithheld { get; set; }
    public decimal MonthlyNationalContributionWithheld { get; set; }
}

public sealed class BankWpsFileDto
{
    public string PayPeriod { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string CompanyAccountIban { get; set; } = string.Empty;
    public string ValueDate { get; set; } = string.Empty;
    public int TotalRecordCount { get; set; }
    public decimal TotalDisbursementAmount { get; set; }
    public string RawFileContent { get; set; } = string.Empty;
    public string SuggestedFileName { get; set; } = string.Empty;
}

public sealed class AnnualTaxCertificateDto
{
    public int TaxYear { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeNameAr { get; set; } = string.Empty;
    public string EmployeeNameEn { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string SscNumber { get; set; } = string.Empty;
    public decimal TotalAnnualGrossEarnings { get; set; }
    public decimal TotalAnnualSscDeducted { get; set; }
    public decimal TotalAnnualTaxableIncome { get; set; }
    public decimal TotalPersonalExemptionsClaimed { get; set; }
    public decimal TotalAnnualIncomeTaxPaid { get; set; }
    public decimal TotalAnnualNationalContributionPaid { get; set; }
}
