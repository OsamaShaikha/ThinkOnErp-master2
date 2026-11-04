using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.SuperAdmin.Tests.Compliance;

public sealed class ComplianceReportExporterTests
{
    private readonly ComplianceReportExporter _exporter = new(NullLogger<ComplianceReportExporter>.Instance);

    public static IEnumerable<object[]> Reports()
    {
        yield return new object[] { new GdprAccessReport { DataSubjectName = "Alice", DataSubjectId = 42 }, "dataSubjectId" };
        yield return new object[] { new GdprDataExportReport { DataSubjectName = "Alice", DataSubjectId = 42 }, "personalDataByEntityType" };
        yield return new object[] { new SoxFinancialAccessReport(), "accessEvents" };
        yield return new object[] { new SoxSegregationOfDutiesReport(), "violations" };
        yield return new object[] { new Iso27001SecurityReport(), "securityEvents" };
        yield return new object[] { new UserActivityReport { UserName = "Alice" }, "userId" };
        yield return new object[] { new DataModificationReport { EntityType = "Invoice" }, "entityType" };
    }

    [Theory]
    [MemberData(nameof(Reports))]
    public async Task Json_IncludesConcreteReportFields(IReport report, string specificField)
    {
        using var json = JsonDocument.Parse(await _exporter.ExportToJsonAsync(report));

        Assert.Equal(report.ReportId, json.RootElement.GetProperty("reportId").GetString());
        Assert.True(json.RootElement.TryGetProperty(specificField, out _));
    }

    [Theory]
    [MemberData(nameof(Reports))]
    public async Task Csv_PreservesReportMetadataAndUtf8Preamble(IReport report, string _)
    {
        var bytes = await _exporter.ExportToCsvAsync(report);

        Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Contains(report.ReportId, Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Csv_EscapesCommasQuotesAndNewlinesInData()
    {
        var report = new GdprAccessReport
        {
            AccessEvents = new List<DataAccessEvent>
            {
                new() { ActorName = "Alice, \"A\"\nB", EntityType = "Invoice", Action = "READ" }
            }
        };

        var csv = Encoding.UTF8.GetString(await _exporter.ExportToCsvAsync(report));

        Assert.Contains("\"Alice, \"\"A\"\"\nB\"", csv);
    }

    [Fact]
    public async Task CancelledExports_DoNotRender()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var report = new GdprAccessReport();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _exporter.ExportToCsvAsync(report, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _exporter.ExportToJsonAsync(report, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _exporter.ExportToPdfAsync(report, cancellation.Token));
    }
}
