using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

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

        var normalizedFormat = (format ?? "CSV").Trim().ToUpperInvariant();
        var records = new List<BankPayrollExportRecordDto>();
        int seq = 1;

        foreach (var line in run.Lines.OrderBy(l => l.EmployeeCode))
        {
            if (line.NetPay <= 0) continue;

            var iban = !string.IsNullOrWhiteSpace(line.Iban) 
                ? line.Iban 
                : (line.Employee?.BankIban ?? line.Employee?.BankAccountNumber ?? string.Empty);

            var bankCode = !string.IsNullOrWhiteSpace(line.BankCode)
                ? line.BankCode
                : (line.Employee?.BankName ?? "BANK");

            var nationalId = line.Employee?.NationalId ?? string.Empty;
            var fullName = !string.IsNullOrWhiteSpace(line.Employee?.NameLocal) 
                ? line.Employee.NameLocal 
                : (!string.IsNullOrWhiteSpace(line.Employee?.NameEn) ? line.Employee.NameEn : line.EmployeeCode);

            records.Add(new BankPayrollExportRecordDto(
                SequenceNo: seq++,
                EmployeeCode: line.EmployeeCode,
                NationalId: nationalId,
                FullName: fullName,
                BankCode: bankCode,
                Iban: iban,
                Currency: "JOD",
                NetAmount: line.NetPay,
                Remarks: $"Payroll {run.PayPeriod}"
            ));
        }

        decimal totalAmount = records.Sum(r => r.NetAmount);
        int totalRecords = records.Count;

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
            default:
                (fileBytes, fileName, contentType) = GenerateCsvFile(run.PayPeriod, records);
                break;
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
            var escapedName = r.FullName.Replace("\"", "\"\"");
            var escapedRemarks = r.Remarks.Replace("\"", "\"\"");
            sb.AppendLine($"{r.SequenceNo},\"{r.EmployeeCode}\",\"{r.NationalId}\",\"{escapedName}\",\"{r.BankCode}\",\"{r.Iban}\",\"{r.Currency}\",{r.NetAmount:F3},\"{escapedRemarks}\"");
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

        sb.AppendLine($"H|{payPeriod}|{totalRecords}|{totalAmount:F3}|{now:yyyyMMddHHmmss}");

        foreach (var r in records)
        {
            sb.AppendLine($"D|{r.SequenceNo}|{r.EmployeeCode}|{r.NationalId}|{r.FullName}|{r.BankCode}|{r.Iban}|{r.Currency}|{r.NetAmount:F3}|{r.Remarks}");
        }

        sb.AppendLine($"T|{totalRecords}|{totalAmount:F3}");

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

        sb.AppendLine($"SCR|THINKON|CBJ|{now:yyyy-MM-dd}|{payPeriod}|{totalAmount:F3}|{totalRecords}|JOD");

        foreach (var r in records)
        {
            sb.AppendLine($"EDR|{r.EmployeeCode}|{r.BankCode}|{r.Iban}|{payPeriod}|{r.NetAmount:F3}|SALARY");
        }

        var encoding = new UTF8Encoding(false);
        var bytes = encoding.GetBytes(sb.ToString());
        var fileName = $"SIF_{payPeriod}_{DateTime.UtcNow:yyyyMMddHHmmss}.sif";
        return (bytes, fileName, "text/plain");
    }
}
