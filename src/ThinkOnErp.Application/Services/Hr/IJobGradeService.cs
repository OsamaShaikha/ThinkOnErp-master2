using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IJobGradeService
{
    Task<List<JobGradeDto>> GetAllAsync(bool activeOnly = true);
    Task<JobGradeDto?> GetByCodeAsync(string code);
    Task<JobGradeDto> CreateAsync(CreateJobGradeDto dto, string currentUser);
    Task<JobGradeDto> UpdateAsync(string code, UpdateJobGradeDto dto, string currentUser);
    Task<bool> DeleteAsync(string code);
}
