using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.SysCode;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// إدارة أكواد النظام والقوائم المنسدلة العامة (System Codes and Lookup Tables API)
/// </summary>
[ApiController]
[Route("api/syscodes")]
[ApiExplorerSettings(GroupName = ApiCategories.System)]
[Authorize]
public class SysCodesController : ControllerBase
{
    private readonly ISysCodeRepository _repo;
    private readonly ILogger<SysCodesController> _logger;

    public SysCodesController(ISysCodeRepository repo, ILogger<SysCodesController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>
    /// استرجاع كافة أكواد النظام في جدول SYS_CODE
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeDto>>>> GetAll()
    {
        var codes = await _repo.GetAllAsync();
        var dtos = codes.Select(c => new SysCodeDto
        {
            CodeMgr = c.CodeMgr,
            CodeMnr = c.CodeMnr,
            CodeLang = c.CodeLang,
            CodeDesc = c.CodeDesc,
            CodeValue = c.CodeValue,
            IsActive = c.IsActive,
            CreationUser = c.CreationUser,
            CreationDate = c.CreationDate,
            UpdateUser = c.UpdateUser,
            UpdateDate = c.UpdateDate
        }).ToList();
        return Ok(ApiResponse<List<SysCodeDto>>.CreateSuccess(dtos, "Codes retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع أرقام فئات الأكواد المتوفرة (Distinct CODE_MGR)
    /// </summary>
    [HttpGet("groups")]
    [ProducesResponseType(typeof(ApiResponse<List<int>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<int>>>> GetGroups()
    {
        var groups = await _repo.GetDistinctCodeMgrsAsync();
        return Ok(ApiResponse<List<int>>.CreateSuccess(groups, "Code groups retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع وفلترة تعريفات الأكواد (حسب اللغة، الفئة، الكود، أو الوصف)
    /// </summary>
    [HttpGet("definitions")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeDto>>>> GetDefinitions(
        [FromQuery] int? codeLang,
        [FromQuery] int? codeMgr,
        [FromQuery] int? codeMnr,
        [FromQuery] string? codeDesc)
    {
        var codes = await _repo.GetDefinitionsAsync(codeLang, codeMgr, codeMnr, codeDesc);
        var dtos = codes.Select(c => new SysCodeDto
        {
            CodeMgr = c.CodeMgr,
            CodeMnr = c.CodeMnr,
            CodeLang = c.CodeLang,
            CodeDesc = c.CodeDesc,
            CodeValue = c.CodeValue,
            IsActive = c.IsActive,
            CreationUser = c.CreationUser,
            CreationDate = c.CreationDate,
            UpdateUser = c.UpdateUser,
            UpdateDate = c.UpdateDate
        }).ToList();
        return Ok(ApiResponse<List<SysCodeDto>>.CreateSuccess(dtos, "Code definitions retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع كافة عناصر فئة معينة لتغذية القوائم المنسدلة في الواجهات (مثل: 32 للألوان، 14 للغات، 16 للأطراف)
    /// </summary>
    [HttpGet("groups/{codeMgr:int}")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeDto>>>> GetByGroup(
        [FromRoute] int codeMgr,
        [FromQuery] int? codeLang = null,
        [FromQuery] bool activeOnly = true)
    {
        var codes = activeOnly 
            ? await _repo.GetActiveByCodeMgrAsync(codeMgr) 
            : await _repo.GetByCodeMgrAsync(codeMgr);

        if (codeLang.HasValue)
        {
            codes = codes.Where(c => c.CodeLang == codeLang.Value).ToList();
        }

        var dtos = codes.Select(c => new SysCodeDto
        {
            CodeMgr = c.CodeMgr,
            CodeMnr = c.CodeMnr,
            CodeLang = c.CodeLang,
            CodeDesc = c.CodeDesc,
            CodeValue = c.CodeValue,
            IsActive = c.IsActive,
            CreationUser = c.CreationUser,
            CreationDate = c.CreationDate,
            UpdateUser = c.UpdateUser,
            UpdateDate = c.UpdateDate
        }).ToList();

        return Ok(ApiResponse<List<SysCodeDto>>.CreateSuccess(dtos, "Codes retrieved successfully", 200));
    }

    /// <summary>
    /// استرجاع كود محدد بواسطة المفتاح المركب (CODE_MGR, CODE_MNR, CODE_LANG)
    /// </summary>
    [HttpGet("groups/{codeMgr:int}/items/{codeMnr:int}/langs/{codeLang:int}")]
    [ProducesResponseType(typeof(ApiResponse<SysCodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SysCodeDto>>> GetById(int codeMgr, int codeMnr, int codeLang)
    {
        var code = await _repo.GetByCodeMgrAndCodeMnrAndCodeLangAsync(codeMgr, codeMnr, codeLang);
        if (code == null)
            return NotFound(ApiResponse<SysCodeDto>.CreateFailure("Code not found", statusCode: 404));

        var dto = new SysCodeDto
        {
            CodeMgr = code.CodeMgr,
            CodeMnr = code.CodeMnr,
            CodeLang = code.CodeLang,
            CodeDesc = code.CodeDesc,
            CodeValue = code.CodeValue,
            IsActive = code.IsActive,
            CreationUser = code.CreationUser,
            CreationDate = code.CreationDate,
            UpdateUser = code.UpdateUser,
            UpdateDate = code.UpdateDate
        };
        return Ok(ApiResponse<SysCodeDto>.CreateSuccess(dto, "Code retrieved successfully", 200));
    }

    /// <summary>
    /// إضافة كود جديد إلى جدول SYS_CODE (خاص بمدير النظام SuperAdmin)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<SysCodeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<SysCodeDto>>> Create([FromBody] CreateSysCodeDto dto)
    {
        var validationErrors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.CodeDesc))
            validationErrors.Add("Code description is required.");
        else if (dto.CodeDesc.Length > 1000)
            validationErrors.Add("Code description must not exceed 1000 characters.");

        if (string.IsNullOrWhiteSpace(dto.CodeValue))
            validationErrors.Add("Code value is required.");
        else if (dto.CodeValue.Length > 200)
            validationErrors.Add("Code value must not exceed 200 characters.");

        if (dto.IsActive is not (0 or 1))
            validationErrors.Add("IsActive must be either 0 or 1.");

        if (validationErrors.Count > 0)
        {
            return BadRequest(ApiResponse<SysCodeDto>.CreateFailure(
                "One or more validation errors occurred",
                validationErrors,
                400));
        }

        var existing = await _repo.GetByCodeMgrAndCodeMnrAndCodeLangAsync(dto.CodeMgr, dto.CodeMnr, dto.CodeLang);
        if (existing != null)
            return BadRequest(ApiResponse<SysCodeDto>.CreateFailure("Code with this key already exists", statusCode: 400));

        var entity = new Domain.Entities.SysCode
        {
            CodeMgr = dto.CodeMgr,
            CodeMnr = dto.CodeMnr,
            CodeLang = dto.CodeLang,
            CodeDesc = dto.CodeDesc,
            CodeValue = dto.CodeValue,
            IsActive = dto.IsActive,
            CreationUser = User.Identity?.Name ?? "system",
            CreationDate = DateTime.UtcNow
        };

        var created = await _repo.AddAsync(entity);
        var result = new SysCodeDto
        {
            CodeMgr = created.CodeMgr,
            CodeMnr = created.CodeMnr,
            CodeLang = created.CodeLang,
            CodeDesc = created.CodeDesc,
            CodeValue = created.CodeValue,
            IsActive = created.IsActive,
            CreationUser = created.CreationUser,
            CreationDate = created.CreationDate
        };

        return CreatedAtAction(nameof(GetById), new { codeMgr = result.CodeMgr, codeMnr = result.CodeMnr, codeLang = result.CodeLang },
            ApiResponse<SysCodeDto>.CreateSuccess(result, "Code created successfully", 201));
    }

    /// <summary>
    /// تعديل بيانات كود محدد في جدول SYS_CODE (خاص بمدير النظام SuperAdmin)
    /// </summary>
    [HttpPut("groups/{codeMgr:int}/items/{codeMnr:int}/langs/{codeLang:int}")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Update(int codeMgr, int codeMnr, int codeLang, [FromBody] UpdateSysCodeDto dto)
    {
        var code = await _repo.GetByCodeMgrAndCodeMnrAndCodeLangAsync(codeMgr, codeMnr, codeLang);
        if (code == null)
            return NotFound(ApiResponse<object>.CreateFailure("Code not found", statusCode: 404));

        code.CodeDesc = dto.CodeDesc;
        code.CodeValue = dto.CodeValue;
        code.IsActive = dto.IsActive;
        code.UpdateUser = User.Identity?.Name ?? "system";
        code.UpdateDate = DateTime.UtcNow;

        await _repo.UpdateAsync(code);
        return Ok(ApiResponse<object>.CreateSuccess(new { }, "Code updated successfully", 200));
    }

    /// <summary>
    /// حذف كود من جدول SYS_CODE (خاص بمدير النظام SuperAdmin)
    /// </summary>
    [HttpDelete("groups/{codeMgr:int}/items/{codeMnr:int}/langs/{codeLang:int}")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int codeMgr, int codeMnr, int codeLang)
    {
        var code = await _repo.GetByCodeMgrAndCodeMnrAndCodeLangAsync(codeMgr, codeMnr, codeLang);
        if (code == null)
            return NotFound(ApiResponse<object>.CreateFailure("Code not found", statusCode: 404));

        await _repo.DeleteAsync(codeMgr, codeMnr, codeLang);
        return Ok(ApiResponse<object>.CreateSuccess(new { }, "Code deleted successfully", 200));
    }
}
