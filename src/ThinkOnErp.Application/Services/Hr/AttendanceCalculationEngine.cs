using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IAttendanceCalculationEngine
{
    Task<RawAttendance> IngestPunchAsync(string employeeCode, DateTime punchTime, string punchType, string? deviceId, string source = "BIOMETRIC", string? eventId = null, string user = "BIOMETRIC_SYNC", CancellationToken cancellationToken = default);
    Task<AttendanceDay> ProcessEmployeeDayAttendanceAsync(string employeeCode, DateTime date, DateTime? checkIn, DateTime? checkOut, AttendancePolicy policy, decimal scheduledHours = 8.0m, TimeSpan? shiftStart = null, TimeSpan? shiftEnd = null, CancellationToken cancellationToken = default);
    string ComputePunchHash(string employeeCode, DateTime punchTime, string punchType);
}

public sealed class AttendanceCalculationEngine : IAttendanceCalculationEngine
{
    private readonly IAttendanceCorrectionRepository _repository;
    private readonly IShiftTimingService? _timing;
    private readonly IAttendanceDayClassifier? _classifier;

    public AttendanceCalculationEngine(IAttendanceCorrectionRepository repository, IShiftTimingService? timing = null, IAttendanceDayClassifier? classifier = null)
    {
        _repository = repository;
        _timing = timing; _classifier = classifier;
    }

    public string ComputePunchHash(string employeeCode, DateTime punchTime, string punchType)
    {
        var rawString = $"{employeeCode.Trim().ToUpperInvariant()}_{punchTime:yyyyMMddHHmmssfffffff}_{punchType.Trim().ToUpperInvariant()}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rawString));
        return Convert.ToHexString(bytes);
    }

    public async Task<RawAttendance> IngestPunchAsync(
        string employeeCode,
        DateTime punchTime,
        string punchType,
        string? deviceId,
        string source = "BIOMETRIC", string? eventId = null, string user = "BIOMETRIC_SYNC",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeCode) || string.IsNullOrWhiteSpace(deviceId) || deviceId.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(eventId) || eventId.Trim().Length > 200 || string.IsNullOrWhiteSpace(source) || source.Trim().Length > 100)
            throw new ArgumentException("Employee, device, source and stable source EventId are required within their field limits.");
        if (punchTime.Kind != DateTimeKind.Utc || punchType is not ("IN" or "OUT")) throw new ArgumentException("Device punch time must explicitly be UTC and type must be IN or OUT.");
        var device = deviceId.Trim(); var external = eventId.Trim(); var origin = source.Trim().ToUpperInvariant();
        var key = "D:" + LeaveMovementKey.Hash(System.Text.Json.JsonSerializer.Serialize(new { device, external, origin }));
        var payload = LeaveMovementKey.Hash(System.Text.Json.JsonSerializer.Serialize(new { employeeCode, PunchTicks = punchTime.Ticks, punchType, device, origin }));
        var raw = new RawAttendance
        {
            EmployeeCode = employeeCode, PunchTime = punchTime, PunchType = punchType, DeviceId = device,
            ExternalReference = external, EventId = key, EventPayloadHash = payload, Source = origin,
            IsProcessed = false, CreationUser = user, CreationDate = DateTime.UtcNow
        };
        // Intake stores evidence only, even when its payroll date is closed.
        // Processing this event into a financial day remains a separate guarded workflow.
        return await _repository.AcceptDeviceEventAsync(raw, cancellationToken);
    }

    public async Task<AttendanceDay> ProcessEmployeeDayAttendanceAsync(
        string employeeCode, DateTime date, DateTime? checkIn, DateTime? checkOut, AttendancePolicy policy,
        decimal scheduledHours = 8.0m, TimeSpan? shiftStart = null, TimeSpan? shiftEnd = null,
        CancellationToken cancellationToken = default)
    {
        // Legacy parameters remain for source compatibility. Configured roster is authoritative.
        var workDate = DateOnly.FromDateTime(date);
        var schedule = await (_timing ?? throw new InvalidOperationException("Configured shift timing is required for attendance calculation."))
            .PreviewConfiguredAsync(employeeCode, workDate, cancellationToken);
        var result = ScheduledAttendanceCalculator.Calculate(employeeCode, workDate, checkIn, checkOut, schedule, policy);
        if (_classifier != null) await _classifier.ClassifyAsync(result, schedule, cancellationToken);
        result.CreationUser = "ATTENDANCE_ENGINE"; result.CreationDate = DateTime.UtcNow;
        return result;
    }
}
