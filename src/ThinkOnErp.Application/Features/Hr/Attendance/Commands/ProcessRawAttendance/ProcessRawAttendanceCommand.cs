using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Commands.ProcessRawAttendance;

public sealed record ProcessRawAttendanceCommand(long Id, string CurrentUser) : IRequest<ApiResponse<RawProcessingResult>>;

public sealed class ProcessRawAttendanceCommandValidator : AbstractValidator<ProcessRawAttendanceCommand>
{
    public ProcessRawAttendanceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class ProcessRawAttendanceCommandHandler : IRequestHandler<ProcessRawAttendanceCommand, ApiResponse<RawProcessingResult>>
{
    private readonly IAttendanceCorrectionRepository _repository;
    private readonly IShiftTimingService _timing;
    private readonly IAttendanceTimeSettingsRepository _settings;
    private readonly IPolicyRepository _policies;
    private readonly IAttendanceDayClassifier _classifier;

    public ProcessRawAttendanceCommandHandler(
        IAttendanceCorrectionRepository repository,
        IShiftTimingService timing,
        IAttendanceTimeSettingsRepository settings,
        IPolicyRepository policies,
        IAttendanceDayClassifier classifier)
    {
        _repository = repository;
        _timing = timing;
        _settings = settings;
        _policies = policies;
        _classifier = classifier;
    }

    public async Task<ApiResponse<RawProcessingResult>> Handle(ProcessRawAttendanceCommand request, CancellationToken cancellationToken)
    {
        var result = await ProcessAsync(request.Id, request.CurrentUser, cancellationToken);
        return ApiResponse<RawProcessingResult>.CreateSuccess(result);
    }

    internal async Task<RawProcessingResult> ProcessAsync(long id, string user, CancellationToken ct)
    {
        var snapshot = await _repository.GetRawEventAsync(id, ct)
            ?? throw new KeyNotFoundException("Raw event not found in the current HR scope.");

        if (snapshot.IsProcessed) return Terminal(snapshot);

        try
        {
            await _repository.RecordRawAttemptAsync(id, user, null, ct);
            if (snapshot.EventId == null || snapshot.EventPayloadHash == null)
                throw new InvalidOperationException("Legacy raw evidence needs UTC/source identity review before processing.");
            if (snapshot.PunchType is not ("IN" or "OUT") || snapshot.PunchTime > DateTime.UtcNow)
                throw new InvalidOperationException("Invalid or future raw punch requires review.");

            var schedule = await ResolveAsync(snapshot, ct);
            var date = schedule.Shift.WorkDate.ToDateTime(TimeOnly.MinValue);
            var bound = await _repository.BindRawWorkDateAsync(id, date, ct);
            if (bound.IsProcessed) return Terminal(bound);

            return await _repository.ExecuteForRawWorkDateAsync(
                snapshot.EmployeeCode, date, () => ApplyAsync(snapshot, schedule.Shift.WorkDate, user, ct), ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            var accepted = await _repository.GetRawEventAsync(id, ct);
            if (accepted?.IsProcessed == true) return Terminal(accepted);
            await _repository.RecordRawAttemptAsync(id, user, ex.Message, ct);
            throw;
        }
    }

    private async Task<RawProcessingResult> ApplyAsync(RawAttendance snapshot, DateOnly expectedDate, string user, CancellationToken ct)
    {
        var raw = await _repository.LockRawEventAsync(snapshot.Id, snapshot.EmployeeCode, ct)
            ?? throw new KeyNotFoundException("Raw event no longer visible.");

        if (raw.IsProcessed) return Terminal(raw);

        if (raw.EventId != snapshot.EventId || raw.EventPayloadHash != snapshot.EventPayloadHash ||
            raw.PunchTime != snapshot.PunchTime || raw.PunchType != snapshot.PunchType)
        {
            throw new InvalidOperationException("Raw evidence changed while acquiring its locks; review it.");
        }

        var schedule = await ResolveAsync(raw, ct);
        if (schedule.Shift.WorkDate != expectedDate || raw.ResolvedWorkDate?.Date != expectedDate.ToDateTime(TimeOnly.MinValue))
        {
            throw new InvalidOperationException("Raw work date changed while acquiring financial locks; review it.");
        }

        var earlierEvents = await _repository.GetEarlierPendingEventsAsync(raw.EmployeeCode, raw.PunchTime, raw.Id, ct);
        if (earlierEvents.Count > 1000)
            throw new InvalidOperationException("Pending evidence volume requires reviewed batching before processing this event.");

        foreach (var earlier in earlierEvents)
        {
            var priorDate = earlier.ResolvedWorkDate.HasValue
                ? DateOnly.FromDateTime(earlier.ResolvedWorkDate.Value)
                : (await ResolveAsync(earlier, ct)).Shift.WorkDate;

            if (priorDate == expectedDate)
                throw new InvalidOperationException("Earlier evidence for this work date must be processed or reviewed first.");
        }

        var date = expectedDate.ToDateTime(TimeOnly.MinValue);
        await _repository.EnsureEmployeeWorkDateAsync(raw.EmployeeCode, date, ct);

        if (await _repository.HasApprovedCorrectionAsync(raw.EmployeeCode, date, ct))
            throw new InvalidOperationException("This day has an approved manual correction. Review or ignore the late evidence instead of overwriting it.");

        var old = await _repository.GetAttendanceDayAsync(raw.EmployeeCode, date, ct);
        if (old != null && old.TimeZoneId == null)
            throw new InvalidOperationException("Legacy day needs a reviewed shift/time snapshot.");

        var setting = await _settings.GetForEmployeeAsync(raw.EmployeeCode, ct);
        var policy = await _policies.GetActiveAttendancePolicyAsync(setting.CompanyId, date, ct) ?? new AttendancePolicy();
        var processed = await _repository.GetProcessedDayEventsAsync(raw.EmployeeCode, date, ct);
        var evidence = AttendanceEvidenceSummary.Build(processed.Append(raw), old, policy.AllowMultipleSessions);

        if (evidence.NeedsReview)
            throw new InvalidOperationException("Multiple sessions, tied opposite punches or reversed evidence require session review; no day was overwritten.");

        decimal? multiSessionMinutes = (policy.AllowMultipleSessions && evidence.TotalSessionsCount > 1) ? evidence.TotalSessionMinutes : null;
        var calculated = ScheduledAttendanceCalculator.Calculate(raw.EmployeeCode, expectedDate, evidence.FirstIn, evidence.LastOut, schedule, policy, multiSessionNetMinutes: multiSessionMinutes);

        AttendanceDay day;
        if (old == null)
        {
            day = calculated;
            day.CreationUser = user;
            day.CreationDate = DateTime.UtcNow;
            await _repository.AddAttendanceDayAsync(day, ct);
        }
        else
        {
            ScheduledAttendanceCalculator.CopyMetrics(calculated, old);
            old.FirstCheckIn = calculated.FirstCheckIn;
            old.LastCheckOut = calculated.LastCheckOut;
            old.UpdateUser = user;
            old.UpdateDate = DateTime.UtcNow;
            day = old;
            await _repository.UpdateAttendanceDayAsync(day, ct);
        }

        if (raw.PunchType == "IN" && day.FirstCheckIn == raw.PunchTime)
        {
            day.CheckInBranchId = raw.BranchId;
            day.CheckInLatitude = raw.Latitude;
            day.CheckInLongitude = raw.Longitude;
            day.CheckInDistanceMeters = raw.DistanceToBranchMeters;
            day.IsCheckInWithinGeofence = raw.IsWithinGeofence;
        }

        if (raw.PunchType == "OUT" && day.LastCheckOut == raw.PunchTime)
        {
            day.CheckOutBranchId = raw.BranchId;
            day.CheckOutLatitude = raw.Latitude;
            day.CheckOutLongitude = raw.Longitude;
            day.CheckOutDistanceMeters = raw.DistanceToBranchMeters;
            day.IsCheckOutWithinGeofence = raw.IsWithinGeofence;
        }

        if (raw.IsWithinGeofence == false)
            day.HasGeofenceViolation = true;

        await _repository.SaveChangesAsync(ct);
        raw.IsProcessed = true;
        raw.ProcessingOutcome = "APPLIED";
        raw.ProcessingReason = null;
        raw.ProcessedBy = user;
        raw.ProcessedDate = DateTime.UtcNow;
        raw.AttendanceDay = day;
        raw.AttendanceDayId = day.Id;

        await _repository.UpdateRawAttendanceAsync(raw, ct);
        await _repository.SaveChangesAsync(ct);

        var classified = await _classifier.ClassifyAsync(day, schedule, ct);
        await _repository.UpdateAttendanceDayAsync(day, ct);
        await _repository.SaveChangesAsync(ct);

        return new(raw.Id, true, false, expectedDate, day.Id, raw.ProcessingOutcome, classified.NeedsReview, classified.ReviewReason);
    }

    private async Task<TimedShiftDto> ResolveAsync(RawAttendance raw, CancellationToken ct)
    {
        var setting = await _settings.GetForEmployeeAsync(raw.EmployeeCode, ct);
        var instant = new DateTimeOffset(DateTime.SpecifyKind(raw.PunchTime, DateTimeKind.Utc));
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, ShiftTimeRules.Zone(setting.TimeZoneId)).DateTime);
        return await AttendanceWorkDateResolver.ResolveAsync(raw.EmployeeCode, instant, localDate, raw.PunchType, _timing, _repository, ct);
    }

    private static RawProcessingResult Terminal(RawAttendance raw) => new(
        raw.Id,
        raw.ProcessingOutcome == "APPLIED",
        true,
        raw.ResolvedWorkDate.HasValue ? DateOnly.FromDateTime(raw.ResolvedWorkDate.Value) : null,
        raw.AttendanceDayId,
        raw.ProcessingOutcome,
        raw.ProcessingOutcome == null,
        raw.ProcessingOutcome == null ? "Legacy processed evidence has no outcome/link audit; review it." : "Evidence already has a terminal outcome.");
}
