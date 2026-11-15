using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class PayrollCurrencyTests
{
    private static readonly PayrollPeriod Period = new()
    {
        CompanyId = 1, PeriodCode = "2026-11", StartDate = new(2026, 11, 1), EndDate = new(2026, 11, 30)
    };

    private static Employee Employee(string code, string? currency)
    {
        var employee = new Employee { EmployeeCode = code, HireDate = new(2024, 1, 1) };
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = code, EffectiveFrom = employee.HireDate, BasicSalary = 100m, CurrencyCode = currency!
        });
        return employee;
    }

    private static PayrollCalculationEngine Engine(IPolicyRepository? policyRepository = null)
    {
        var ssc = new Mock<ISSCCalculationService>();
        ssc.Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, It.IsAny<SSCPolicy?>()))
            .Returns(new SSCResult(0, 0, 0, 0, 0, 0, 0, "TEST"));
        var tax = new Mock<ITaxCalculationEngine>();
        tax.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), 0, 0, It.IsAny<TaxPolicy?>()))
            .Returns(new TaxResult(0, 0, 0, 0, 0, 0, "TEST"));
        var loans = new Mock<ILoanDeductionService>();
        loans.Setup(l => l.CalculateAndApplyDeductionsAsync(null, It.IsAny<string>(), Period.PeriodCode,
            It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0, 0, 0, new(), new()));
        var adjustments = new Mock<IPayrollAdjustmentRepository>();
        adjustments.Setup(a => a.GetPayrollCalculationAdjustmentsAsync(1, Period.PeriodCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PayrollAdjustment>());
        return new PayrollCalculationEngine(policyRepository ?? Mock.Of<IPolicyRepository>(),
            new PayrollProrationService(Mock.Of<IPolicyRepository>(), Mock.Of<IWorkCalendarService>()),
            Mock.Of<IOvertimeCalculationService>(), ssc.Object, tax.Object, loans.Object,
            adjustments.Object, Mock.Of<ILeaveService>());
    }

    [Theory]
    [InlineData(1, 2.5, 3)]
    [InlineData(1, -2.5, -3)]
    [InlineData(2, 2.5, 2)]
    [InlineData(2, -2.5, -2)]
    [InlineData(3, 2.01, 3)]
    [InlineData(3, -2.01, -3)]
    [InlineData(4, 2.99, 2)]
    [InlineData(4, -2.99, -2)]
    [InlineData(5, 2.01, 3)]
    [InlineData(5, -2.01, -2)]
    [InlineData(6, 2.01, 2)]
    [InlineData(6, -2.01, -3)]
    public void BranchRoundingRulesApplyTheirDocumentedDecimalSemantics(int rule, decimal value, decimal expected)
    {
        Assert.Equal(expected, PayrollCurrencyRules.Round(value, 0, rule));
    }

    [Theory]
    [InlineData(1, 100.01)]
    [InlineData(2, 100.00)]
    public async Task PayrollRunUsesBranchRoundingRuleForCurrencyPrecision(int roundingRule, decimal expectedBasic)
    {
        var policyRepository = new Mock<IPolicyRepository>();
        policyRepository.Setup(r => r.GetBranchRoundingRulesAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roundingRule);
        var engine = Engine(policyRepository.Object);
        var employee = Employee("E1", "USD");
        employee.BranchId = 7;
        employee.SalaryStructures.Single().BasicSalary = 100.005m;

        var run = await engine.CalculatePayrollForPeriodAsync(Period, new[] { employee });

        Assert.Equal(expectedBasic, Assert.Single(run.Lines).GrossSalary);
        policyRepository.Verify(r => r.GetBranchRoundingRulesAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, "100")]
    [InlineData(4, "100.005")]
    public async Task PayrollRunUsesCompanyConfiguredCurrencyDecimalPlaces(int decimalPlaces, string expectedBasic)
    {
        var policyRepository = new Mock<IPolicyRepository>();
        policyRepository.Setup(r => r.GetConfiguredPayrollCurrencyDecimalsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>(StringComparer.Ordinal) { ["USD"] = decimalPlaces });
        var employee = Employee("E-CURRENCY", "usd");
        employee.SalaryStructures.Single().BasicSalary = 100.005m;

        var run = await Engine(policyRepository.Object).CalculatePayrollForPeriodAsync(Period, new[] { employee });

        Assert.Equal(decimal.Parse(expectedBasic, System.Globalization.CultureInfo.InvariantCulture), Assert.Single(run.Lines).GrossSalary);
        policyRepository.Verify(r => r.GetConfiguredPayrollCurrencyDecimalsAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task CalculationRejectsMixedOrMissingCurrencyBeforeSaving(string? otherCurrency)
    {
        var repository = new Mock<IPayrollPeriodRepository>();
        repository.Setup(r => r.GetPeriodByCodeAsync(1, Period.PeriodCode, It.IsAny<CancellationToken>())).ReturnsAsync(Period);
        repository.Setup(r => r.GetActiveEmployeesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Employee("E1", "USD"), Employee("E2", otherCurrency) });
        var validation = new Mock<IPayrollValidationService>();
        validation.Setup(v => v.ValidatePreCalculationAsync(1, Period.PeriodCode, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThinkOnErp.Application.DTOs.Hr.PayrollValidationResultDto(true, new(), new()));
        var service = new PayrollService(repository.Object, Engine(), validation.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAndCalculatePayrollRunAsync(
            new ThinkOnErp.Application.DTOs.Hr.CreatePayrollRunDto(1, Period.PeriodCode, 1), "HR"));
        repository.Verify(r => r.AddPayrollRunAsync(It.IsAny<PayrollRun>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SingleCurrencyTotalsNormalizeComparisonWithoutChangingSalaryData()
    {
        var employees = new[] { Employee("E1", "USD"), Employee("E2", " usd ") };
        var run = await Engine().CalculatePayrollForPeriodAsync(Period, employees);
        Assert.Equal(200m, run.TotalGrossSalary);
        Assert.Equal(200m, run.TotalNetSalary);
        Assert.Equal(" usd ", employees[1].SalaryStructures[0].CurrencyCode);
        Assert.Equal(2, run.Lines.Count);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData(null)]
    [InlineData("")]
    public async Task ExistingRunCannotBeApprovedOrSettleLoansWithMixedOrMissingCurrency(string? otherCurrency)
    {
        var run = new PayrollRun { Id = 1, PayPeriod = Period.PeriodCode, Status = "CALCULATED" };
        run.Lines.Add(new PayrollRunLine { EmployeeCode = "E1", CurrencyCode = "USD", NetPay = 100m });
        run.Lines.Add(new PayrollRunLine { EmployeeCode = "E2", CurrencyCode = otherCurrency, NetPay = 100m });
        var repository = new Mock<IPayrollPeriodRepository>();
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(Period);
        var validation = new PayrollValidationService(repository.Object, Mock.Of<IPolicyRepository>());
        var loans = new Mock<ILoanDeductionService>(MockBehavior.Strict);
        var service = new PayrollService(repository.Object, Engine(), validation);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApprovePayrollRunAsync(1, "HR"));
        Assert.Contains("currency", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("CALCULATED", run.Status);
        Assert.Null(run.ApprovalDate);
        loans.VerifyNoOtherCalls();
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExistingSingleCurrencyRunPassesCurrencyApprovalValidation()
    {
        var run = new PayrollRun { Id = 1, Status = "CALCULATED" };
        run.Lines.Add(new PayrollRunLine { CurrencyCode = "USD" });
        run.Lines.Add(new PayrollRunLine { CurrencyCode = " usd " });
        var repository = new Mock<IPayrollPeriodRepository>();
        repository.Setup(r => r.GetPayrollRunByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        repository.Setup(r => r.GetPeriodForRunAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(Period);
        var result = await new PayrollValidationService(repository.Object, Mock.Of<IPolicyRepository>()).ValidatePostApprovalAsync(1);
        Assert.True(result.IsValid, string.Join(",", result.Errors));
    }

    [Theory]
    [InlineData("JOD", 3)]
    [InlineData("KWD", 3)]
    [InlineData("BHD", 3)]
    [InlineData("OMR", 3)]
    [InlineData("TND", 3)]
    [InlineData("IQD", 3)]
    [InlineData("JPY", 0)]
    [InlineData("KRW", 0)]
    [InlineData("VND", 0)]
    [InlineData("SAR", 2)]
    [InlineData("AED", 2)]
    [InlineData("USD", 2)]
    [InlineData("EUR", 2)]
    [InlineData("EGP", 2)]
    [InlineData("  jod  ", 3)]
    [InlineData("  jpy  ", 0)]
    [InlineData(null, 2)]
    [InlineData("", 2)]
    public void GetCurrencyDecimals_ReturnsExpectedDecimals(string? currency, int expectedDecimals)
    {
        var decimals = PayrollCurrencyRules.GetCurrencyDecimals(currency);
        Assert.Equal(expectedDecimals, decimals);
    }

    [Fact]
    public void RequireSingleCurrency_WhenEmptySequence_ReturnsNull()
    {
        var result = PayrollCurrencyRules.RequireSingleCurrency(Array.Empty<string?>());
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RequireSingleCurrency_WhenCurrencyMissing_ThrowsInvalidOperationException(string? missing)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PayrollCurrencyRules.RequireSingleCurrency(new[] { "USD", missing }));
        Assert.Contains("Payroll currency is missing", ex.Message);
    }

    [Fact]
    public void RequireSingleCurrency_WhenDifferentCurrencies_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PayrollCurrencyRules.RequireSingleCurrency(new[] { "USD", "EUR" }));
        Assert.Contains("Payroll cannot combine different currencies", ex.Message);
    }

    [Fact]
    public void RequireSingleCurrency_WhenAllMatchWithCasingAndWhitespace_ReturnsNormalized()
    {
        var result = PayrollCurrencyRules.RequireSingleCurrency(new[] { " usd ", "USD", "UsD" });
        Assert.Equal("USD", result);
    }

    [Fact]
    public void RequireSingleCurrency_SingleElement_ReturnsNormalizedUppercase()
    {
        var result = PayrollCurrencyRules.RequireSingleCurrency(new[] { "  sar  " });
        Assert.Equal("SAR", result);
    }

    [Fact]
    public void RequireSingleCurrency_MultipleMatchingCurrencies_ReturnsNormalized()
    {
        var result = PayrollCurrencyRules.RequireSingleCurrency(new[] { "JOD", "jod", " JOD " });
        Assert.Equal("JOD", result);
    }

    [Theory]
    [InlineData("  kwd  ", 3)]
    [InlineData("bhd", 3)]
    [InlineData("  omr ", 3)]
    [InlineData("tnd", 3)]
    [InlineData("iqd", 3)]
    [InlineData("krw", 0)]
    [InlineData("vnd", 0)]
    [InlineData("aed", 2)]
    [InlineData("qar", 2)]
    public void GetCurrencyDecimals_HandlesExtendedCodesWithWhitespace(string currency, int expected)
    {
        Assert.Equal(expected, PayrollCurrencyRules.GetCurrencyDecimals(currency));
    }

    [Fact]
    public void BuildSegments_ThrowsInvalidOperationException_WhenSegmentsHaveDifferentCurrencies()
    {
        var employee = new Employee { EmployeeCode = "E_DIFF", HireDate = new DateTime(2024, 1, 1) };
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = "E_DIFF",
            EffectiveFrom = new DateTime(2026, 11, 1),
            EffectiveTo = new DateTime(2026, 11, 15),
            BasicSalary = 500m,
            CurrencyCode = "USD",
            IsActive = true
        });
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = "E_DIFF",
            EffectiveFrom = new DateTime(2026, 11, 16),
            EffectiveTo = new DateTime(2026, 11, 30),
            BasicSalary = 600m,
            CurrencyCode = "EUR",
            IsActive = true
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PayrollSalarySegments.Build(employee, Period));
        Assert.Contains("Payroll cannot combine different currencies", ex.Message);
    }

    [Fact]
    public void BuildSegments_ThrowsInvalidOperationException_WhenSegmentCurrencyIsMissing()
    {
        var employee = new Employee { EmployeeCode = "E_MISSING", HireDate = new DateTime(2024, 1, 1) };
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = "E_MISSING",
            EffectiveFrom = new DateTime(2026, 11, 1),
            EffectiveTo = new DateTime(2026, 11, 30),
            BasicSalary = 500m,
            CurrencyCode = "   ",
            IsActive = true
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PayrollSalarySegments.Build(employee, Period));
        Assert.Contains("Payroll currency is missing", ex.Message);
    }

    [Fact]
    public void RequireSingleCurrency_EmptySequence_ReturnsNull()
    {
        var result = PayrollCurrencyRules.RequireSingleCurrency(Array.Empty<string>());
        Assert.Null(result);
    }

    [Theory]
    [InlineData("jpy", 0)]
    [InlineData("JPY", 0)]
    [InlineData(" JPY ", 0)]
    public void GetCurrencyDecimals_ZeroDecimalCurrencies_JPY(string currency, int expected)
    {
        Assert.Equal(expected, PayrollCurrencyRules.GetCurrencyDecimals(currency));
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("CHF")]
    [InlineData("CAD")]
    [InlineData("AUD")]
    [InlineData("TRY")]
    [InlineData("EGP")]
    public void GetCurrencyDecimals_DefaultTwoDecimals_ForStandardCurrencies(string currency)
    {
        Assert.Equal(2, PayrollCurrencyRules.GetCurrencyDecimals(currency));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCurrencyDecimals_NullOrWhitespace_ReturnsTwo(string? currency)
    {
        Assert.Equal(2, PayrollCurrencyRules.GetCurrencyDecimals(currency));
    }

    [Fact]
    public void RequireSingleCurrency_MultipleSegmentsWithSameCurrency_DifferentCasing_ReturnsNormalized()
    {
        var result = PayrollCurrencyRules.RequireSingleCurrency(new[] { "jod", "JOD", " Jod " });
        Assert.Equal("JOD", result);
    }

    [Fact]
    public void RequireSingleCurrency_WhenMultipleWhitespacesAround_ReturnsCleanNormalizedCode()
    {
        var result = PayrollCurrencyRules.RequireSingleCurrency(new[] { "\t\n usd \r" });
        Assert.Equal("USD", result);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("\t\r\n", "")]
    [InlineData("  usd  ", "USD")]
    [InlineData("jOd", "JOD")]
    [InlineData(" krw ", "KRW")]
    public void SalaryPaymentMetadata_Normalize_HandlesNullEmptyAndWhitespace(string? input, string expected)
    {
        var normalized = SalaryPaymentMetadata.Normalize(input);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("USD", "BANK_TRANSFER")]
    [InlineData("SAR", "CASH")]
    [InlineData("EUR", "CHEQUE")]
    [InlineData("JOD", "BANK_TRANSFER")]
    [InlineData("JPY", "CASH")]
    public void SalaryPaymentMetadata_Validate_ValidPaymentMethodsAndCurrency_Succeeds(string currency, string method)
    {
        // Should not throw
        SalaryPaymentMetadata.Validate(currency, method);
    }

    [Theory]
    [InlineData("USD", "BITCOIN")]
    [InlineData("USD", "CREDIT_CARD")]
    [InlineData("USD", "PAYPAL")]
    [InlineData("USD", "")]
    [InlineData("USD", "   ")]
    [InlineData("USD", "bank_transfer")] // Case sensitive check
    public void SalaryPaymentMetadata_Validate_InvalidPaymentMethod_ThrowsArgumentException(string currency, string method)
    {
        var ex = Assert.Throws<ArgumentException>(() => SalaryPaymentMetadata.Validate(currency, method));
        Assert.Contains("PaymentMethod must be BANK_TRANSFER, CASH or CHEQUE", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SalaryPaymentMetadata_Validate_EmptyOrWhitespaceCurrency_ThrowsArgumentException(string currency)
    {
        var ex = Assert.Throws<ArgumentException>(() => SalaryPaymentMetadata.Validate(currency, "CASH"));
        Assert.Contains("configured currency code of at most 20 characters is required", ex.Message);
    }

    [Fact]
    public void SalaryPaymentMetadata_Validate_CurrencyExceeding20Chars_ThrowsArgumentException()
    {
        var longCurrency = new string('X', 21);
        var ex = Assert.Throws<ArgumentException>(() => SalaryPaymentMetadata.Validate(longCurrency, "CASH"));
        Assert.Contains("at most 20 characters is required", ex.Message);
    }

    [Theory]
    [InlineData("USD\0")]
    [InlineData("US\tD")]
    [InlineData("US\nD")]
    [InlineData("US\rD")]
    public void SalaryPaymentMetadata_Validate_CurrencyWithControlChars_ThrowsArgumentException(string currency)
    {
        var ex = Assert.Throws<ArgumentException>(() => SalaryPaymentMetadata.Validate(currency, "CASH"));
        Assert.Contains("configured currency code of at most 20 characters is required", ex.Message);
    }

    [Fact]
    public void RequireSingleCurrency_WhenOneThousandMatchingCurrencies_ReturnsNormalizedCode()
    {
        var list = Enumerable.Repeat("usd", 1000).ToList();
        var result = PayrollCurrencyRules.RequireSingleCurrency(list);
        Assert.Equal("USD", result);
    }

    [Fact]
    public void RequireSingleCurrency_WhenLastCurrencyDiffers_ThrowsInvalidOperationException()
    {
        var list = Enumerable.Repeat("USD", 50).ToList();
        list.Add("EUR");
        var ex = Assert.Throws<InvalidOperationException>(() => PayrollCurrencyRules.RequireSingleCurrency(list));
        Assert.Contains("Payroll cannot combine different currencies", ex.Message);
    }

    [Fact]
    public void RequireSingleCurrency_WhenLastCurrencyWhitespace_ThrowsInvalidOperationException()
    {
        var list = Enumerable.Repeat("USD", 50).ToList();
        list.Add("   ");
        var ex = Assert.Throws<InvalidOperationException>(() => PayrollCurrencyRules.RequireSingleCurrency(list));
        Assert.Contains("Payroll currency is missing", ex.Message);
    }

    [Fact]
    public void BuildSegments_WhenMultipleStructuresHaveSameCurrencyDifferentCasing_Succeeds()
    {
        var employee = new Employee { EmployeeCode = "E_SAME_NORM", HireDate = new DateTime(2024, 1, 1) };
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = "E_SAME_NORM",
            EffectiveFrom = new DateTime(2026, 11, 1),
            EffectiveTo = new DateTime(2026, 11, 15),
            BasicSalary = 500m,
            CurrencyCode = "usd",
            IsActive = true
        });
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = "E_SAME_NORM",
            EffectiveFrom = new DateTime(2026, 11, 16),
            EffectiveTo = new DateTime(2026, 11, 30),
            BasicSalary = 600m,
            CurrencyCode = "USD",
            IsActive = true
        });

        var segments = PayrollSalarySegments.Build(employee, Period);
        Assert.Equal(2, segments.Count);
        Assert.Equal("usd", segments[0].Structure.CurrencyCode);
        Assert.Equal("USD", segments[1].Structure.CurrencyCode);
        Assert.Equal("USD", PayrollCurrencyRules.RequireSingleCurrency(segments.Select(s => s.Structure.CurrencyCode)));
    }

    [Fact]
    public void BuildSegments_WhenSingleStructureWithJOD_ReturnsThreeDecimalCurrency()
    {
        var employee = new Employee { EmployeeCode = "E_JOD", HireDate = new DateTime(2024, 1, 1) };
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = "E_JOD",
            EffectiveFrom = new DateTime(2026, 11, 1),
            EffectiveTo = new DateTime(2026, 11, 30),
            BasicSalary = 750m,
            CurrencyCode = "  jod  ",
            IsActive = true
        });

        var segments = PayrollSalarySegments.Build(employee, Period);
        Assert.Single(segments);
        Assert.Equal("  jod  ", segments[0].Structure.CurrencyCode);
        Assert.Equal(3, PayrollCurrencyRules.GetCurrencyDecimals(segments[0].Structure.CurrencyCode));
    }

    [Fact]
    public void BuildSegments_WhenSingleStructureWithJPY_ReturnsZeroDecimalCurrency()
    {
        var employee = new Employee { EmployeeCode = "E_JPY", HireDate = new DateTime(2024, 1, 1) };
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = "E_JPY",
            EffectiveFrom = new DateTime(2026, 11, 1),
            EffectiveTo = new DateTime(2026, 11, 30),
            BasicSalary = 300000m,
            CurrencyCode = "JPY",
            IsActive = true
        });

        var segments = PayrollSalarySegments.Build(employee, Period);
        Assert.Single(segments);
        Assert.Equal("JPY", segments[0].Structure.CurrencyCode);
        Assert.Equal(0, PayrollCurrencyRules.GetCurrencyDecimals(segments[0].Structure.CurrencyCode));
    }
}
