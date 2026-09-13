using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosTillService
{
    Task<ApiResponse<IReadOnlyList<TillDto>>> GetTillsByBranchAsync(long branchId, bool? activeOnly = null, CancellationToken ct = default);
    Task<ApiResponse<TillDto>> GetTillByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<TillDto>> CreateTillAsync(CreateTillDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<TillDto>> UpdateTillAsync(long id, UpdateTillDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteTillAsync(long id, CancellationToken ct = default);
}

public class PosTillService : IPosTillService
{
    private readonly IPosTillRepository _tillRepository;

    public PosTillService(IPosTillRepository tillRepository)
    {
        _tillRepository = tillRepository;
    }

    public async Task<ApiResponse<IReadOnlyList<TillDto>>> GetTillsByBranchAsync(long branchId, bool? activeOnly = null, CancellationToken ct = default)
    {
        var tills = await _tillRepository.GetTillsByBranchAsync(branchId, activeOnly, ct);
        var dtos = tills.Select(t => MapToDto(t)).ToList();
        return ApiResponse<IReadOnlyList<TillDto>>.CreateSuccess(dtos);
    }

    public async Task<ApiResponse<TillDto>> GetTillByIdAsync(long id, CancellationToken ct = default)
    {
        var till = await _tillRepository.GetTillByIdAsync(id, ct);
        if (till == null)
            return ApiResponse<TillDto>.CreateFailure("Till not found", null, 404);

        return ApiResponse<TillDto>.CreateSuccess(MapToDto(till));
    }

    public async Task<ApiResponse<TillDto>> CreateTillAsync(CreateTillDto dto, string username, CancellationToken ct = default)
    {
        var existing = await _tillRepository.GetByCodeAsync(dto.BranchId, dto.TillCode, ct);
        if (existing != null)
            return ApiResponse<TillDto>.CreateFailure($"Till code '{dto.TillCode}' already exists for this branch", null, 400);

        var till = new PosTill
        {
            BranchId = dto.BranchId,
            TillCode = dto.TillCode.Trim().ToUpper(),
            TillName = dto.TillName.Trim(),
            MachineIdentifier = dto.MachineIdentifier?.Trim(),
            IpAddress = dto.IpAddress?.Trim(),
            DefaultFloatAmount = dto.DefaultFloatAmount,
            IsActive = true,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        await _tillRepository.AddTillAsync(till, ct);
        await _tillRepository.SaveChangesAsync(ct);

        return ApiResponse<TillDto>.CreateSuccess(MapToDto(till), "Till created successfully");
    }

    public async Task<ApiResponse<TillDto>> UpdateTillAsync(long id, UpdateTillDto dto, string username, CancellationToken ct = default)
    {
        var till = await _tillRepository.GetTillByIdAsync(id, ct);
        if (till == null)
            return ApiResponse<TillDto>.CreateFailure("Till not found", null, 404);

        till.TillName = dto.TillName.Trim();
        till.MachineIdentifier = dto.MachineIdentifier?.Trim();
        till.IpAddress = dto.IpAddress?.Trim();
        till.DefaultFloatAmount = dto.DefaultFloatAmount;
        till.IsActive = dto.IsActive;
        till.UpdateUser = username;
        till.UpdateDate = DateTime.UtcNow;

        await _tillRepository.UpdateTillAsync(till, ct);
        await _tillRepository.SaveChangesAsync(ct);

        return ApiResponse<TillDto>.CreateSuccess(MapToDto(till), "Till updated successfully");
    }

    public async Task<ApiResponse<bool>> DeleteTillAsync(long id, CancellationToken ct = default)
    {
        var till = await _tillRepository.GetTillByIdAsync(id, ct);
        if (till == null)
            return ApiResponse<bool>.CreateFailure("Till not found", null, 404);

        till.IsActive = false;
        await _tillRepository.UpdateTillAsync(till, ct);
        await _tillRepository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true, "Till deactivated successfully");
    }

    private static TillDto MapToDto(PosTill till)
    {
        return new TillDto
        {
            Id = till.Id,
            BranchId = till.BranchId,
            TillCode = till.TillCode,
            TillName = till.TillName,
            MachineIdentifier = till.MachineIdentifier,
            IpAddress = till.IpAddress,
            DefaultFloatAmount = till.DefaultFloatAmount,
            IsActive = till.IsActive,
            CreationDate = till.CreationDate
        };
    }
}
