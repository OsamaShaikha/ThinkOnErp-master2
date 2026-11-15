using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrScheduledAttendanceVerification
{
    private sealed class FutureVerificationClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(9023, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var seed = await (from e in db.Employees.AsNoTracking() join b in db.SysBranches on e.BranchId equals (long?)b.Id
                          where b.CompanyId.HasValue && b.IsActive select new { e.EmployeeCode, CompanyId = b.CompanyId!.Value }).FirstAsync();
        db.HrScopeCompanyId = seed.CompanyId;
        var settings = new AttendanceTimeSettingsRepository(db);
        if (await settings.GetCurrentAsync() != null) throw new InvalidOperationException("Scheduled attendance fixture needs an unconfigured company.");
        var today = DateTime.UtcNow.Date; var previous = today.AddDays(-1); var future = new DateTime(9022, 1, 1);
        var dates = new[] { previous.AddDays(-1), previous, today, future };
        if (await db.Set<EmployeeShiftDay>().AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && dates.Contains(d.WorkDate)) ||
            await db.AttendanceDays.AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && dates.Contains(d.AttendanceDate)))
            throw new InvalidOperationException("Scheduled attendance fixture dates occupied; existing days are never replaced.");
        var prefix = "AC_" + Guid.NewGuid().ToString("N");
        var rawIds = new List<long>(); long periodId = 0;
        try
        {
            await settings.InitializeAsync("UTC", "HR_VERIFY");
            var definitions = new ShiftDefinitionService(new ShiftDefinitionRepository(db));
            var shift = await definitions.CreateAsync(new(prefix, new("Night calculation", "Night calculation", TimeSpan.FromHours(22), TimeSpan.FromHours(6), 30, new[] { 0, 1, 2, 3, 4, 5, 6 }, true)), "HR_VERIFY");
            var daily = new ShiftDayService(new ShiftDayRepository(db));
            foreach (var date in new[] { previous, future }) await daily.CreateAsync(seed.EmployeeCode, new(DateOnly.FromDateTime(date), shift.ShiftCode, false, true, shift.Version), "HR_VERIFY");
            await daily.CreateAsync(seed.EmployeeCode, new(DateOnly.FromDateTime(today), shift.ShiftCode, false, true, shift.Version), "HR_VERIFY");
            await daily.CreateAsync(seed.EmployeeCode, new(DateOnly.FromDateTime(previous.AddDays(-1)), null, true, true, null), "HR_VERIFY");
            var branchAccess = new AttendanceBranchRepository(db);
            var authorizedBranches = await branchAccess.ListAsync(null);
            if (authorizedBranches.Any(b => b.CompanyId != seed.CompanyId)) throw new InvalidOperationException("HR branch list crossed company scope.");
            var wrongCompanyDenied = false;
            try { await branchAccess.ListAsync(seed.CompanyId + 1); } catch (UnauthorizedAccessException) { wrongCompanyDenied = true; }
            db.HrScopeEnabled = true; db.HrScopeAllBranches = false; db.HrScopeBranchIds.Clear();
            if (!wrongCompanyDenied || (await branchAccess.ListAsync(null)).Count != 0 || (authorizedBranches.Count > 0 && await branchAccess.GetAsync(authorizedBranches[0].Id) != null))
                throw new InvalidOperationException("HR branch permission scope was bypassed.");
            db.HrScopeEnabled = false;
            report.Add("Oracle HR attendance branch reads: company-only list, explicit foreign-company refusal and empty authorized-branch list/get denial passed.");
            var timing = new ShiftTimingService(new EffectiveShiftService(new EmployeeRepository(db), new ShiftResolutionRepository(db)), settings);
            var repository = new AttendanceCorrectionRepository(db);
            var engine = new AttendanceCalculationEngine(repository, timing);
            var request = new AttendanceCorrectionRequest { EmployeeCode = seed.EmployeeCode, AttendanceDate = future, OriginalDayVersion = AttendanceCorrectionRules.Version(null),
                RequestedCheckIn = future.AddHours(22).AddMinutes(25), RequestedCheckOut = future.AddDays(1).AddHours(6).AddMinutes(-20),
                Reason = "Synthetic night verification", CreationUser = "HR_VERIFY" };
            db.Add(request); await db.SaveChangesAsync();
            var correction = new AttendanceCorrectionService(repository, engine, clock: new FutureVerificationClock());
            await correction.ProcessCorrectionRequestAsync(request.Id, true, "HR_VERIFY", policy: new() { GracePeriodMinutes = 10, EarlyLeaveToleranceMinutes = 5 });
            var corrected = await db.AttendanceDays.AsNoTracking().SingleAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate == future);
            if (corrected.ShiftCode != prefix || corrected.ScheduledHours != 7.5m || corrected.ActualWorkedHours != 6.75m || corrected.LateArrivalMinutes != 15 || corrected.EarlyLeaveMinutes != 20)
                throw new InvalidOperationException("Oracle scheduled correction calculation failed.");
            db.AttendanceDays.Add(new() { EmployeeCode = seed.EmployeeCode, AttendanceDate = previous, ShiftCode = prefix,
                TimeZoneId = "UTC", ShiftStartsAtUtc = previous.AddHours(22), ShiftEndsAtUtc = today.AddHours(6), ShiftBreakMinutes = 30, ShiftSource = "DAILY_MANUAL",
                FirstCheckIn = previous.AddHours(22), ScheduledHours = 7.5m, CreationUser = "HR_VERIFY" });
            var policy = new AttendancePolicy { CompanyId = seed.CompanyId, Code = prefix, NameAr = "Verification", NameEn = "Verification",
                EffectiveFrom = previous, IsDefault = true, EnforceGeofence = false, CreationUser = "HR_VERIFY" };
            db.Add(policy); await db.SaveChangesAsync();
            var dayContext = new AttendanceDayContextRepository(db, timing, settings, repository);
            var dayClassifier = new AttendanceDayClassifier(dayContext);
            var rawProcessing = new RawAttendanceProcessingService(repository, timing, settings, new PolicyRepository(db), dayClassifier);
            var service = new HrAttendanceService(repository, new EmployeeRepository(db), new BranchRepository(db, NullLogger<BranchRepository>.Instance),
                new PolicyRepository(db), new GeoLocationService(NullLogger<GeoLocationService>.Instance), NullLogger<HrAttendanceService>.Instance,
                timing: timing, timeSettings: settings, processing: rawProcessing, branchAccess: new AttendanceBranchRepository(db));
            var punch = new ThinkOnErp.Application.DTOs.Hr.CheckOutRequestDto(seed.EmployeeCode, 0m, 0m, EventId: Guid.NewGuid().ToString());
            var result = await service.CheckOutAsync(punch, "HR_VERIFY");
            var period = new PayrollPeriod { CompanyId = seed.CompanyId, PeriodCode = "EV_" + prefix[^8..], NameAr = "Event replay", NameEn = "Event replay", FiscalYear = today.Year, Month = today.Month,
                StartDate = previous, EndDate = today, PayDate = today, Status = "CLOSED", CreationUser = "HR_VERIFY" };
            db.PayrollPeriods.Add(period); await db.SaveChangesAsync(); periodId = period.Id;
            var replayService = new HrAttendanceService(repository, new EmployeeRepository(db), new BranchRepository(db, NullLogger<BranchRepository>.Instance),
                new PolicyRepository(db), new GeoLocationService(NullLogger<GeoLocationService>.Instance), NullLogger<HrAttendanceService>.Instance,
                new ThinkOnErp.Infrastructure.Services.Hr.HrUnitOfWork(db), timing, settings);
            var replay = await replayService.CheckOutAsync(punch, "RETRY");
            if (replay != result) throw new InvalidOperationException("Accepted event retry did not return the exact original response.");
            var conflict = false;
            try { await replayService.CheckOutAsync(punch with { Latitude = 1m }, "RETRY"); } catch (MobilePunchEventConflictException) { conflict = true; }
            if (!conflict) throw new InvalidOperationException("Reused mobile event ID accepted a changed payload.");
            var eventId = Guid.Parse(punch.EventId!).ToString("N");
            if (await db.RawAttendances.CountAsync(r => r.EmployeeCode == seed.EmployeeCode && r.EventId == eventId) != 1) throw new InvalidOperationException("Event retry duplicated raw evidence.");
            var duplicateBlocked = false;
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO HR_RAW_ATTENDANCE (EMPLOYEE_CODE,PUNCH_TIME,PUNCH_TYPE,SOURCE,IS_PROCESSED,CREATION_USER,CREATION_DATE,EVENT_ID) VALUES ({seed.EmployeeCode},{DateTime.UtcNow},{"OUT"},{"MOBILE_APP"},1,{"HR_VERIFY"},{DateTime.UtcNow},{eventId})");
            }
            catch (OracleException ex) when (ex.Number == 1) { duplicateBlocked = true; }
            if (!duplicateBlocked) throw new InvalidOperationException("Oracle event uniqueness did not reject a duplicate.");
            for (var i = 0; i < 2; i++) db.RawAttendances.Add(new() { EmployeeCode = seed.EmployeeCode, PunchTime = today, PunchType = "IN", Source = "BIOMETRIC", DeviceId = prefix, CreationUser = "HR_VERIFY" });
            await db.SaveChangesAsync();
            if (await db.RawAttendances.CountAsync(r => r.DeviceId == prefix && r.EventId == null) != 2) throw new InvalidOperationException("Legacy NULL event IDs were incorrectly constrained.");
            rawIds.AddRange(await db.RawAttendances.Where(r => r.DeviceId == prefix).Select(r => r.Id).ToListAsync());
            var pendingPunch = new ThinkOnErp.Application.DTOs.Hr.CheckInRequestDto(seed.EmployeeCode, 0m, 0m, EventId: Guid.NewGuid().ToString());
            var receipt = await service.CheckInAsync(pendingPunch, "HR_VERIFY");
            var pending = await repository.GetRawEventAsync(receipt.RawEventId ?? 0) ?? throw new InvalidOperationException("Mobile evidence was lost.");
            rawIds.Add(pending.Id);
            if (!receipt.Success || !receipt.EvidenceOnly || pending.IsProcessed || pending.LastAttemptedAtUtc == null ||
                pending.ProcessingReason?.Contains("closed payroll period") != true || await db.AttendanceDays.AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate == today))
                throw new InvalidOperationException("Closed period failed to preserve a pending mobile receipt and attempt audit.");
            if (await service.CheckInAsync(pendingPunch, "RETRY") != receipt) throw new InvalidOperationException("Pending mobile receipt changed on retry.");
            policy.GeofenceViolationAction = "INVALID_FIXTURE_ACTION"; await db.SaveChangesAsync();
            var deniedPunch = pendingPunch with { EventId = Guid.NewGuid().ToString() };
            var deniedReceipt = await service.CheckInAsync(deniedPunch, "HR_VERIFY");
            var deniedRaw = await repository.GetRawEventAsync(deniedReceipt.RawEventId ?? 0) ?? throw new InvalidOperationException("Rejected mobile evidence was lost.");
            rawIds.Add(deniedRaw.Id);
            if (deniedReceipt.Success || deniedReceipt.EvidenceOnly || deniedRaw.ProcessingOutcome != "REJECTED" || !deniedRaw.IsProcessed ||
                deniedRaw.ProcessedBy != "HR_VERIFY" || deniedRaw.ProcessedDate == null || string.IsNullOrWhiteSpace(deniedRaw.ProcessingReason) ||
                await service.CheckInAsync(deniedPunch, "RETRY") != deniedReceipt)
                throw new InvalidOperationException("Rejected mobile evidence receipt/audit/retry failed.");
            policy.GeofenceViolationAction = "REJECT"; await db.SaveChangesAsync();
            report.Add("Oracle mobile rejected evidence: invalid company-action policy retained a terminal REJECTED raw with reason/reviewer/time and exact original failed receipt on retry.");
            var rejectedCorrection = await correction.SubmitCorrectionRequestAsync(seed.EmployeeCode, previous, null,
                DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-1), DateTimeKind.Utc), "Closed-period review fixture", "HR_VERIFY");
            var rejection = await correction.ProcessCorrectionRequestAsync(rejectedCorrection.Id, false, "HR_REVIEW", "Evidence reviewed; no financial change");
            if (rejection.Status != "REJECTED" || rejection.ApprovedBy != "HR_REVIEW") throw new InvalidOperationException("Closed-period correction rejection failed.");
            // Only synthetic fixture status is changed. Original periods are never reopened.
            period.Status = "OPEN"; await db.SaveChangesAsync();
            var appliedReceipt = await rawProcessing.ProcessAsync(pending.Id, "HR_PROCESS");
            if (!appliedReceipt.Applied || (await service.CheckInAsync(pendingPunch, "RETRY")) != receipt)
                throw new InvalidOperationException("Applying retained evidence changed its immutable receipt.");
            period.Status = "CLOSED"; await db.SaveChangesAsync();
            report.Add("Oracle mobile evidence: new capture survives actual CLOSED guard with pending receipt/attempt audit, exact retry, later explicit financial application without changing receipt, and reasoned correction rejection while CLOSED passed.");
            var sourceId = Guid.NewGuid().ToString("N"); var deviceTime = DateTime.SpecifyKind(today.AddHours(9), DateTimeKind.Utc);
            var device = await engine.IngestPunchAsync(seed.EmployeeCode, deviceTime, "IN", prefix + "_D1", eventId: sourceId);
            var deviceRetry = await engine.IngestPunchAsync(seed.EmployeeCode, deviceTime, "IN", prefix + "_D1", eventId: sourceId);
            if (device.Id != deviceRetry.Id || deviceRetry.IsProcessed) throw new InvalidOperationException("Device retry duplicated evidence or processed a financial day.");
            var deviceConflict = false;
            try { await engine.IngestPunchAsync(seed.EmployeeCode, deviceTime.AddTicks(1), "IN", prefix + "_D1", eventId: sourceId); }
            catch (AttendanceEventConflictException) { deviceConflict = true; }
            if (!deviceConflict) throw new InvalidOperationException("Device source event changed its accepted timestamp.");
            var otherDevice = await engine.IngestPunchAsync(seed.EmployeeCode, deviceTime, "IN", prefix + "_D2", eventId: sourceId);
            if (otherDevice.Id == device.Id || await db.AttendanceDays.CountAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate == today) != 1)
                throw new InvalidOperationException("Device namespace collision or unexpected financial-day write.");
            rawIds.Add(device.Id); rawIds.Add(otherDevice.Id);
            report.Add("Oracle device events: stable source/device identity, same raw ID on retry, changed timestamp rejection, separate devices, and unprocessed evidence accepted while payroll period is CLOSED passed; no attendance day created.");
            report.Add("Oracle mobile events: exact original-response retry after period closure, changed-payload rejection, one raw event, database duplicate guard and multiple legacy NULL IDs passed. Replay with real HrUnitOfWork bypassed writes; concurrent sessions were not tested.");
            var closed = await db.AttendanceDays.AsNoTracking().SingleAsync(d => d.EmployeeCode == seed.EmployeeCode && d.AttendanceDate == previous);
            rawIds.AddRange(await db.RawAttendances.Where(r => r.EmployeeCode == seed.EmployeeCode && r.CreationUser == "HR_VERIFY" && r.Source == "MOBILE_APP").Select(r => r.Id).ToListAsync());
            if (!result.Success || closed.LastCheckOut == null || closed.HasMissingPunch || closed.ScheduledHours != 7.5m || closed.ShiftCode != prefix)
                throw new InvalidOperationException("Oracle mobile checkout lost prior work date/shift.");
            report.Add("Oracle scheduled attendance: correction uses configured night shift/break/late/early metrics; actual mobile service closes previous open work date and persists raw UTC evidence. Both ran under outer rollback; new mobile capture also exercised the actual CLOSED financial guard. Own commit lifecycle was not tested.");
        }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); }
        if (await settings.GetCurrentAsync() != null || await db.Set<ShiftSchedule>().AnyAsync(s => s.ShiftCode == prefix) ||
            await db.Set<EmployeeShiftDay>().AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && dates.Contains(d.WorkDate)) ||
            await db.AttendanceDays.AnyAsync(d => d.EmployeeCode == seed.EmployeeCode && dates.Contains(d.AttendanceDate)) ||
            await db.AttendanceCorrectionRequests.AnyAsync(r => r.EmployeeCode == seed.EmployeeCode && r.CreationUser == "HR_VERIFY" && dates.Contains(r.AttendanceDate)) ||
            await db.Set<AttendancePolicy>().AnyAsync(p => p.Code == prefix) || await db.PayrollPeriods.AnyAsync(p => p.Id == periodId) || await db.Set<RawAttendance>().AnyAsync(r => rawIds.Contains(r.Id)))
            throw new InvalidOperationException("Scheduled attendance fixture rollback failed.");
        report.Add("Scheduled attendance rollback verified; no company settings, daily/attendance, raw or policy fixtures retained. HTTP/concurrent sessions not tested.");
    }
}
