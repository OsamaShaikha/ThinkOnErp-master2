using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Common;

/// <summary>
/// Shared write path for the employee profile. Used by the Update, UpdateWork, UpdateBank,
/// UpdateIdentity, Delete and dependent handlers so the payroll guard and the profile
/// invariants are defined in exactly one place.
/// </summary>
internal sealed class EmployeeProfileWriter
{
    private readonly IEmployeeRepository _repository;
    private readonly IHrUnitOfWork _unitOfWork;
    private readonly EmployeeFieldGuard _fields;

    public EmployeeProfileWriter(IEmployeeRepository repository, IHrUnitOfWork unitOfWork, EmployeeFieldGuard fields)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _fields = fields;
    }

    /// <summary>
    /// Runs <paramref name="action"/> under the employee payroll-input lock.
    /// The present profile and dependents are undated payroll snapshot inputs, so a change can
    /// alter historic results: the lock covers history from the earlier of the stored and requested hire date.
    /// </summary>
    public async Task<T> ProtectAsync<T>(string employeeCode, DateTime? requestedHireDate, Func<Task<T>> action, CancellationToken ct)
    {
        var current = await _repository.GetEmployeeByCodeAsync(employeeCode, ct)
            ?? throw new KeyNotFoundException("Employee not found in current HR scope.");

        var from = requestedHireDate.HasValue && requestedHireDate.Value.Date < current.HireDate.Date
            ? requestedHireDate.Value.Date
            : current.HireDate.Date;

        return await _unitOfWork.ExecuteEmployeeProfileAsync(employeeCode, from, action, ct);
    }

    /// <summary>
    /// Updates one permission group (bank or identity). Every other field is copied from the row
    /// re-read inside the lock, so concurrent writers to other groups are never overwritten.
    /// </summary>
    public async Task<EmployeeDetailsDto> UpdateFieldGroupAsync(
        string employeeCode, string feature, Func<Employee, UpdateEmployeeDto> replace, string username, CancellationToken ct)
    {
        employeeCode = EmployeeProfileRules.Required(employeeCode, nameof(employeeCode), 100).ToUpperInvariant();

        var before = await _repository.GetEmployeeByCodeAsync(employeeCode, ct)
            ?? throw new KeyNotFoundException("Employee not found.");
        _fields.Require(feature, before.BranchId);

        return await ProtectAsync(employeeCode, null, async () =>
        {
            var current = await _repository.GetEmployeeByCodeAsync(employeeCode, ct)
                ?? throw new KeyNotFoundException("Employee not found.");
            _fields.Require(feature, current.BranchId);

            var replacement = EmployeeInputValidation.Normalize(employeeCode, replace(current));
            return await ApplyProfileAsync(employeeCode, replacement, username, limitedUpdate: true, ct);
        }, ct);
    }

    /// <summary>Applies a full, already-normalized profile. Must run inside <see cref="ProtectAsync{T}"/>.</summary>
    public async Task<EmployeeDetailsDto> ApplyProfileAsync(
        string employeeCode, UpdateEmployeeDto dto, string username, bool limitedUpdate, CancellationToken ct)
    {
        var emp = await _repository.GetEmployeeByCodeAsync(employeeCode, ct)
            ?? throw new KeyNotFoundException($"Employee with code '{employeeCode}' not found.");

        EnsureTerminationIsNotChanged(emp, dto);
        RequireBranchPermissions(emp, dto, limitedUpdate);

        if (emp.UserId.HasValue && emp.BranchId != dto.BranchId &&
            !await _repository.IsUserAvailableForEmployeeAsync(emp.UserId.Value, dto.BranchId, ct))
            throw new InvalidOperationException("The linked user must have access to the new employee branch. Unlink the account or assign its branch access first.");

        if (await _repository.ExistsByNationalIdAsync(dto.NationalId, employeeCode, ct))
            throw new InvalidOperationException("National ID is already assigned to another employee.");

        ApplyTo(emp, dto, username);

        await _repository.UpdateEmployeeAsync(emp, ct);
        await _repository.SaveChangesAsync(ct);

        return _fields.Protect(EmployeeMappings.ToDetails(emp));
    }

    /// <summary>Snapshot of the stored profile, used as the base for partial (group) updates.</summary>
    public static UpdateEmployeeDto StoredProfile(Employee e) => new(
        e.NameLocal, e.NameEn, e.NationalId, e.Nationality, e.PassportNumber, e.DateOfBirth, e.Gender, e.MaritalStatus, e.Email, e.Phone,
        e.HireDate, e.ProbationEndDate, e.TerminationDate, e.TerminationReason, e.EmploymentType, e.EmploymentStatus, e.DepartmentCode,
        e.PositionCode, e.BranchId, e.ManagerEmployeeCode, e.SscNumber, e.TaxExemptionCount, e.IsHighRiskRole, e.BankName,
        e.BankAccountNumber, e.BankIban, e.IsActive, e.CitizenshipCountryCode);

    private static void EnsureTerminationIsNotChanged(Employee emp, UpdateEmployeeDto dto)
    {
        if (!string.Equals(emp.EmploymentStatus, dto.EmploymentStatus, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(dto.EmploymentStatus, "TERMINATED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Employee termination must be completed through the End of Service workflow.");

        if (dto.TerminationDate?.Date != emp.TerminationDate?.Date ||
            !string.Equals(dto.TerminationReason?.Trim(), emp.TerminationReason?.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException("Termination date and reason can only be changed through the End of Service workflow.");
    }

    private void RequireBranchPermissions(Employee emp, UpdateEmployeeDto dto, bool limitedUpdate)
    {
        var branchChanged = emp.BranchId != dto.BranchId;
        foreach (var branch in new[] { emp.BranchId, dto.BranchId }.Distinct())
        {
            // Full updates touch identity and bank fields; a branch move touches all groups.
            if (!limitedUpdate || branchChanged)
            {
                _fields.Require(EmployeeFieldAccess.EditIdentity, branch);
                _fields.Require(EmployeeFieldAccess.EditBank, branch);
            }
            if (branchChanged)
                _fields.Require(EmployeeFieldAccess.EditSalary, branch);
        }
    }

    private static void ApplyTo(Employee emp, UpdateEmployeeDto dto, string username)
    {
        emp.NameLocal = dto.NameLocal;
        emp.NameEn = dto.NameEn;
        emp.NationalId = dto.NationalId;
        emp.Nationality = dto.Nationality;
        if (dto.CitizenshipCountryCode != null)
            emp.CitizenshipCountryCode = string.IsNullOrEmpty(dto.CitizenshipCountryCode) ? null : dto.CitizenshipCountryCode;
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
        emp.UpdateUser = username;
        emp.UpdateDate = DateTime.UtcNow;
    }
}
