using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Pos;

public sealed class PosReportExportFilterDto : PosSalesPdfFilterDto
{
    [Range(1, long.MaxValue)] public long? FloorId { get; set; }
    [Range(1, 1440)] public int? DelayedThresholdMinutes { get; set; }
}

public sealed class PosReportCatalogEntryDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Aliases { get; set; } = new();
    public bool IsLive { get; set; }
    public bool SupportsCashier { get; set; } = true;
    public bool SupportsFloor { get; set; }
    public bool SupportsDelayThreshold { get; set; }
    public string PdfUrl { get; set; } = "";
}

public sealed class PosReportPrintDataDto
{
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsLive { get; set; }
    public PosSalesPrintDataDto Header { get; set; } = new();
    public List<string> FilterNotes { get; set; } = new();
    public List<PosReportBranchSectionDto> Branches { get; set; } = new();
}

public sealed class PosReportBranchSectionDto
{
    public long BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public List<PosReportMetricDto> Metrics { get; set; } = new();
    public List<PosReportTableDto> Tables { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

public sealed class PosReportMetricDto
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string Unit { get; set; } = "";
}

public sealed class PosReportTableDto
{
    public string Title { get; set; } = "";
    public List<PosReportColumnDto> Columns { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
}

public sealed class PosReportColumnDto
{
    public string Label { get; set; } = "";
    public bool Numeric { get; set; }
    public bool Code { get; set; }
    public float Width { get; set; } = 1;
}
