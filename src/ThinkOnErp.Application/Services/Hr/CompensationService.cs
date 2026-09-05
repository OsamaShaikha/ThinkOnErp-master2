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

public sealed class CompensationService : ICompensationService
{
    private readonly ICompensationRepository _compensationRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IStatutoryRuleService _statutoryRuleService;
    private readonly ILogger<CompensationService> _logger;

    public CompensationService(
        ICompensationRepository compensationRepository,
        IEmployeeRepository employeeRepository,
        IStatutoryRuleService statutoryRuleService,
        ILogger<CompensationService> logger)
    {
        _compensationRepository = compensationRepository ?? throw new ArgumentNullException(nameof(compensationRepository));
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _statutoryRuleService = statutoryRuleService ?? throw new ArgumentNullException(nameof(statutoryRuleService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<SalaryComponentDto>> GetAllComponentsAsync(bool activeOnly = true)
    {
        var components = await _compensationRepository.GetAllComponentsAsync(activeOnly);
        return components.Select(MapToComponentDto).ToList();
    }

    public async Task<SalaryComponentDto?> GetComponentByCodeAsync(string code)
    {
        var comp = await _compensationRepository.GetComponentByCodeAsync(code);
        return comp == null ? null : MapToComponentDto(comp);
    }

    public async Task<SalaryComponentDto> CreateComponentAsync(CreateSalaryComponentDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var code = dto.ComponentCode.Trim().ToUpperInvariant();

        if (await _compensationRepository.ComponentCodeExistsAsync(code))
        {
            throw new HrConflictException($"رمز بند الراتب ({code}) معرف مسبقاً.", "SALARY_COMPONENT_DUPLICATE");
        }

        var component = new SalaryComponent
        {
            ComponentCode = code,
            NameAr = dto.NameAr.Trim(),
            NameEn = dto.NameEn.Trim(),
            ComponentType = string.IsNullOrWhiteSpace(dto.ComponentType) ? "EARNING" : dto.ComponentType.Trim().ToUpperInvariant(),
            IsTaxable = dto.IsTaxable,
            IsSscApplicable = dto.IsSscApplicable,
            CalculationType = string.IsNullOrWhiteSpace(dto.CalculationType) ? "FIXED_AMOUNT" : dto.CalculationType.Trim().ToUpperInvariant(),
            DefaultAmount = dto.DefaultAmount,
            DefaultPercent = dto.DefaultPercent,
            GlAccountCode = dto.GlAccountCode?.Trim(),
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _compensationRepository.AddComponentAsync(component);
        await _compensationRepository.SaveChangesAsync();

        _logger.LogInformation("Created salary component {Code} by {User}", code, currentUser);
        return MapToComponentDto(component);
    }

    public async Task<EmployeeSalaryStructureDto?> GetActiveStructureAsync(string employeeCode, DateTime? effectiveDate = null)
    {
        var date = effectiveDate ?? DateTime.UtcNow;
        var structure = await _compensationRepository.GetActiveStructureAsync(employeeCode, date);
        return structure == null ? null : MapToStructureDto(structure);
    }

    public async Task<List<EmployeeSalaryStructureDto>> GetStructureHistoryAsync(string employeeCode)
    {
        var list = await _compensationRepository.GetStructureHistoryAsync(employeeCode);
        return list.Select(MapToStructureDto).ToList();
    }

    public async Task<EmployeeSalaryStructureDto> SetStructureAsync(string employeeCode, SetEmployeeSalaryStructureDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var empCode = employeeCode.Trim().ToUpperInvariant();

        var emp = await _employeeRepository.GetByCodeAsync(empCode);
        if (emp == null)
        {
            throw new HrNotFoundException($"الموظف ({empCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        // Validate minimum wage statutory rule
        var minWage = await _statutoryRuleService.GetEffectiveValueAsync("MINIMUM_WAGE", dto.EffectiveFrom);
        if (minWage > 0 && dto.BasicSalary < minWage)
        {
            throw new HrValidationException($"الراتب الأساسي ({dto.BasicSalary} د.أ) أقل من الحد الأدنى القانوني للأجور في الأردن ({minWage} د.أ).", "BELOW_MINIMUM_WAGE");
        }

        // Close previous active structure
        var activeOld = await _compensationRepository.GetActiveStructureAsync(empCode, dto.EffectiveFrom);
        if (activeOld != null && activeOld.EffectiveTo == null)
        {
            activeOld.EffectiveTo = dto.EffectiveFrom.AddDays(-1);
            activeOld.UpdateUser = currentUser;
            activeOld.UpdateDate = DateTime.UtcNow;
            _compensationRepository.UpdateStructure(activeOld);
        }

        var structure = new EmployeeSalaryStructure
        {
            EmployeeCode = empCode,
            EffectiveFrom = dto.EffectiveFrom.Date,
            BasicSalary = dto.BasicSalary,
            CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? "JOD" : dto.CurrencyCode.Trim().ToUpperInvariant(),
            PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "BANK_TRANSFER" : dto.PaymentMethod.Trim().ToUpperInvariant(),
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        if (dto.Lines != null)
        {
            foreach (var line in dto.Lines)
            {
                var compCode = line.ComponentCode.Trim().ToUpperInvariant();
                var comp = await _compensationRepository.GetComponentByCodeAsync(compCode);
                if (comp == null)
                {
                    throw new HrNotFoundException($"بند الراتب ({compCode}) غير موجود.", "COMPONENT_NOT_FOUND");
                }

                structure.Lines.Add(new EmployeeSalaryStructureLine
                {
                    ComponentCode = compCode,
                    Amount = line.Amount,
                    Percent = line.Percent,
                    IsActive = true,
                    CreationUser = structure.CreationUser,
                    CreationDate = DateTime.UtcNow
                });
            }
        }

        await _compensationRepository.AddStructureAsync(structure);
        await _compensationRepository.SaveChangesAsync();

        _logger.LogInformation("Set salary structure for employee {Emp} (Basic: {Basic}) by {User}", empCode, dto.BasicSalary, currentUser);
        return (await GetActiveStructureAsync(empCode, dto.EffectiveFrom))!;
    }

    public async Task<List<SalaryRevisionDto>> GetSalaryRevisionsAsync(string employeeCode)
    {
        var list = await _compensationRepository.GetRevisionsByEmployeeAsync(employeeCode);
        return list.Select(r => new SalaryRevisionDto
        {
            Id = r.Id,
            EmployeeCode = r.EmployeeCode,
            EffectiveDate = r.EffectiveDate,
            OldBasicSalary = r.OldBasicSalary,
            NewBasicSalary = r.NewBasicSalary,
            OldGrossSalary = r.OldGrossSalary,
            NewGrossSalary = r.NewGrossSalary,
            Reason = r.Reason,
            ApprovedBy = r.ApprovedBy,
            CreationDate = r.CreationDate
        }).ToList();
    }

    public async Task<SalaryRevisionDto> CreateRevisionAsync(string employeeCode, CreateSalaryRevisionDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var empCode = employeeCode.Trim().ToUpperInvariant();

        var emp = await _employeeRepository.GetByCodeAsync(empCode);
        if (emp == null)
        {
            throw new HrNotFoundException($"الموظف ({empCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var currentStructure = await _compensationRepository.GetActiveStructureAsync(empCode, dto.EffectiveDate);
        var oldBasic = currentStructure?.BasicSalary ?? 0m;
        var oldGross = oldBasic + (currentStructure?.Lines.Where(l => l.Component?.ComponentType == "EARNING").Sum(l => l.Amount) ?? 0m);

        var newGross = dto.NewBasicSalary + (dto.NewAllowanceLines?.Sum(l => l.Amount) ?? 0m);

        var revision = new SalaryRevision
        {
            EmployeeCode = empCode,
            EffectiveDate = dto.EffectiveDate.Date,
            OldBasicSalary = oldBasic,
            NewBasicSalary = dto.NewBasicSalary,
            OldGrossSalary = oldGross,
            NewGrossSalary = newGross,
            Reason = dto.Reason.Trim(),
            ApprovedBy = string.IsNullOrWhiteSpace(dto.ApprovedBy) ? (string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser) : dto.ApprovedBy,
            CreationDate = DateTime.UtcNow
        };

        await _compensationRepository.AddRevisionAsync(revision);

        // Apply new structure
        var setStructureDto = new SetEmployeeSalaryStructureDto
        {
            EffectiveFrom = dto.EffectiveDate.Date,
            BasicSalary = dto.NewBasicSalary,
            CurrencyCode = currentStructure?.CurrencyCode ?? "JOD",
            PaymentMethod = currentStructure?.PaymentMethod ?? "BANK_TRANSFER",
            Lines = dto.NewAllowanceLines ?? new List<SalaryStructureLineInputDto>()
        };

        await SetStructureAsync(empCode, setStructureDto, currentUser);

        _logger.LogInformation("Recorded salary revision for {Emp}: Basic {Old} -> {New}", empCode, oldBasic, dto.NewBasicSalary);

        return new SalaryRevisionDto
        {
            Id = revision.Id,
            EmployeeCode = revision.EmployeeCode,
            EffectiveDate = revision.EffectiveDate,
            OldBasicSalary = revision.OldBasicSalary,
            NewBasicSalary = revision.NewBasicSalary,
            OldGrossSalary = revision.OldGrossSalary,
            NewGrossSalary = revision.NewGrossSalary,
            Reason = revision.Reason,
            ApprovedBy = revision.ApprovedBy,
            CreationDate = revision.CreationDate
        };
    }

    public async Task<List<EmploymentContractDto>> GetContractsAsync(string employeeCode)
    {
        var list = await _compensationRepository.GetContractsByEmployeeAsync(employeeCode);
        return list.Select(MapToContractDto).ToList();
    }

    public async Task<EmploymentContractDto?> GetActiveContractAsync(string employeeCode)
    {
        var contract = await _compensationRepository.GetActiveContractAsync(employeeCode);
        return contract == null ? null : MapToContractDto(contract);
    }

    public async Task<EmploymentContractDto> CreateContractAsync(string employeeCode, CreateEmploymentContractDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var empCode = employeeCode.Trim().ToUpperInvariant();

        var emp = await _employeeRepository.GetByCodeAsync(empCode);
        if (emp == null)
        {
            throw new HrNotFoundException($"الموظف ({empCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var contract = new EmploymentContract
        {
            EmployeeCode = empCode,
            ContractType = string.IsNullOrWhiteSpace(dto.ContractType) ? "UNLIMITED" : dto.ContractType.Trim().ToUpperInvariant(),
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate?.Date,
            FileReference = dto.FileReference?.Trim(),
            Notes = dto.Notes?.Trim(),
            Status = "ACTIVE",
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _compensationRepository.AddContractAsync(contract);
        await _compensationRepository.SaveChangesAsync();

        _logger.LogInformation("Created contract for employee {Emp} ({Type})", empCode, contract.ContractType);
        return MapToContractDto(contract);
    }

    private static SalaryComponentDto MapToComponentDto(SalaryComponent c)
    {
        return new SalaryComponentDto
        {
            ComponentCode = c.ComponentCode,
            NameAr = c.NameAr,
            NameEn = c.NameEn,
            ComponentType = c.ComponentType,
            IsTaxable = c.IsTaxable,
            IsSscApplicable = c.IsSscApplicable,
            CalculationType = c.CalculationType,
            DefaultAmount = c.DefaultAmount,
            DefaultPercent = c.DefaultPercent,
            GlAccountCode = c.GlAccountCode,
            IsActive = c.IsActive
        };
    }

    private static EmployeeSalaryStructureDto MapToStructureDto(EmployeeSalaryStructure s)
    {
        var allowanceSum = s.Lines.Where(l => l.Component?.ComponentType == "EARNING" && l.IsActive).Sum(l => l.Amount);

        return new EmployeeSalaryStructureDto
        {
            Id = s.Id,
            EmployeeCode = s.EmployeeCode,
            EffectiveFrom = s.EffectiveFrom,
            EffectiveTo = s.EffectiveTo,
            BasicSalary = s.BasicSalary,
            CurrencyCode = s.CurrencyCode,
            PaymentMethod = s.PaymentMethod,
            TotalAllowances = allowanceSum,
            TotalGrossSalary = s.BasicSalary + allowanceSum,
            IsActive = s.IsActive,
            Lines = s.Lines.Select(l => new EmployeeSalaryStructureLineDto
            {
                Id = l.Id,
                ComponentCode = l.ComponentCode,
                ComponentNameEn = l.Component?.NameEn ?? string.Empty,
                ComponentType = l.Component?.ComponentType ?? "EARNING",
                IsTaxable = l.Component?.IsTaxable ?? true,
                IsSscApplicable = l.Component?.IsSscApplicable ?? true,
                Amount = l.Amount,
                Percent = l.Percent
            }).ToList()
        };
    }

    private static EmploymentContractDto MapToContractDto(EmploymentContract c)
    {
        return new EmploymentContractDto
        {
            Id = c.Id,
            EmployeeCode = c.EmployeeCode,
            ContractType = c.ContractType,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            FileReference = c.FileReference,
            Status = c.Status,
            Notes = c.Notes,
            IsActive = c.IsActive
        };
    }
}
