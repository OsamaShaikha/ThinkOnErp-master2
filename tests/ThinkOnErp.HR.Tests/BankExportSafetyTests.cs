using System.Globalization;
using System.Text;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class BankExportSafetyTests
{
    private static PayrollRun Run() => new()
    {
        Id = 1, Status = "APPROVED", PayPeriod = "2026-11",
        Lines = new() { new PayrollRunLine { EmployeeCode = "E1", NetPay = 1234.567m,
            Iban = "GB82WEST12345698765432", BankCode = "APPROVED_BANK", BeneficiaryName = "Approved Name", CurrencyCode = "USD" } }
    };
    private static BankPayrollExportService Service(PayrollRun run)
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        return new BankPayrollExportService(repository.Object);
    }

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("CALCULATED")]
    [InlineData("CANCELLED")]
    public async Task UnapprovedPayrollCannotGeneratePaymentFile(string status)
    {
        var run = Run(); run.Status = status;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(run).ExportPayrollToBankFormatAsync(1, "CSV"));
    }

    [Fact]
    public async Task CashEmployeesAreExcludedAndApprovedRoutingIsNotReadFromCurrentEmployee()
    {
        var run = Run();
        run.Lines[0].Employee = new Employee { BankIban = "CHANGED_IBAN", BankName = "CHANGED_BANK", NameLocal = "Changed Name" };
        run.Lines.Add(new PayrollRunLine { EmployeeCode = "CASH1", NetPay = 500m, PaymentMethod = "CASH" });
        var result = await Service(run).ExportPayrollToBankFormatAsync(1, "CSV");
        Assert.Equal(1, result.TotalRecords);
        Assert.Equal(1234.567m, result.TotalAmount);
        var csv = Encoding.UTF8.GetString(result.FileBytes);
        Assert.Contains("GB82WEST12345698765432", csv);
        Assert.Contains("Approved Name", csv);
        Assert.DoesNotContain("CHANGED_IBAN", csv);
        Assert.DoesNotContain("CASH1", csv);
    }

    [Theory]
    [InlineData("iban")]
    [InlineData("bank")]
    [InlineData("currency")]
    [InlineData("name")]
    public async Task MissingSnapshotDataRejectsWholeFileInsteadOfUsingCurrentEmployee(string missing)
    {
        var run = Run(); var line = run.Lines[0];
        line.Employee = new Employee { BankIban = "CURRENT_IBAN", BankName = "CURRENT_BANK", NameLocal = "Current Name" };
        if (missing == "iban") line.Iban = null;
        if (missing == "bank") line.BankCode = null;
        if (missing == "currency") line.CurrencyCode = null;
        if (missing == "name") line.BeneficiaryName = null;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(run).ExportPayrollToBankFormatAsync(1, "CSV"));
    }

    [Fact]
    public async Task CsvUsesInvariantAmountsAndEscapesAllTextFields()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var run = Run(); run.Lines[0].EmployeeCode = "E\"1";
            var result = await Service(run).ExportPayrollToBankFormatAsync(1, "CSV");
            var csv = Encoding.UTF8.GetString(result.FileBytes);
            Assert.Contains("1234.567", csv);
            Assert.Contains("\"E\"\"1\"", csv);
            Assert.Contains("\"USD\"", csv);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public async Task UnknownFormatCannotSilentlyProduceCsv()
        => await Assert.ThrowsAsync<ArgumentException>(() => Service(Run()).ExportPayrollToBankFormatAsync(1, "UNKNOWN"));

    [Fact]
    public async Task MixedCurrenciesCannotProduceMisleadingTotal()
    {
        var run = Run();
        run.Lines.Add(new PayrollRunLine { EmployeeCode = "E2", NetPay = 5m, Iban = "IBAN", BankCode = "BANK", BeneficiaryName = "Name", CurrencyCode = "JOD" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(run).ExportPayrollToBankFormatAsync(1, "CSV"));
    }

    [Fact]
    public async Task ExportPayroll_WhenLineStatusCancelled_ThrowsInvalidOperationException()
    {
        var run = Run();
        run.Lines[0].Status = "CANCELLED";
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(run).ExportPayrollToBankFormatAsync(1, "CSV"));
        Assert.Contains("cancelled payment line", ex.Message);
    }

    [Fact]
    public async Task ExportPayroll_WhenPaymentMethodUnsupported_ThrowsInvalidOperationException()
    {
        var run = Run();
        run.Lines[0].PaymentMethod = "CHEQUE";
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(run).ExportPayrollToBankFormatAsync(1, "CSV"));
        Assert.Contains("unsupported payment method", ex.Message);
    }

    [Fact]
    public async Task ExportPayroll_WhenZeroRecordsWithPositiveNetPay_ThrowsInvalidOperationException()
    {
        var run = Run();
        run.Lines[0].NetPay = 0m;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(run).ExportPayrollToBankFormatAsync(1, "CSV"));
        Assert.Contains("no positive bank-transfer payments to export", ex.Message);
    }

    [Fact]
    public async Task ExportPayroll_WhenDuplicateEmployeePayment_ThrowsInvalidOperationException()
    {
        var run = Run();
        run.Lines.Add(new PayrollRunLine
        {
            EmployeeCode = "e1", // case-insensitive duplicate
            NetPay = 200m,
            Iban = "GB29NWBK60161331926819",
            BankCode = "APPROVED_BANK",
            BeneficiaryName = "Approved Name 2",
            CurrencyCode = "USD"
        });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(run).ExportPayrollToBankFormatAsync(1, "CSV"));
        Assert.Contains("duplicate employee payments", ex.Message);
    }

    [Theory]
    [InlineData("RAWATEBKOM")]
    [InlineData("WPS")]
    [InlineData("SIF")]
    public async Task ExportPayroll_WhenLegacyFormatNonJodCurrency_ThrowsInvalidOperationException(string format)
    {
        var run = Run(); // Currency is USD
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(run).ExportPayrollToBankFormatAsync(1, format));
        Assert.Contains("legacy bank templates support JOD only", ex.Message);
    }

    [Theory]
    [InlineData("Name|WithPipe")]
    [InlineData("Line\rBreak")]
    [InlineData("Line\nBreak")]
    public async Task ExportPayroll_WhenLegacyFormatContainsSeparatorOrNewline_ThrowsInvalidOperationException(string badText)
    {
        var run = Run();
        run.Lines[0].CurrencyCode = "JOD";
        run.Lines[0].BeneficiaryName = badText;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(run).ExportPayrollToBankFormatAsync(1, "RAWATEBKOM"));
        Assert.Contains("cannot contain pipe separators or line breaks", ex.Message);
    }

    [Theory]
    [InlineData("POSTED_TO_GL")]
    [InlineData("PAID")]
    public async Task ExportPayroll_WhenPostedToGlOrPaidStatus_Succeeds(string status)
    {
        var run = Run();
        run.Status = status;
        var result = await Service(run).ExportPayrollToBankFormatAsync(1, "CSV");
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalRecords);
        Assert.Equal(1234.567m, result.TotalAmount);
    }

    [Fact]
    public async Task ExportPayroll_SifFormatAlias_ProducesSameFormatPrefixAsWps()
    {
        var run = Run();
        run.Lines[0].CurrencyCode = "JOD";
        var result = await Service(run).ExportPayrollToBankFormatAsync(1, "SIF");
        Assert.NotNull(result);
        Assert.Equal("SIF", result.Format);
        Assert.StartsWith("SIF_2026-11_", result.FileName);
        Assert.Equal("text/plain", result.ContentType);
    }
}
