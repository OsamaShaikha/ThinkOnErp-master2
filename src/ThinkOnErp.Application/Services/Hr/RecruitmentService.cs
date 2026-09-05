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

public sealed class RecruitmentService : IRecruitmentService
{
    private readonly IRecruitmentRepository _recruitmentRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ICompensationRepository _compensationRepository;
    private readonly IEmploymentEventRepository _eventRepository;
    private readonly ILogger<RecruitmentService> _logger;

    public RecruitmentService(
        IRecruitmentRepository recruitmentRepository,
        IEmployeeRepository employeeRepository,
        ICompensationRepository compensationRepository,
        IEmploymentEventRepository eventRepository,
        ILogger<RecruitmentService> logger)
    {
        _recruitmentRepository = recruitmentRepository ?? throw new ArgumentNullException(nameof(recruitmentRepository));
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _compensationRepository = compensationRepository ?? throw new ArgumentNullException(nameof(compensationRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<JobRequisitionDto>> GetAllRequisitionsAsync(string? status = null)
    {
        var list = await _recruitmentRepository.GetAllRequisitionsAsync(status);
        return list.Select(MapToRequisitionDto).ToList();
    }

    public async Task<JobRequisitionDto?> GetRequisitionByCodeAsync(string code)
    {
        var req = await _recruitmentRepository.GetRequisitionByCodeAsync(code);
        return req == null ? null : MapToRequisitionDto(req);
    }

    public async Task<JobRequisitionDto> CreateRequisitionAsync(CreateJobRequisitionDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var code = dto.RequisitionCode.Trim().ToUpperInvariant();

        if (await _recruitmentRepository.RequisitionCodeExistsAsync(code))
        {
            throw new HrConflictException($"طلب التوظيف ({code}) موجود مسبقاً.", "REQUISITION_CODE_DUPLICATE");
        }

        var req = new JobRequisition
        {
            RequisitionCode = code,
            PositionCode = dto.PositionCode.Trim().ToUpperInvariant(),
            DepartmentCode = dto.DepartmentCode.Trim().ToUpperInvariant(),
            BranchId = dto.BranchId,
            Headcount = dto.Headcount > 0 ? dto.Headcount : 1,
            TargetHireDate = dto.TargetHireDate,
            JobDescription = dto.JobDescription?.Trim(),
            Status = "DRAFT",
            RequestedBy = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _recruitmentRepository.AddRequisitionAsync(req);
        await _recruitmentRepository.SaveChangesAsync();

        _logger.LogInformation("Created job requisition {Code} for position {Position}", code, req.PositionCode);
        return (await GetRequisitionByCodeAsync(code))!;
    }

    public async Task<JobRequisitionDto> ApproveRequisitionAsync(string code, string approvedBy)
    {
        var req = await _recruitmentRepository.GetRequisitionByCodeAsync(code);
        if (req == null)
        {
            throw new HrNotFoundException($"طلب التوظيف ({code}) غير موجود.", "REQUISITION_NOT_FOUND");
        }

        req.Status = "OPEN";
        req.ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? "SYSTEM" : approvedBy;
        req.ApprovalDate = DateTime.UtcNow;
        req.UpdateUser = req.ApprovedBy;
        req.UpdateDate = DateTime.UtcNow;

        _recruitmentRepository.UpdateRequisition(req);
        await _recruitmentRepository.SaveChangesAsync();

        _logger.LogInformation("Approved requisition {Code} by {User}", code, approvedBy);
        return MapToRequisitionDto(req);
    }

    public async Task<List<CandidateDto>> GetAllCandidatesAsync()
    {
        var list = await _recruitmentRepository.GetAllCandidatesAsync();
        return list.Select(MapToCandidateDto).ToList();
    }

    public async Task<CandidateDto?> GetCandidateByCodeAsync(string code)
    {
        var candidate = await _recruitmentRepository.GetCandidateByCodeAsync(code);
        return candidate == null ? null : MapToCandidateDto(candidate);
    }

    public async Task<CandidateDto> CreateCandidateAsync(CreateCandidateDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var code = dto.CandidateCode.Trim().ToUpperInvariant();

        if (await _recruitmentRepository.CandidateCodeExistsAsync(code))
        {
            throw new HrConflictException($"رمز المرشح ({code}) موجود مسبقاً.", "CANDIDATE_DUPLICATE");
        }

        var candidate = new Candidate
        {
            CandidateCode = code,
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            Email = dto.Email.Trim(),
            Phone = dto.Phone?.Trim(),
            NationalId = dto.NationalId?.Trim(),
            ResumeFileReference = dto.ResumeFileReference?.Trim(),
            Source = string.IsNullOrWhiteSpace(dto.Source) ? "DIRECT" : dto.Source.Trim().ToUpperInvariant(),
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(dto.ApplyRequisitionCode))
        {
            var req = await _recruitmentRepository.GetRequisitionByCodeAsync(dto.ApplyRequisitionCode);
            if (req != null)
            {
                candidate.Applications.Add(new CandidateApplication
                {
                    CandidateCode = code,
                    RequisitionCode = req.RequisitionCode,
                    Stage = "APPLIED",
                    CreationUser = candidate.CreationUser,
                    CreationDate = DateTime.UtcNow
                });
            }
        }

        await _recruitmentRepository.AddCandidateAsync(candidate);
        await _recruitmentRepository.SaveChangesAsync();

        _logger.LogInformation("Created candidate {Code} ({Name})", code, candidate.NameEn);
        return (await GetCandidateByCodeAsync(code))!;
    }

    public async Task<CandidateApplicationDto> UpdateApplicationStageAsync(long applicationId, UpdateApplicationStageDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var app = await _recruitmentRepository.GetApplicationByIdAsync(applicationId);
        if (app == null)
        {
            throw new HrNotFoundException($"طلب الترشح رقم ({applicationId}) غير موجود.", "APPLICATION_NOT_FOUND");
        }

        app.Stage = dto.Stage.Trim().ToUpperInvariant();
        app.OfferedSalary = dto.OfferedSalary;
        app.InterviewDate = dto.InterviewDate;
        app.Notes = dto.Notes;
        app.UpdateUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        app.UpdateDate = DateTime.UtcNow;

        _recruitmentRepository.UpdateApplication(app);
        await _recruitmentRepository.SaveChangesAsync();

        _logger.LogInformation("Updated application #{Id} stage to {Stage}", applicationId, app.Stage);

        return new CandidateApplicationDto
        {
            Id = app.Id,
            CandidateCode = app.CandidateCode,
            CandidateNameEn = app.Candidate?.NameEn ?? string.Empty,
            RequisitionCode = app.RequisitionCode,
            Stage = app.Stage,
            OfferedSalary = app.OfferedSalary,
            InterviewDate = app.InterviewDate,
            Notes = app.Notes,
            HiredEmployeeCode = app.HiredEmployeeCode,
            CreationDate = app.CreationDate
        };
    }

    public async Task<EmployeeDto> HireCandidateAsync(long applicationId, HireCandidateDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var app = await _recruitmentRepository.GetApplicationByIdAsync(applicationId);
        if (app == null)
        {
            throw new HrNotFoundException($"طلب الترشح رقم ({applicationId}) غير موجود.", "APPLICATION_NOT_FOUND");
        }

        var candidate = app.Candidate;
        var requisition = app.Requisition;

        if (candidate == null || requisition == null)
        {
            throw new HrValidationException("بيانات المرشح أو الوظيفة المطلوبة غير مكتملة للتعيين.", "INCOMPLETE_HIRING_DATA");
        }

        var empCode = dto.EmployeeCode.Trim().ToUpperInvariant();
        if (await _employeeRepository.CodeExistsAsync(empCode))
        {
            throw new HrConflictException($"الرقم الوظيفي ({empCode}) مسجل مسبقاً لموظف آخر.", "EMPLOYEE_CODE_DUPLICATE");
        }

        // 1. Initialize Employee Master Record
        var employee = new Employee
        {
            EmployeeCode = empCode,
            NameAr = candidate.NameAr,
            NameEn = candidate.NameEn,
            NationalId = dto.NationalId?.Trim() ?? candidate.NationalId ?? "PENDING",
            PositionCode = requisition.PositionCode,
            DepartmentCode = requisition.DepartmentCode,
            BranchId = requisition.BranchId,
            EmploymentStatus = "PROBATION",
            EmploymentType = "FULL_TIME",
            HireDate = dto.HireDate.Date,
            ProbationEndDate = dto.HireDate.Date.AddMonths(3), // 3 months statutory probation
            SscNumber = dto.SscNumber?.Trim(),
            BankName = dto.BankName?.Trim(),
            BankAccountNumber = dto.BankAccountNumber?.Trim(),
            BankIban = dto.BankIban?.Trim(),
            Email = dto.Email?.Trim() ?? candidate.Email,
            Phone = dto.Phone?.Trim() ?? candidate.Phone,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _employeeRepository.AddAsync(employee);

        // 2. Initialize Starting Salary Structure
        var structure = new EmployeeSalaryStructure
        {
            EmployeeCode = empCode,
            EffectiveFrom = dto.HireDate.Date,
            BasicSalary = dto.BasicSalary > 0 ? dto.BasicSalary : (app.OfferedSalary ?? 300m),
            CurrencyCode = "JOD",
            PaymentMethod = "BANK_TRANSFER",
            IsActive = true,
            CreationUser = employee.CreationUser,
            CreationDate = DateTime.UtcNow
        };
        await _compensationRepository.AddStructureAsync(structure);

        // 3. Record Immutable HIRE Event
        var hireEvent = new EmploymentEvent
        {
            EmployeeCode = empCode,
            EventType = "HIRE",
            EffectiveDate = dto.HireDate.Date,
            ToValue = $"Hired from Requisition {requisition.RequisitionCode} (Position: {requisition.PositionCode})",
            Reason = "Candidate recruitment conversion to employee",
            ApprovedBy = currentUser,
            CreationDate = DateTime.UtcNow
        };
        await _eventRepository.AddAsync(hireEvent);

        // 4. Spawn Standard Onboarding Checklist Tasks
        var standardTasks = new List<string>
        {
            "Issue Laptop, IT Credentials & Email Account",
            "Register Employee in Social Security Corporation (SSC Jordan)",
            "Sign Employment Contract & Hand over Company Policies",
            "Collect Bank IBAN verification & Open salary file"
        };

        foreach (var taskName in standardTasks)
        {
            await _recruitmentRepository.AddOnboardingTaskAsync(new OnboardingTask
            {
                EmployeeCode = empCode,
                TaskName = taskName,
                AssignedTo = "HR Operations",
                DueDate = dto.HireDate.Date.AddDays(7),
                IsCompleted = false,
                CreationUser = currentUser,
                CreationDate = DateTime.UtcNow
            });
        }

        // 5. Update Application and Requisition statuses
        app.Stage = "HIRED";
        app.HiredEmployeeCode = empCode;
        app.UpdateUser = currentUser;
        app.UpdateDate = DateTime.UtcNow;
        _recruitmentRepository.UpdateApplication(app);

        // Check if headcount filled
        var hiredCount = (await _recruitmentRepository.GetRequisitionByCodeAsync(requisition.RequisitionCode))?
            .Applications.Count(a => a.Stage == "HIRED") ?? 1;

        if (hiredCount >= requisition.Headcount)
        {
            requisition.Status = "FILLED";
            requisition.UpdateUser = currentUser;
            requisition.UpdateDate = DateTime.UtcNow;
            _recruitmentRepository.UpdateRequisition(requisition);
        }

        await _recruitmentRepository.SaveChangesAsync();
        await _employeeRepository.SaveChangesAsync();

        _logger.LogInformation("Hired candidate {Cand} as employee {Emp} for requisition {Req}", candidate.CandidateCode, empCode, requisition.RequisitionCode);

        var fullEmp = await _employeeRepository.GetByCodeAsync(empCode, includeDetails: true);
        return MapToEmployeeDto(fullEmp!);
    }

    public async Task<List<OnboardingTaskDto>> GetOnboardingTasksAsync(string employeeCode)
    {
        var list = await _recruitmentRepository.GetOnboardingTasksByEmployeeAsync(employeeCode);
        return list.Select(MapToTaskDto).ToList();
    }

    public async Task<OnboardingTaskDto> CreateOnboardingTaskAsync(CreateOnboardingTaskDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var empCode = dto.EmployeeCode.Trim().ToUpperInvariant();

        var emp = await _employeeRepository.GetByCodeAsync(empCode);
        if (emp == null)
        {
            throw new HrNotFoundException($"الموظف ({empCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var task = new OnboardingTask
        {
            EmployeeCode = empCode,
            TaskName = dto.TaskName.Trim(),
            AssignedTo = dto.AssignedTo?.Trim(),
            DueDate = dto.DueDate,
            Notes = dto.Notes?.Trim(),
            IsCompleted = false,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _recruitmentRepository.AddOnboardingTaskAsync(task);
        await _recruitmentRepository.SaveChangesAsync();

        return MapToTaskDto(task);
    }

    public async Task<OnboardingTaskDto> CompleteOnboardingTaskAsync(long taskId, string completedBy)
    {
        var task = await _recruitmentRepository.GetOnboardingTaskByIdAsync(taskId);
        if (task == null)
        {
            throw new HrNotFoundException($"مهمة التهيئة رقم ({taskId}) غير موجودة.", "TASK_NOT_FOUND");
        }

        task.IsCompleted = true;
        task.CompletedDate = DateTime.UtcNow;
        task.CompletedBy = string.IsNullOrWhiteSpace(completedBy) ? "SYSTEM" : completedBy;

        _recruitmentRepository.UpdateOnboardingTask(task);
        await _recruitmentRepository.SaveChangesAsync();

        _logger.LogInformation("Completed onboarding task #{Id} by {User}", taskId, completedBy);
        return MapToTaskDto(task);
    }

    private static JobRequisitionDto MapToRequisitionDto(JobRequisition r)
    {
        return new JobRequisitionDto
        {
            RequisitionCode = r.RequisitionCode,
            PositionCode = r.PositionCode,
            PositionTitleEn = r.Position?.TitleEn ?? string.Empty,
            DepartmentCode = r.DepartmentCode,
            DepartmentNameEn = r.Department?.NameEn ?? string.Empty,
            BranchId = r.BranchId,
            Headcount = r.Headcount,
            TargetHireDate = r.TargetHireDate,
            Status = r.Status,
            RequestedBy = r.RequestedBy,
            ApprovedBy = r.ApprovedBy,
            ApprovalDate = r.ApprovalDate,
            JobDescription = r.JobDescription,
            CandidateCount = r.Applications.Count,
            CreationDate = r.CreationDate
        };
    }

    private static CandidateDto MapToCandidateDto(Candidate c)
    {
        return new CandidateDto
        {
            CandidateCode = c.CandidateCode,
            NameAr = c.NameAr,
            NameEn = c.NameEn,
            Email = c.Email,
            Phone = c.Phone,
            NationalId = c.NationalId,
            ResumeFileReference = c.ResumeFileReference,
            Source = c.Source,
            CreationDate = c.CreationDate,
            Applications = c.Applications.Select(a => new CandidateApplicationDto
            {
                Id = a.Id,
                CandidateCode = a.CandidateCode,
                CandidateNameEn = c.NameEn,
                RequisitionCode = a.RequisitionCode,
                Stage = a.Stage,
                OfferedSalary = a.OfferedSalary,
                InterviewDate = a.InterviewDate,
                Notes = a.Notes,
                HiredEmployeeCode = a.HiredEmployeeCode,
                CreationDate = a.CreationDate
            }).ToList()
        };
    }

    private static OnboardingTaskDto MapToTaskDto(OnboardingTask t)
    {
        return new OnboardingTaskDto
        {
            Id = t.Id,
            EmployeeCode = t.EmployeeCode,
            TaskName = t.TaskName,
            AssignedTo = t.AssignedTo,
            DueDate = t.DueDate,
            IsCompleted = t.IsCompleted,
            CompletedDate = t.CompletedDate,
            CompletedBy = t.CompletedBy,
            Notes = t.Notes
        };
    }

    private static EmployeeDto MapToEmployeeDto(Employee e)
    {
        return new EmployeeDto
        {
            EmployeeCode = e.EmployeeCode,
            NameAr = e.NameAr,
            NameEn = e.NameEn,
            NationalId = e.NationalId,
            PassportNumber = e.PassportNumber,
            Nationality = e.Nationality,
            DateOfBirth = e.DateOfBirth,
            Gender = e.Gender,
            MaritalStatus = e.MaritalStatus,
            HireDate = e.HireDate,
            PositionCode = e.PositionCode,
            PositionTitle = e.Position?.TitleEn,
            DepartmentCode = e.DepartmentCode,
            DepartmentName = e.Department?.NameEn,
            BranchId = e.BranchId,
            EmploymentType = e.EmploymentType,
            EmploymentStatus = e.EmploymentStatus,
            SscNumber = e.SscNumber,
            IsHighRiskRole = e.IsHighRiskRole,
            TaxExemptionCount = e.TaxExemptionCount,
            BankAccountNumber = e.BankAccountNumber,
            BankName = e.BankName,
            BankIban = e.BankIban,
            Email = e.Email,
            Phone = e.Phone,
            ProbationEndDate = e.ProbationEndDate,
            ManagerEmployeeCode = e.ManagerEmployeeCode,
            IsActive = e.IsActive
        };
    }
}
