using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class EmployeeImportPayrollLockTests
{
    [Fact]
    public async Task ProtectedPayrollRejectsWholeImportBeforeEmployeeMutation()
    {
        var reader = new Mock<IXlsxEmployeeWorkbookReader>();
        var employees = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        IReadOnlyList<EmployeeImportRowDto> rows = new[] { new EmployeeImportRowDto { EmployeeCode = "E1", NationalId = "N1", BasicSalary = 700m } };
        reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((rows, new EmployeeImportResultDto { TotalRows = 1 }));
        unitOfWork.Setup(u => u.ExecuteEmployeeImportAsync(rows, It.IsAny<Func<Task<EmployeeImportResultDto>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Approved payroll exists."));
        var service = new EmployeeExcelService(employees.Object, reader.Object, Mock.Of<IXlsxEmployeeWorkbookWriter>(),
            Mock.Of<ILogger<EmployeeExcelService>>(), unitOfWork.Object);
        using var workbook = new MemoryStream();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportEmployeesAsync(workbook, true));
        employees.Verify(r => r.GetEmployeeByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        employees.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DuplicateRowsAreRejectedBeforeStartingPayrollTransaction()
    {
        var reader = new Mock<IXlsxEmployeeWorkbookReader>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        IReadOnlyList<EmployeeImportRowDto> rows = new[]
        {
            new EmployeeImportRowDto { RowNumber = 2, EmployeeCode = "E1", NationalId = "N1" },
            new EmployeeImportRowDto { RowNumber = 3, EmployeeCode = "E1", NationalId = "N2" }
        };
        reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((rows, new EmployeeImportResultDto { TotalRows = 2 }));
        var service = new EmployeeExcelService(Mock.Of<IEmployeeRepository>(), reader.Object, Mock.Of<IXlsxEmployeeWorkbookWriter>(),
            Mock.Of<ILogger<EmployeeExcelService>>(), unitOfWork.Object);
        using var workbook = new MemoryStream();
        var result = await service.ImportEmployeesAsync(workbook, true);
        Assert.Contains(result.Errors, e => e.Code == "DUPLICATE_IN_FILE");
        unitOfWork.Verify(u => u.ExecuteEmployeeImportAsync(It.IsAny<IReadOnlyList<EmployeeImportRowDto>>(),
            It.IsAny<Func<Task<EmployeeImportResultDto>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportEmployeesAsync_WhenUnitOfWorkIsNull_ThrowsInvalidOperationException()
    {
        var reader = new Mock<IXlsxEmployeeWorkbookReader>();
        IReadOnlyList<EmployeeImportRowDto> rows = new[]
        {
            new EmployeeImportRowDto { RowNumber = 2, EmployeeCode = "E1", NationalId = "N1" }
        };
        reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((rows, new EmployeeImportResultDto { TotalRows = 1 }));

        var service = new EmployeeExcelService(Mock.Of<IEmployeeRepository>(), reader.Object, Mock.Of<IXlsxEmployeeWorkbookWriter>(),
            Mock.Of<ILogger<EmployeeExcelService>>(), unitOfWork: null);

        using var workbook = new MemoryStream();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportEmployeesAsync(workbook, true));
        Assert.Contains("requires its payroll-protected unit of work", ex.Message);
    }

    [Fact]
    public async Task ImportEmployeesAsync_WhenReaderReturnsErrors_StopsBeforeUnitOfWork()
    {
        var reader = new Mock<IXlsxEmployeeWorkbookReader>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var errorResult = new EmployeeImportResultDto { TotalRows = 1 };
        errorResult.Errors.Add(new EmployeeImportErrorDto(2, "DateOfBirth", "INVALID_DATE", "Date is unparseable"));

        reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<EmployeeImportRowDto>(), errorResult));

        var service = new EmployeeExcelService(Mock.Of<IEmployeeRepository>(), reader.Object, Mock.Of<IXlsxEmployeeWorkbookWriter>(),
            Mock.Of<ILogger<EmployeeExcelService>>(), unitOfWork.Object);

        using var workbook = new MemoryStream();
        var result = await service.ImportEmployeesAsync(workbook, true);

        Assert.Equal(1, result.FailureCount);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_DATE");
        unitOfWork.Verify(u => u.ExecuteEmployeeImportAsync(It.IsAny<IReadOnlyList<EmployeeImportRowDto>>(),
            It.IsAny<Func<Task<EmployeeImportResultDto>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportEmployeesAsync_WhenWorkbookEmpty_ReturnsZeroCountWithoutCallingUnitOfWork()
    {
        var reader = new Mock<IXlsxEmployeeWorkbookReader>();
        var unitOfWork = new Mock<IHrUnitOfWork>();

        reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<EmployeeImportRowDto>(), new EmployeeImportResultDto { TotalRows = 0 }));

        var service = new EmployeeExcelService(Mock.Of<IEmployeeRepository>(), reader.Object, Mock.Of<IXlsxEmployeeWorkbookWriter>(),
            Mock.Of<ILogger<EmployeeExcelService>>(), unitOfWork.Object);

        using var workbook = new MemoryStream();
        var result = await service.ImportEmployeesAsync(workbook, true);

        Assert.Equal(0, result.TotalRows);
        Assert.Empty(result.Errors);
        unitOfWork.Verify(u => u.ExecuteEmployeeImportAsync(It.IsAny<IReadOnlyList<EmployeeImportRowDto>>(),
            It.IsAny<Func<Task<EmployeeImportResultDto>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportEmployeesAsync_DuplicateNationalIdInWorkbook_StopsBeforeUnitOfWork()
    {
        var reader = new Mock<IXlsxEmployeeWorkbookReader>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        IReadOnlyList<EmployeeImportRowDto> rows = new[]
        {
            new EmployeeImportRowDto { RowNumber = 2, EmployeeCode = "E1", NationalId = "999999999" },
            new EmployeeImportRowDto { RowNumber = 3, EmployeeCode = "E2", NationalId = "999999999" }
        };
        reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((rows, new EmployeeImportResultDto { TotalRows = 2 }));

        var service = new EmployeeExcelService(Mock.Of<IEmployeeRepository>(), reader.Object, Mock.Of<IXlsxEmployeeWorkbookWriter>(),
            Mock.Of<ILogger<EmployeeExcelService>>(), unitOfWork.Object);

        using var workbook = new MemoryStream();
        var result = await service.ImportEmployeesAsync(workbook, true);

        Assert.Contains(result.Errors, e => e.Column == "NationalId" && e.Code == "DUPLICATE_IN_FILE");
        unitOfWork.Verify(u => u.ExecuteEmployeeImportAsync(It.IsAny<IReadOnlyList<EmployeeImportRowDto>>(),
            It.IsAny<Func<Task<EmployeeImportResultDto>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateWorkbookAsync_EmptyWorkbook_AddsWarning()
    {
        var reader = new Mock<IXlsxEmployeeWorkbookReader>();
        reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<EmployeeImportRowDto>(), new EmployeeImportResultDto { TotalRows = 0 }));

        var service = new EmployeeExcelService(Mock.Of<IEmployeeRepository>(), reader.Object, Mock.Of<IXlsxEmployeeWorkbookWriter>(),
            Mock.Of<ILogger<EmployeeExcelService>>());

        using var workbook = new MemoryStream();
        var result = await service.ValidateWorkbookAsync(workbook);

        Assert.NotEmpty(result.Warnings);
        Assert.Contains("contains no employee data rows", result.Warnings[0]);
    }

    [Fact]
    public async Task ValidateWorkbookAsync_DuplicateEmployeeCodeInWorkbook_AddsDuplicateInFileError()
    {
        var reader = new Mock<IXlsxEmployeeWorkbookReader>();
        IReadOnlyList<EmployeeImportRowDto> rows = new[]
        {
            new EmployeeImportRowDto { RowNumber = 2, EmployeeCode = "EMP_DUP", NationalId = "111" },
            new EmployeeImportRowDto { RowNumber = 3, EmployeeCode = "EMP_DUP", NationalId = "222" }
        };
        reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((rows, new EmployeeImportResultDto { TotalRows = 2 }));

        var service = new EmployeeExcelService(Mock.Of<IEmployeeRepository>(), reader.Object, Mock.Of<IXlsxEmployeeWorkbookWriter>(),
            Mock.Of<ILogger<EmployeeExcelService>>());

        using var workbook = new MemoryStream();
        var result = await service.ValidateWorkbookAsync(workbook);

        Assert.Contains(result.Errors, e => e.Column == "EmployeeCode" && e.Code == "DUPLICATE_IN_FILE");
    }

    [Fact]
    public async Task ValidateWorkbookAsync_ReaderReturnsErrors_ReturnsFailureCount()
    {
        var reader = new Mock<IXlsxEmployeeWorkbookReader>();
        var errorResult = new EmployeeImportResultDto { TotalRows = 2 };
        errorResult.Errors.Add(new EmployeeImportErrorDto(2, "BasicSalary", "INVALID_FORMAT", "Not a number"));
        errorResult.Errors.Add(new EmployeeImportErrorDto(3, "HireDate", "INVALID_FORMAT", "Not a date"));

        reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<EmployeeImportRowDto>(), errorResult));

        var service = new EmployeeExcelService(Mock.Of<IEmployeeRepository>(), reader.Object, Mock.Of<IXlsxEmployeeWorkbookWriter>(),
            Mock.Of<ILogger<EmployeeExcelService>>());

        using var workbook = new MemoryStream();
        var result = await service.ValidateWorkbookAsync(workbook);

        Assert.Equal(2, result.FailureCount);
        Assert.Equal(2, result.Errors.Count);
    }
}
