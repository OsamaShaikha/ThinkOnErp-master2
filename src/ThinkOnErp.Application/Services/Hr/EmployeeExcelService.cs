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
    private readonly IHrUnitOfWork? _unitOfWork;
    private readonly EmployeeFieldAccess? _fields;

    public EmployeeExcelService(
        IEmployeeRepository employeeRepo,
        IXlsxEmployeeWorkbookReader workbookReader,
        IXlsxEmployeeWorkbookWriter workbookWriter,
        ILogger<EmployeeExcelService> logger, IHrUnitOfWork? unitOfWork = null, EmployeeFieldAccess? fields = null)
    {
        _employeeRepo = employeeRepo ?? throw new ArgumentNullException(nameof(employeeRepo));
        _workbookReader = workbookReader ?? throw new ArgumentNullException(nameof(workbookReader));
        _workbookWriter = workbookWriter ?? throw new ArgumentNullException(nameof(workbookWriter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _unitOfWork = unitOfWork;
        _fields = fields;
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
        foreach(var employee in employees)
        {
            _fields?.Require(EmployeeFieldAccess.ViewIdentity,employee.BranchId);
            _fields?.Require(EmployeeFieldAccess.ViewBank,employee.BranchId);
            _fields?.Require(EmployeeFieldAccess.ViewSalary,employee.BranchId);
        }

        var today = DateTime.UtcNow.Date;
        var exportDtos = employees.Select(e =>
        {
            var activeStructure = e.SalaryStructures?
                .Where(s => s.IsActive && s.EffectiveFrom.Date <= today && (!s.EffectiveTo.HasValue || s.EffectiveTo.Value.Date >= today))
                .OrderByDescending(s => s.EffectiveFrom)
                .FirstOrDefault();

            return new EmployeeExportRowDto
            {
                EmployeeCode = e.EmployeeCode,
                NameLocal = e.NameLocal,
                NameEn = e.NameEn,
                NationalId = e.NationalId,
                Nationality = e.Nationality,
                CitizenshipCountryCode = e.CitizenshipCountryCode,
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
                SalaryEffectiveFrom = activeStructure?.EffectiveFrom,
                CurrencyCode = activeStructure?.CurrencyCode,
                PaymentMethod = activeStructure?.PaymentMethod,
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
        result.PreviewGeneratedAtUtc = DateTime.UtcNow;
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNationalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in rows)
        {
            r.EmployeeCode = r.EmployeeCode?.Trim().ToUpperInvariant() ?? string.Empty;
            r.NationalId = r.NationalId?.Trim() ?? string.Empty;
            if (!seenCodes.Add(r.EmployeeCode))
            {
                result.Errors.Add(new EmployeeImportErrorDto(r.RowNumber, "EmployeeCode", "DUPLICATE_IN_FILE", $"Duplicate EmployeeCode '{r.EmployeeCode}' in file."));
            }

            if (!string.IsNullOrWhiteSpace(r.NationalId) && !seenNationalIds.Add(r.NationalId))
            {
                result.Errors.Add(new EmployeeImportErrorDto(r.RowNumber, "NationalId", "DUPLICATE_IN_FILE", $"Duplicate NationalId '{r.NationalId}' in file."));
            }

            var existing = await _employeeRepo.GetEmployeeByCodeAsync(r.EmployeeCode, cancellationToken);
            if (existing == null && await _employeeRepo.ExistsByCodeAsync(r.EmployeeCode, cancellationToken))
            { result.Errors.Add(new(r.RowNumber, "EmployeeCode", "EMPLOYEE_NOT_EDITABLE", "Employee code already exists outside the editable scope.")); continue; }
            try { _fields?.RequireImport(existing?.BranchId ?? r.BranchId); EmployeeInputValidation.NormalizeImportRow(r, existing); _fields?.RequireImport(r.BranchId); await _employeeRepo.ValidateStorageAsync(EmployeeInputValidation.ImportCandidate(r), cancellationToken); }
            catch (UnauthorizedAccessException) { result.Errors.Add(new(r.RowNumber,null,"EMPLOYEE_FIELD_PERMISSION_REQUIRED","Full employee workbook access requires identity, bank and salary read/edit permissions in both branches.")); continue; }
            catch (ArgumentException ex) { result.Errors.Add(new(r.RowNumber, null, "INVALID_EMPLOYEE_PROFILE", ex.Message)); continue; }
            if (await _employeeRepo.ExistsByNationalIdAsync(r.NationalId, existing?.EmployeeCode, cancellationToken))
                result.Errors.Add(new(r.RowNumber, "NationalId", "NATIONAL_ID_EXISTS", "National ID is already assigned to another employee."));
            DateTime? plannedSalaryFrom = null;
            string? previewCurrency = null, previewMethod = null;
            var salaryChange = r.BasicSalary.HasValue ? "UNCHANGED" : "NOT_PROVIDED";
            if (existing != null) result.Warnings.Add($"Row {r.RowNumber}: Employee already exists; updateExisting is required.");
            var salaryError = await ApplyImportedSalaryAsync(existing ?? EmployeeInputValidation.ImportCandidate(r),r,"VALIDATION",apply:false,
                planned: from => plannedSalaryFrom = from,firstEmployee:existing == null,cancellationToken:cancellationToken,
                resolved: (currency,method) => { previewCurrency=currency; previewMethod=method; });
            if(salaryError != null) { result.Errors.Add(salaryError); salaryChange = "INVALID"; }
            if (plannedSalaryFrom.HasValue && salaryChange != "INVALID") salaryChange = "NEW_VERSION";
            var earliest = r.HireDate!.Value.Date;
            if (r.SalaryEffectiveFrom.HasValue && r.SalaryEffectiveFrom.Value.Date < earliest) earliest = r.SalaryEffectiveFrom.Value.Date;
            if (existing != null)
            {
                if (existing.HireDate.Date < earliest) earliest = existing.HireDate.Date;
                foreach (var salary in existing.SalaryStructures) if (salary.EffectiveFrom.Date < earliest) earliest = salary.EffectiveFrom.Date;
            }
            try
            {
                var impact = await _employeeRepo.GetImportImpactAsync(r.EmployeeCode,r.BranchId,earliest,cancellationToken)
                    ?? throw new InvalidOperationException("Employee import impact could not be resolved.");
                var blockers = new List<string>();
                if (impact.Periods.Any(p => p.Status == "CLOSED")) blockers.Add("CLOSED_PAYROLL_PERIOD");
                if (impact.Periods.Any(p => p.HasApprovedPayroll)) blockers.Add("APPROVED_PAYROLL");
                if (impact.HasUnmappedApprovedPayroll) blockers.Add("UNMAPPED_APPROVED_PAYROLL");
                if (blockers.Count > 0) result.Errors.Add(new(r.RowNumber,null,"PAYROLL_INPUT_BLOCKED","Import affects protected payroll inputs. Review the preview blocking reasons."));
                result.PreviewRows.Add(new(r.RowNumber,r.EmployeeCode,existing == null ? "INSERT" : "UPDATE",existing != null,salaryChange,
                    plannedSalaryFrom,earliest,impact.Periods,impact.HasUnmappedApprovedPayroll,blockers,previewCurrency,previewMethod));
            }
            catch (UnauthorizedAccessException)
            { result.Errors.Add(new(r.RowNumber,null,"IMPORT_SCOPE_UNAVAILABLE","Cannot preview imports outside the active permitted company and branches.")); }
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
            r.EmployeeCode = r.EmployeeCode?.Trim().ToUpperInvariant() ?? string.Empty;
            r.NationalId = r.NationalId?.Trim() ?? string.Empty;
            if (!seenCodes.Add(r.EmployeeCode))
            {
                result.Errors.Add(new EmployeeImportErrorDto(r.RowNumber, "EmployeeCode", "DUPLICATE_IN_FILE", $"Duplicate EmployeeCode '{r.EmployeeCode}' in file."));
            }

            if (!string.IsNullOrWhiteSpace(r.NationalId) && !seenNationalIds.Add(r.NationalId))
            {
                result.Errors.Add(new EmployeeImportErrorDto(r.RowNumber, "NationalId", "DUPLICATE_IN_FILE", $"Duplicate NationalId '{r.NationalId}' in file."));
            }
        }

        if (result.Errors.Count > 0)
        {
            result.FailureCount = result.Errors.Select(e => e.RowNumber).Distinct().Count();
            return result;
        }

        if(_fields!=null)
        {
            foreach(var row in rows)
            {
                var existing=await _employeeRepo.GetEmployeeByCodeAsync(row.EmployeeCode,cancellationToken);
                try { _fields.RequireImport(existing?.BranchId ?? row.BranchId); if(row.BranchId.HasValue) _fields.RequireImport(row.BranchId); }
                catch(UnauthorizedAccessException) { result.Errors.Add(new(row.RowNumber,null,"EMPLOYEE_FIELD_PERMISSION_REQUIRED","Full employee workbook access requires identity, bank and salary read/edit permissions in both branches.")); }
            }
            if(result.Errors.Count>0) { result.FailureCount=result.Errors.Select(e=>e.RowNumber).Distinct().Count(); return result; }
        }
        if (_unitOfWork == null) throw new InvalidOperationException("Employee import requires its payroll-protected unit of work.");
        return await _unitOfWork.ExecuteEmployeeImportAsync(rows,
            () => ImportRowsAsync(rows, result, updateExisting, user, cancellationToken), cancellationToken);
    }

    private async Task<EmployeeImportResultDto> ImportRowsAsync(IReadOnlyList<EmployeeImportRowDto> rows,
        EmployeeImportResultDto result, bool updateExisting, string user, CancellationToken cancellationToken)
    {
        var employeesToInsert = new List<Employee>();
        var employeesToUpdate = new List<Employee>();

        foreach (var r in rows)
        {
            var existing = await _employeeRepo.GetEmployeeByCodeAsync(r.EmployeeCode, cancellationToken);
            if (existing == null && await _employeeRepo.ExistsByCodeAsync(r.EmployeeCode, cancellationToken))
            { result.Errors.Add(new(r.RowNumber, "EmployeeCode", "EMPLOYEE_NOT_EDITABLE", "Employee code already exists outside the editable scope.")); continue; }
            try { _fields?.RequireImport(existing?.BranchId ?? r.BranchId); EmployeeInputValidation.NormalizeImportRow(r, existing); _fields?.RequireImport(r.BranchId); await _employeeRepo.ValidateStorageAsync(EmployeeInputValidation.ImportCandidate(r), cancellationToken); }
            catch (UnauthorizedAccessException) { result.Errors.Add(new(r.RowNumber,null,"EMPLOYEE_FIELD_PERMISSION_REQUIRED","Full employee workbook access requires identity, bank and salary read/edit permissions in both branches.")); continue; }
            catch (ArgumentException ex) { result.Errors.Add(new(r.RowNumber, null, "INVALID_EMPLOYEE_PROFILE", ex.Message)); continue; }
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

                if (existing.UserId.HasValue && existing.BranchId != r.BranchId &&
                    !await _employeeRepo.IsUserAvailableForEmployeeAsync(existing.UserId.Value, r.BranchId, cancellationToken))
                {
                    result.Errors.Add(new EmployeeImportErrorDto(r.RowNumber, "BranchId", "LINKED_USER_BRANCH_ACCESS",
                        "The linked login account must have access to the new employee branch."));
                    continue;
                }

                if (await ApplyImportedSalaryAsync(existing,r,user,cancellationToken:cancellationToken) is { } salaryError)
                {
                    result.Errors.Add(salaryError!);
                    continue;
                }

                // Update existing employee
                existing.NameLocal = r.NameLocal;
                existing.NameEn = r.NameEn;
                existing.NationalId = r.NationalId;
                existing.Nationality = r.Nationality;
                if (r.PresentColumns == null || r.PresentColumns.Contains(nameof(r.CitizenshipCountryCode)))
                    existing.CitizenshipCountryCode = string.IsNullOrEmpty(r.CitizenshipCountryCode) ? null : r.CitizenshipCountryCode;
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
                    CitizenshipCountryCode = string.IsNullOrEmpty(r.CitizenshipCountryCode) ? null : r.CitizenshipCountryCode,
                    PassportNumber = r.PassportNumber,
                    DateOfBirth = r.DateOfBirth!.Value,
                    Gender = r.Gender,
                    MaritalStatus = r.MaritalStatus,
                    Email = r.Email,
                    Phone = r.Phone,
                    HireDate = r.HireDate!.Value,
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

                if (await ApplyImportedSalaryAsync(newEmp,r,user,firstEmployee:true,cancellationToken:cancellationToken) is { } salaryError)
                { result.Errors.Add(salaryError); continue; }

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

    private static EmployeeImportErrorDto? ResolveSalaryMetadata(EmployeeImportRowDto row,SalaryStructure? previous,out string currency,out string method)
    {
        var suppliedCurrency=SalaryPaymentMetadata.Normalize(row.CurrencyCode);
        var suppliedMethod=SalaryPaymentMetadata.Normalize(row.PaymentMethod);
        currency=suppliedCurrency.Length>0?suppliedCurrency:SalaryPaymentMetadata.Normalize(previous?.CurrencyCode);
        method=suppliedMethod.Length>0?suppliedMethod:SalaryPaymentMetadata.Normalize(previous?.PaymentMethod);
        if(currency.Length==0 || method.Length==0)
            return new(row.RowNumber,null,"SALARY_METADATA_REQUIRED","Provide CurrencyCode and PaymentMethod for the first salary structure.");
        if(previous!=null && (suppliedCurrency.Length>0 && suppliedCurrency!=SalaryPaymentMetadata.Normalize(previous.CurrencyCode) ||
            suppliedMethod.Length>0 && suppliedMethod!=SalaryPaymentMetadata.Normalize(previous.PaymentMethod)))
            return new(row.RowNumber,null,"SALARY_METADATA_CHANGE_REQUIRES_ASSIGNMENT","Use the protected salary assignment workflow to change an existing salary currency or payment method.");
        try { SalaryPaymentMetadata.Validate(currency,method); }
        catch(ArgumentException ex) { return new(row.RowNumber,null,"INVALID_SALARY_METADATA",ex.Message); }
        return null;
    }

    private async Task<EmployeeImportErrorDto?> ApplyImportedSalaryAsync(Employee employee,EmployeeImportRowDto row,string user,bool apply=true,
        Action<DateTime>? planned=null,bool firstEmployee=false,CancellationToken cancellationToken=default,Action<string,string>? resolved=null)
    {
        if(!row.BasicSalary.HasValue)
            return string.IsNullOrWhiteSpace(row.CurrencyCode) && string.IsNullOrWhiteSpace(row.PaymentMethod)?null:
                new(row.RowNumber,"BasicSalary","SALARY_REQUIRED","Provide BasicSalary with currency/payment metadata.");
        if(row.BasicSalary.Value<=0) return new(row.RowNumber,"BasicSalary","INVALID_SALARY","BasicSalary must be positive when provided.");
        var active=employee.SalaryStructures.Where(s=>s.IsActive).OrderByDescending(s=>s.EffectiveFrom).ToList();
        var today=DateTime.UtcNow.Date;
        var current=active.FirstOrDefault(s=>s.EffectiveFrom.Date<=today && (!s.EffectiveTo.HasValue || s.EffectiveTo.Value.Date>=today));
        if(!row.SalaryEffectiveFrom.HasValue && !firstEmployee)
        {
            if(current?.BasicSalary==row.BasicSalary.Value)
            {
                var error=ResolveSalaryMetadata(row,current,out var currentCurrency,out var currentMethod);
                if(error==null) resolved?.Invoke(currentCurrency,currentMethod);
                return error;
            }
            return new(row.RowNumber,"SalaryEffectiveFrom","SALARY_DATE_REQUIRED","Provide SalaryEffectiveFrom to change an existing employee salary.");
        }
        var from=row.SalaryEffectiveFrom?.Date ?? employee.HireDate.Date;
        if(from<(row.HireDate?.Date ?? employee.HireDate.Date))
            return new(row.RowNumber,"SalaryEffectiveFrom","SALARY_BEFORE_HIRE","Salary cannot start before the hire date.");
        var sameStart=active.FirstOrDefault(s=>s.EffectiveFrom.Date==from);
        if(sameStart!=null && sameStart.BasicSalary==row.BasicSalary.Value)
        {
            var error=ResolveSalaryMetadata(row,sameStart,out var sameCurrency,out var sameMethod);
            if(error==null) resolved?.Invoke(sameCurrency,sameMethod);
            return error;
        }
        if(active.Any(s=>s.EffectiveFrom.Date>=from))
            return new(row.RowNumber,"SalaryEffectiveFrom","SALARY_DATE_CONFLICT","A salary structure starts on or after this date; review it before importing a backdated change.");
        if(active.Count(s=>s.EffectiveFrom.Date<from && (!s.EffectiveTo.HasValue || s.EffectiveTo.Value.Date>=from))>1)
            return new(row.RowNumber,"SalaryEffectiveFrom","OVERLAPPING_SALARIES","Existing salary structures overlap. Resolve them before importing a new salary.");
        var previous=active.FirstOrDefault(s=>s.EffectiveFrom.Date<from);
        var metadataError=ResolveSalaryMetadata(row,previous,out var currency,out var method);
        if(metadataError!=null) return metadataError;
        try { await _employeeRepo.ValidateSalaryMetadataAsync(currency,method,cancellationToken); }
        catch(ArgumentException ex) { return new(row.RowNumber,null,"INVALID_SALARY_METADATA",ex.Message); }

        // Importing a basic-salary change carries the previous active component lines into
        // a new structure. Validate those assignments before truncating the old structure,
        // just like the protected salary-assignment workflow does.
        var carriedLines = new List<SalaryStructureLine>();
        if (previous != null)
        {
            foreach (var line in previous.Lines.Where(l => l.IsActive))
            {
                var componentCode = line.ComponentCode?.Trim();
                if (string.IsNullOrWhiteSpace(componentCode) || componentCode.Length > 50)
                    return new(row.RowNumber, "SalaryComponents", "INVALID_SALARY_COMPONENT", "An existing active salary line has an invalid component code; review the salary structure before importing.");

                var component = await _employeeRepo.GetSalaryComponentByCodeAsync(componentCode, cancellationToken);
                if (component == null)
                    return new(row.RowNumber, "SalaryComponents", "SALARY_COMPONENT_NOT_FOUND", $"Existing salary component '{componentCode}' no longer exists; review the salary structure before importing.");

                var candidate = new SalaryStructureLine
                {
                    ComponentCode = component.ComponentCode,
                    Amount = line.Amount,
                    Percent = line.Percent
                };
                try
                {
                    SalaryStructureLineRules.ValidateAssignment(candidate, component, row.BasicSalary.Value);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    return new(row.RowNumber, "SalaryComponents", "INVALID_SALARY_COMPONENT_ASSIGNMENT", ex.Message);
                }

                carriedLines.Add(candidate);
            }
        }

        resolved?.Invoke(currency,method);
        planned?.Invoke(from);
        if(!apply) return null;
        var previousEnd=previous?.EffectiveTo;
        if(previous!=null && (!previous.EffectiveTo.HasValue || previous.EffectiveTo.Value.Date>=from))
        { previous.EffectiveTo=from.AddDays(-1); previous.UpdateUser=user; previous.UpdateDate=DateTime.UtcNow; }
        var replacement=new SalaryStructure
        {
            EmployeeCode=employee.EmployeeCode,EffectiveFrom=from,
            EffectiveTo=previousEnd.HasValue && previousEnd.Value.Date>=from?previousEnd:null,
            BasicSalary=row.BasicSalary.Value,CurrencyCode=currency,PaymentMethod=method,IsActive=true,CreationUser=user,CreationDate=DateTime.UtcNow
        };
        foreach (var line in carriedLines)
            replacement.Lines.Add(new SalaryStructureLine { ComponentCode=line.ComponentCode,Amount=line.Amount,Percent=line.Percent,IsActive=true,CreationUser=user,CreationDate=DateTime.UtcNow });
        employee.SalaryStructures.Add(replacement);
        return null;
    }
}
