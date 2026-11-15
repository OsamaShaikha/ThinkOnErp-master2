using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class SalaryMetadataStorageTests : IDisposable
{
    private readonly OracleDbContext db = new(new DbContextOptionsBuilder<OracleDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public SalaryMetadataStorageTests()
    {
        db.Add(new SysCurrency { Id = 1, ShortNameEn = " usd " });
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    [Theory]
    [InlineData("CASH")]
    [InlineData("BANK_TRANSFER")]
    [InlineData("CHEQUE")]
    public async Task SupportedMethodsUseExactlyOneConfiguredCodeWithoutPersistence(string method)
    {
        await new EmployeeRepository(db).ValidateSalaryMetadataAsync("USD", method);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task UnknownOrAmbiguousCurrencyCannotBeChosen()
    {
        var repository = new EmployeeRepository(db);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.ValidateSalaryMetadataAsync("EUR", "CASH"));
        db.Add(new SysCurrency { Id = 2, ShortNameEn = "USD" });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => repository.ValidateSalaryMetadataAsync("USD", "CASH"));
    }

    [Fact]
    public async Task ConfiguredCodesAreNotRestrictedToACountryOrThreeLetterFormat()
    {
        db.Add(new SysCurrency { Id = 2, ShortNameEn = "CLIENT_UNIT" });
        await db.SaveChangesAsync();
        await new EmployeeRepository(db).ValidateSalaryMetadataAsync("CLIENT_UNIT", "CASH");
        await Assert.ThrowsAsync<ArgumentException>(() => new EmployeeRepository(db).ValidateSalaryMetadataAsync("CLIENT_UNIT", "CARD"));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    [InlineData("usd", "USD")]
    [InlineData("  Jod  ", "JOD")]
    [InlineData("Client_Unit_1", "CLIENT_UNIT_1")]
    public void SalaryPaymentMetadata_Normalize_ReturnsTrimmedUpperCase(string? input, string expected)
    {
        var result = SalaryPaymentMetadata.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SalaryPaymentMetadata_Validate_NullOrWhitespaceCurrency_ThrowsArgumentException(string? currency)
    {
        var ex = Assert.Throws<ArgumentException>(() => SalaryPaymentMetadata.Validate(currency!, "CASH"));
        Assert.Contains("configured currency code", ex.Message);
    }

    [Fact]
    public void SalaryPaymentMetadata_Validate_CurrencyTooLong_ThrowsArgumentException()
    {
        var longCurrency = new string('A', 21);
        var ex = Assert.Throws<ArgumentException>(() => SalaryPaymentMetadata.Validate(longCurrency, "CASH"));
        Assert.Contains("at most 20 characters", ex.Message);
    }

    [Theory]
    [InlineData("USD\0")]
    [InlineData("USD\t")]
    [InlineData("USD\n")]
    public void SalaryPaymentMetadata_Validate_ControlCharactersInCurrency_ThrowsArgumentException(string currency)
    {
        var ex = Assert.Throws<ArgumentException>(() => SalaryPaymentMetadata.Validate(currency, "CASH"));
        Assert.Contains("configured currency code", ex.Message);
    }

    [Theory]
    [InlineData("CRYPTO")]
    [InlineData("WIRE")]
    [InlineData("PAYPAL")]
    [InlineData("")]
    [InlineData(null)]
    public void SalaryPaymentMetadata_Validate_InvalidPaymentMethod_ThrowsArgumentException(string? method)
    {
        var ex = Assert.Throws<ArgumentException>(() => SalaryPaymentMetadata.Validate("USD", method!));
        Assert.Contains("PaymentMethod must be BANK_TRANSFER, CASH or CHEQUE", ex.Message);
    }

    [Fact]
    public async Task ValidateSalaryMetadataAsync_CaseAndWhitespaceInsensitiveMatch_Succeeds()
    {
        var repository = new EmployeeRepository(db);
        // db contains SysCurrency with ShortNameEn = " usd "
        // passing "USD" matches c.ShortNameEn.Trim().ToUpper() == "USD"
        await repository.ValidateSalaryMetadataAsync("USD", "BANK_TRANSFER");
        Assert.False(db.ChangeTracker.HasChanges());
    }

    public void Dispose() => db.Dispose();
}
