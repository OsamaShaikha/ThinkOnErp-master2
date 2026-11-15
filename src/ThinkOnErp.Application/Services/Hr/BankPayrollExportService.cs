using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Domain.Rules;

namespace ThinkOnErp.Application.Services.Hr;

public interface IBankPayrollExportService
{
    Task<BankPayrollExportResultDto> ExportPayrollToBankFormatAsync(
        long payrollRunId, 
        string format, 
        CancellationToken cancellationToken = default);
}

public sealed class BankPayrollExportService : IBankPayrollExportService
{
    private readonly IPayrollPeriodRepository _payrollRepo;

    public BankPayrollExportService(IPayrollPeriodRepository payrollRepo)
    {
        _payrollRepo = payrollRepo;
    }

    public async Task<BankPayrollExportResultDto> ExportPayrollToBankFormatAsync(
        long payrollRunId, 
        string format, 
        CancellationToken cancellationToken = default)
    {
        var run = await _payrollRepo.GetPayrollRunByIdAsync(payrollRunId, cancellationToken);
        if (run == null)
        {
            throw new KeyNotFoundException($"Payroll run {payrollRunId} not found.");
        }
        if (run.Status is not ("APPROVED" or "POSTED_TO_GL" or "PAID"))
            throw new InvalidOperationException("Only approved, posted, or paid payroll can be exported to the bank.");

        var normalizedFormat = (format ?? "CSV").Trim().ToUpperInvariant();
        if (normalizedFormat is not ("CSV" or "RAWATEBKOM" or "WPS" or "SIF"))
            throw new ArgumentException("Unsupported bank export format. Select CSV, RAWATEBKOM, WPS, or SIF.");
        var records = new List<BankPayrollExportRecordDto>();
        int seq = 1;

        foreach (var line in run.Lines.OrderBy(l => l.EmployeeCode))
        {
            if (line.NetPay <= 0) continue;
            if (line.Status == "CANCELLED") throw new InvalidOperationException("Payroll contains a cancelled payment line. Review the approved payroll before export.");
            if (string.Equals(line.PaymentMethod, "CASH", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(line.PaymentMethod, "BANK_TRANSFER", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Employee '{line.EmployeeCode}' has an unsupported payment method. Configure it before bank export.");

            if (string.IsNullOrWhiteSpace(line.Iban) || string.IsNullOrWhiteSpace(line.BankCode) ||
                string.IsNullOrWhiteSpace(line.CurrencyCode) || string.IsNullOrWhiteSpace(line.BeneficiaryName))
                throw new InvalidOperationException($"Employee '{line.EmployeeCode}' has incomplete approved bank-payment details. Correct the payroll snapshot through the payroll workflow before export.");
            if (!IbanRules.TryNormalize(line.Iban, out var iban))
                throw new InvalidOperationException($"Employee '{line.EmployeeCode}' has an invalid IBAN in the approved payroll snapshot.");
            var bankCode = line.BankCode.Trim();
            var nationalId = line.BeneficiaryNationalId ?? string.Empty;
            var fullName = line.BeneficiaryName;

            records.Add(new BankPayrollExportRecordDto(
                SequenceNo: seq++,
                EmployeeCode: line.EmployeeCode,
                NationalId: nationalId,
                FullName: fullName,
                BankCode: bankCode,
                Iban: iban,
                Currency: line.CurrencyCode.Trim().ToUpperInvariant(),
                NetAmount: line.NetPay,
                Remarks: $"Payroll {run.PayPeriod}"
            ));
        }

        decimal totalAmount = records.Sum(r => r.NetAmount);
        int totalRecords = records.Count;
        if (totalRecords == 0)
            throw new InvalidOperationException("This payroll has no positive bank-transfer payments to export.");
        if (records.Select(r => r.Currency).Distinct().Count() > 1)
            throw new InvalidOperationException("This export contains multiple currencies. Create separate payment batches per currency before bank export.");
        if (records.GroupBy(r => r.EmployeeCode, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Payroll contains duplicate employee payments. Resolve them before bank export.");
        if (normalizedFormat != "CSV")
        {
            if (records[0].Currency != "JOD")
                throw new InvalidOperationException("The legacy bank templates support JOD only. Configure a bank-specific profile for other currencies or use CSV for review.");
            foreach (var record in records)
                if (new[] { record.EmployeeCode, record.NationalId, record.FullName, record.BankCode, record.Iban, record.Remarks }
                    .Any(v => v.Contains('|') || v.Contains('\r') || v.Contains('\n')))
                    throw new InvalidOperationException("Bank template values cannot contain pipe separators or line breaks.");
        }

        byte[] fileBytes;
        string fileName;
        string contentType;

        switch (normalizedFormat)
        {
            case "RAWATEBKOM":
                (fileBytes, fileName, contentType) = GenerateRawatebkomFile(run.PayPeriod, records, totalRecords, totalAmount);
                break;
            case "WPS":
            case "SIF":
                (fileBytes, fileName, contentType) = GenerateWpsSifFile(run.PayPeriod, records, totalRecords, totalAmount);
                break;
            case "CSV":
                (fileBytes, fileName, contentType) = GenerateCsvFile(run.PayPeriod, records);
                break;
            default:
                throw new ArgumentException("Unsupported bank export format.");
        }

        return new BankPayrollExportResultDto(
            PayrollRunId: payrollRunId,
            PayPeriod: run.PayPeriod,
            Format: normalizedFormat,
            TotalRecords: totalRecords,
            TotalAmount: totalAmount,
            FileBytes: fileBytes,
            FileName: fileName,
            ContentType: contentType
        );
    }

    private static (byte[] bytes, string fileName, string contentType) GenerateCsvFile(
        string payPeriod, 
        List<BankPayrollExportRecordDto> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SequenceNo,EmployeeCode,NationalId,FullName,BankCode,IBAN,Currency,NetAmount,Remarks");

        foreach (var r in records)
        {
            static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
            sb.AppendLine(string.Join(",", r.SequenceNo.ToString(CultureInfo.InvariantCulture), Quote(r.EmployeeCode),
                Quote(r.NationalId), Quote(r.FullName), Quote(r.BankCode), Quote(r.Iban), Quote(r.Currency),
                r.NetAmount.ToString("F3", CultureInfo.InvariantCulture), Quote(r.Remarks)));
        }

        var encoding = new UTF8Encoding(true);
        var bytes = encoding.GetBytes(sb.ToString());
        var fileName = $"payroll_{payPeriod}_bank_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return (bytes, fileName, "text/csv");
    }

    private static (byte[] bytes, string fileName, string contentType) GenerateRawatebkomFile(
        string payPeriod, 
        List<BankPayrollExportRecordDto> records, 
        int totalRecords, 
        decimal totalAmount)
    {
        var sb = new StringBuilder();
        var now = DateTime.UtcNow;

        sb.AppendLine(FormattableString.Invariant($"H|{payPeriod}|{totalRecords}|{totalAmount:F3}|{now:yyyyMMddHHmmss}"));

        foreach (var r in records)
        {
            sb.AppendLine(FormattableString.Invariant($"D|{r.SequenceNo}|{r.EmployeeCode}|{r.NationalId}|{r.FullName}|{r.BankCode}|{r.Iban}|{r.Currency}|{r.NetAmount:F3}|{r.Remarks}"));
        }

        sb.AppendLine(FormattableString.Invariant($"T|{totalRecords}|{totalAmount:F3}"));

        var encoding = new UTF8Encoding(false);
        var bytes = encoding.GetBytes(sb.ToString());
        var fileName = $"RAWATEBKOM_{payPeriod}_{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
        return (bytes, fileName, "text/plain");
    }

    private static (byte[] bytes, string fileName, string contentType) GenerateWpsSifFile(
        string payPeriod, 
        List<BankPayrollExportRecordDto> records, 
        int totalRecords, 
        decimal totalAmount)
    {
        var sb = new StringBuilder();
        var now = DateTime.UtcNow;

        sb.AppendLine(FormattableString.Invariant($"SCR|THINKON|CBJ|{now:yyyy-MM-dd}|{payPeriod}|{totalAmount:F3}|{totalRecords}|JOD"));

        foreach (var r in records)
        {
            sb.AppendLine(FormattableString.Invariant($"EDR|{r.EmployeeCode}|{r.BankCode}|{r.Iban}|{payPeriod}|{r.NetAmount:F3}|SALARY"));
        }

        var encoding = new UTF8Encoding(false);
        var bytes = encoding.GetBytes(sb.ToString());
        var fileName = $"SIF_{payPeriod}_{DateTime.UtcNow:yyyyMMddHHmmss}.sif";
        return (bytes, fileName, "text/plain");
    }
}
