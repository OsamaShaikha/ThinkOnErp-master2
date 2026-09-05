using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IGlCostCenterRepository _costCenterRepository;
    private readonly ILogger<DepartmentService> _logger;

    public DepartmentService(
        IDepartmentRepository departmentRepository,
        IGlCostCenterRepository costCenterRepository,
        ILogger<DepartmentService> logger)
    {
        _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
        _costCenterRepository = costCenterRepository ?? throw new ArgumentNullException(nameof(costCenterRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<DepartmentDto>> GetAllAsync(long? branchId = null, bool activeOnly = true)
    {
        var list = await _departmentRepository.GetAllAsync(branchId, activeOnly);
        return list.Select(MapToDto).ToList();
    }

    public async Task<List<DepartmentTreeNodeDto>> GetTreeAsync(long? branchId = null)
    {
        var allDepts = await _departmentRepository.GetAllAsync(branchId, activeOnly: true);
        var nodeMap = allDepts.ToDictionary(
            d => d.DepartmentCode,
            d => new DepartmentTreeNodeDto
            {
                DepartmentCode = d.DepartmentCode,
                NameAr = d.NameAr,
                NameEn = d.NameEn,
                ParentDepartmentCode = d.ParentDepartmentCode,
                BranchId = d.BranchId,
                CostCenterCode = d.CostCenterCode,
                EmployeeCount = d.Employees.Count
            });

        var roots = new List<DepartmentTreeNodeDto>();

        foreach (var dept in allDepts)
        {
            var node = nodeMap[dept.DepartmentCode];
            if (string.IsNullOrWhiteSpace(dept.ParentDepartmentCode) || !nodeMap.TryGetValue(dept.ParentDepartmentCode, out var parentNode))
            {
                roots.Add(node);
            }
            else
            {
                parentNode.Children.Add(node);
            }
        }

        return roots;
    }

    public async Task<DepartmentDto?> GetByCodeAsync(string code)
    {
        var dept = await _departmentRepository.GetByCodeAsync(code);
        return dept == null ? null : MapToDto(dept);
    }

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var code = dto.DepartmentCode.Trim().ToUpperInvariant();

        if (await _departmentRepository.CodeExistsAsync(code))
        {
            throw new HrConflictException($"رمز القسم ({code}) موجود مسبقاً.", "DEPARTMENT_CODE_DUPLICATE");
        }

        if (!string.IsNullOrWhiteSpace(dto.ParentDepartmentCode))
        {
            var parent = await _departmentRepository.GetByCodeAsync(dto.ParentDepartmentCode);
            if (parent == null)
            {
                throw new HrNotFoundException($"القسم الرئيسي رقم ({dto.ParentDepartmentCode}) غير موجود.", "PARENT_DEPARTMENT_NOT_FOUND");
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.CostCenterCode))
        {
            var costCenter = await _costCenterRepository.GetByCodeAsync(dto.CostCenterCode);
            if (costCenter == null)
            {
                throw new HrNotFoundException($"مركز التكلفة ({dto.CostCenterCode}) غير موجود في دليل مراكز التكلفة.", "COST_CENTER_NOT_FOUND");
            }
        }

        var dept = new Department
        {
            DepartmentCode = code,
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            ParentDepartmentCode = string.IsNullOrWhiteSpace(dto.ParentDepartmentCode) ? null : dto.ParentDepartmentCode.Trim().ToUpperInvariant(),
            BranchId = dto.BranchId,
            CostCenterCode = string.IsNullOrWhiteSpace(dto.CostCenterCode) ? null : dto.CostCenterCode.Trim(),
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _departmentRepository.AddAsync(dept);
        await _departmentRepository.SaveChangesAsync();

        _logger.LogInformation("Created department {Code} ({NameEn}) by {User}", dept.DepartmentCode, dept.NameEn, currentUser);
        return MapToDto(dept);
    }

    public async Task<DepartmentDto> UpdateAsync(string code, UpdateDepartmentDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var dept = await _departmentRepository.GetByCodeAsync(code);
        if (dept == null)
        {
            throw new HrNotFoundException($"القسم ({code}) غير موجود.", "DEPARTMENT_NOT_FOUND");
        }

        if (!string.IsNullOrWhiteSpace(dto.ParentDepartmentCode))
        {
            if (dto.ParentDepartmentCode.Equals(code, StringComparison.OrdinalIgnoreCase))
            {
                throw new HrValidationException("لا يمكن للقسم أن يكون قسماً رئيسياً لنفسه.", "SELF_PARENT_NOT_ALLOWED");
            }

            var parent = await _departmentRepository.GetByCodeAsync(dto.ParentDepartmentCode);
            if (parent == null)
            {
                throw new HrNotFoundException($"القسم الرئيسي ({dto.ParentDepartmentCode}) غير موجود.", "PARENT_DEPARTMENT_NOT_FOUND");
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.CostCenterCode))
        {
            var costCenter = await _costCenterRepository.GetByCodeAsync(dto.CostCenterCode);
            if (costCenter == null)
            {
                throw new HrNotFoundException($"مركز التكلفة ({dto.CostCenterCode}) غير موجود.", "COST_CENTER_NOT_FOUND");
            }
        }

        dept.NameAr = dto.NameAr.Trim();
        dept.NameEn = dto.NameEn.Trim();
        dept.ParentDepartmentCode = string.IsNullOrWhiteSpace(dto.ParentDepartmentCode) ? null : dto.ParentDepartmentCode.Trim().ToUpperInvariant();
        dept.BranchId = dto.BranchId;
        dept.CostCenterCode = string.IsNullOrWhiteSpace(dto.CostCenterCode) ? null : dto.CostCenterCode.Trim();
        dept.IsActive = dto.IsActive;
        dept.UpdateUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        dept.UpdateDate = DateTime.UtcNow;

        _departmentRepository.Update(dept);
        await _departmentRepository.SaveChangesAsync();

        _logger.LogInformation("Updated department {Code} by {User}", code, currentUser);
        return MapToDto(dept);
    }

    public async Task<bool> DeleteAsync(string code)
    {
        var dept = await _departmentRepository.GetByCodeAsync(code);
        if (dept == null)
        {
            return false;
        }

        if (await _departmentRepository.HasChildrenAsync(code))
        {
            throw new HrValidationException($"لا يمكن حذف القسم ({code}) لوجود أقسام فرعية تابعة له.", "DEPARTMENT_HAS_CHILDREN");
        }

        if (await _departmentRepository.HasEmployeesAsync(code))
        {
            throw new HrValidationException($"لا يمكن حذف القسم ({code}) لوجود موظفين مسجلين به.", "DEPARTMENT_HAS_EMPLOYEES");
        }

        _departmentRepository.Remove(dept);
        await _departmentRepository.SaveChangesAsync();
        _logger.LogInformation("Deleted department {Code}", code);
        return true;
    }

    private static DepartmentDto MapToDto(Department d)
    {
        return new DepartmentDto
        {
            DepartmentCode = d.DepartmentCode,
            NameAr = d.NameAr,
            NameEn = d.NameEn,
            ParentDepartmentCode = d.ParentDepartmentCode,
            ParentDepartmentName = d.ParentDepartment?.NameEn,
            BranchId = d.BranchId,
            BranchName = d.Branch?.BranchNameEn,
            CostCenterCode = d.CostCenterCode,
            CostCenterName = d.CostCenter?.NameEn,
            IsActive = d.IsActive,
            EmployeeCount = d.Employees.Count,
            PositionCount = d.Positions.Count,
            CreationDate = d.CreationDate
        };
    }
}
