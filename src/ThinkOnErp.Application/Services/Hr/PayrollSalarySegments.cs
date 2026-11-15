using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed record PayrollSalarySegment(SalaryStructure Structure, DateTime Start, DateTime End);

public static class PayrollSalarySegments
{
    public static IReadOnlyList<PayrollSalarySegment> Build(Employee employee, PayrollPeriod period)
    {
        var start = employee.HireDate.Date > period.StartDate.Date ? employee.HireDate.Date : period.StartDate.Date;
        var end = employee.TerminationDate.HasValue && employee.TerminationDate.Value.Date < period.EndDate.Date
            ? employee.TerminationDate.Value.Date : period.EndDate.Date;
        if (start > end) return Array.Empty<PayrollSalarySegment>();
        var segments = employee.SalaryStructures.Where(s => s.IsActive && s.EffectiveFrom.Date <= end &&
                (!s.EffectiveTo.HasValue || s.EffectiveTo.Value.Date >= start))
            .OrderBy(s => s.EffectiveFrom).Select(s => new PayrollSalarySegment(s,
                s.EffectiveFrom.Date > start ? s.EffectiveFrom.Date : start,
                s.EffectiveTo.HasValue && s.EffectiveTo.Value.Date < end ? s.EffectiveTo.Value.Date : end)).ToList();
        var cursor = start;
        foreach (var segment in segments)
        {
            if (segment.Start != cursor)
                throw new InvalidOperationException($"Employee {employee.EmployeeCode} has overlapping salary structures or a coverage gap inside payroll period {period.PeriodCode}.");
            cursor = segment.End.AddDays(1);
        }
        if (cursor != end.AddDays(1))
            throw new InvalidOperationException($"Employee {employee.EmployeeCode} has no salary covering all eligible payroll dates.");
        PayrollCurrencyRules.RequireSingleCurrency(segments.Select(s => s.Structure.CurrencyCode));
        return segments;
    }
}
