namespace ThinkOnErp.Domain.Entities.Hr;

public static class PayrollPostingRules
{
    public static void ValidateTotals(PayrollRun run)
    {
        if (run.TotalOtherDeductions < 0m)
            throw new InvalidOperationException("Negative other deductions require an explicit accounting treatment before payroll posting.");
        if (run.TotalGrossSalary != run.Lines.Sum(l => l.GrossSalary) ||
            run.TotalNetSalary != run.Lines.Sum(l => l.NetPay) ||
            run.TotalEmployeeSsc != run.Lines.Sum(l => l.SscEmployeeContrib) ||
            run.TotalEmployerSsc != run.Lines.Sum(l => l.SscEmployerContrib) ||
            run.TotalIncomeTax != run.Lines.Sum(l => l.IncomeTaxWithheld) ||
            run.TotalNationalContrib != run.Lines.Sum(l => l.NationalContribWithheld) ||
            run.TotalOtherDeductions != run.Lines.Sum(l => l.OtherDeductions))
            throw new InvalidOperationException("Payroll totals do not match the stored employee lines. Review the payroll before posting.");

        var debit = run.TotalGrossSalary + run.TotalEmployerSsc;
        var credit = run.TotalNetSalary + run.TotalEmployeeSsc + run.TotalEmployerSsc +
            run.TotalIncomeTax + run.TotalNationalContrib + run.TotalOtherDeductions;
        if (debit != credit)
            throw new InvalidOperationException("Payroll journal is not balanced.");
    }
}
