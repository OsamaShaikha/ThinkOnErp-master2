using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Users API: Fast user lookup for cashier stations and order taker / waiter selection.
/// </summary>
[ApiController]
[Route("api/pos/users")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosUsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;

    public PosUsersController(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// Retrieves active users for POS cashier selection (Order Takers / Waiters / Staff).
    /// </summary>
    /// <param name="branchId">Optional filter by branch.</param>
    /// <param name="search">Optional keyword search matching user name or login name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active POS users.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosUserDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PosUserDto>>>> GetPosUsers(
        [FromQuery] long? branchId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        List<SysUser>? users = null;

        if (branchId.HasValue && branchId.Value > 0)
        {
            users = await _userRepository.GetByBranchIdAsync(branchId.Value);
        }

        // If no branch was specified or branch-specific search returned empty, fallback to all users
        if (users == null || !users.Any())
        {
            var allUsers = await _userRepository.GetAllAsync();
            if (branchId.HasValue && branchId.Value > 0)
            {
                users = allUsers.Where(u => !u.BranchId.HasValue || u.BranchId.Value == branchId.Value).ToList();
                if (!users.Any())
                {
                    users = allUsers;
                }
            }
            else
            {
                users = allUsers;
            }
        }

        var activeQuery = users.Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            activeQuery = activeQuery.Where(u =>
                (!string.IsNullOrWhiteSpace(u.FullNameLocal) && u.FullNameLocal.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(u.FullNameEn) && u.FullNameEn.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(u.UserName) && u.UserName.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var result = activeQuery.Select(u => new PosUserDto
        {
            Id = u.Id,
            UserName = u.UserName,
            FullNameLocal = u.FullNameLocal,
            FullNameEn = u.FullNameEn,
            Phone = u.Phone,
            Email = u.Email,
            BranchId = u.BranchId,
            RoleId = u.RoleId,
            IsActive = u.IsActive
        }).ToList();

        return Ok(ApiResponse<IReadOnlyList<PosUserDto>>.CreateSuccess(
            result,
            ResponseCodes.DataRetrieved,
            200));
    }

    /// <summary>
    /// Retrieves a specific active user by ID for POS cashier register.
    /// </summary>
    /// <param name="id">User ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>POS user details.</returns>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PosUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosUserDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PosUserDto>>> GetUserById(
        long id,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null || !user.IsActive)
        {
            return NotFound(ApiResponse<PosUserDto>.CreateFailure("User not found or inactive for POS.", null, 404));
        }

        var dto = new PosUserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            FullNameLocal = user.FullNameLocal,
            FullNameEn = user.FullNameEn,
            Phone = user.Phone,
            Email = user.Email,
            BranchId = user.BranchId,
            RoleId = user.RoleId,
            IsActive = user.IsActive
        };

        return Ok(ApiResponse<PosUserDto>.CreateSuccess(
            dto,
            ResponseCodes.DataRetrieved,
            200));
    }
}
