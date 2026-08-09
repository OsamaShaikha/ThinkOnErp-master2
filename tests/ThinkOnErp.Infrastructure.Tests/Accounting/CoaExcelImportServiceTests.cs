using Moq;
using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Accounting;

public sealed class CoaExcelImportServiceTests
{
    [Fact]
    public async Task ValidateAsync_WhenAccountCountIsNot315_ReturnsCountError()
    {
        var result = await ValidateAsync(new[] { ValidRow("1", null, 1, "HEADER") });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "COA_ACCOUNT_COUNT");
    }

    [Fact]
    public async Task ValidateAsync_WhenParentDoesNotExist_ReturnsParentError()
    {
        var rows = BuildValid315RowChart();
        rows.Single(row => row.AccountCode == "100000").ParentCode = "1999";

        var result = await ValidateAsync(rows);

        Assert.False(result.IsValid);
        Assert.DoesNotContain(result.Errors, error => error.Code == "COA_ACCOUNT_COUNT");
        Assert.Contains(result.Errors, error =>
            error.Code == "COA_PARENT_NOT_FOUND" && error.RowNumber == 6);
    }

    [Fact]
    public async Task ValidateAsync_WhenReaderReportsBadHeader_PropagatesHeaderError()
    {
        var readResult = new CoaWorkbookReadResultDto
        {
            Errors = new List<CoaImportErrorDto>
            {
                new(1, "account_code", "MISSING_HEADER", "Required header is missing.")
            }
        };

        var result = await ValidateAsync(readResult);

        var error = Assert.Single(result.Errors);
        Assert.Equal("MISSING_HEADER", error.Code);
        Assert.Equal("account_code", error.Column);
        Assert.Equal(0, result.TotalRows);
    }

    private static async Task<CoaImportResultDto> ValidateAsync(
        IReadOnlyCollection<CoaImportRowDto> rows) =>
        await ValidateAsync(new CoaWorkbookReadResultDto { Rows = rows.ToList() });

    private static async Task<CoaImportResultDto> ValidateAsync(
        CoaWorkbookReadResultDto readResult)
    {
        var reader = new Mock<ICoaWorkbookReader>(MockBehavior.Strict);
        reader
            .Setup(item => item.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(readResult);

        var repository = new Mock<IGlAccountRepository>(MockBehavior.Strict);
        var tenantContext = new Mock<ICurrentTenantContext>(MockBehavior.Strict);
        var service = new CoaExcelImportService(
            reader.Object,
            repository.Object,
            tenantContext.Object);

        await using var stream = new MemoryStream(new byte[] { 1 });
        var result = await service.ValidateAsync(stream);

        reader.VerifyAll();
        repository.VerifyNoOtherCalls();
        tenantContext.VerifyNoOtherCalls();
        return result;
    }

    private static List<CoaImportRowDto> BuildValid315RowChart()
    {
        var rows = new List<CoaImportRowDto>
        {
            ValidRow("1", null, 1, "HEADER"),
            ValidRow("10", "1", 2, "HEADER"),
            ValidRow("100", "10", 3, "HEADER")
        };

        for (var header = 0; header < 4; header++)
        {
            var headerCode = $"100{header}";
            rows.Add(ValidRow(headerCode, "100", 4, "HEADER"));

            for (var leaf = 0; leaf < 77; leaf++)
            {
                rows.Add(ValidRow($"{headerCode}{leaf:00}", headerCode, 5, "DETAIL"));
            }
        }

        Assert.Equal(315, rows.Count);
        for (var index = 0; index < rows.Count; index++)
        {
            rows[index].RowNumber = index + 2;
        }

        return rows;
    }

    private static CoaImportRowDto ValidRow(
        string code,
        string? parentCode,
        int level,
        string accountType) => new()
        {
            RowNumber = 2,
            AccountCode = code,
            ParentCode = parentCode,
            AccountNameAr = $"حساب {code}",
            AccountNameEn = $"Account {code}",
            AccountLevel = level,
            AccountType = accountType,
            CategoryCode = "ASSETS",
            NormalBalance = "D",
            FinancialStatement = "BALANCE_SHEET"
        };
}
