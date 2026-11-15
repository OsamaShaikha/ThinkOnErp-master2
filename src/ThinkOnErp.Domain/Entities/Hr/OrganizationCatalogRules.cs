using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed record DepartmentWrite
{
    [System.Text.Json.Serialization.JsonConstructor]
    public DepartmentWrite() { }

    public DepartmentWrite(string nameLocal, string nameEn, long? branchId, string? parentDepartmentCode, bool isActive)
    {
        NameLocal = nameLocal;
        NameEn = nameEn;
        BranchId = branchId;
        ParentDepartmentCode = parentDepartmentCode;
        IsActive = isActive;
    }

    public DepartmentWrite(string nameLocal, string nameEn, long? branchId, string? parentDepartmentCode, bool isActive, string? managerEmployeeCode)
        : this(nameLocal, nameEn, branchId, parentDepartmentCode, isActive)
    {
        ManagerEmployeeCode = managerEmployeeCode;
    }

    public string NameLocal { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public long? BranchId { get; init; }
    public string? ParentDepartmentCode { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public bool IsActive { get; init; }
    private string? _managerEmployeeCode;
    public string? ManagerEmployeeCode
    {
        get => _managerEmployeeCode;
        init { _managerEmployeeCode = value; ManagerEmployeeCodeSpecified = true; }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ManagerEmployeeCodeSpecified { get; internal init; }
}
public sealed record PositionWrite(string DepartmentCode, string TitleLocal, string TitleEn, string? ReportsToPositionCode,
    [property: System.Text.Json.Serialization.JsonRequired] int Headcount, [property: System.Text.Json.Serialization.JsonRequired] bool IsActive);
public sealed class OrganizationVersionConflictException() : InvalidOperationException("Organization version changed. Read it again before editing.");

public static class OrganizationCatalogRules
{
    public static string Code(string value)
    {
        return Text(value, 50, "Code").ToUpperInvariant();
    }

    public static string? OptionalCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Code(value);
    }

    public static string Text(string? value, int max, string field)
    {
        value = value?.Trim();
        return string.IsNullOrEmpty(value) || value.Length > max
            ? throw new ArgumentException($"{field} is required and must not exceed {max} characters.")
            : value;
    }
    public static DepartmentWrite Normalize(DepartmentWrite value)
    {
        return value.BranchId <= 0
            ? throw new ArgumentException("BranchId must be positive when supplied.")
            : (value with
            {
                NameLocal = Text(value.NameLocal, 200, nameof(value.NameLocal)),
                NameEn = Text(value.NameEn, 200, nameof(value.NameEn)),
                ParentDepartmentCode = OptionalCode(value.ParentDepartmentCode),
                ManagerEmployeeCode = OptionalCode(value.ManagerEmployeeCode),
                ManagerEmployeeCodeSpecified = value.ManagerEmployeeCodeSpecified
            });
    }
    public static PositionWrite Normalize(PositionWrite value)
    {
        return value.Headcount < 0
            ? throw new ArgumentException("Headcount must be nonnegative.")
            : (value with { DepartmentCode = Code(value.DepartmentCode), TitleLocal = Text(value.TitleLocal, 200, nameof(value.TitleLocal)), TitleEn = Text(value.TitleEn, 200, nameof(value.TitleEn)), ReportsToPositionCode = OptionalCode(value.ReportsToPositionCode) });
    }
    public static string ExpectedVersion(string? value)
    {
        value = value?.Trim().Trim('"');
        return value?.Length != 64 || !value.All(Uri.IsHexDigit)
            ? throw new ArgumentException("A single current If-Match version is required.")
            : value;
    }
    public static string Version(HrDepartment d)
    {
        return Hash(new { d.DepartmentCode, d.BranchId, d.NameLocal, d.NameEn, d.ParentDepartmentCode, d.ManagerEmployeeCode, d.CostCenterCode, d.IsActive, Created = d.CreationDate.Ticks, d.CreationUser, Updated = d.UpdateDate?.Ticks, d.UpdateUser });
    }

    public static string Version(HrPosition p)
    {
        return Hash(new { p.PositionCode, p.DepartmentCode, p.TitleLocal, p.TitleEn, p.ReportsToPositionCode, p.Headcount, p.JobGradeCode, p.IsActive, Created = p.CreationDate.Ticks, p.CreationUser, Updated = p.UpdateDate?.Ticks, p.UpdateUser });
    }

    private static string Hash(object value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    }
}
