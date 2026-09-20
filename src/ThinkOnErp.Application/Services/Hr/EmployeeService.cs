using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IEmployeeService
{
    Task<(IReadOnlyList<EmployeeSummaryDto> Items, int TotalCount)> GetEmployeesPagedAsync(
        string? searchKeyword = null,
        string? departmentCode = null,
        string? status = null,
        long? branchId = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<EmployeeDetailsDto?> GetEmployeeByCodeAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<EmployeeDetailsDto> CreateEmployeeAsync(CreateEmployeeDto dto, string user, CancellationToken cancellationToken = default);
    Task<EmployeeDetailsDto> UpdateEmployeeAsync(string employeeCode, UpdateEmployeeDto dto, string user, CancellationToken cancellationToken = default);
    Task DeleteEmployeeAsync(string employeeCode, string user, CancellationToken cancellationToken = default);

    Task<DependentDto> AddDependentAsync(string employeeCode, CreateDependentDto dto, string user, CancellationToken cancellationToken = default);
    Task DeleteDependentAsync(long dependentId, string user, CancellationToken cancellationToken = default);

    Task<SalaryStructureDetailsDto> AssignSalaryStructureAsync(string employeeCode, AssignSalaryStructureDto dto, string user, CancellationToken cancellationToken = default);
    Task<SalaryStructureDetailsDto?> GetActiveSalaryStructureAsync(string employeeCode, DateTime? asOfDate = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalaryComponentDto>> GetSalaryComponentsAsync(CancellationToken cancellationToken = default);
    Task<SalaryComponentDto> CreateSalaryComponentAsync(CreateSalaryComponentDto dto, string user, CancellationToken cancellationToken = default);
}

public sealed class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepo;

    public EmployeeService(IEmployeeRepository employeeRepo)
    {
        _employeeRepo = employeeRepo;
    }

    public async Task<(IReadOnlyList<EmployeeSummaryDto> Items, int TotalCount)> GetEmployeesPagedAsync(
        string? searchKeyword = null,
        string? departmentCode = null,
        string? status = null,
        long? branchId = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _employeeRepo.GetEmployeesAsync(
            searchKeyword, departmentCode, status, branchId, pageIndex, pageSize, cancellationToken);

        var dtos = items.Select(e =>
        {
            var activeStructure = e.SalaryStructures
                .Where(s => s.IsActive && s.EffectiveFrom <= DateTime.UtcNow && (!s.EffectiveTo.HasValue || s.EffectiveTo.Value >= DateTime.UtcNow))
                .OrderByDescending(s => s.EffectiveFrom)
                .FirstOrDefault();

            return new EmployeeSummaryDto(
                e.EmployeeCode,
                e.NameLocal,
                e.NameEn,
                e.NationalId,
                e.DepartmentCode,
                e.PositionCode,
                e.EmploymentStatus,
                activeStructure?.BasicSalary,
                e.Phone,
                e.Email
            );
        }).ToList();

        return (dtos, totalCount);
    }

    public async Task<EmployeeDetailsDto?> GetEmployeeByCodeAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        var emp = await _employeeRepo.GetEmployeeByCodeAsync(employeeCode, cancellationToken);
        if (emp == null) return null;

        return MapToDetailsDto(emp);
    }

    public async Task<EmployeeDetailsDto> CreateEmployeeAsync(CreateEmployeeDto dto, string user, CancellationToken cancellationToken = default)
    {
        if (await _employeeRepo.ExistsByCodeAsync(dto.EmployeeCode, cancellationToken))
        {
            throw new InvalidOperationException($"Employee with code '{dto.EmployeeCode}' already exists.");
        }

        if (await _employeeRepo.ExistsByNationalIdAsync(dto.NationalId, null, cancellationToken))
        {
            throw new InvalidOperationException($"Employee with National ID '{dto.NationalId}' already exists.");
        }

        var emp = new Employee
        {
            EmployeeCode = dto.EmployeeCode.Trim().ToUpperInvariant(),
            NameLocal = dto.NameLocal,
            NameEn = dto.NameEn,
            NationalId = dto.NationalId,
            Nationality = dto.Nationality,
            PassportNumber = dto.PassportNumber,
            DateOfBirth = dto.DateOfBirth,
            Gender = dto.Gender,
            MaritalStatus = dto.MaritalStatus,
            Email = dto.Email,
            Phone = dto.Phone,
            HireDate = dto.HireDate,
            ProbationEndDate = dto.ProbationEndDate,
            EmploymentType = dto.EmploymentType,
            EmploymentStatus = dto.EmploymentStatus,
            DepartmentCode = dto.DepartmentCode,
            PositionCode = dto.PositionCode,
            BranchId = dto.BranchId,
            ManagerEmployeeCode = dto.ManagerEmployeeCode,
            SscNumber = dto.SscNumber,
            TaxExemptionCount = dto.TaxExemptionCount,
            IsHighRiskRole = dto.IsHighRiskRole,
            BankName = dto.BankName,
            BankAccountNumber = dto.BankAccountNumber,
            BankIban = dto.BankIban,
            IsActive = true,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        await _employeeRepo.AddEmployeeAsync(emp, cancellationToken);
        await _employeeRepo.SaveChangesAsync(cancellationToken);

        return MapToDetailsDto(emp);
    }

    public async Task<EmployeeDetailsDto> UpdateEmployeeAsync(string employeeCode, UpdateEmployeeDto dto, string user, CancellationToken cancellationToken = default)
    {
        var emp = await _employeeRepo.GetEmployeeByCodeAsync(employeeCode, cancellationToken);
        if (emp == null) throw new InvalidOperationException($"Employee with code '{employeeCode}' not found.");

        if (await _employeeRepo.ExistsByNationalIdAsync(dto.NationalId, employeeCode, cancellationToken))
        {
            throw new InvalidOperationException($"Another employee with National ID '{dto.NationalId}' already exists.");
        }

        emp.NameLocal = dto.NameLocal;
        emp.NameEn = dto.NameEn;
        emp.NationalId = dto.NationalId;
        emp.Nationality = dto.Nationality;
        emp.PassportNumber = dto.PassportNumber;
        emp.DateOfBirth = dto.DateOfBirth;
        emp.Gender = dto.Gender;
        emp.MaritalStatus = dto.MaritalStatus;
        emp.Email = dto.Email;
        emp.Phone = dto.Phone;
        emp.HireDate = dto.HireDate;
        emp.ProbationEndDate = dto.ProbationEndDate;
        emp.TerminationDate = dto.TerminationDate;
        emp.TerminationReason = dto.TerminationReason;
        emp.EmploymentType = dto.EmploymentType;
        emp.EmploymentStatus = dto.EmploymentStatus;
        emp.DepartmentCode = dto.DepartmentCode;
        emp.PositionCode = dto.PositionCode;
        emp.BranchId = dto.BranchId;
        emp.ManagerEmployeeCode = dto.ManagerEmployeeCode;
        emp.SscNumber = dto.SscNumber;
        emp.TaxExemptionCount = dto.TaxExemptionCount;
        emp.IsHighRiskRole = dto.IsHighRiskRole;
        emp.BankName = dto.BankName;
        emp.BankAccountNumber = dto.BankAccountNumber;
        emp.BankIban = dto.BankIban;
        emp.IsActive = dto.IsActive;
        emp.UpdateUser = user;
        emp.UpdateDate = DateTime.UtcNow;

        await _employeeRepo.UpdateEmployeeAsync(emp, cancellationToken);
        await _employeeRepo.SaveChangesAsync(cancellationToken);

        return MapToDetailsDto(emp);
    }

    public async Task DeleteEmployeeAsync(string employeeCode, string user, CancellationToken cancellationToken = default)
    {
        var emp = await _employeeRepo.GetEmployeeByCodeAsync(employeeCode, cancellationToken);
        if (emp == null) throw new InvalidOperationException($"Employee with code '{employeeCode}' not found.");

        emp.IsActive = false;
        emp.EmploymentStatus = "TERMINATED";
        emp.TerminationDate = DateTime.UtcNow;
        emp.TerminationReason = "Deactivated by user " + user;
        emp.UpdateUser = user;
        emp.UpdateDate = DateTime.UtcNow;

        await _employeeRepo.UpdateEmployeeAsync(emp, cancellationToken);
        await _employeeRepo.SaveChangesAsync(cancellationToken);
    }

    public async Task<DependentDto> AddDependentAsync(string employeeCode, CreateDependentDto dto, string user, CancellationToken cancellationToken = default)
    {
        var emp = await _employeeRepo.GetEmployeeByCodeAsync(employeeCode, cancellationToken);
        if (emp == null) throw new InvalidOperationException($"Employee with code '{employeeCode}' not found.");

        var dep = new EmployeeDependent
        {
            EmployeeCode = employeeCode,
            NameLocal = dto.NameLocal,
            NameEn = dto.NameEn,
            Relationship = dto.Relationship,
            DateOfBirth = dto.DateOfBirth,
            Gender = dto.Gender,
            NationalId = dto.NationalId,
            IsTaxExemptionClaimed = dto.IsTaxExemptionClaimed,
            IsMedicalCovered = dto.IsMedicalCovered,
            IsActive = true,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        await _employeeRepo.AddDependentAsync(dep, cancellationToken);

        // Update employee tax exemption count if claimed
        if (dep.IsTaxExemptionClaimed)
        {
            emp.TaxExemptionCount++;
            await _employeeRepo.UpdateEmployeeAsync(emp, cancellationToken);
        }

        await _employeeRepo.SaveChangesAsync(cancellationToken);

        return new DependentDto(
            dep.Id,
            dep.EmployeeCode,
            dep.NameLocal,
            dep.NameEn,
            dep.Relationship,
            dep.DateOfBirth,
            dep.Gender,
            dep.NationalId,
            dep.IsTaxExemptionClaimed,
            dep.IsMedicalCovered,
            dep.IsActive
        );
    }

    public async Task DeleteDependentAsync(long dependentId, string user, CancellationToken cancellationToken = default)
    {
        var dep = await _employeeRepo.GetDependentByIdAsync(dependentId, cancellationToken);
        if (dep == null) throw new InvalidOperationException($"Dependent with ID {dependentId} not found.");

        var emp = await _employeeRepo.GetEmployeeByCodeAsync(dep.EmployeeCode, cancellationToken);
        if (emp != null && dep.IsTaxExemptionClaimed && emp.TaxExemptionCount > 0)
        {
            emp.TaxExemptionCount--;
            await _employeeRepo.UpdateEmployeeAsync(emp, cancellationToken);
        }

        await _employeeRepo.DeleteDependentAsync(dep, cancellationToken);
        await _employeeRepo.SaveChangesAsync(cancellationToken);
    }

    public async Task<SalaryStructureDetailsDto> AssignSalaryStructureAsync(string employeeCode, AssignSalaryStructureDto dto, string user, CancellationToken cancellationToken = default)
    {
        var emp = await _employeeRepo.GetEmployeeByCodeAsync(employeeCode, cancellationToken);
        if (emp == null) throw new InvalidOperationException($"Employee with code '{employeeCode}' not found.");

        // Deactivate previous active structures or end their effective date
        var existingStructures = await _employeeRepo.GetSalaryStructuresAsync(employeeCode, cancellationToken);
        foreach (var existing in existingStructures.Where(s => s.IsActive && (!s.EffectiveTo.HasValue || s.EffectiveTo.Value >= dto.EffectiveFrom)))
        {
            existing.EffectiveTo = dto.EffectiveFrom.AddDays(-1);
            existing.UpdateUser = user;
            existing.UpdateDate = DateTime.UtcNow;
        }

        var structure = new SalaryStructure
        {
            EmployeeCode = employeeCode,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            BasicSalary = dto.BasicSalary,
            CurrencyCode = dto.CurrencyCode,
            PaymentMethod = dto.PaymentMethod,
            IsActive = true,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        if (dto.Lines != null && dto.Lines.Count > 0)
        {
            foreach (var line in dto.Lines)
            {
                structure.Lines.Add(new SalaryStructureLine
                {
                    ComponentCode = line.ComponentCode,
                    Amount = line.Amount,
                    Percent = line.Percent,
                    IsActive = true,
                    CreationUser = user,
                    CreationDate = DateTime.UtcNow
                });
            }
        }

        await _employeeRepo.AddSalaryStructureAsync(structure, cancellationToken);
        await _employeeRepo.SaveChangesAsync(cancellationToken);

        // Fetch reloaded structure with component names
        var active = await _employeeRepo.GetActiveSalaryStructureAsync(employeeCode, dto.EffectiveFrom, cancellationToken);
        return MapToStructureDetailsDto(active ?? structure);
    }

    public async Task<SalaryStructureDetailsDto?> GetActiveSalaryStructureAsync(string employeeCode, DateTime? asOfDate = null, CancellationToken cancellationToken = default)
    {
        var targetDate = asOfDate ?? DateTime.UtcNow;
        var structure = await _employeeRepo.GetActiveSalaryStructureAsync(employeeCode, targetDate, cancellationToken);
        return structure == null ? null : MapToStructureDetailsDto(structure);
    }

    public async Task<IReadOnlyList<SalaryComponentDto>> GetSalaryComponentsAsync(CancellationToken cancellationToken = default)
    {
        var components = await _employeeRepo.GetSalaryComponentsAsync(cancellationToken);
        return components.Select(c => new SalaryComponentDto(
            c.ComponentCode,
            c.NameLocal,
            c.NameEn,
            c.ComponentType,
            c.CalculationType,
            c.DefaultAmount,
            c.DefaultPercent,
            c.IsTaxable,
            c.IsSscApplicable,
            c.GlAccountCode,
            c.IsActive
        )).ToList();
    }

    public async Task<SalaryComponentDto> CreateSalaryComponentAsync(CreateSalaryComponentDto dto, string user, CancellationToken cancellationToken = default)
    {
        var existing = await _employeeRepo.GetSalaryComponentByCodeAsync(dto.ComponentCode, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Salary component '{dto.ComponentCode}' already exists.");
        }

        var component = new SalaryComponent
        {
            ComponentCode = dto.ComponentCode.Trim().ToUpperInvariant(),
            NameLocal = dto.NameLocal,
            NameEn = dto.NameEn,
            ComponentType = dto.ComponentType.ToUpperInvariant(),
            CalculationType = dto.CalculationType.ToUpperInvariant(),
            DefaultAmount = dto.DefaultAmount,
            DefaultPercent = dto.DefaultPercent,
            IsTaxable = dto.IsTaxable,
            IsSscApplicable = dto.IsSscApplicable,
            GlAccountCode = dto.GlAccountCode,
            IsActive = true,
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        await _employeeRepo.AddSalaryComponentAsync(component, cancellationToken);
        await _employeeRepo.SaveChangesAsync(cancellationToken);

        return new SalaryComponentDto(
            component.ComponentCode,
            component.NameLocal,
            component.NameEn,
            component.ComponentType,
            component.CalculationType,
            component.DefaultAmount,
            component.DefaultPercent,
            component.IsTaxable,
            component.IsSscApplicable,
            component.GlAccountCode,
            component.IsActive
        );
    }

    private static EmployeeDetailsDto MapToDetailsDto(Employee emp)
    {
        var activeStructure = emp.SalaryStructures
            .Where(s => s.IsActive && s.EffectiveFrom <= DateTime.UtcNow && (!s.EffectiveTo.HasValue || s.EffectiveTo.Value >= DateTime.UtcNow))
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefault();

        var dependents = emp.Dependents.Select(d => new DependentDto(
            d.Id,
            d.EmployeeCode,
            d.NameLocal,
            d.NameEn,
            d.Relationship,
            d.DateOfBirth,
            d.Gender,
            d.NationalId,
            d.IsTaxExemptionClaimed,
            d.IsMedicalCovered,
            d.IsActive
        )).ToList();

        return new EmployeeDetailsDto(
            emp.EmployeeCode,
            emp.NameLocal,
            emp.NameEn,
            emp.NationalId,
            emp.Nationality,
            emp.PassportNumber,
            emp.DateOfBirth,
            emp.Gender,
            emp.MaritalStatus,
            emp.Email,
            emp.Phone,
            emp.HireDate,
            emp.ProbationEndDate,
            emp.TerminationDate,
            emp.TerminationReason,
            emp.EmploymentType,
            emp.EmploymentStatus,
            emp.DepartmentCode,
            emp.PositionCode,
            emp.BranchId,
            emp.ManagerEmployeeCode,
            emp.SscNumber,
            emp.TaxExemptionCount,
            emp.IsHighRiskRole,
            emp.BankName,
            emp.BankAccountNumber,
            emp.BankIban,
            emp.IsActive,
            dependents,
            activeStructure == null ? null : MapToStructureDetailsDto(activeStructure)
        );
    }

    private static SalaryStructureDetailsDto MapToStructureDetailsDto(SalaryStructure s)
    {
        var lines = s.Lines.Select(l => new SalaryStructureLineDetailDto(
            l.Id,
            l.ComponentCode,
            l.Component?.NameLocal ?? l.ComponentCode,
            l.Component?.NameEn ?? l.ComponentCode,
            l.Component?.ComponentType ?? "ALLOWANCE",
            l.Amount,
            l.Percent,
            l.IsActive
        )).ToList();

        return new SalaryStructureDetailsDto(
            s.Id,
            s.EmployeeCode,
            s.EffectiveFrom,
            s.EffectiveTo,
            s.BasicSalary,
            s.CurrencyCode,
            s.PaymentMethod,
            s.IsActive,
            lines
        );
    }
}
