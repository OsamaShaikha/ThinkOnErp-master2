using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class AssetAssignmentService : IAssetAssignmentService
{
    private readonly IAssetAssignmentRepository _assetRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<AssetAssignmentService> _logger;

    public AssetAssignmentService(
        IAssetAssignmentRepository assetRepository,
        IEmployeeRepository employeeRepository,
        ILogger<AssetAssignmentService> logger)
    {
        _assetRepository = assetRepository ?? throw new ArgumentNullException(nameof(assetRepository));
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<AssetAssignmentDto>> GetAllAssignmentsAsync(string? employeeCode = null, string? status = null, string? category = null)
    {
        var list = await _assetRepository.GetAllAssignmentsAsync(employeeCode, status, category);
        return list.Select(MapToDto).ToList();
    }

    public async Task<AssetAssignmentDto?> GetAssignmentByIdAsync(long id)
    {
        var asset = await _assetRepository.GetAssignmentByIdAsync(id);
        return asset == null ? null : MapToDto(asset);
    }

    public async Task<AssetAssignmentDto> AssignAssetAsync(CreateAssetAssignmentDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var empCode = dto.EmployeeCode.Trim().ToUpperInvariant();
        var tag = dto.AssetTag.Trim().ToUpperInvariant();

        var emp = await _employeeRepository.GetByCodeAsync(empCode);
        if (emp == null)
        {
            throw new HrNotFoundException($"الموظف ({empCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var existingTag = await _assetRepository.GetAssignmentByTagAsync(tag);
        if (existingTag != null && existingTag.Status == "ASSIGNED")
        {
            throw new HrConflictException($"الأصل ({tag}) مسلّم مسبقاً لموظف آخر بحالة نشطة.", "ASSET_ALREADY_ASSIGNED");
        }

        var asset = new AssetAssignment
        {
            EmployeeCode = empCode,
            AssetTag = tag,
            AssetDescription = dto.AssetDescription.Trim(),
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "LAPTOP" : dto.Category.Trim().ToUpperInvariant(),
            SerialNumber = dto.SerialNumber?.Trim(),
            IssuedDate = dto.IssuedDate.Date,
            ExpectedReturnDate = dto.ExpectedReturnDate?.Date,
            Status = "ASSIGNED",
            IssuedCondition = dto.IssuedCondition?.Trim() ?? "NEW",
            Notes = dto.Notes?.Trim(),
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _assetRepository.AddAssignmentAsync(asset);
        await _assetRepository.SaveChangesAsync();

        _logger.LogInformation("Assigned asset {Tag} ({Desc}) to employee {Emp}", tag, asset.AssetDescription, empCode);
        return (await GetAssignmentByIdAsync(asset.Id))!;
    }

    public async Task<AssetAssignmentDto> ReturnAssetAsync(long id, ReturnAssetDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var asset = await _assetRepository.GetAssignmentByIdAsync(id);
        if (asset == null)
        {
            throw new HrNotFoundException($"سجل تسليم الأصل رقم ({id}) غير موجود.", "ASSET_ASSIGNMENT_NOT_FOUND");
        }

        if (asset.Status != "ASSIGNED")
        {
            throw new HrValidationException($"الأصل بحالة ({asset.Status}) وليس بحالة تسليم نشط.", "ASSET_NOT_ASSIGNED");
        }

        asset.ReturnedDate = dto.ReturnedDate.Date;
        asset.ReturnedCondition = dto.ReturnedCondition?.Trim() ?? "GOOD";
        asset.Status = string.IsNullOrWhiteSpace(dto.Status) ? "RETURNED" : dto.Status.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            asset.Notes = $"{asset.Notes} | Return Note: {dto.Notes.Trim()}";
        }
        asset.UpdateUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        asset.UpdateDate = DateTime.UtcNow;

        _assetRepository.UpdateAssignment(asset);
        await _assetRepository.SaveChangesAsync();

        _logger.LogInformation("Returned asset {Tag} from employee {Emp}, status {Status}", asset.AssetTag, asset.EmployeeCode, asset.Status);
        return MapToDto(asset);
    }

    private static AssetAssignmentDto MapToDto(AssetAssignment a)
    {
        return new AssetAssignmentDto
        {
            Id = a.Id,
            EmployeeCode = a.EmployeeCode,
            EmployeeNameEn = a.Employee?.NameEn ?? string.Empty,
            AssetTag = a.AssetTag,
            AssetDescription = a.AssetDescription,
            Category = a.Category,
            SerialNumber = a.SerialNumber,
            IssuedDate = a.IssuedDate,
            ExpectedReturnDate = a.ExpectedReturnDate,
            ReturnedDate = a.ReturnedDate,
            Status = a.Status,
            IssuedCondition = a.IssuedCondition,
            ReturnedCondition = a.ReturnedCondition,
            Notes = a.Notes
        };
    }
}
