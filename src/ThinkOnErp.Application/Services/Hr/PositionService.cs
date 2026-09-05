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

public sealed class PositionService : IPositionService
{
    private readonly IPositionRepository _positionRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IJobGradeRepository _jobGradeRepository;
    private readonly ILogger<PositionService> _logger;

    public PositionService(
        IPositionRepository positionRepository,
        IDepartmentRepository departmentRepository,
        IJobGradeRepository jobGradeRepository,
        ILogger<PositionService> logger)
    {
        _positionRepository = positionRepository ?? throw new ArgumentNullException(nameof(positionRepository));
        _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
        _jobGradeRepository = jobGradeRepository ?? throw new ArgumentNullException(nameof(jobGradeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<PositionDto>> GetAllAsync(string? departmentCode = null, bool activeOnly = true)
    {
        var list = await _positionRepository.GetAllAsync(departmentCode, activeOnly);
        return list.Select(MapToDto).ToList();
    }

    public async Task<PositionDto?> GetByCodeAsync(string code)
    {
        var pos = await _positionRepository.GetByCodeAsync(code);
        return pos == null ? null : MapToDto(pos);
    }

    public async Task<List<OrgChartNodeDto>> GetOrgChartAsync(long? branchId = null)
    {
        var allPositions = await _positionRepository.GetAllAsync(activeOnly: true);
        if (branchId.HasValue)
        {
            allPositions = allPositions.Where(p => p.Department?.BranchId == branchId.Value).ToList();
        }

        var nodeMap = allPositions.ToDictionary(
            p => p.PositionCode,
            p => new OrgChartNodeDto
            {
                PositionCode = p.PositionCode,
                TitleAr = p.TitleAr,
                TitleEn = p.TitleEn,
                DepartmentCode = p.DepartmentCode,
                DepartmentName = p.Department?.NameEn ?? string.Empty,
                JobGradeCode = p.JobGradeCode,
                Incumbents = p.Employees.Where(e => e.IsActive && e.EmploymentStatus != "TERMINATED").Select(e => new EmployeeSummaryDto
                {
                    EmployeeCode = e.EmployeeCode,
                    NameAr = e.NameAr,
                    NameEn = e.NameEn,
                    PositionCode = e.PositionCode,
                    PositionTitle = p.TitleEn,
                    DepartmentCode = e.DepartmentCode,
                    DepartmentName = p.Department?.NameEn,
                    EmploymentStatus = e.EmploymentStatus
                }).ToList()
            });

        var roots = new List<OrgChartNodeDto>();

        foreach (var pos in allPositions)
        {
            var node = nodeMap[pos.PositionCode];
            if (string.IsNullOrWhiteSpace(pos.ReportsToPositionCode) || !nodeMap.TryGetValue(pos.ReportsToPositionCode, out var parentNode))
            {
                roots.Add(node);
            }
            else
            {
                parentNode.DirectReports.Add(node);
            }
        }

        return roots;
    }

    public async Task<PositionDto> CreateAsync(CreatePositionDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var code = dto.PositionCode.Trim().ToUpperInvariant();

        if (await _positionRepository.CodeExistsAsync(code))
        {
            throw new HrConflictException($"رمز المنصب ({code}) موجود مسبقاً.", "POSITION_CODE_DUPLICATE");
        }

        var dept = await _departmentRepository.GetByCodeAsync(dto.DepartmentCode);
        if (dept == null)
        {
            throw new HrNotFoundException($"القسم ({dto.DepartmentCode}) غير موجود.", "DEPARTMENT_NOT_FOUND");
        }

        if (!string.IsNullOrWhiteSpace(dto.JobGradeCode))
        {
            var grade = await _jobGradeRepository.GetByCodeAsync(dto.JobGradeCode);
            if (grade == null)
            {
                throw new HrNotFoundException($"الدرجة الوظيفية ({dto.JobGradeCode}) غير موجودة.", "JOB_GRADE_NOT_FOUND");
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.ReportsToPositionCode))
        {
            var parent = await _positionRepository.GetByCodeAsync(dto.ReportsToPositionCode);
            if (parent == null)
            {
                throw new HrNotFoundException($"المنصب الأعلى ({dto.ReportsToPositionCode}) غير موجود.", "PARENT_POSITION_NOT_FOUND");
            }
        }

        var position = new Position
        {
            PositionCode = code,
            TitleAr = dto.TitleAr.Trim(),
            TitleEn = dto.TitleEn.Trim(),
            DepartmentCode = dto.DepartmentCode.Trim().ToUpperInvariant(),
            JobGradeCode = string.IsNullOrWhiteSpace(dto.JobGradeCode) ? null : dto.JobGradeCode.Trim().ToUpperInvariant(),
            ReportsToPositionCode = string.IsNullOrWhiteSpace(dto.ReportsToPositionCode) ? null : dto.ReportsToPositionCode.Trim().ToUpperInvariant(),
            Headcount = dto.Headcount > 0 ? dto.Headcount : 1,
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _positionRepository.AddAsync(position);
        await _positionRepository.SaveChangesAsync();

        _logger.LogInformation("Created position {Code} ({TitleEn}) by {User}", position.PositionCode, position.TitleEn, currentUser);
        return MapToDto(position);
    }

    public async Task<PositionDto> UpdateAsync(string code, UpdatePositionDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var position = await _positionRepository.GetByCodeAsync(code);
        if (position == null)
        {
            throw new HrNotFoundException($"المنصب ({code}) غير موجود.", "POSITION_NOT_FOUND");
        }

        var dept = await _departmentRepository.GetByCodeAsync(dto.DepartmentCode);
        if (dept == null)
        {
            throw new HrNotFoundException($"القسم ({dto.DepartmentCode}) غير موجود.", "DEPARTMENT_NOT_FOUND");
        }

        if (!string.IsNullOrWhiteSpace(dto.JobGradeCode))
        {
            var grade = await _jobGradeRepository.GetByCodeAsync(dto.JobGradeCode);
            if (grade == null)
            {
                throw new HrNotFoundException($"الدرجة الوظيفية ({dto.JobGradeCode}) غير موجودة.", "JOB_GRADE_NOT_FOUND");
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.ReportsToPositionCode))
        {
            if (dto.ReportsToPositionCode.Equals(code, StringComparison.OrdinalIgnoreCase))
            {
                throw new HrValidationException("لا يمكن للمنصب أن يتبع لنفسه.", "SELF_REPORT_NOT_ALLOWED");
            }

            var parent = await _positionRepository.GetByCodeAsync(dto.ReportsToPositionCode);
            if (parent == null)
            {
                throw new HrNotFoundException($"المنصب الأعلى ({dto.ReportsToPositionCode}) غير موجود.", "PARENT_POSITION_NOT_FOUND");
            }
        }

        position.TitleAr = dto.TitleAr.Trim();
        position.TitleEn = dto.TitleEn.Trim();
        position.DepartmentCode = dto.DepartmentCode.Trim().ToUpperInvariant();
        position.JobGradeCode = string.IsNullOrWhiteSpace(dto.JobGradeCode) ? null : dto.JobGradeCode.Trim().ToUpperInvariant();
        position.ReportsToPositionCode = string.IsNullOrWhiteSpace(dto.ReportsToPositionCode) ? null : dto.ReportsToPositionCode.Trim().ToUpperInvariant();
        position.Headcount = dto.Headcount > 0 ? dto.Headcount : 1;
        position.IsActive = dto.IsActive;
        position.UpdateUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        position.UpdateDate = DateTime.UtcNow;

        _positionRepository.Update(position);
        await _positionRepository.SaveChangesAsync();

        _logger.LogInformation("Updated position {Code} by {User}", code, currentUser);
        return MapToDto(position);
    }

    public async Task<bool> DeleteAsync(string code)
    {
        var position = await _positionRepository.GetByCodeAsync(code);
        if (position == null)
        {
            return false;
        }

        if (await _positionRepository.HasReportsAsync(code))
        {
            throw new HrValidationException($"لا يمكن حذف المنصب ({code}) لوجود مناصب تابعة له بالهيكل الوظيفي.", "POSITION_HAS_DIRECT_REPORTS");
        }

        if (await _positionRepository.HasEmployeesAsync(code))
        {
            throw new HrValidationException($"لا يمكن حذف المنصب ({code}) لوجود موظفين يشغلونه حالياً.", "POSITION_HAS_EMPLOYEES");
        }

        _positionRepository.Remove(position);
        await _positionRepository.SaveChangesAsync();
        _logger.LogInformation("Deleted position {Code}", code);
        return true;
    }

    private static PositionDto MapToDto(Position p)
    {
        return new PositionDto
        {
            PositionCode = p.PositionCode,
            TitleAr = p.TitleAr,
            TitleEn = p.TitleEn,
            DepartmentCode = p.DepartmentCode,
            DepartmentName = p.Department?.NameEn,
            JobGradeCode = p.JobGradeCode,
            JobGradeName = p.JobGrade?.NameEn,
            ReportsToPositionCode = p.ReportsToPositionCode,
            ReportsToPositionTitle = p.ReportsToPosition?.TitleEn,
            Headcount = p.Headcount,
            ActiveEmployeeCount = p.Employees.Count(e => e.IsActive && e.EmploymentStatus != "TERMINATED"),
            IsActive = p.IsActive,
            CreationDate = p.CreationDate
        };
    }
}
