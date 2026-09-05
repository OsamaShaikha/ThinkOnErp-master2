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

public sealed class JobGradeService : IJobGradeService
{
    private readonly IJobGradeRepository _repository;
    private readonly ILogger<JobGradeService> _logger;

    public JobGradeService(
        IJobGradeRepository repository,
        ILogger<JobGradeService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<JobGradeDto>> GetAllAsync(bool activeOnly = true)
    {
        var list = await _repository.GetAllAsync(activeOnly);
        return list.Select(MapToDto).ToList();
    }

    public async Task<JobGradeDto?> GetByCodeAsync(string code)
    {
        var grade = await _repository.GetByCodeAsync(code);
        return grade == null ? null : MapToDto(grade);
    }

    public async Task<JobGradeDto> CreateAsync(CreateJobGradeDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var code = dto.GradeCode.Trim().ToUpperInvariant();

        if (await _repository.CodeExistsAsync(code))
        {
            throw new HrConflictException($"رمز الدرجة الوظيفية ({code}) موجود مسبقاً.", "JOB_GRADE_DUPLICATE");
        }

        if (dto.MinSalary > dto.MaxSalary)
        {
            throw new HrValidationException("الحد الأدنى للراتب لا يمكن أن يتجاوز الحد الأقصى.", "SALARY_RANGE_INVALID");
        }

        var grade = new JobGrade
        {
            GradeCode = code,
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            Level = dto.Level,
            MinSalary = dto.MinSalary,
            MidSalary = dto.MidSalary,
            MaxSalary = dto.MaxSalary,
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _repository.AddAsync(grade);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Created job grade {Code} by {User}", grade.GradeCode, currentUser);
        return MapToDto(grade);
    }

    public async Task<JobGradeDto> UpdateAsync(string code, UpdateJobGradeDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var grade = await _repository.GetByCodeAsync(code);
        if (grade == null)
        {
            throw new HrNotFoundException($"الدرجة الوظيفية ({code}) غير موجودة.", "JOB_GRADE_NOT_FOUND");
        }

        if (dto.MinSalary > dto.MaxSalary)
        {
            throw new HrValidationException("الحد الأدنى للراتب لا يمكن أن يتجاوز الحد الأقصى.", "SALARY_RANGE_INVALID");
        }

        grade.NameAr = dto.NameAr.Trim();
        grade.NameEn = dto.NameEn.Trim();
        grade.Level = dto.Level;
        grade.MinSalary = dto.MinSalary;
        grade.MidSalary = dto.MidSalary;
        grade.MaxSalary = dto.MaxSalary;
        grade.IsActive = dto.IsActive;
        grade.UpdateUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        grade.UpdateDate = DateTime.UtcNow;

        _repository.Update(grade);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Updated job grade {Code} by {User}", code, currentUser);
        return MapToDto(grade);
    }

    public async Task<bool> DeleteAsync(string code)
    {
        var grade = await _repository.GetByCodeAsync(code);
        if (grade == null)
        {
            return false;
        }

        if (await _repository.HasPositionsAsync(code))
        {
            throw new HrValidationException($"لا يمكن حذف الدرجة الوظيفية ({code}) لارتباطها بمناصب وظيفية قائمة.", "JOB_GRADE_HAS_POSITIONS");
        }

        _repository.Remove(grade);
        await _repository.SaveChangesAsync();
        _logger.LogInformation("Deleted job grade {Code}", code);
        return true;
    }

    private static JobGradeDto MapToDto(JobGrade g)
    {
        return new JobGradeDto
        {
            GradeCode = g.GradeCode,
            NameAr = g.NameAr,
            NameEn = g.NameEn,
            Level = g.Level,
            MinSalary = g.MinSalary,
            MidSalary = g.MidSalary,
            MaxSalary = g.MaxSalary,
            IsActive = g.IsActive,
            PositionCount = g.Positions.Count,
            CreationDate = g.CreationDate
        };
    }
}
