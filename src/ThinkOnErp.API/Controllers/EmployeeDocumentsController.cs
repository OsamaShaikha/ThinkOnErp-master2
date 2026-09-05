using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/hr")]
[ApiExplorerSettings(GroupName = ThinkOnErp.API.Swagger.ApiCategories.Hr)]
[TenantScoped]
[Authorize]
public sealed class EmployeeDocumentsController : ControllerBase
{
    private readonly IEmployeeDocumentService _documentService;

    public EmployeeDocumentsController(IEmployeeDocumentService documentService)
    {
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
    }

    /// <summary>
    /// Retrieves all documents uploaded for an employee.
    /// </summary>
    [HttpGet("employees/{employeeCode}/documents")]
    [ProducesResponseType(typeof(ApiResponse<List<EmployeeDocumentDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<EmployeeDocumentDto>>>> GetEmployeeDocuments(string employeeCode)
    {
        var list = await _documentService.GetByEmployeeCodeAsync(employeeCode);
        return Ok(ApiResponse<List<EmployeeDocumentDto>>.CreateSuccess(list, "Employee documents retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Uploads/attaches a compliance or personal document to an employee.
    /// </summary>
    [HttpPost("employees/{employeeCode}/documents")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDocumentDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<EmployeeDocumentDto>>> AddDocument(string employeeCode, [FromBody] CreateEmployeeDocumentDto dto)
    {
        var username = User.Identity?.Name ?? "SYSTEM";
        var created = await _documentService.AddDocumentAsync(employeeCode, dto, username);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<EmployeeDocumentDto>.CreateSuccess(created, "Employee document added successfully.", StatusCodes.Status201Created));
    }

    /// <summary>
    /// Scans and reports all documents expiring within a given threshold of days.
    /// </summary>
    [HttpGet("reports/documents-expiring-soon")]
    [ProducesResponseType(typeof(ApiResponse<List<DocumentExpiryReportDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<DocumentExpiryReportDto>>>> GetExpiringDocuments([FromQuery] int withinDays = 30)
    {
        var report = await _documentService.GetExpiringDocumentsAsync(withinDays);
        return Ok(ApiResponse<List<DocumentExpiryReportDto>>.CreateSuccess(report, $"Documents expiring within {withinDays} days retrieved successfully.", StatusCodes.Status200OK));
    }

    /// <summary>
    /// Deletes an employee document record.
    /// </summary>
    [HttpDelete("documents/{documentId:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDocument(long documentId)
    {
        var success = await _documentService.DeleteDocumentAsync(documentId);
        if (!success)
        {
            return NotFound(ApiResponse<object>.CreateFailure($"Document ({documentId}) not found.", statusCode: StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<object>.CreateSuccess(new { deleted = true }, "Employee document deleted successfully.", StatusCodes.Status200OK));
    }
}
