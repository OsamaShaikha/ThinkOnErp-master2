using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class BankExportTests
{
    private readonly Mock<IPayrollPeriodRepository> _payrollRepoMock;
    private readonly BankPayrollExportService _service;

    public BankExportTests()
    {
        _payrollRepoMock = new Mock<IPayrollPeriodRepository>();
        _service = new BankPayrollExportService(_payrollRepoMock.Object);
    }

    private PayrollRun CreateSampleRun(string status = "APPROVED")
    {
        var run = new PayrollRun
        {
            Id = 100,
            PayPeriod = "2026-09",
            Status = status,
            Lines = new List<PayrollRunLine>
            {
                new()
                {
                    Id = 1,
                    EmployeeCode = "EMP001",
                    NetPay = 750.500m,
                    PaymentMethod = "BANK_TRANSFER",
                    BankCode = "ARAB_BANK",
                    Iban = "GB82WEST12345698765432",
                    CurrencyCode = "JOD",
                    BeneficiaryName = "أحمد محمد",
                    BeneficiaryNationalId = "9901020304",
                    Employee = new Employee
                    {
                        EmployeeCode = "EMP001",
                        NameLocal = "أحمد محمد",
                        NationalId = "9901020304",
                        BankName = "ARAB_BANK",
                        BankIban = "GB82WEST12345698765432"
                    }
                },
                new()
                {
                    Id = 2,
                    EmployeeCode = "EMP002",
                    NetPay = 920.000m,
                    PaymentMethod = "BANK_TRANSFER",
                    BankCode = "HOUSING_BANK",
                    Iban = "GB29NWBK60161331926819",
                    CurrencyCode = "JOD",
                    BeneficiaryName = "سارة خليل",
                    BeneficiaryNationalId = "9951020305",
                    Employee = new Employee
                    {
                        EmployeeCode = "EMP002",
                        NameLocal = "سارة خليل",
                        NationalId = "9951020305",
                        BankName = "HOUSING_BANK",
                        BankIban = "GB29NWBK60161331926819"
                    }
                },
                new()
                {
                    Id = 3,
                    EmployeeCode = "EMP003",
                    NetPay = 0m, // Zero net pay - should be skipped from bank file
                    PaymentMethod = "BANK_TRANSFER",
                    BankCode = "ARAB_BANK",
                    Iban = "GB82WEST12345698765432",
                    CurrencyCode = "JOD",
                    BeneficiaryName = "خالد علي",
                    Employee = new Employee
                    {
                        EmployeeCode = "EMP003",
                        NameLocal = "خالد علي",
                        NationalId = "9881020306"
                    }
                }
            }
        };

        return run;
    }

    [Fact]
    public async Task ExportPayroll_Csv_ReturnsValidCsvFile()
    {
        var sampleRun = CreateSampleRun();
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "CSV");

        Assert.NotNull(result);
        Assert.Equal(100, result.PayrollRunId);
        Assert.Equal("2026-09", result.PayPeriod);
        Assert.Equal("CSV", result.Format);
        Assert.Equal(2, result.TotalRecords); // Only 2 with NetPay > 0
        Assert.Equal(1670.500m, result.TotalAmount);
        Assert.Equal("text/csv", result.ContentType);
        Assert.EndsWith(".csv", result.FileName);

        var content = Encoding.UTF8.GetString(result.FileBytes);
        Assert.Contains("EMP001", content);
        Assert.Contains("EMP002", content);
        Assert.DoesNotContain("EMP003", content);
        Assert.Contains("GB82WEST12345698765432", content);
    }

    [Fact]
    public async Task ExportPayroll_Rawatebkom_ReturnsValidFixedFormat()
    {
        var sampleRun = CreateSampleRun();
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "RAWATEBKOM");

        Assert.NotNull(result);
        Assert.Equal("RAWATEBKOM", result.Format);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal("text/plain", result.ContentType);
        Assert.StartsWith("RAWATEBKOM_2026-09_", result.FileName);

        var content = Encoding.UTF8.GetString(result.FileBytes);
        Assert.Contains("H|2026-09|2|1670.500|", content);
        Assert.Contains("D|1|EMP001|9901020304|أحمد محمد|ARAB_BANK|GB82WEST12345698765432|JOD|750.500|", content);
        Assert.Contains("T|2|1670.500", content);
    }

    [Fact]
    public async Task ExportPayroll_WpsSif_ReturnsValidSifFormat()
    {
        var sampleRun = CreateSampleRun();
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "WPS");

        Assert.NotNull(result);
        Assert.Equal("WPS", result.Format);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal("text/plain", result.ContentType);
        Assert.StartsWith("SIF_2026-09_", result.FileName);

        var content = Encoding.UTF8.GetString(result.FileBytes);
        Assert.Contains("SCR|THINKON|CBJ|", content);
        Assert.Contains("EDR|EMP001|ARAB_BANK|GB82WEST12345698765432|2026-09|750.500|SALARY", content);
    }

    [Fact]
    public async Task ExportPayroll_SifFormatExplicit_ReturnsValidSifFile()
    {
        var sampleRun = CreateSampleRun();
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "SIF");

        Assert.NotNull(result);
        Assert.Equal("SIF", result.Format);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal("text/plain", result.ContentType);
        Assert.EndsWith(".sif", result.FileName);
        Assert.StartsWith("SIF_2026-09_", result.FileName);

        var content = Encoding.UTF8.GetString(result.FileBytes);
        Assert.Contains("SCR|THINKON|CBJ|", content);
        Assert.Contains("EDR|EMP002|HOUSING_BANK|GB29NWBK60161331926819|2026-09|920.000|SALARY", content);
    }

    [Fact]
    public async Task ExportPayroll_NonExistentRun_ThrowsKeyNotFoundException()
    {
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ExportPayrollToBankFormatAsync(999, "CSV"));
    }

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("CALCULATED")]
    [InlineData("REJECTED")]
    [InlineData("CANCELLED")]
    public async Task ExportPayroll_WhenStatusNotApprovedOrPostedOrPaid_ThrowsInvalidOperationException(string status)
    {
        var sampleRun = CreateSampleRun(status);
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExportPayrollToBankFormatAsync(100, "CSV"));

        Assert.Equal("Only approved, posted, or paid payroll can be exported to the bank.", ex.Message);
    }

    [Theory]
    [InlineData("POSTED_TO_GL")]
    [InlineData("PAID")]
    public async Task ExportPayroll_WhenStatusPostedToGlOrPaid_ExportsSuccessfully(string status)
    {
        var sampleRun = CreateSampleRun(status);
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "CSV");

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal(1670.500m, result.TotalAmount);
    }

    [Theory]
    [InlineData("XML")]
    [InlineData("JSON")]
    [InlineData("EXCEL")]
    [InlineData("PDF")]
    public async Task ExportPayroll_WhenFormatUnsupported_ThrowsArgumentException(string format)
    {
        var sampleRun = CreateSampleRun();
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportPayrollToBankFormatAsync(100, format));

        Assert.Contains("Unsupported bank export format", ex.Message);
    }

    [Fact]
    public async Task ExportPayroll_WhenLineStatusCancelled_ThrowsInvalidOperationException()
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines[0].Status = "CANCELLED";
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExportPayrollToBankFormatAsync(100, "CSV"));

        Assert.Equal("Payroll contains a cancelled payment line. Review the approved payroll before export.", ex.Message);
    }

    [Fact]
    public async Task ExportPayroll_WhenLinePaymentMethodIsCash_SkipsLineCleanly()
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines[0].PaymentMethod = "CASH";
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "CSV");

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalRecords); // Only EMP002 remaining
        Assert.Equal(920.000m, result.TotalAmount);

        var content = Encoding.UTF8.GetString(result.FileBytes);
        Assert.DoesNotContain("EMP001", content);
        Assert.Contains("EMP002", content);
    }

    [Fact]
    public async Task ExportPayroll_WhenLinePaymentMethodUnsupported_ThrowsInvalidOperationException()
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines[0].PaymentMethod = "CHEQUE";
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExportPayrollToBankFormatAsync(100, "CSV"));

        Assert.Contains("has an unsupported payment method", ex.Message);
    }

    [Theory]
    [InlineData(null, "ARAB_BANK", "JOD", "أحمد")]
    [InlineData("   ", "ARAB_BANK", "JOD", "أحمد")]
    [InlineData("GB82WEST12345698765432", null, "JOD", "أحمد")]
    [InlineData("GB82WEST12345698765432", "   ", "JOD", "أحمد")]
    [InlineData("GB82WEST12345698765432", "ARAB_BANK", null, "أحمد")]
    [InlineData("GB82WEST12345698765432", "ARAB_BANK", "   ", "أحمد")]
    [InlineData("GB82WEST12345698765432", "ARAB_BANK", "JOD", null)]
    [InlineData("GB82WEST12345698765432", "ARAB_BANK", "JOD", "   ")]
    public async Task ExportPayroll_WhenApprovedBankDetailsIncomplete_ThrowsInvalidOperationException(
        string? iban, string? bankCode, string? currency, string? name)
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines[0].Iban = iban;
        sampleRun.Lines[0].BankCode = bankCode;
        sampleRun.Lines[0].CurrencyCode = currency;
        sampleRun.Lines[0].BeneficiaryName = name;

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExportPayrollToBankFormatAsync(100, "CSV"));

        Assert.Contains("has incomplete approved bank-payment details", ex.Message);
    }

    [Fact]
    public async Task ExportPayroll_WhenNoPositiveBankTransferRecords_ThrowsInvalidOperationException()
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines.Clear();
        sampleRun.Lines.Add(new PayrollRunLine
        {
            Id = 1,
            EmployeeCode = "EMP001",
            NetPay = 0m,
            PaymentMethod = "BANK_TRANSFER"
        });
        sampleRun.Lines.Add(new PayrollRunLine
        {
            Id = 2,
            EmployeeCode = "EMP002",
            NetPay = 500m,
            PaymentMethod = "CASH"
        });

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExportPayrollToBankFormatAsync(100, "CSV"));

        Assert.Equal("This payroll has no positive bank-transfer payments to export.", ex.Message);
    }

    [Fact]
    public async Task ExportPayroll_WhenMultipleCurrenciesPresent_ThrowsInvalidOperationException()
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines[1].CurrencyCode = "USD";

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExportPayrollToBankFormatAsync(100, "CSV"));

        Assert.Equal("This export contains multiple currencies. Create separate payment batches per currency before bank export.", ex.Message);
    }

    [Fact]
    public async Task ExportPayroll_WhenDuplicateEmployeeLinesPresent_ThrowsInvalidOperationException()
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines[1].EmployeeCode = "EMP001"; // Duplicate employee code

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExportPayrollToBankFormatAsync(100, "CSV"));

        Assert.Equal("Payroll contains duplicate employee payments. Resolve them before bank export.", ex.Message);
    }

    [Theory]
    [InlineData("RAWATEBKOM")]
    [InlineData("WPS")]
    [InlineData("SIF")]
    public async Task ExportPayroll_WhenLegacyFormatWithNonJodCurrency_ThrowsInvalidOperationException(string format)
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines[0].CurrencyCode = "USD";
        sampleRun.Lines[1].CurrencyCode = "USD";

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExportPayrollToBankFormatAsync(100, format));

        Assert.Equal("The legacy bank templates support JOD only. Configure a bank-specific profile for other currencies or use CSV for review.", ex.Message);
    }

    [Theory]
    [InlineData("RAWATEBKOM", "Name|WithPipe")]
    [InlineData("WPS", "Name\nWithNewline")]
    [InlineData("SIF", "Name\rWithCarriageReturn")]
    public async Task ExportPayroll_WhenLegacyFormatContainsPipeOrNewline_ThrowsInvalidOperationException(string format, string taintedName)
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines[0].BeneficiaryName = taintedName;

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExportPayrollToBankFormatAsync(100, format));

        Assert.Equal("Bank template values cannot contain pipe separators or line breaks.", ex.Message);
    }

    [Fact]
    public async Task ExportPayroll_CsvEscaping_HandlesQuotesAndCommasInFields()
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines[0].BeneficiaryName = "Ahmed \"The Specialist\", Engineer";

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "CSV");

        Assert.NotNull(result);
        var content = Encoding.UTF8.GetString(result.FileBytes);
        Assert.Contains("\"Ahmed \"\"The Specialist\"\", Engineer\"", content);
    }

    [Fact]
    public async Task ExportPayroll_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var sampleRun = CreateSampleRun();

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, ct))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "CSV", ct);

        Assert.NotNull(result);
        _payrollRepoMock.Verify(r => r.GetPayrollRunByIdAsync(100, ct), Times.Once);
    }

    [Fact]
    public async Task ExportPayroll_WhenFormatNull_DefaultsToCsv()
    {
        var sampleRun = CreateSampleRun();
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, null!);

        Assert.NotNull(result);
        Assert.Equal("CSV", result.Format);
        Assert.Equal("text/csv", result.ContentType);
        Assert.EndsWith(".csv", result.FileName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExportPayroll_WhenFormatEmptyOrWhitespace_ThrowsArgumentException(string format)
    {
        var sampleRun = CreateSampleRun();
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExportPayrollToBankFormatAsync(100, format));

        Assert.Contains("Unsupported bank export format", ex.Message);
    }

    [Fact]
    public async Task ExportPayroll_WhenFormatHasLeadingOrTrailingSpacesAndMixedCase_NormalizesCorrectly()
    {
        var sampleRun = CreateSampleRun();
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "  rawatebkom  ");

        Assert.NotNull(result);
        Assert.Equal("RAWATEBKOM", result.Format);
        Assert.Equal("text/plain", result.ContentType);
        Assert.StartsWith("RAWATEBKOM_2026-09_", result.FileName);
    }

    [Fact]
    public async Task ExportPayroll_WhenBeneficiaryNationalIdIsNull_ExportsEmptyStringWithoutFailure()
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines[0].BeneficiaryNationalId = null;

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var csvResult = await _service.ExportPayrollToBankFormatAsync(100, "CSV");
        var rawResult = await _service.ExportPayrollToBankFormatAsync(100, "RAWATEBKOM");

        var csvContent = Encoding.UTF8.GetString(csvResult.FileBytes);
        var rawContent = Encoding.UTF8.GetString(rawResult.FileBytes);

        Assert.Contains(",\"\",", csvContent);
        Assert.Contains("D|1|EMP001||أحمد محمد|", rawContent);
    }

    [Fact]
    public async Task ExportPayroll_OrdersRecordsAscendingByEmployeeCode()
    {
        var sampleRun = CreateSampleRun();
        // Reverse order
        sampleRun.Lines = new List<PayrollRunLine> { sampleRun.Lines[1], sampleRun.Lines[0] };

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "CSV");

        Assert.NotNull(result);
        var content = Encoding.UTF8.GetString(result.FileBytes);
        var emp1Index = content.IndexOf("EMP001", StringComparison.Ordinal);
        var emp2Index = content.IndexOf("EMP002", StringComparison.Ordinal);

        Assert.True(emp1Index > 0);
        Assert.True(emp2Index > 0);
        Assert.True(emp1Index < emp2Index);
    }

    [Fact]
    public async Task ExportPayroll_NegativeNetPay_IsSkippedCleanly()
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines.Add(new PayrollRunLine
        {
            Id = 4,
            EmployeeCode = "EMP004",
            NetPay = -120m, // Negative pay (clawback/deduction exceed)
            PaymentMethod = "BANK_TRANSFER",
            BankCode = "ARAB_BANK",
            Iban = "GB82WEST12345698765432",
            CurrencyCode = "JOD",
            BeneficiaryName = "سالم عمر"
        });

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "CSV");

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal(1670.500m, result.TotalAmount);
        var content = Encoding.UTF8.GetString(result.FileBytes);
        Assert.DoesNotContain("EMP004", content);
    }

    [Fact]
    public async Task ExportPayroll_CurrencyCodeWithWhitespaceOrLowercase_NormalizesToTrimmedUpper()
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines[0].CurrencyCode = "  jod  ";
        sampleRun.Lines[1].CurrencyCode = "JOD";

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "CSV");

        Assert.NotNull(result);
        var content = Encoding.UTF8.GetString(result.FileBytes);
        Assert.Contains("\"JOD\"", content);
        Assert.DoesNotContain("jod", content);
    }

    [Fact]
    public async Task ExportPayroll_WhenBankCodeHasPipe_InLegacyFormat_ThrowsInvalidOperationException()
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines[0].BankCode = "ARAB|BANK";

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExportPayrollToBankFormatAsync(100, "RAWATEBKOM"));

        Assert.Equal("Bank template values cannot contain pipe separators or line breaks.", ex.Message);
    }

    [Fact]
    public async Task ExportPayroll_WhenIbanHasNewline_InLegacyFormat_ThrowsInvalidOperationException()
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines[0].Iban = "GB82WEST123456987654324\n5678";

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExportPayrollToBankFormatAsync(100, "SIF"));

        Assert.Contains("has an invalid IBAN", ex.Message);
    }

    [Fact]
    public async Task ExportPayroll_WhenEmployeeCodeHasPipe_InLegacyFormat_ThrowsInvalidOperationException()
    {
        var sampleRun = CreateSampleRun();
        sampleRun.Lines[0].EmployeeCode = "EMP|001";

        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExportPayrollToBankFormatAsync(100, "RAWATEBKOM"));

        Assert.Equal("Bank template values cannot contain pipe separators or line breaks.", ex.Message);
    }

    [Fact]
    public async Task ExportPayroll_Remarks_ReflectsPayrollPayPeriod()
    {
        var sampleRun = CreateSampleRun();
        _payrollRepoMock.Setup(r => r.GetPayrollRunByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleRun);

        var result = await _service.ExportPayrollToBankFormatAsync(100, "RAWATEBKOM");

        Assert.NotNull(result);
        var content = Encoding.UTF8.GetString(result.FileBytes);
        Assert.Contains("Payroll 2026-09", content);
    }
}
