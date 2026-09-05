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

public sealed class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly IEmployeeDependentRepository _dependentRepository;
    private readonly IEmploymentEventRepository _eventRepository;
    private readonly ILogger<EmployeeService> _logger;

    public EmployeeService(
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository,
        IPositionRepository positionRepository,
        IEmployeeDependentRepository dependentRepository,
        IEmploymentEventRepository eventRepository,
        ILogger<EmployeeService> logger)
    {
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
        _positionRepository = positionRepository ?? throw new ArgumentNullException(nameof(positionRepository));
        _dependentRepository = dependentRepository ?? throw new ArgumentNullException(nameof(dependentRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<EmployeeDto>> GetAllAsync(
        string? departmentCode = null,
        long? branchId = null,
        string? status = null,
        bool activeOnly = true)
    {
        var list = await _employeeRepository.GetAllAsync(departmentCode, branchId, status, activeOnly);
        return list.Select(MapToDto).ToList();
    }

    public async Task<EmployeeDto?> GetByCodeAsync(string employeeCode, bool includeDetails = true)
    {
        var emp = await _employeeRepository.GetByCodeAsync(employeeCode, includeDetails);
        return emp == null ? null : MapToDto(emp);
    }

    public async Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var code = dto.EmployeeCode.Trim().ToUpperInvariant();

        if (await _employeeRepository.CodeExistsAsync(code))
        {
            throw new HrConflictException($"الرقم الوظيفي ({code}) موجود مسبقاً.", "EMPLOYEE_CODE_DUPLICATE");
        }

        if (await _employeeRepository.NationalIdExistsAsync(dto.NationalId.Trim()))
        {
            throw new HrConflictException($"الرقم الوطني ({dto.NationalId}) مسجل مسبقاً لموظف آخر.", "NATIONAL_ID_DUPLICATE");
        }

        if (!string.IsNullOrWhiteSpace(dto.DepartmentCode))
        {
            var dept = await _departmentRepository.GetByCodeAsync(dto.DepartmentCode);
            if (dept == null)
            {
                throw new HrNotFoundException($"القسم ({dto.DepartmentCode}) غير موجود.", "DEPARTMENT_NOT_FOUND");
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.PositionCode))
        {
            var pos = await _positionRepository.GetByCodeAsync(dto.PositionCode);
            if (pos == null)
            {
                throw new HrNotFoundException($"المنصب ({dto.PositionCode}) غير موجود.", "POSITION_NOT_FOUND");
            }
        }

        var now = DateTime.UtcNow;
        var user = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;

        var employee = new Employee
        {
            EmployeeCode = code,
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            NationalId = dto.NationalId.Trim(),
            PassportNumber = string.IsNullOrWhiteSpace(dto.PassportNumber) ? null : dto.PassportNumber.Trim(),
            Nationality = string.IsNullOrWhiteSpace(dto.Nationality) ? "Jordanian" : dto.Nationality.Trim(),
            DateOfBirth = dto.DateOfBirth,
            Gender = string.IsNullOrWhiteSpace(dto.Gender) ? "MALE" : dto.Gender.Trim().ToUpperInvariant(),
            MaritalStatus = string.IsNullOrWhiteSpace(dto.MaritalStatus) ? "SINGLE" : dto.MaritalStatus.Trim().ToUpperInvariant(),
            HireDate = dto.HireDate,
            PositionCode = string.IsNullOrWhiteSpace(dto.PositionCode) ? null : dto.PositionCode.Trim().ToUpperInvariant(),
            DepartmentCode = string.IsNullOrWhiteSpace(dto.DepartmentCode) ? null : dto.DepartmentCode.Trim().ToUpperInvariant(),
            BranchId = dto.BranchId,
            EmploymentType = string.IsNullOrWhiteSpace(dto.EmploymentType) ? "FULL_TIME" : dto.EmploymentType.Trim().ToUpperInvariant(),
            EmploymentStatus = string.IsNullOrWhiteSpace(dto.EmploymentStatus) ? "ACTIVE" : dto.EmploymentStatus.Trim().ToUpperInvariant(),
            SscNumber = string.IsNullOrWhiteSpace(dto.SscNumber) ? null : dto.SscNumber.Trim(),
            IsHighRiskRole = dto.IsHighRiskRole,
            TaxExemptionCount = dto.TaxExemptionCount >= 0 ? dto.TaxExemptionCount : 0,
            BankAccountNumber = dto.BankAccountNumber?.Trim(),
            BankName = dto.BankName?.Trim(),
            BankIban = dto.BankIban?.Trim(),
            Email = dto.Email?.Trim(),
            Phone = dto.Phone?.Trim(),
            ProbationEndDate = dto.ProbationEndDate,
            ManagerEmployeeCode = string.IsNullOrWhiteSpace(dto.ManagerEmployeeCode) ? null : dto.ManagerEmployeeCode.Trim().ToUpperInvariant(),
            IsActive = true,
            CreationUser = user,
            CreationDate = now
        };

        await _employeeRepository.AddAsync(employee);

        // Immutable HIRE lifecycle event
        var hireEvent = new EmploymentEvent
        {
            EmployeeCode = code,
            EventType = "HIRE",
            EffectiveDate = dto.HireDate,
            FromValue = null,
            ToValue = employee.EmploymentStatus,
            Reason = "New employee onboarded into ThinkOn ERP",
            ApprovedBy = user,
            CreationDate = now
        };
        await _eventRepository.AddAsync(hireEvent);

        await _employeeRepository.SaveChangesAsync();
        _logger.LogInformation("Created employee {Code} ({NameEn}) with HIRE event by {User}", code, employee.NameEn, user);

        return (await GetByCodeAsync(code, includeDetails: true))!;
    }

    public async Task<EmployeeDto> UpdateAsync(string employeeCode, UpdateEmployeeDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var employee = await _employeeRepository.GetByCodeAsync(employeeCode, includeDetails: true);
        if (employee == null)
        {
            throw new HrNotFoundException($"الموظف ({employeeCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        if (await _employeeRepository.NationalIdExistsAsync(dto.NationalId.Trim(), excludeEmployeeCode: employeeCode))
        {
            throw new HrConflictException($"الرقم الوطني ({dto.NationalId}) مسجل مسبقاً لموظف آخر.", "NATIONAL_ID_DUPLICATE");
        }

        if (!string.IsNullOrWhiteSpace(dto.DepartmentCode))
        {
            var dept = await _departmentRepository.GetByCodeAsync(dto.DepartmentCode);
            if (dept == null)
            {
                throw new HrNotFoundException($"القسم ({dto.DepartmentCode}) غير موجود.", "DEPARTMENT_NOT_FOUND");
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.PositionCode))
        {
            var pos = await _positionRepository.GetByCodeAsync(dto.PositionCode);
            if (pos == null)
            {
                throw new HrNotFoundException($"المنصب ({dto.PositionCode}) غير موجود.", "POSITION_NOT_FOUND");
            }
        }

        var oldPosition = employee.PositionCode;
        var oldDepartment = employee.DepartmentCode;

        employee.NameAr = dto.NameAr.Trim();
        employee.NameEn = dto.NameEn.Trim();
        employee.NationalId = dto.NationalId.Trim();
        employee.PassportNumber = string.IsNullOrWhiteSpace(dto.PassportNumber) ? null : dto.PassportNumber.Trim();
        employee.Nationality = string.IsNullOrWhiteSpace(dto.Nationality) ? "Jordanian" : dto.Nationality.Trim();
        employee.DateOfBirth = dto.DateOfBirth;
        employee.Gender = string.IsNullOrWhiteSpace(dto.Gender) ? "MALE" : dto.Gender.Trim().ToUpperInvariant();
        employee.MaritalStatus = string.IsNullOrWhiteSpace(dto.MaritalStatus) ? "SINGLE" : dto.MaritalStatus.Trim().ToUpperInvariant();
        employee.PositionCode = string.IsNullOrWhiteSpace(dto.PositionCode) ? null : dto.PositionCode.Trim().ToUpperInvariant();
        employee.DepartmentCode = string.IsNullOrWhiteSpace(dto.DepartmentCode) ? null : dto.DepartmentCode.Trim().ToUpperInvariant();
        employee.BranchId = dto.BranchId;
        employee.EmploymentType = string.IsNullOrWhiteSpace(dto.EmploymentType) ? "FULL_TIME" : dto.EmploymentType.Trim().ToUpperInvariant();
        employee.SscNumber = string.IsNullOrWhiteSpace(dto.SscNumber) ? null : dto.SscNumber.Trim();
        employee.IsHighRiskRole = dto.IsHighRiskRole;
        employee.TaxExemptionCount = dto.TaxExemptionCount >= 0 ? dto.TaxExemptionCount : 0;
        employee.BankAccountNumber = dto.BankAccountNumber?.Trim();
        employee.BankName = dto.BankName?.Trim();
        employee.BankIban = dto.BankIban?.Trim();
        employee.Email = dto.Email?.Trim();
        employee.Phone = dto.Phone?.Trim();
        employee.ProbationEndDate = dto.ProbationEndDate;
        employee.ManagerEmployeeCode = string.IsNullOrWhiteSpace(dto.ManagerEmployeeCode) ? null : dto.ManagerEmployeeCode.Trim().ToUpperInvariant();
        employee.IsActive = dto.IsActive;
        employee.UpdateUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        employee.UpdateDate = DateTime.UtcNow;

        // Record transfer/promotion event if position or department changed
        if (oldDepartment != employee.DepartmentCode || oldPosition != employee.PositionCode)
        {
            var transferEvent = new EmploymentEvent
            {
                EmployeeCode = employeeCode,
                EventType = oldPosition != employee.PositionCode ? "PROMOTION" : "TRANSFER",
                EffectiveDate = DateTime.UtcNow,
                FromValue = $"Dept: {oldDepartment}, Pos: {oldPosition}",
                ToValue = $"Dept: {employee.DepartmentCode}, Pos: {employee.PositionCode}",
                Reason = "Position/Department profile update",
                ApprovedBy = employee.UpdateUser,
                CreationDate = DateTime.UtcNow
            };
            await _eventRepository.AddAsync(transferEvent);
        }

        _employeeRepository.Update(employee);
        await _employeeRepository.SaveChangesAsync();

        _logger.LogInformation("Updated employee {Code} by {User}", employeeCode, currentUser);
        return (await GetByCodeAsync(employeeCode, includeDetails: true))!;
    }

    public async Task<EmployeeDto> ChangeStatusAsync(string employeeCode, ChangeEmployeeStatusDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var employee = await _employeeRepository.GetByCodeAsync(employeeCode, includeDetails: true);
        if (employee == null)
        {
            throw new HrNotFoundException($"الموظف ({employeeCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var oldStatus = employee.EmploymentStatus;
        var newStatus = dto.NewStatus.Trim().ToUpperInvariant();
        var user = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;

        employee.EmploymentStatus = newStatus;
        employee.UpdateUser = user;
        employee.UpdateDate = DateTime.UtcNow;

        if (newStatus == "TERMINATED")
        {
            employee.TerminationDate = dto.EffectiveDate;
            employee.TerminationReason = dto.Reason;
            employee.IsActive = false;
        }
        else if (oldStatus == "TERMINATED" && newStatus == "ACTIVE")
        {
            employee.TerminationDate = null;
            employee.TerminationReason = null;
            employee.IsActive = true;
        }

        // Record immutable lifecycle event
        var eventType = newStatus switch
        {
            "TERMINATED" => "TERMINATION",
            "ACTIVE" when oldStatus == "TERMINATED" => "REHIRE",
            "ACTIVE" when oldStatus == "PROBATION" => "PROBATION_CONFIRM",
            "SUSPENDED" => "SUSPENSION",
            _ => "STATUS_CHANGE"
        };

        var lifecycleEvent = new EmploymentEvent
        {
            EmployeeCode = employeeCode,
            EventType = eventType,
            EffectiveDate = dto.EffectiveDate,
            FromValue = oldStatus,
            ToValue = newStatus,
            Reason = dto.Reason,
            ApprovedBy = string.IsNullOrWhiteSpace(dto.ApprovedBy) ? user : dto.ApprovedBy,
            CreationDate = DateTime.UtcNow
        };

        await _eventRepository.AddAsync(lifecycleEvent);
        _employeeRepository.Update(employee);
        await _employeeRepository.SaveChangesAsync();

        _logger.LogInformation("Changed status for employee {Code} from {Old} to {New} by {User}", employeeCode, oldStatus, newStatus, user);
        return (await GetByCodeAsync(employeeCode, includeDetails: true))!;
    }

    public async Task<EmploymentEventDto> RecordEventAsync(string employeeCode, RecordEmploymentEventDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var employee = await _employeeRepository.GetByCodeAsync(employeeCode);
        if (employee == null)
        {
            throw new HrNotFoundException($"الموظف ({employeeCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var employmentEvent = new EmploymentEvent
        {
            EmployeeCode = employeeCode,
            EventType = dto.EventType.Trim().ToUpperInvariant(),
            EffectiveDate = dto.EffectiveDate,
            FromValue = dto.FromValue,
            ToValue = dto.ToValue,
            Reason = dto.Reason,
            ApprovedBy = string.IsNullOrWhiteSpace(dto.ApprovedBy) ? (string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser) : dto.ApprovedBy,
            CreationDate = DateTime.UtcNow
        };

        await _eventRepository.AddAsync(employmentEvent);
        await _eventRepository.SaveChangesAsync();

        _logger.LogInformation("Recorded employment event {EventType} for employee {Code}", employmentEvent.EventType, employeeCode);

        return new EmploymentEventDto
        {
            Id = employmentEvent.Id,
            EmployeeCode = employmentEvent.EmployeeCode,
            EventType = employmentEvent.EventType,
            EffectiveDate = employmentEvent.EffectiveDate,
            FromValue = employmentEvent.FromValue,
            ToValue = employmentEvent.ToValue,
            Reason = employmentEvent.Reason,
            ApprovedBy = employmentEvent.ApprovedBy,
            CreationDate = employmentEvent.CreationDate
        };
    }

    public async Task<List<EmploymentEventDto>> GetEventsAsync(string employeeCode)
    {
        var events = await _eventRepository.GetByEmployeeCodeAsync(employeeCode);
        return events.Select(e => new EmploymentEventDto
        {
            Id = e.Id,
            EmployeeCode = e.EmployeeCode,
            EventType = e.EventType,
            EffectiveDate = e.EffectiveDate,
            FromValue = e.FromValue,
            ToValue = e.ToValue,
            Reason = e.Reason,
            ApprovedBy = e.ApprovedBy,
            CreationDate = e.CreationDate
        }).ToList();
    }

    public async Task<List<EmployeeDependentDto>> GetDependentsAsync(string employeeCode)
    {
        var list = await _dependentRepository.GetByEmployeeCodeAsync(employeeCode);
        return list.Select(MapToDependentDto).ToList();
    }

    public async Task<EmployeeDependentDto> AddDependentAsync(string employeeCode, CreateEmployeeDependentDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var employee = await _employeeRepository.GetByCodeAsync(employeeCode);
        if (employee == null)
        {
            throw new HrNotFoundException($"الموظف ({employeeCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var dependent = new EmployeeDependent
        {
            EmployeeCode = employeeCode,
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            Relationship = string.IsNullOrWhiteSpace(dto.Relationship) ? "CHILD" : dto.Relationship.Trim().ToUpperInvariant(),
            NationalId = dto.NationalId?.Trim(),
            DateOfBirth = dto.DateOfBirth,
            Gender = string.IsNullOrWhiteSpace(dto.Gender) ? "MALE" : dto.Gender.Trim().ToUpperInvariant(),
            IsTaxExemptionClaimed = dto.IsTaxExemptionClaimed,
            IsMedicalCovered = dto.IsMedicalCovered,
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _dependentRepository.AddAsync(dependent);

        // Auto update tax exemption count on employee profile
        if (dependent.IsTaxExemptionClaimed)
        {
            var currentClaimed = (await _dependentRepository.GetByEmployeeCodeAsync(employeeCode))
                .Count(d => d.IsTaxExemptionClaimed) + 1;
            employee.TaxExemptionCount = currentClaimed;
            _employeeRepository.Update(employee);
        }

        await _dependentRepository.SaveChangesAsync();
        _logger.LogInformation("Added dependent {NameEn} for employee {Code}", dependent.NameEn, employeeCode);

        return MapToDependentDto(dependent);
    }

    public async Task<EmployeeDependentDto> UpdateDependentAsync(long dependentId, UpdateEmployeeDependentDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var dependent = await _dependentRepository.GetByIdAsync(dependentId);
        if (dependent == null)
        {
            throw new HrNotFoundException($"بيانات التابع رقم ({dependentId}) غير موجودة.", "DEPENDENT_NOT_FOUND");
        }

        dependent.NameAr = dto.NameAr.Trim();
        dependent.NameEn = dto.NameEn.Trim();
        dependent.Relationship = string.IsNullOrWhiteSpace(dto.Relationship) ? "CHILD" : dto.Relationship.Trim().ToUpperInvariant();
        dependent.NationalId = dto.NationalId?.Trim();
        dependent.DateOfBirth = dto.DateOfBirth;
        dependent.Gender = string.IsNullOrWhiteSpace(dto.Gender) ? "MALE" : dto.Gender.Trim().ToUpperInvariant();
        dependent.IsTaxExemptionClaimed = dto.IsTaxExemptionClaimed;
        dependent.IsMedicalCovered = dto.IsMedicalCovered;
        dependent.IsActive = dto.IsActive;
        dependent.UpdateUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser;
        dependent.UpdateDate = DateTime.UtcNow;

        _dependentRepository.Update(dependent);

        // Recalculate tax exemptions on employee
        var employee = await _employeeRepository.GetByCodeAsync(dependent.EmployeeCode);
        if (employee != null)
        {
            var claimed = (await _dependentRepository.GetByEmployeeCodeAsync(dependent.EmployeeCode))
                .Count(d => d.Id != dependentId && d.IsTaxExemptionClaimed) + (dto.IsTaxExemptionClaimed && dto.IsActive ? 1 : 0);
            employee.TaxExemptionCount = claimed;
            _employeeRepository.Update(employee);
        }

        await _dependentRepository.SaveChangesAsync();
        return MapToDependentDto(dependent);
    }

    public async Task<bool> DeleteDependentAsync(long dependentId)
    {
        var dependent = await _dependentRepository.GetByIdAsync(dependentId);
        if (dependent == null)
        {
            return false;
        }

        var empCode = dependent.EmployeeCode;
        _dependentRepository.Remove(dependent);

        var employee = await _employeeRepository.GetByCodeAsync(empCode);
        if (employee != null)
        {
            var claimed = (await _dependentRepository.GetByEmployeeCodeAsync(empCode))
                .Count(d => d.Id != dependentId && d.IsTaxExemptionClaimed);
            employee.TaxExemptionCount = claimed;
            _employeeRepository.Update(employee);
        }

        await _dependentRepository.SaveChangesAsync();
        return true;
    }

    private static EmployeeDto MapToDto(Employee e)
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
            BranchName = e.Branch?.BranchNameEn,
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
            TerminationDate = e.TerminationDate,
            TerminationReason = e.TerminationReason,
            ManagerEmployeeCode = e.ManagerEmployeeCode,
            ManagerName = e.Manager?.NameEn,
            IsActive = e.IsActive,
            CreationDate = e.CreationDate,
            Dependents = e.Dependents.Select(MapToDependentDto).ToList(),
            Documents = e.Documents.Select(MapToDocumentDto).ToList()
        };
    }

    private static EmployeeDependentDto MapToDependentDto(EmployeeDependent d)
    {
        return new EmployeeDependentDto
        {
            Id = d.Id,
            EmployeeCode = d.EmployeeCode,
            NameAr = d.NameAr,
            NameEn = d.NameEn,
            Relationship = d.Relationship,
            NationalId = d.NationalId,
            DateOfBirth = d.DateOfBirth,
            Gender = d.Gender,
            IsTaxExemptionClaimed = d.IsTaxExemptionClaimed,
            IsMedicalCovered = d.IsMedicalCovered,
            IsActive = d.IsActive
        };
    }

    private static EmployeeDocumentDto MapToDocumentDto(EmployeeDocument doc)
    {
        return new EmployeeDocumentDto
        {
            Id = doc.Id,
            EmployeeCode = doc.EmployeeCode,
            DocumentType = doc.DocumentType,
            DocumentNumber = doc.DocumentNumber,
            FileReference = doc.FileReference,
            FileName = doc.FileName,
            IssuedDate = doc.IssuedDate,
            ExpiryDate = doc.ExpiryDate,
            Notes = doc.Notes,
            IsActive = doc.IsActive
        };
    }
}
