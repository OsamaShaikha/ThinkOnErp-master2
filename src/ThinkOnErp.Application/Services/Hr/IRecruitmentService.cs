using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IRecruitmentService
{
    // Requisitions
    Task<List<JobRequisitionDto>> GetAllRequisitionsAsync(string? status = null);
    Task<JobRequisitionDto?> GetRequisitionByCodeAsync(string code);
    Task<JobRequisitionDto> CreateRequisitionAsync(CreateJobRequisitionDto dto, string currentUser);
    Task<JobRequisitionDto> ApproveRequisitionAsync(string code, string approvedBy);

    // Candidates & Applications
    Task<List<CandidateDto>> GetAllCandidatesAsync();
    Task<CandidateDto?> GetCandidateByCodeAsync(string code);
    Task<CandidateDto> CreateCandidateAsync(CreateCandidateDto dto, string currentUser);
    Task<CandidateApplicationDto> UpdateApplicationStageAsync(long applicationId, UpdateApplicationStageDto dto, string currentUser);
    Task<EmployeeDto> HireCandidateAsync(long applicationId, HireCandidateDto dto, string currentUser);

    // Onboarding Tasks
    Task<List<OnboardingTaskDto>> GetOnboardingTasksAsync(string employeeCode);
    Task<OnboardingTaskDto> CreateOnboardingTaskAsync(CreateOnboardingTaskDto dto, string currentUser);
    Task<OnboardingTaskDto> CompleteOnboardingTaskAsync(long taskId, string completedBy);
}
