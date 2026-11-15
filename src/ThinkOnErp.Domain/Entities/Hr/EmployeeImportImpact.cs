namespace ThinkOnErp.Domain.Entities.Hr;

public sealed record EmployeeImportPeriodImpact(string PeriodCode, DateTime StartDate, DateTime EndDate, string Status, bool HasApprovedPayroll);
public sealed record EmployeeImportImpact(IReadOnlyList<EmployeeImportPeriodImpact> Periods, bool HasUnmappedApprovedPayroll);
