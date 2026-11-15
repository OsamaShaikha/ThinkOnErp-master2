using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Common;

/// <summary>
/// Field-level permission gate for sensitive employee data (identity, bank, salary).
/// HTTP requests always use the request-scoped <see cref="EmployeeFieldAccess"/> populated by HrAccessFilter.
/// <see cref="Trusted"/> is reserved for internal jobs and verification tools that run without a user,
/// replacing the old convention of passing <c>null</c> to mean "no field checks".
/// </summary>
public sealed class EmployeeFieldGuard
{
    private readonly EmployeeFieldAccess? _access;

    public EmployeeFieldGuard(EmployeeFieldAccess access)
        => _access = access ?? throw new ArgumentNullException(nameof(access));

    private EmployeeFieldGuard() { }

    /// <summary>No field-level checks. Never use this for user-facing requests.</summary>
    public static EmployeeFieldGuard Trusted { get; } = new();

    public bool IsEnforced => _access is not null;

    public void Require(string feature, long? branchId) => _access?.Require(feature, branchId);

    public EmployeeSummaryDto Protect(EmployeeSummaryDto dto, long? branchId) => _access?.Protect(dto, branchId) ?? dto;

    public EmployeeDetailsDto Protect(EmployeeDetailsDto dto) => _access?.Protect(dto) ?? dto;

    internal static EmployeeFieldGuard From(EmployeeFieldAccess? access) => access is null ? Trusted : new EmployeeFieldGuard(access);
}
