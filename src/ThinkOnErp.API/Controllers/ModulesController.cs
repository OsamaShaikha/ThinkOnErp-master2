using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Module;
using ThinkOnErp.Application.Features.Modules.Commands.CreateModule;
using ThinkOnErp.Application.Features.Modules.Commands.UpdateModule;
using ThinkOnErp.Application.Features.Modules.Commands.DeleteModule;
using ThinkOnErp.Application.Features.Modules.Queries.GetAllModules;
using ThinkOnErp.Application.Features.Modules.Queries.GetModuleById;

namespace ThinkOnErp.API.Controllers;

[ApiController]
[Route("api/modules")]
[Authorize(Policy = "SuperAdminOnly")]
public class ModulesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ModulesController> _logger;

    public ModulesController(IMediator mediator, ILogger<ModulesController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ModuleDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ModuleDto>>>> GetAllModules()
    {
        try
        {
            _logger.LogInformation("Retrieving all modules");
            var modules = await _mediator.Send(new GetAllModulesQuery());
            return Ok(ApiResponse<List<ModuleDto>>.CreateSuccess(modules, "Modules retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all modules");
            throw;
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ModuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ModuleDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ModuleDto>>> GetModuleById(long id)
    {
        try
        {
            _logger.LogInformation("Retrieving module with ID: {ModuleId}", id);
            var module = await _mediator.Send(new GetModuleByIdQuery { ModuleId = id });

            if (module == null)
            {
                return NotFound(ApiResponse<ModuleDto>.CreateFailure("No module found with the specified identifier", statusCode: 404));
            }

            return Ok(ApiResponse<ModuleDto>.CreateSuccess(module, "Module retrieved successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving module with ID: {ModuleId}", id);
            throw;
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<long>>> CreateModule([FromBody] CreateModuleCommand command)
    {
        try
        {
            command.CreationUser = User.Identity?.Name ?? "system";
            _logger.LogInformation("Creating new module: {ModuleCode}", command.ModuleCode);
            var moduleId = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetModuleById), new { id = moduleId },
                ApiResponse<long>.CreateSuccess(moduleId, "Module created successfully", 201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating module: {ModuleCode}", command.ModuleCode);
            throw;
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<long>>> UpdateModule(long id, [FromBody] UpdateModuleCommand command)
    {
        try
        {
            command.ModuleId = id;
            command.UpdateUser = User.Identity?.Name ?? "system";
            _logger.LogInformation("Updating module with ID: {ModuleId}", id);
            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                return NotFound(ApiResponse<long>.CreateFailure("No module found with the specified identifier", statusCode: 404));
            }

            return Ok(ApiResponse<long>.CreateSuccess(rowsAffected, "Module updated successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating module with ID: {ModuleId}", id);
            throw;
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<long>>> DeleteModule(long id)
    {
        try
        {
            var updateUser = User.Identity?.Name ?? "system";
            _logger.LogInformation("Deleting module with ID: {ModuleId}", id);
            var command = new DeleteModuleCommand { ModuleId = id, UpdateUser = updateUser };
            var rowsAffected = await _mediator.Send(command);

            if (rowsAffected == 0)
            {
                return NotFound(ApiResponse<long>.CreateFailure("No module found with the specified identifier", statusCode: 404));
            }

            return Ok(ApiResponse<long>.CreateSuccess(rowsAffected, "Module deleted successfully", 200));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting module with ID: {ModuleId}", id);
            throw;
        }
    }
}
