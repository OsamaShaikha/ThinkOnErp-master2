using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.SysSetting;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/syssettings")]
[Authorize(Policy = "AdminOnly")]
public class SysSettingsController : ControllerBase
{
    private readonly ISysSettingRepository _repo;
    private readonly ILogger<SysSettingsController> _logger;

    public SysSettingsController(ISysSettingRepository repo, ILogger<SysSettingsController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<SysSettingDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<SysSettingDto>>>> GetAll()
    {
        var settings = await _repo.GetAllAsync();
        var dtos = settings.Select(s => new SysSettingDto
        {
            SettingCode = s.SettingCode,
            SettingDesc = s.SettingDesc,
            SettingValue = s.SettingValue
        }).ToList();
        return Ok(ApiResponse<List<SysSettingDto>>.CreateSuccess(dtos, "Settings retrieved successfully", 200));
    }

    [HttpGet("{settingCode}")]
    [ProducesResponseType(typeof(ApiResponse<SysSettingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SysSettingDto>>> GetByCode(int settingCode)
    {
        var setting = await _repo.GetByCodeAsync(settingCode);
        if (setting == null)
            return NotFound(ApiResponse<SysSettingDto>.CreateFailure("Setting not found", statusCode: 404));

        var dto = new SysSettingDto
        {
            SettingCode = setting.SettingCode,
            SettingDesc = setting.SettingDesc,
            SettingValue = setting.SettingValue
        };
        return Ok(ApiResponse<SysSettingDto>.CreateSuccess(dto, "Setting retrieved successfully", 200));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SysSettingDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<SysSettingDto>>> Create([FromBody] CreateSysSettingDto dto)
    {
        var existing = await _repo.GetByCodeAsync(dto.SettingCode);
        if (existing != null)
            return BadRequest(ApiResponse<SysSettingDto>.CreateFailure("Setting with this code already exists", statusCode: 400));

        var entity = new Domain.Entities.SysSetting
        {
            SettingCode = dto.SettingCode,
            SettingDesc = dto.SettingDesc,
            SettingValue = dto.SettingValue
        };

        var created = await _repo.AddAsync(entity);
        var result = new SysSettingDto
        {
            SettingCode = created.SettingCode,
            SettingDesc = created.SettingDesc,
            SettingValue = created.SettingValue
        };

        return CreatedAtAction(nameof(GetByCode), new { settingCode = result.SettingCode },
            ApiResponse<SysSettingDto>.CreateSuccess(result, "Setting created successfully", 201));
    }

    [HttpPut("{settingCode}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Update(int settingCode, [FromBody] UpdateSysSettingDto dto)
    {
        var setting = await _repo.GetByCodeAsync(settingCode);
        if (setting == null)
            return NotFound(ApiResponse<object>.CreateFailure("Setting not found", statusCode: 404));

        setting.SettingDesc = dto.SettingDesc;
        setting.SettingValue = dto.SettingValue;

        await _repo.UpdateAsync(setting);
        return Ok(ApiResponse<object>.CreateSuccess(new { }, "Setting updated successfully", 200));
    }

    [HttpDelete("{settingCode}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int settingCode)
    {
        var setting = await _repo.GetByCodeAsync(settingCode);
        if (setting == null)
            return NotFound(ApiResponse<object>.CreateFailure("Setting not found", statusCode: 404));

        await _repo.DeleteAsync(settingCode);
        return Ok(ApiResponse<object>.CreateSuccess(new { }, "Setting deleted successfully", 200));
    }
}
