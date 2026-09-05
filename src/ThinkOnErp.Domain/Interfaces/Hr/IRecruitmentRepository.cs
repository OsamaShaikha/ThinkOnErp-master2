using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IRecruitmentRepository
{
    // Requisitions
    Task<IReadOnlyList<JobRequisition>> GetAllRequisitionsAsync(string? status = null, CancellationToken cancellationToken = default);
    Task<JobRequisition?> GetRequisitionByCodeAsync(string code, bool includeApplications = true, CancellationToken cancellationToken = default);
    Task<bool> RequisitionCodeExistsAsync(string code, CancellationToken cancellationToken = default);
    Task AddRequisitionAsync(JobRequisition requisition, CancellationToken cancellationToken = default);
    void UpdateRequisition(JobRequisition requisition);

    // Candidates & Applications
    Task<IReadOnlyList<Candidate>> GetAllCandidatesAsync(CancellationToken cancellationToken = default);
    Task<Candidate?> GetCandidateByCodeAsync(string code, bool includeApplications = true, CancellationToken cancellationToken = default);
    Task<bool> CandidateCodeExistsAsync(string code, CancellationToken cancellationToken = default);
    Task AddCandidateAsync(Candidate candidate, CancellationToken cancellationToken = default);
    void UpdateCandidate(Candidate candidate);

    Task<CandidateApplication?> GetApplicationByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddApplicationAsync(CandidateApplication application, CancellationToken cancellationToken = default);
    void UpdateApplication(CandidateApplication application);

    // Onboarding Tasks
    Task<IReadOnlyList<OnboardingTask>> GetOnboardingTasksByEmployeeAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<OnboardingTask?> GetOnboardingTaskByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddOnboardingTaskAsync(OnboardingTask task, CancellationToken cancellationToken = default);
    void UpdateOnboardingTask(OnboardingTask task);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
