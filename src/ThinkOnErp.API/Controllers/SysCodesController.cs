using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.SysCode;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/syscodes")]
[Authorize(Policy = "AdminOnly")]
public class SysCodesController : ControllerBase
{
    private readonly ISysCodeRepository _repo;
    private readonly ILogger<SysCodesController> _logger;

    public SysCodesController(ISysCodeRepository repo, ILogger<SysCodesController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

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

    [HttpGet("groups")]
    [ProducesResponseType(typeof(ApiResponse<List<int>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<int>>>> GetGroups()
    {
        var groups = await _repo.GetDistinctCodeMgrsAsync();
        return Ok(ApiResponse<List<int>>.CreateSuccess(groups, "Code groups retrieved successfully", 200));
    }

    [HttpGet("groups/{codeMgr}")]
    [ProducesResponseType(typeof(ApiResponse<List<SysCodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysCodeDto>>>> GetByGroup(int codeMgr)
    {
        var codes = await _repo.GetByCodeMgrAsync(codeMgr);
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

    [HttpGet("groups/{codeMgr}/items/{codeMnr}/langs/{codeLang}")]
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

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SysCodeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<SysCodeDto>>> Create([FromBody] CreateSysCodeDto dto)
    {
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

    [HttpPut("groups/{codeMgr}/items/{codeMnr}/langs/{codeLang}")]
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

    [HttpDelete("groups/{codeMgr}/items/{codeMnr}/langs/{codeLang}")]
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
