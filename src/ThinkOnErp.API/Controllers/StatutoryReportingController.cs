using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/hr/reports")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
[Produces("application/json")]
public sealed class StatutoryReportingController : ControllerBase
{
    private readonly IStatutoryReportingService _reportingService;

    public StatutoryReportingController(IStatutoryReportingService reportingService)
    {
        _reportingService = reportingService ?? throw new ArgumentNullException(nameof(reportingService));
    }

    [HttpGet("statutory/ssc-monthly")]
    [ProducesResponseType(typeof(SscMonthlyReturnDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SscMonthlyReturnDto>> GetSscMonthlyReturn(
        [FromQuery] string payPeriod,
        [FromQuery] long? branchId = null)
    {
        var report = await _reportingService.GenerateSscMonthlyReturnAsync(payPeriod, branchId);
        return Ok(report);
    }

    [HttpGet("statutory/istd-tax-monthly")]
    [ProducesResponseType(typeof(IstdMonthlyTaxStatementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IstdMonthlyTaxStatementDto>> GetIstdMonthlyTaxStatement(
        [FromQuery] string payPeriod,
        [FromQuery] long? branchId = null)
    {
        var report = await _reportingService.GenerateIstdMonthlyTaxStatementAsync(payPeriod, branchId);
        return Ok(report);
    }

    [HttpGet("statutory/wps-export")]
    [ProducesResponseType(typeof(BankWpsFileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BankWpsFileDto>> GetBankWpsPayrollFile(
        [FromQuery] string payPeriod,
        [FromQuery] string companyAccountIban,
        [FromQuery] string companyId = "THINKON_JO",
        [FromQuery] long? branchId = null)
    {
        var file = await _reportingService.GenerateBankWpsPayrollFileAsync(payPeriod, companyAccountIban, companyId, branchId);
        return Ok(file);
    }

    [HttpGet("statutory/wps-download")]
    [Produces("text/plain")]
    public async Task<IActionResult> DownloadBankWpsPayrollFile(
        [FromQuery] string payPeriod,
        [FromQuery] string companyAccountIban,
        [FromQuery] string companyId = "THINKON_JO",
        [FromQuery] long? branchId = null)
    {
        var file = await _reportingService.GenerateBankWpsPayrollFileAsync(payPeriod, companyAccountIban, companyId, branchId);
        var bytes = Encoding.UTF8.GetBytes(file.RawFileContent);
        return File(bytes, "text/plain", file.SuggestedFileName);
    }

    [HttpGet("statutory/tax-certificate/{employeeCode}/{taxYear:int}")]
    [ProducesResponseType(typeof(AnnualTaxCertificateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnualTaxCertificateDto>> GetAnnualTaxCertificate(string employeeCode, int taxYear)
    {
        var certificate = await _reportingService.GenerateAnnualTaxCertificateAsync(employeeCode, taxYear);
        return Ok(certificate);
    }
}
