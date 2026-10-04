using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class EmployeeExcelService : IEmployeeExcelService
{
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IXlsxEmployeeWorkbookReader _workbookReader;
    private readonly IXlsxEmployeeWorkbookWriter _workbookWriter;
    private readonly ILogger<EmployeeExcelService> _logger;

    public EmployeeExcelService(
        IEmployeeRepository employeeRepo,
        IXlsxEmployeeWorkbookReader workbookReader,
        IXlsxEmployeeWorkbookWriter workbookWriter,
        ILogger<EmployeeExcelService> logger)
    {
        _employeeRepo = employeeRepo ?? throw new ArgumentNullException(nameof(employeeRepo));
        _workbookReader = workbookReader ?? throw new ArgumentNullException(nameof(workbookReader));
        _workbookWriter = workbookWriter ?? throw new ArgumentNullException(nameof(workbookWriter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]> ExportEmployeesAsync(
        string? searchKeyword = null,
        string? departmentCode = null,
        string? status = null,
        long? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var employees = await _employeeRepo.GetAllEmployeesForExportAsync(
            searchKeyword, departmentCode, status, branchId, cancellationToken);

        var exportDtos = employees.Select(e =>
        {
            var activeStructure = e.SalaryStructures?
                .Where(s => s.IsActive && s.EffectiveFrom <= DateTime.UtcNow && (!s.EffectiveTo.HasValue || s.EffectiveTo.Value >= DateTime.UtcNow))
                .OrderByDescending(s => s.EffectiveFrom)
                .FirstOrDefault();

            return new EmployeeExportRowDto
            {
                EmployeeCode = e.EmployeeCode,
                NameLocal = e.NameLocal,
                NameEn = e.NameEn,
                NationalId = e.NationalId,
                Nationality = e.Nationality,
                PassportNumber = e.PassportNumber,
                DateOfBirth = e.DateOfBirth,
                Gender = e.Gender,
                MaritalStatus = e.MaritalStatus,
                Email = e.Email,
                Phone = e.Phone,
                HireDate = e.HireDate,
                ProbationEndDate = e.ProbationEndDate,
                TerminationDate = e.TerminationDate,
                TerminationReason = e.TerminationReason,
                EmploymentType = e.EmploymentType,
                EmploymentStatus = e.EmploymentStatus,
                DepartmentCode = e.DepartmentCode,
                PositionCode = e.PositionCode,
                BranchId = e.BranchId,
                ManagerEmployeeCode = e.ManagerEmployeeCode,
                SscNumber = e.SscNumber,
                TaxExemptionCount = e.TaxExemptionCount,
                IsHighRiskRole = e.IsHighRiskRole,
                BankName = e.BankName,
                BankAccountNumber = e.BankAccountNumber,
                BankIban = e.BankIban,
                BasicSalary = activeStructure?.BasicSalary,
                IsActive = e.IsActive
            };
        }).ToList();

        _logger.LogInformation("Exported {Count} employees to Excel.", exportDtos.Count);
        return _workbookWriter.WriteExportWorkbook(exportDtos);
    }

    public Task<byte[]> GenerateTemplateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_workbookWriter.WriteTemplateWorkbook());
    }

    public async Task<EmployeeImportResultDto> ValidateWorkbookAsync(
        Stream workbook,
        CancellationToken cancellationToken = default)
    {
        var (rows, result) = await _workbookReader.ReadAsync(workbook, cancellationToken);
        if (result.Errors.Count > 0)
        {
            result.FailureCount = result.Errors.Select(e => e.RowNumber).Distinct().Count();
            return result;
        }

        if (rows.Count == 0)
        {
            result.Warnings.Add("The uploaded Excel workbook contains no employee data rows.");
            return result;
        }

        // Validate duplicates within workbook
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNationalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in rows)
        {
            if (!seenCodes.Add(r.EmployeeCode))
            {
                result.Errors.Add(new EmployeeImportErrorDto(r.RowNumber, "EmployeeCode", "DUPLICATE_IN_FILE", $"Duplicate EmployeeCode '{r.EmployeeCode}' in file."));
            }

            if (!seenNationalIds.Add(r.NationalId))
            {
                result.Errors.Add(new EmployeeImportErrorDto(r.RowNumber, "NationalId", "DUPLICATE_IN_FILE", $"Duplicate NationalId '{r.NationalId}' in file."));
            }

            // Check against DB
            if (await _employeeRepo.ExistsByCodeAsync(r.EmployeeCode, cancellationToken))
            {
                result.Warnings.Add($"Row {r.RowNumber}: Employee '{r.EmployeeCode}' already exists in system (will be updated if updateExisting is enabled).");
            }
            else if (await _employeeRepo.ExistsByNationalIdAsync(r.NationalId, null, cancellationToken))
            {
                result.Errors.Add(new EmployeeImportErrorDto(r.RowNumber, "NationalId", "NATIONAL_ID_EXISTS", $"Employee with National ID '{r.NationalId}' already exists in system."));
            }
        }

        result.FailureCount = result.Errors.Select(e => e.RowNumber).Distinct().Count();
        result.SuccessCount = result.TotalRows - result.FailureCount;
        return result;
    }

    public async Task<EmployeeImportResultDto> ImportEmployeesAsync(
        Stream workbook,
        bool updateExisting = false,
        string user = "SYSTEM",
        CancellationToken cancellationToken = default)
    {
        var (rows, result) = await _workbookReader.ReadAsync(workbook, cancellationToken);
        if (result.Errors.Count > 0)
        {
            result.FailureCount = result.Errors.Select(e => e.RowNumber).Distinct().Count();
            return result;
        }

        if (rows.Count == 0)
        {
            result.Warnings.Add("The uploaded Excel workbook contains no data.");
            return result;
        }

        // Check file internal duplicates
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNationalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in rows)
        {
            if (!seenCodes.Add(r.EmployeeCode))
            {
                result.Errors.Add(new EmployeeImportErrorDto(r.RowNumber, "EmployeeCode", "DUPLICATE_IN_FILE", $"Duplicate EmployeeCode '{r.EmployeeCode}' in file."));
            }

            if (!seenNationalIds.Add(r.NationalId))
            {
                result.Errors.Add(new EmployeeImportErrorDto(r.RowNumber, "NationalId", "DUPLICATE_IN_FILE", $"Duplicate NationalId '{r.NationalId}' in file."));
            }
        }

        if (result.Errors.Count > 0)
        {
            result.FailureCount = result.Errors.Select(e => e.RowNumber).Distinct().Count();
            return result;
        }

        var employeesToInsert = new List<Employee>();
        var employeesToUpdate = new List<Employee>();

        foreach (var r in rows)
        {
            var existing = await _employeeRepo.GetEmployeeByCodeAsync(r.EmployeeCode, cancellationToken);
            if (existing != null)
            {
                if (!updateExisting)
                {
                    result.Errors.Add(new EmployeeImportErrorDto(r.RowNumber, "EmployeeCode", "EMPLOYEE_EXISTS",
                        $"Employee '{r.EmployeeCode}' already exists. Enable updateExisting to overwrite."));
                    continue;
                }

                // Check National ID uniqueness against other employees
                if (await _employeeRepo.ExistsByNationalIdAsync(r.NationalId, existing.EmployeeCode, cancellationToken))
                {
                    result.Errors.Add(new EmployeeImportErrorDto(r.RowNumber, "NationalId", "NATIONAL_ID_EXISTS",
                        $"National ID '{r.NationalId}' is already assigned to another employee."));
                    continue;
                }

                // Update existing employee
                existing.NameLocal = r.NameLocal;
                existing.NameEn = r.NameEn;
                existing.NationalId = r.NationalId;
                existing.Nationality = r.Nationality;
                existing.PassportNumber = r.PassportNumber;
                if (r.DateOfBirth.HasValue) existing.DateOfBirth = r.DateOfBirth.Value;
                existing.Gender = r.Gender;
                existing.MaritalStatus = r.MaritalStatus;
                existing.Email = r.Email;
                existing.Phone = r.Phone;
                if (r.HireDate.HasValue) existing.HireDate = r.HireDate.Value;
                existing.ProbationEndDate = r.ProbationEndDate;
                existing.TerminationDate = r.TerminationDate;
                existing.TerminationReason = r.TerminationReason;
                existing.EmploymentType = r.EmploymentType;
                existing.EmploymentStatus = r.EmploymentStatus;
                existing.DepartmentCode = r.DepartmentCode;
                existing.PositionCode = r.PositionCode;
                existing.BranchId = r.BranchId;
                existing.ManagerEmployeeCode = r.ManagerEmployeeCode;
                existing.SscNumber = r.SscNumber;
                existing.TaxExemptionCount = r.TaxExemptionCount;
                existing.IsHighRiskRole = r.IsHighRiskRole;
                existing.BankName = r.BankName;
                existing.BankAccountNumber = r.BankAccountNumber;
                existing.BankIban = r.BankIban;
                existing.IsActive = r.IsActive;
                existing.UpdateUser = user;
                existing.UpdateDate = DateTime.UtcNow;

                // Update salary structure if salary provided
                if (r.BasicSalary.HasValue && r.BasicSalary.Value > 0)
                {
                    var activeStructure = existing.SalaryStructures?
                        .FirstOrDefault(s => s.IsActive && (!s.EffectiveTo.HasValue || s.EffectiveTo.Value >= DateTime.UtcNow));

                    if (activeStructure != null)
                    {
                        activeStructure.BasicSalary = r.BasicSalary.Value;
                        activeStructure.UpdateUser = user;
                        activeStructure.UpdateDate = DateTime.UtcNow;
                    }
                    else
                    {
                        existing.SalaryStructures ??= new List<SalaryStructure>();
                        existing.SalaryStructures.Add(new SalaryStructure
                        {
                            EmployeeCode = existing.EmployeeCode,
                            EffectiveFrom = r.HireDate ?? DateTime.UtcNow.Date,
                            BasicSalary = r.BasicSalary.Value,
                            CurrencyCode = "JOD",
                            PaymentMethod = "BANK_TRANSFER",
                            IsActive = true,
                            CreationUser = user,
                            CreationDate = DateTime.UtcNow
                        });
                    }
                }

                employeesToUpdate.Add(existing);
            }
            else
            {
                // Verify National ID not taken
                if (await _employeeRepo.ExistsByNationalIdAsync(r.NationalId, null, cancellationToken))
                {
                    result.Errors.Add(new EmployeeImportErrorDto(r.RowNumber, "NationalId", "NATIONAL_ID_EXISTS",
                        $"Employee with National ID '{r.NationalId}' already exists."));
                    continue;
                }

                var newEmp = new Employee
                {
                    EmployeeCode = r.EmployeeCode,
                    NameLocal = r.NameLocal,
                    NameEn = r.NameEn,
                    NationalId = r.NationalId,
                    Nationality = r.Nationality,
                    PassportNumber = r.PassportNumber,
                    DateOfBirth = r.DateOfBirth ?? new DateTime(1990, 1, 1),
                    Gender = r.Gender,
                    MaritalStatus = r.MaritalStatus,
                    Email = r.Email,
                    Phone = r.Phone,
                    HireDate = r.HireDate ?? DateTime.UtcNow.Date,
                    ProbationEndDate = r.ProbationEndDate,
                    TerminationDate = r.TerminationDate,
                    TerminationReason = r.TerminationReason,
                    EmploymentType = r.EmploymentType,
                    EmploymentStatus = r.EmploymentStatus,
                    DepartmentCode = r.DepartmentCode,
                    PositionCode = r.PositionCode,
                    BranchId = r.BranchId,
                    ManagerEmployeeCode = r.ManagerEmployeeCode,
                    SscNumber = r.SscNumber,
                    TaxExemptionCount = r.TaxExemptionCount,
                    IsHighRiskRole = r.IsHighRiskRole,
                    BankName = r.BankName,
                    BankAccountNumber = r.BankAccountNumber,
                    BankIban = r.BankIban,
                    IsActive = r.IsActive,
                    CreationUser = user,
                    CreationDate = DateTime.UtcNow
                };

                if (r.BasicSalary.HasValue && r.BasicSalary.Value > 0)
                {
                    newEmp.SalaryStructures.Add(new SalaryStructure
                    {
                        EmployeeCode = newEmp.EmployeeCode,
                        EffectiveFrom = newEmp.HireDate,
                        BasicSalary = r.BasicSalary.Value,
                        CurrencyCode = "JOD",
                        PaymentMethod = "BANK_TRANSFER",
                        IsActive = true,
                        CreationUser = user,
                        CreationDate = DateTime.UtcNow
                    });
                }

                employeesToInsert.Add(newEmp);
            }
        }

        // If any row failed validation, do not commit any rows (all or nothing / safe rollback)
        if (result.Errors.Count > 0)
        {
            result.FailureCount = result.Errors.Select(e => e.RowNumber).Distinct().Count();
            return result;
        }

        // Persist inserts and updates
        foreach (var emp in employeesToInsert)
        {
            await _employeeRepo.AddEmployeeAsync(emp, cancellationToken);
        }

        foreach (var emp in employeesToUpdate)
        {
            await _employeeRepo.UpdateEmployeeAsync(emp, cancellationToken);
        }

        await _employeeRepo.SaveChangesAsync(cancellationToken);

        result.InsertedCount = employeesToInsert.Count;
        result.UpdatedCount = employeesToUpdate.Count;
        result.SuccessCount = result.InsertedCount + result.UpdatedCount;
        result.FailureCount = 0;

        _logger.LogInformation("Imported {Inserted} employees, updated {Updated} employees from Excel.",
            result.InsertedCount, result.UpdatedCount);

        return result;
    }
}
