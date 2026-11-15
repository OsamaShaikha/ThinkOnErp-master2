using System.Diagnostics.Metrics;

namespace ThinkOnErp.Infrastructure.Services.Hr;

/// <summary>Low-cardinality metrics for HR background queues.</summary>
internal static class HrBackgroundMetrics
{
    private static readonly Meter Meter = new("ThinkOnErp.HR");

    internal static readonly Counter<long> RawAttendanceEvents = Meter.CreateCounter<long>(
        "thinkonerp.hr.attendance.raw.events");
    internal static readonly Counter<long> RawAttendanceBatches = Meter.CreateCounter<long>(
        "thinkonerp.hr.attendance.raw.batches");
    internal static readonly Counter<long> OutboxMessages = Meter.CreateCounter<long>(
        "thinkonerp.hr.accounting.outbox.messages");
    internal static readonly Counter<long> OutboxBatches = Meter.CreateCounter<long>(
        "thinkonerp.hr.accounting.outbox.batches");
}
