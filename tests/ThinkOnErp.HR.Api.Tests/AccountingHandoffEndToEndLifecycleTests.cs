using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Accounting;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class AccountingHandoffEndToEndLifecycleTests
{
    [Fact]
    public async Task CalculateApproveAndAcceptPayroll_UsesActualCalculationHandoffOutboxAndGlDraft()
    {
        using var harness = new EndToEndLifecycleHarness();
        await harness.SeedInitialDataAsync();

        await using var db = harness.CreateDb();
        var unrelatedSeedEmployee = await db.Employees.SingleAsync(e => e.EmployeeCode == "E-501");
        unrelatedSeedEmployee.IsActive = false;
        db.PayrollPeriods.Add(new PayrollPeriod
        {
            Id = 901,
            CompanyId = 3,
            PeriodCode = "2026-11",
            StartDate = new DateTime(2026, 11, 1),
            EndDate = new DateTime(2026, 11, 30),
            Status = "OPEN"
        });
        var employee = new Employee
        {
            EmployeeCode = "E-901",
            NameLocal = "Integration Employee",
            HireDate = new DateTime(2024, 1, 1),
            BranchId = 2,
            IsActive = true,
            EmploymentStatus = "ACTIVE"
        };
        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = employee.EmployeeCode,
            EffectiveFrom = new DateTime(2024, 1, 1),
            IsActive = true,
            BasicSalary = 900m,
            CurrencyCode = "USD",
            Lines =
            [
                new SalaryStructureLine
                {
                    ComponentCode = "OTHER",
                    Amount = 90m,
                    IsActive = true,
                    Component = new SalaryComponent { ComponentCode = "OTHER", ComponentType = "DEDUCTION" }
                }
            ]
        });
        db.Employees.Add(employee);
        db.AttendanceDays.Add(new AttendanceDay
        {
            EmployeeCode = employee.EmployeeCode,
            AttendanceDate = new DateTime(2026, 11, 5),
            TimeZoneId = "UTC",
            ShiftCode = "OPS07_DAY",
            ShiftSource = "BASE",
            ShiftStartsAtUtc = new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc),
            ShiftEndsAtUtc = new DateTime(2026, 11, 5, 17, 0, 0, DateTimeKind.Utc),
            ShiftBreakMinutes = 30,
            FirstCheckIn = new DateTime(2026, 11, 5, 9, 30, 0, DateTimeKind.Utc),
            LastCheckOut = new DateTime(2026, 11, 5, 17, 0, 0, DateTimeKind.Utc),
            ScheduledHours = 7.5m,
            ActualWorkedHours = 7.5m,
            LateArrivalMinutes = 30,
            Status = "PRESENT",
            CreationUser = "attendance-processor"
        });
        db.Set<AttendanceTimeSettings>().Add(new AttendanceTimeSettings { CompanyId = 3, TimeZoneId = "UTC", CreationUser = "ops07-test" });
        db.ShiftSchedules.Add(new ShiftSchedule
        {
            ShiftCode = "OPS07_DAY",
            NameLocal = "OPS-07 Day Shift",
            NameEn = "OPS-07 Day Shift",
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(17),
            BreakMinutes = 30,
            WorkingDaysJson = "[0,1,2,3,4,5,6]",
            IsActive = true
        });
        db.EmployeeShiftAssignments.Add(new EmployeeShiftAssignment
        {
            EmployeeCode = employee.EmployeeCode,
            ShiftCode = "OPS07_DAY",
            AssignmentKind = "BASE",
            EffectiveFrom = new DateTime(2026, 11, 1),
            IsActive = true
        });
        var unpaidLeaveType = new LeaveType { LeaveTypeCode = "OPS07_UNPAID", NameLocal = "Unpaid leave", IsPaid = false, IsActive = true };
        db.LeaveTypes.Add(unpaidLeaveType);
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeCode = employee.EmployeeCode,
            LeaveTypeCode = unpaidLeaveType.LeaveTypeCode,
            LeaveType = unpaidLeaveType,
            StartDate = new DateTime(2026, 11, 10),
            EndDate = new DateTime(2026, 11, 12),
            DaysRequested = 3m,
            Status = "APPROVED",
            ApprovedBy = "leave-approver",
            ApprovalDate = new DateTime(2026, 11, 9)
        });
        await db.SaveChangesAsync();

        var inputSnapshots = new PayrollInputSnapshotService(db);
        var snapshotPeriod = await db.PayrollPeriods.SingleAsync(p => p.Id == 901);
        var snapshotBeforeCorrection = await inputSnapshots.CaptureAsync(snapshotPeriod, 2);
        var shiftResolution = new ShiftResolutionRepository(db);
        var effectiveShifts = new EffectiveShiftService(new EmployeeRepository(db), shiftResolution);
        var shiftTiming = new ShiftTimingService(effectiveShifts, new AttendanceTimeSettingsRepository(db));
        var attendanceEngine = new AttendanceCalculationEngine(new AttendanceCorrectionRepository(db), shiftTiming);
        var rawRepository = new AttendanceCorrectionRepository(db);
        var dayClassifier = new Mock<IAttendanceDayClassifier>();
        dayClassifier.Setup(c => c.ClassifyAsync(It.IsAny<AttendanceDay>(), It.IsAny<TimedShiftDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceDay day, TimedShiftDto _, CancellationToken _) => new AttendanceDayClassificationDto(day, false, false, false, false, null));
        var rawProcessor = new RawAttendanceProcessingService(rawRepository, shiftTiming,
            new AttendanceTimeSettingsRepository(db), Mock.Of<IPolicyRepository>(), dayClassifier.Object);
        var rawIn = await attendanceEngine.IngestPunchAsync("E-901", new DateTime(2026, 11, 5, 7, 55, 0, DateTimeKind.Utc),
            "IN", "OPS07_DEVICE", "BIOMETRIC", "OPS07-IN", "device-processor");
        var rawInResult = await rawProcessor.ProcessAsync(rawIn.Id, "attendance-processor");
        Assert.True(rawInResult.Applied);
        Assert.Equal("APPLIED", (await rawProcessor.GetAsync(rawIn.Id)).Outcome);
        var rawOut = await attendanceEngine.IngestPunchAsync("E-901", new DateTime(2026, 11, 5, 17, 0, 0, DateTimeKind.Utc),
            "OUT", "OPS07_DEVICE", "BIOMETRIC", "OPS07-OUT", "device-processor");
        var rawOutResult = await rawProcessor.ProcessAsync(rawOut.Id, "attendance-processor");
        Assert.True(rawOutResult.Applied);
        Assert.Equal(rawInResult.AttendanceDayId, rawOutResult.AttendanceDayId);
        var attendanceCorrections = new AttendanceCorrectionService(new AttendanceCorrectionRepository(db), attendanceEngine);
        var correction = await attendanceCorrections.SubmitCorrectionRequestAsync("E-901", new DateTime(2026, 11, 5),
            new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 5, 17, 0, 0, DateTimeKind.Utc),
            "Correct source attendance", "employee");
        var approvedCorrection = await attendanceCorrections.ProcessCorrectionRequestAsync(correction.Id, true, "attendance-approver");
        Assert.Equal("APPROVED", approvedCorrection.Status);
        Assert.Equal("attendance-approver", approvedCorrection.ApprovedBy);
        Assert.Equal(new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc),
            (await db.AttendanceDays.SingleAsync(d => d.EmployeeCode == "E-901")).FirstCheckIn);
        var correctedAttendance = await db.AttendanceDays.SingleAsync(d => d.EmployeeCode == "E-901");
        Assert.Equal("OPS07_DAY", correctedAttendance.ShiftCode);
        Assert.Equal(7.5m, correctedAttendance.ScheduledHours);
        Assert.Equal(8.5m, correctedAttendance.ActualWorkedHours);
        var snapshotAfterCorrection = await inputSnapshots.CaptureAsync(snapshotPeriod, 2);
        Assert.NotEqual(snapshotBeforeCorrection, snapshotAfterCorrection);

        var policies = new Mock<IPolicyRepository>();
        var proration = new Mock<IPayrollProrationService>();
        proration.Setup(p => p.CalculateProrationFactorAsync(3, new DateTime(2026, 11, 1), new DateTime(2026, 11, 30),
                employee.HireDate, null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1m, 30m, 30m, "CALENDAR_DAYS", "OPS07_TEST"));
        var ssc = new Mock<ISSCCalculationService>();
        ssc.Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, It.IsAny<SSCPolicy?>(), It.IsAny<int>()))
            .Returns<decimal, bool, SSCPolicy?, int>((eligible, _, _, _) => new SSCResult(eligible, 0m, 0m, 0m, 0m, 0m, 0m, "OPS07_TEST"));
        var tax = new Mock<ITaxCalculationEngine>();
        tax.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<TaxPolicy?>(), It.IsAny<int>()))
            .Returns<decimal, decimal, int, TaxPolicy?, int>((gross, _, _, _, _) => new TaxResult(gross, 0m, gross * 12m, gross * 12m, 0m, 0m, "OPS07_TEST"));
        var loans = new Mock<ILoanDeductionService>();
        loans.Setup(l => l.CalculateAndApplyDeductionsAsync(It.IsAny<long?>(), "E-901", "2026-11", It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0m, 0m, 0m, new(), new()));
        var adjustments = new Mock<IPayrollAdjustmentRepository>();
        adjustments.Setup(a => a.GetPayrollCalculationAdjustmentsAsync(3, "2026-11", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollAdjustment>());
        ILeaveService leave = new LeaveService(new LeaveRepository(db), Mock.Of<IEmployeeRepository>());
        var calculationEngine = new PayrollCalculationEngine(policies.Object, proration.Object, Mock.Of<IOvertimeCalculationService>(),
            ssc.Object, tax.Object, loans.Object, adjustments.Object, leave);

        harness.ValidationService.Setup(v => v.ValidatePostApprovalAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new(), new()));
        harness.ValidationService.Setup(v => v.ValidatePreCalculationAsync(3, "2026-11", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollValidationResultDto(true, new(), new()));
        var unitOfWork = new Mock<IHrUnitOfWork>();
        unitOfWork.Setup(u => u.ExecuteForPayrollPeriodAsync(3, "2026-11", It.IsAny<Func<Task<PayrollRun>>>(), It.IsAny<CancellationToken>()))
            .Returns<long, string, Func<Task<PayrollRun>>, CancellationToken>((_, _, action, _) => action());
        unitOfWork.Setup(u => u.ExecuteForPayrollPeriodAsync(3, "2026-11", It.IsAny<Func<Task<PayrollCalculationResultDto>>>(), It.IsAny<CancellationToken>()))
            .Returns<long, string, Func<Task<PayrollCalculationResultDto>>, CancellationToken>((_, _, action, _) => action());
        var payrollService = new PayrollService(
            new ThinkOnErp.Infrastructure.Repositories.Hr.PayrollPeriodRepository(db),
            calculationEngine,
            harness.ValidationService.Object,
            unitOfWork: unitOfWork.Object,
            inputSnapshots: inputSnapshots,
            accountingHandoffs: harness.CreateHandoffService(db));

        var calculated = await payrollService.CreateAndCalculatePayrollRunAsync(new CreatePayrollRunDto(3, "2026-11", 2), "hr-calculator");
        Assert.Equal(1, calculated.EmployeeCount);
        Assert.Equal(810m, calculated.TotalGross);
        Assert.Equal(720m, calculated.TotalNet);
        Assert.Equal(90m, calculated.TotalOtherDeductions);
        var savedCalculation = await db.PayrollRuns.Include(r => r.Lines).ThenInclude(l => l.Components)
            .SingleAsync(r => r.Id == calculated.PayrollRunId);
        Assert.Equal("CALCULATED", savedCalculation.Status);
        var savedLine = Assert.Single(savedCalculation.Lines);
        Assert.Equal("E-901", savedLine.EmployeeCode);
        Assert.Equal("USD", savedLine.CurrencyCode);
        Assert.Equal(810m, savedLine.BasicSalary);
        Assert.Equal(810m, savedLine.GrossSalary);
        Assert.Equal(90m, savedLine.OtherDeductions);
        Assert.Equal(720m, savedLine.NetPay);
        Assert.Contains(savedLine.Components, c => c.ComponentCode == "BASIC" && c.Amount == 810m);
        Assert.Contains(savedLine.Components, c => c.ComponentCode == "UNPAID_LEAVE" && c.Amount == 90m);
        Assert.Contains(savedLine.Components, c => c.ComponentCode == "OTHER" && c.ComponentType == "DEDUCTION" && c.Amount == 90m);
        var approved = await payrollService.ApprovePayrollRunAsync(calculated.PayrollRunId, "hr-approver");

        Assert.Equal("APPROVED", approved.Status);
        var handoff = await db.HrAccountingHandoffs.Include(h => h.Lines).Include(h => h.Transitions)
            .SingleAsync(h => h.SourceType == "PAYROLL" && h.SourceId == calculated.PayrollRunId.ToString());
        Assert.Equal("QUEUED", handoff.DeliveryStatus);
        Assert.Equal("2026-11", handoff.PayPeriod);
        Assert.Equal("hr-approver", handoff.CreatedBy);
        Assert.NotEmpty(handoff.PayloadHash);
        Assert.Contains(handoff.Lines, l => l.LineType == "CONTROL:GROSS" && l.Amount == 810m);
        Assert.Contains(handoff.Lines, l => l.LineType == "CONTROL:NET_PAY" && l.Amount == 720m);
        Assert.Contains(handoff.Transitions, t => t.FromStatus == "INITIAL" && t.ToStatus == "QUEUED");
        Assert.Single(db.HrAccountingOutboxMessages.Where(m => m.EventType == "PAYROLL_SUBMIT" && m.Status == "PENDING"));
        unitOfWork.Verify(u => u.ExecuteForPayrollPeriodAsync(3, "2026-11", It.IsAny<Func<Task<PayrollRun>>>(), It.IsAny<CancellationToken>()), Times.Once);

        var (worker, outboxProvider) = harness.CreateOutboxWorker();
        using (outboxProvider)
        {
            await worker.RelayBatchAsync(CancellationToken.None);
        }

        db.ChangeTracker.Clear();
        var delivered = await db.HrAccountingHandoffs.Include(h => h.Transitions).SingleAsync(h => h.Id == handoff.Id);
        Assert.Equal("DELIVERED", delivered.DeliveryStatus);
        Assert.Contains(delivered.Transitions, t => t.FromStatus == "QUEUED" && t.ToStatus == "DELIVERED" && t.Actor == "OUTBOX");
        Assert.Single(db.AccountingHandoffInboxMessages.Where(m => m.HandoffId == handoff.Id && m.Status == "RECEIVED"));
        Assert.Equal("DELIVERED", await db.HrAccountingOutboxMessages.Where(m => m.HandoffId == handoff.Id).Select(m => m.Status).SingleAsync());

        var accepted = await harness.CreateHandoffService(db).ReviewAsync(delivered.Id, true, null, "accounting-reviewer");
        Assert.Equal("ACCEPTED", accepted.DeliveryStatus);
        Assert.Contains(accepted.Transitions, t => t.FromStatus == "DELIVERED" && t.ToStatus == "ACCEPTED" && t.Actor == "accounting-reviewer");

        var handoffService = new HrAccountingHandoffService(db, new GlVoucherRepository(db),
            new HrPayrollPostingService(db), harness.ReceiptPayments.Object, harness.GlVouchers.Object);
        var journalId = await handoffService.CreateJournalVoucherAsync(accepted.Id, "accounting-user");
        var createdJournal = await db.GlVoucherHeaders.Include(v => v.Details).SingleAsync(v => v.Id == journalId);
        Assert.Equal(1, createdJournal.Status);
        Assert.Equal("HR_HANDOFF", createdJournal.SourceSystemCode);
        Assert.Equal(accepted.Id, createdJournal.SourceRefId);
        Assert.Equal(createdJournal.Details.Sum(d => d.Debit), createdJournal.Details.Sum(d => d.Credit));
        Assert.Equal("ACCEPTED", (await db.HrAccountingHandoffs.SingleAsync(h => h.Id == accepted.Id)).DeliveryStatus);
        Assert.Single(db.AccountingHandoffJournalLinks.Where(link => link.HandoffId == accepted.Id && link.VoucherId == journalId));

        createdJournal.Status = 3;
        createdJournal.PostDate = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        await handoffService.ConfirmJournalPostedAsync(accepted.Id, journalId, "ops07-posted-event-1", "accounting-poster");
        var postedHandoff = await db.HrAccountingHandoffs.Include(h => h.Transitions).SingleAsync(h => h.Id == accepted.Id);
        Assert.Equal("POSTED", postedHandoff.DeliveryStatus);
        Assert.Contains(postedHandoff.Transitions, t => t.FromStatus == "ACCEPTED" && t.ToStatus == "POSTED" && t.Actor == "accounting-poster");
        Assert.Single(db.AccountingHandoffPostingReceipts.Where(receipt => receipt.HandoffId == accepted.Id && receipt.VoucherId == journalId));
    }

    [Fact]
    public async Task FullFinancialLifecycle_Approve_Accept_Post_PartialPay_FailPay_RetryPay_ReversePay_ReverseJv_CorrectRun()
    {
        using var harness = new EndToEndLifecycleHarness();
        await harness.SeedInitialDataAsync();

        const long handoffId = 1001;
        const long runId = 10;

        // Step 1: Initial state verification
        await using (var db = harness.CreateDb())
        {
            var initialAdvance = await db.EmployeeAdvances.SingleAsync(a => a.Id == 41);
            Assert.Equal(100m, initialAdvance.RemainingBalance);
            Assert.Equal(0m, initialAdvance.DeductedAmount);
            Assert.Null(initialAdvance.ReservedByHandoffId);

            var initialSchedule = await db.LoanRepaymentSchedules.SingleAsync(s => s.Id == 71);
            Assert.Equal(50m, initialSchedule.ScheduledAmount);
            Assert.Equal(0m, initialSchedule.PaidAmount);
            Assert.Null(initialSchedule.ReservedByHandoffId);
        }

        // Step 2: Handoff Creation upon Approval with Reservations
        await using (var db = harness.CreateDb())
        {
            var handoff = new HrAccountingHandoff
            {
                Id = handoffId,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "PAYROLL",
                SourceId = runId.ToString(),
                SourceVersion = 1,
                Operation = "SUBMIT",
                PayPeriod = "2026-11",
                HandoffReference = $"HR-PAY-3-{runId}-1",
                PayloadHash = "e2e-hash-001",
                DeliveryStatus = "DELIVERED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "hr-admin",
                Lines =
                [
                    new HrAccountingHandoffLine
                    {
                        LineType = "CONTROL:NET_PAY",
                        CurrencyCode = "USD",
                        Amount = 810m,
                        BranchId = 2,
                        SourceReference = "NET_PAY"
                    },
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:ADVANCE",
                        CurrencyCode = "USD",
                        Amount = 40m,
                        BranchId = 2,
                        SourceReference = "ADVANCE:41:PAYROLL_LINE:501"
                    },
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:SCHEDULE",
                        CurrencyCode = "USD",
                        Amount = 50m,
                        BranchId = 2,
                        SourceReference = "SCHEDULE:71:PAYROLL_LINE:501"
                    }
                ]
            };
            db.HrAccountingHandoffs.Add(handoff);

            // Reserve advance and loan for this handoff
            var adv = await db.EmployeeAdvances.SingleAsync(a => a.Id == 41);
            adv.ReservedByHandoffId = handoffId;

            var sch = await db.LoanRepaymentSchedules.SingleAsync(s => s.Id == 71);
            sch.ReservedByHandoffId = handoffId;

            await db.SaveChangesAsync();
        }

        // Step 3: Accounting reviews and accepts handoff
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            await handoffService.ReviewAsync(handoffId, accept: true, reason: null, user: "accounting-auditor");
        }

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == handoffId);
            Assert.Equal("ACCEPTED", handoff.DeliveryStatus);
        }

        // Step 4: Accounting creates Journal Voucher Draft
        long jvId;
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            jvId = await handoffService.CreateJournalVoucherAsync(handoffId, "accounting-auditor");
            Assert.Equal(7, jvId);
        }

        // Step 5: Accounting Posts JV & Settlement Snapshot is captured
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            await handoffService.ConfirmJournalPostedAsync(handoffId, jvId, "jv-post-event-01", "accounting-poster");
        }

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == handoffId);
            Assert.Equal("POSTED", handoff.DeliveryStatus);

            var receipt = await db.AccountingHandoffPostingReceipts.SingleAsync(r => r.HandoffId == handoffId);
            Assert.NotNull(receipt.SettlementSnapshotJson);
            Assert.Contains("ADVANCE", receipt.SettlementSnapshotJson);
            Assert.Contains("SCHEDULE", receipt.SettlementSnapshotJson);

            // Settlements applied
            var advance = await db.EmployeeAdvances.SingleAsync(a => a.Id == 41);
            Assert.Equal(40m, advance.DeductedAmount);
            Assert.Equal(60m, advance.RemainingBalance);
            Assert.Null(advance.ReservedByHandoffId);

            var schedule = await db.LoanRepaymentSchedules.SingleAsync(s => s.Id == 71);
            Assert.Equal(50m, schedule.PaidAmount);
            Assert.Equal("PAID", schedule.Status);
            Assert.Null(schedule.ReservedByHandoffId);
        }

        // Step 6: Accounting prepares Partial Payment 1 ($500)
        long payment1Id;
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var p1 = await handoffService.PreparePaymentAsync(handoffId, 500m, "USD", "BANK", "e2e-pay-01", "accounting-cashier");
            payment1Id = p1.Id;
            harness.LinkPaymentVoucher(payment1Id, 201);
        }

        // Step 7: Confirm Payment 1
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            await handoffService.ConfirmPaymentAsync(payment1Id, "ext-bank-ref-01", "evid-01", "accounting-cashier");
        }

        // Step 8: Accounting prepares Partial Payment 2 ($310)
        long payment2Id;
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var p2 = await handoffService.PreparePaymentAsync(handoffId, 310m, "USD", "BANK", "e2e-pay-02", "accounting-cashier");
            payment2Id = p2.Id;
            harness.LinkPaymentVoucher(payment2Id, 202);
        }

        // Step 9: Payment 2 Fails
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            await handoffService.FailPaymentAsync(payment2Id, "Bank gateway network timeout", "accounting-cashier");
        }

        await using (var db = harness.CreateDb())
        {
            var p2 = await db.AccountingHandoffPayments.SingleAsync(p => p.Id == payment2Id);
            Assert.Equal("FAILED", p2.Status);
            Assert.Equal("Bank gateway network timeout", p2.FailureReason);
            var voidedVoucher = await db.GlVoucherHeaders.SingleAsync(v =>
                v.SourceSystemCode == "HR_PAYMENT" && v.SourceRefId == payment2Id);
            Assert.Equal(4, voidedVoucher.Status);
        }

        // Step 10: Retry Payment 2 as Payment 3 ($310) and Confirm
        long payment3Id;
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var p3 = await handoffService.PreparePaymentAsync(handoffId, 310m, "USD", "BANK", "e2e-pay-03", "accounting-cashier");
            payment3Id = p3.Id;
            harness.LinkPaymentVoucher(payment3Id, 203);
            await handoffService.ConfirmPaymentAsync(payment3Id, "ext-bank-ref-03", "evid-03", "accounting-cashier");
        }

        await using (var db = harness.CreateDb())
        {
            var p3 = await db.AccountingHandoffPayments.SingleAsync(p => p.Id == payment3Id);
            Assert.Equal("CONFIRMED", p3.Status);

            var summary = await harness.CreateHandoffService(db).GetAccountingPaymentSummaryAsync(handoffId);
            Assert.NotNull(summary);
            Assert.Equal(810m, summary.PayableAmount);
            Assert.Equal(810m, summary.PaidAmount);
            Assert.Equal(0m, summary.RemainingAmount);
        }

        // Step 11: Reversing JV while payments are active fails fast
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handoffService.ReverseJournalVoucherAsync(handoffId, DateTime.UtcNow.Date, "Correction requested", "accounting-lead"));
            Assert.Contains("Void prepared payments and reverse every", ex.Message);
        }

        // Step 12: Reverse Payment 3 then Payment 1
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            await handoffService.ReversePaymentAsync(payment3Id, "Payroll error recalculation", "ext-rev-03", "evid-rev-03", "accounting-cashier");
            await handoffService.ReversePaymentAsync(payment1Id, "Payroll error recalculation", "ext-rev-01", "evid-rev-01", "accounting-cashier");
        }

        await using (var db = harness.CreateDb())
        {
            var p1 = await db.AccountingHandoffPayments.SingleAsync(p => p.Id == payment1Id);
            var p3 = await db.AccountingHandoffPayments.SingleAsync(p => p.Id == payment3Id);
            Assert.Equal("REVERSED", p1.Status);
            Assert.Equal("REVERSED", p3.Status);
        }

        // Step 13: Reverse Journal Voucher in Accounting -> restores Advance & Loan
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var reversalVoucherId = await handoffService.ReverseJournalVoucherAsync(handoffId, DateTime.UtcNow.Date, "Full correction needed", "accounting-lead");
            Assert.Equal(8, reversalVoucherId);
        }

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == handoffId);
            Assert.Equal("REVERSED", handoff.DeliveryStatus);

            // Verify Advance restored to exact pre-settlement state
            var advance = await db.EmployeeAdvances.SingleAsync(a => a.Id == 41);
            Assert.Equal(0m, advance.DeductedAmount);
            Assert.Equal(100m, advance.RemainingBalance);
            Assert.Equal("ACTIVE", advance.Status);

            // Verify Loan schedule restored to exact pre-settlement state
            var schedule = await db.LoanRepaymentSchedules.SingleAsync(s => s.Id == 71);
            Assert.Equal(0m, schedule.PaidAmount);
            Assert.Equal("PENDING", schedule.Status);
        }

        // Step 14: HR reads the persisted Accounting outcome before creating the correction.
        // This keeps the payroll decision tied to the handoff lifecycle exercised above.
        string reversedHandoffStatus;
        await using (var db = harness.CreateDb())
        {
            reversedHandoffStatus = (await db.HrAccountingHandoffs
                .Where(h => h.Id == handoffId)
                .Select(h => h.DeliveryStatus)
                .SingleAsync())!;
            Assert.Equal("REVERSED", reversedHandoffStatus);
        }

        // Step 15: HR creates Payroll Correction (Version 2) from the observed status.
        var payrollService = harness.CreatePayrollService(handoffStatus: reversedHandoffStatus);
        var correctionResult = await payrollService.CreatePayrollCorrectionAsync(runId, "Adjusting shift allowances and deductions", "hr-admin");

        Assert.Equal(2, correctionResult.CorrectionVersion);
        Assert.Equal(runId, correctionResult.CorrectionOfRunId);
        Assert.Equal(runId, correctionResult.CorrectionRootRunId);
        Assert.Equal("CALCULATED", correctionResult.Status);
        Assert.Equal("Adjusting shift allowances and deductions", correctionResult.CorrectionReason);

        harness.PayrollPeriodRepo.Verify(r => r.SupersedePayrollCorrectionFamilyAsync(runId, "hr-admin", It.IsAny<CancellationToken>()), Times.Once);
        harness.PayrollPeriodRepo.Verify(r => r.AddPayrollRunAsync(It.Is<PayrollRun>(r => r.CorrectionVersion == 2), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WithdrawalLifecycle_Approve_Withdraw_CorrectRun()
    {
        using var harness = new EndToEndLifecycleHarness();
        await harness.SeedInitialDataAsync();

        const long handoffId = 1002;
        const long runId = 10;

        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = handoffId,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "PAYROLL",
                SourceId = runId.ToString(),
                SourceVersion = 1,
                Operation = "SUBMIT",
                PayPeriod = "2026-11",
                HandoffReference = $"HR-PAY-3-{runId}-1",
                PayloadHash = "e2e-hash-withdraw",
                DeliveryStatus = "DELIVERED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "hr-admin",
                Lines =
                [
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:ADVANCE",
                        CurrencyCode = "USD",
                        Amount = 40m,
                        BranchId = 2,
                        SourceReference = "ADVANCE:41:PAYROLL_LINE:501"
                    }
                ]
            });

            var adv = await db.EmployeeAdvances.SingleAsync(a => a.Id == 41);
            adv.ReservedByHandoffId = handoffId;

            await db.SaveChangesAsync();
        }

        // HR withdraws before Accounting acceptance
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var result = await handoffService.WithdrawPayrollHandoffAsync(runId, "Banking coordinates input error", "hr-admin");
            Assert.NotNull(result);
            Assert.Equal("WITHDRAWN", result.DeliveryStatus);
        }

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == handoffId);
            Assert.Equal("WITHDRAWN", handoff.DeliveryStatus);

            var adv = await db.EmployeeAdvances.SingleAsync(a => a.Id == 41);
            Assert.Null(adv.ReservedByHandoffId);
            Assert.Equal(100m, adv.RemainingBalance);
        }

        // HR creates correction for the withdrawn handoff
        var payrollService = harness.CreatePayrollService(handoffStatus: "WITHDRAWN");
        var correctionResult = await payrollService.CreatePayrollCorrectionAsync(runId, "Correction following withdrawal", "hr-admin");

        Assert.Equal(2, correctionResult.CorrectionVersion);
        Assert.Equal(runId, correctionResult.CorrectionOfRunId);
    }

    [Fact]
    public async Task AccountingReturnLifecycle_Approve_Return_CorrectRun()
    {
        using var harness = new EndToEndLifecycleHarness();
        await harness.SeedInitialDataAsync();

        const long handoffId = 1003;
        const long runId = 10;

        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = handoffId,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "PAYROLL",
                SourceId = runId.ToString(),
                SourceVersion = 1,
                Operation = "SUBMIT",
                PayPeriod = "2026-11",
                HandoffReference = $"HR-PAY-3-{runId}-1",
                PayloadHash = "e2e-hash-return",
                DeliveryStatus = "DELIVERED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "hr-admin",
                Lines =
                [
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:ADVANCE",
                        CurrencyCode = "USD",
                        Amount = 40m,
                        BranchId = 2,
                        SourceReference = "ADVANCE:41:PAYROLL_LINE:501"
                    }
                ]
            });

            var adv = await db.EmployeeAdvances.SingleAsync(a => a.Id == 41);
            adv.ReservedByHandoffId = handoffId;

            await db.SaveChangesAsync();
        }

        // Accounting returns handoff with reason
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            await handoffService.ReviewAsync(handoffId, accept: false, reason: "Branch cost center mismatch", user: "accounting-reviewer");
        }

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == handoffId);
            Assert.Equal("RETURNED", handoff.DeliveryStatus);

            var adv = await db.EmployeeAdvances.SingleAsync(a => a.Id == 41);
            Assert.Null(adv.ReservedByHandoffId);
            Assert.Equal(100m, adv.RemainingBalance);
        }

        // HR creates correction for returned handoff
        var payrollService = harness.CreatePayrollService(handoffStatus: "RETURNED");
        var correctionResult = await payrollService.CreatePayrollCorrectionAsync(runId, "Correction following accounting return", "hr-admin");

        Assert.Equal(2, correctionResult.CorrectionVersion);
        Assert.Equal(runId, correctionResult.CorrectionOfRunId);
    }

    [Fact]
    public async Task EndOfServiceHandoffLifecycle_Approve_Accept_PreparePayment_ConfirmPayment_ReversePayment()
    {
        using var harness = new EndToEndLifecycleHarness();
        await harness.SeedInitialDataAsync();

        const long eosHandoffId = 2001;
        const long eosId = 101;

        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = eosHandoffId,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "EOS",
                SourceId = eosId.ToString(),
                SourceVersion = 1,
                Operation = "SUBMIT",
                HandoffReference = $"HR-EOS-3-{eosId}-1",
                PayloadHash = "e2e-hash-eos",
                DeliveryStatus = "DELIVERED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "hr-admin",
                Lines =
                [
                    new HrAccountingHandoffLine
                    {
                        LineType = "CONTROL:NET_PAY",
                        CurrencyCode = "USD",
                        Amount = 3500m,
                        BranchId = 2,
                        SourceReference = "EOS_NET_PAY"
                    },
                    new HrAccountingHandoffLine
                    {
                        LineType = "EOS_SETTLEMENT",
                        CurrencyCode = "USD",
                        Amount = 3500m,
                        BranchId = 2,
                        SourceReference = $"EOS:{eosId}"
                    }
                ]
            });
            await db.SaveChangesAsync();
        }

        // Step 1: Accounting accepts EOS handoff
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var reviewed = await handoffService.ReviewAsync(eosHandoffId, accept: true, reason: null, user: "accounting-reviewer");
            Assert.Equal("ACCEPTED", reviewed.DeliveryStatus);
        }

        // Step 1.5: Accounting links JV, posts and confirms posting receipt -> DeliveryStatus transitions to POSTED
        await using (var db = harness.CreateDb())
        {
            db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
            {
                HandoffId = eosHandoffId,
                VoucherId = 701,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var handoffService = harness.CreateHandoffService(db);
            await handoffService.ConfirmJournalPostedAsync(eosHandoffId, 701, "evt-eos-jv-01", "accounting-lead");
        }

        // Step 2: Prepare payment
        long eosPaymentId;
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var payment = await handoffService.PreparePaymentAsync(eosHandoffId, 3500m, "USD", "BANK", "eos-pay-key-1", "accounting-cashier");
            eosPaymentId = payment.Id;
            Assert.Equal("PREPARED", payment.Status);
            Assert.Equal(3500m, payment.Amount);
            harness.LinkPaymentVoucher(eosPaymentId, 301);
        }

        // Step 3: Confirm payment with external evidence
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var payment = await handoffService.ConfirmPaymentAsync(eosPaymentId, "BANK-EOS-301", "WIRE-CONF-99", "accounting-cashier");
            Assert.Equal("CONFIRMED", payment.Status);
            Assert.Equal("BANK-EOS-301", payment.ExternalReference);
            Assert.Equal("WIRE-CONF-99", payment.EvidenceReference);
        }

        // Step 4: Verification of payment record in DB
        await using (var db = harness.CreateDb())
        {
            var p = await db.AccountingHandoffPayments.SingleAsync(p => p.Id == eosPaymentId);
            Assert.Equal("CONFIRMED", p.Status);
            Assert.Equal(3500m, p.Amount);
            Assert.Equal("BANK-EOS-301", p.ExternalReference);
            Assert.Equal("WIRE-CONF-99", p.EvidenceReference);

            // Payment summary endpoint rejects non-PAYROLL source type
            var handoffService = harness.CreateHandoffService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handoffService.GetAccountingPaymentSummaryAsync(eosHandoffId));
            Assert.Contains("payroll submission handoffs", ex.Message);
        }

        // Step 5: Reverse payment
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var payment = await handoffService.ReversePaymentAsync(eosPaymentId, "EOS correction", "REV-EOS-301", "REV-WIRE-99", "accounting-cashier");
            Assert.Equal("REVERSED", payment.Status);
        }

        await using (var db = harness.CreateDb())
        {
            var p = await db.AccountingHandoffPayments.SingleAsync(p => p.Id == eosPaymentId);
            Assert.Equal("REVERSED", p.Status);
            Assert.Equal("REV-EOS-301", p.ReversalExternalReference);
            Assert.Equal("REV-WIRE-99", p.ReversalEvidenceReference);
        }
    }

    [Fact]
    public async Task RejectionGuardsDuringLifecycle()
    {
        using var harness = new EndToEndLifecycleHarness();
        await harness.SeedInitialDataAsync();

        const long handoffId = 1004;
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = handoffId,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "PAYROLL",
                SourceId = "10",
                SourceVersion = 1,
                Operation = "SUBMIT",
                PayPeriod = "2026-11",
                HandoffReference = "HR-PAY-3-10-1",
                PayloadHash = "e2e-hash-guards",
                DeliveryStatus = "ACCEPTED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "hr-admin"
            });
            await db.SaveChangesAsync();
        }

        // Guard 1: Cannot withdraw handoff when DeliveryStatus is ACCEPTED
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handoffService.WithdrawPayrollHandoffAsync(10, "Withdraw after accept", "hr-admin"));
            Assert.Contains("withdraw", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // Guard 2: Cannot prepare payment when DeliveryStatus is ACCEPTED (not POSTED)
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handoffService.PreparePaymentAsync(handoffId, 100m, "USD", "BANK", "guard-key-1", "cashier"));
            Assert.Contains("journal posting receipt", ex.Message);
        }

        // Guard 3: Cannot create payroll correction when handoff is in ACCEPTED status
        var payrollService = harness.CreatePayrollService(handoffStatus: "ACCEPTED");
        var corrEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            payrollService.CreatePayrollCorrectionAsync(10, "Correction while accepted", "hr-admin"));
        Assert.Contains("Accounting must return or withdraw", corrEx.Message);

        // Guard 4: Posting receipt throws when voucher does not exist
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                handoffService.ConfirmJournalPostedAsync(handoffId, 9999L, "evt-missing", "poster"));
            Assert.Contains("Journal voucher was not found", ex.Message);
        }

        // Guard 5: Confirm payment throws when references are whitespace
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                handoffService.ConfirmPaymentAsync(10, "   ", "evid", "cashier"));
            Assert.Contains("evidence reference are required", ex.Message);
        }
    }

    [Fact]
    public async Task FullFinancialLifecycle_WithPartialScheduleSettlement_AndMultipleLoansAdvances()
    {
        using var harness = new EndToEndLifecycleHarness();
        await harness.SeedInitialDataAsync();

        const long handoffId = 1005;

        // Seed a second advance and update schedule 71 to 100m scheduled
        await using (var db = harness.CreateDb())
        {
            var sch71 = await db.LoanRepaymentSchedules.SingleAsync(s => s.Id == 71);
            sch71.ScheduledAmount = 100m;

            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 42,
                EmployeeCode = "E-501",
                AdvanceAmount = 50m,
                CurrencyCode = "USD",
                TargetPayPeriod = "2026-11",
                DeductedAmount = 0m,
                RemainingBalance = 50m,
                Status = "ACTIVE",
                CreationUser = "seed",
                CreationDate = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = handoffId,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "PAYROLL",
                SourceId = "10",
                SourceVersion = 1,
                Operation = "SUBMIT",
                PayPeriod = "2026-11",
                HandoffReference = "HR-PAY-3-10-5",
                PayloadHash = "e2e-hash-multi",
                DeliveryStatus = "DELIVERED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "hr-admin",
                Lines =
                [
                    new HrAccountingHandoffLine
                    {
                        LineType = "CONTROL:NET_PAY",
                        CurrencyCode = "USD",
                        Amount = 750m,
                        BranchId = 2,
                        SourceReference = "NET_PAY"
                    },
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:ADVANCE",
                        CurrencyCode = "USD",
                        Amount = 40m,
                        BranchId = 2,
                        SourceReference = "ADVANCE:41:PAYROLL_LINE:501"
                    },
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:ADVANCE",
                        CurrencyCode = "USD",
                        Amount = 50m,
                        BranchId = 2,
                        SourceReference = "ADVANCE:42:PAYROLL_LINE:501"
                    },
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:SCHEDULE",
                        CurrencyCode = "USD",
                        Amount = 60m,
                        BranchId = 2,
                        SourceReference = "SCHEDULE:71:PAYROLL_LINE:501"
                    }
                ]
            });

            var a1 = await db.EmployeeAdvances.SingleAsync(a => a.Id == 41);
            a1.ReservedByHandoffId = handoffId;

            var a2 = await db.EmployeeAdvances.SingleAsync(a => a.Id == 42);
            a2.ReservedByHandoffId = handoffId;

            var s1 = await db.LoanRepaymentSchedules.SingleAsync(s => s.Id == 71);
            s1.ReservedByHandoffId = handoffId;

            db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
            {
                HandoffId = handoffId,
                VoucherId = 705,
                CreatedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        // Accept handoff
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            await handoffService.ReviewAsync(handoffId, accept: true, reason: null, user: "reviewer");
        }

        // Confirm JV posted -> settles both advances (one partial, one full) and loan schedule (partial with carried forward)
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            await handoffService.ConfirmJournalPostedAsync(handoffId, 705, "multi-post-evt-1", "poster");
        }

        // Verify state after multi settlement
        await using (var db = harness.CreateDb())
        {
            var a1 = await db.EmployeeAdvances.SingleAsync(a => a.Id == 41);
            Assert.Equal(40m, a1.DeductedAmount);
            Assert.Equal(60m, a1.RemainingBalance);
            Assert.Equal("ACTIVE", a1.Status);
            Assert.Null(a1.ReservedByHandoffId);

            var a2 = await db.EmployeeAdvances.SingleAsync(a => a.Id == 42);
            Assert.Equal(50m, a2.DeductedAmount);
            Assert.Equal(0m, a2.RemainingBalance);
            Assert.Equal("RECOVERED", a2.Status);
            Assert.Null(a2.ReservedByHandoffId);

            var s1 = await db.LoanRepaymentSchedules.SingleAsync(s => s.Id == 71);
            Assert.Equal(60m, s1.PaidAmount);
            Assert.Equal(40m, s1.CarriedForwardAmount);
            Assert.Equal("PARTIAL", s1.Status);
            Assert.Null(s1.ReservedByHandoffId);

            var loan = await db.EmployeeLoans.SingleAsync(l => l.Id == 31);
            Assert.Equal(60m, loan.TotalPaidAmount);
            Assert.Equal(140m, loan.RemainingBalance);
            Assert.Equal("ACTIVE", loan.Status);
        }

        // Reverse Journal Voucher -> restores all 3 records to exact pre-settlement state
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            await handoffService.ReverseJournalVoucherAsync(handoffId, DateTime.UtcNow.Date, "Multi settlement reversal", "auditor");
        }

        // Verify restoration after JV reversal
        await using (var db = harness.CreateDb())
        {
            var a1 = await db.EmployeeAdvances.SingleAsync(a => a.Id == 41);
            Assert.Equal(0m, a1.DeductedAmount);
            Assert.Equal(100m, a1.RemainingBalance);
            Assert.Equal("ACTIVE", a1.Status);

            var a2 = await db.EmployeeAdvances.SingleAsync(a => a.Id == 42);
            Assert.Equal(0m, a2.DeductedAmount);
            Assert.Equal(50m, a2.RemainingBalance);
            Assert.Equal("ACTIVE", a2.Status);

            var s1 = await db.LoanRepaymentSchedules.SingleAsync(s => s.Id == 71);
            Assert.Equal(0m, s1.PaidAmount);
            Assert.Equal(0m, s1.CarriedForwardAmount);
            Assert.Equal("PENDING", s1.Status);

            var loan = await db.EmployeeLoans.SingleAsync(l => l.Id == 31);
            Assert.Equal(0m, loan.TotalPaidAmount);
            Assert.Equal(200m, loan.RemainingBalance);
            Assert.Equal("ACTIVE", loan.Status);
        }
    }

    [Fact]
    public async Task MultiVoucherPaymentLifecycle_PaymentSummaryAndReversals_EndToEnd()
    {
        using var harness = new EndToEndLifecycleHarness();
        await harness.SeedInitialDataAsync();

        const long handoffId = 1001;
        const long runId = 10;

        await using (var db = harness.CreateDb())
        {
            var handoff = new HrAccountingHandoff
            {
                Id = handoffId,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "PAYROLL",
                SourceId = runId.ToString(),
                SourceVersion = 1,
                Operation = "SUBMIT",
                PayPeriod = "2026-11",
                HandoffReference = $"HR-PAY-3-{runId}-1",
                PayloadHash = "multi-voucher-hash-01",
                DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "hr-admin",
                Lines =
                [
                    new HrAccountingHandoffLine
                    {
                        LineType = "CONTROL:NET_PAY",
                        CurrencyCode = "USD",
                        Amount = 810m,
                        BranchId = 2,
                        SourceReference = "NET_PAY"
                    }
                ]
            };
            db.HrAccountingHandoffs.Add(handoff);

            db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
            {
                HandoffId = handoffId,
                VoucherId = 7,
                CreatedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        // Step 1: Initial Payment Summary verification (UNPAID)
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var summary = await handoffService.GetAccountingPaymentSummaryAsync(handoffId);
            Assert.NotNull(summary);
            Assert.Equal(810m, summary!.PayableAmount);
            Assert.Equal(0m, summary.PreparedAmount);
            Assert.Equal(0m, summary.PaidAmount);
            Assert.Equal(0m, summary.ReversedAmount);
            Assert.Equal(810m, summary.RemainingAmount);
            Assert.Empty(summary.Payments);

            var payrollStatus = await handoffService.GetPayrollHandoffPaymentStatusAsync(runId);
            Assert.NotNull(payrollStatus);
            Assert.Equal("UNPAID", payrollStatus!.PaymentStatus);
        }

        // Step 2: Prepare First Payment Voucher ($500)
        long payment1Id;
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var p1 = await handoffService.PreparePaymentAsync(handoffId, 500m, "USD", "BANK", "idemp-batch-001", "cashier");
            payment1Id = p1.Id;
            harness.LinkPaymentVoucher(payment1Id, 201);

            var summary = await handoffService.GetAccountingPaymentSummaryAsync(handoffId);
            Assert.NotNull(summary);
            Assert.Equal(500m, summary!.PreparedAmount);
            Assert.Equal(0m, summary.PaidAmount);
            Assert.Equal(310m, summary.RemainingAmount);

            var payrollStatus = await handoffService.GetPayrollHandoffPaymentStatusAsync(runId);
            Assert.NotNull(payrollStatus);
            Assert.Equal("UNPAID", payrollStatus!.PaymentStatus); // Only prepared, nothing confirmed yet
        }

        // Step 3: Confirm First Payment Voucher ($500) -> PARTIAL
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            await handoffService.ConfirmPaymentAsync(payment1Id, "EXT-PAY-BATCH-01", "EVID-BATCH-01", "cashier");

            var summary = await handoffService.GetAccountingPaymentSummaryAsync(handoffId);
            Assert.NotNull(summary);
            Assert.Equal(0m, summary!.PreparedAmount);
            Assert.Equal(500m, summary.PaidAmount);
            Assert.Equal(310m, summary.RemainingAmount);

            var payrollStatus = await handoffService.GetPayrollHandoffPaymentStatusAsync(runId);
            Assert.NotNull(payrollStatus);
            Assert.Equal("PARTIAL", payrollStatus!.PaymentStatus);
        }

        // Step 4: Prepare and Confirm Second Payment Voucher ($310) -> PAID
        long payment2Id;
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var p2 = await handoffService.PreparePaymentAsync(handoffId, 310m, "USD", "BANK", "idemp-batch-002", "cashier");
            payment2Id = p2.Id;
            harness.LinkPaymentVoucher(payment2Id, 203);

            await handoffService.ConfirmPaymentAsync(payment2Id, "EXT-PAY-BATCH-02", "EVID-BATCH-02", "cashier");

            var summary = await handoffService.GetAccountingPaymentSummaryAsync(handoffId);
            Assert.NotNull(summary);
            Assert.Equal(0m, summary!.PreparedAmount);
            Assert.Equal(810m, summary.PaidAmount);
            Assert.Equal(0m, summary.RemainingAmount);
            Assert.Equal(2, summary.Payments.Count);

            var payrollStatus = await handoffService.GetPayrollHandoffPaymentStatusAsync(runId);
            Assert.NotNull(payrollStatus);
            Assert.Equal("PAID", payrollStatus!.PaymentStatus);
        }

        // Step 5: Guard against exceeding total payable
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handoffService.PreparePaymentAsync(handoffId, 10m, "USD", "BANK", "idemp-batch-003", "cashier"));
            Assert.Contains("Payment amount exceeds the remaining payable", ex.Message);
        }

        // Step 6: Reverse Second Payment Voucher ($310) -> Returns to PARTIAL
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            await handoffService.ReversePaymentAsync(payment2Id, "Payroll error recalculation", "EXT-REV-BATCH-02", "EVID-REV-BATCH-02", "accounting-cashier");

            var summary = await handoffService.GetAccountingPaymentSummaryAsync(handoffId);
            Assert.NotNull(summary);
            Assert.Equal(0m, summary!.PreparedAmount);
            Assert.Equal(500m, summary.PaidAmount);
            Assert.Equal(310m, summary.ReversedAmount);
            Assert.Equal(310m, summary.RemainingAmount);

            var payrollStatus = await handoffService.GetPayrollHandoffPaymentStatusAsync(runId);
            Assert.NotNull(payrollStatus);
            Assert.Equal("PARTIAL", payrollStatus!.PaymentStatus);
        }

        // Step 7: Reverse First Payment Voucher ($500) -> Status becomes REVERSED
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            await handoffService.ReversePaymentAsync(payment1Id, "Payroll error recalculation", "EXT-REV-BATCH-01", "EVID-REV-BATCH-01", "accounting-cashier");

            var summary = await handoffService.GetAccountingPaymentSummaryAsync(handoffId);
            Assert.NotNull(summary);
            Assert.Equal(0m, summary!.PreparedAmount);
            Assert.Equal(0m, summary.PaidAmount);
            Assert.Equal(810m, summary.ReversedAmount);
            Assert.Equal(810m, summary.RemainingAmount);

            var payrollStatus = await handoffService.GetPayrollHandoffPaymentStatusAsync(runId);
            Assert.NotNull(payrollStatus);
            Assert.Equal("REVERSED", payrollStatus!.PaymentStatus);
        }
    }

    [Fact]
    public async Task ReturnWorkflowAndReReview_EndToEndLifecycle_TransitionAuditing()
    {
        using var harness = new EndToEndLifecycleHarness();
        await harness.SeedInitialDataAsync();

        const long handoffId = 1002;
        const long runId = 10;

        await using (var db = harness.CreateDb())
        {
            var handoff = new HrAccountingHandoff
            {
                Id = handoffId,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "PAYROLL",
                SourceId = runId.ToString(),
                SourceVersion = 1,
                Operation = "SUBMIT",
                PayPeriod = "2026-11",
                HandoffReference = $"HR-PAY-3-{runId}-1",
                PayloadHash = "return-audit-hash-01",
                DeliveryStatus = "DELIVERED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "hr-admin",
                Lines =
                [
                    new HrAccountingHandoffLine
                    {
                        LineType = "CONTROL:NET_PAY",
                        CurrencyCode = "USD",
                        Amount = 810m,
                        BranchId = 2,
                        SourceReference = "NET_PAY"
                    }
                ]
            };
            db.HrAccountingHandoffs.Add(handoff);
            await db.SaveChangesAsync();
        }

        // Guard: Cannot create JV or prepare payment when handoff is only DELIVERED
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var exJv = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handoffService.CreateJournalVoucherAsync(handoffId, "accountant"));
            Assert.Contains("Only an Accounting-accepted handoff can create a journal voucher", exJv.Message);

            var exPay = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handoffService.PreparePaymentAsync(handoffId, 100m, "USD", "BANK", "idemp-fail-01", "accountant"));
            Assert.Contains("Payment can be prepared only after the journal posting receipt", exPay.Message);
        }

        // Step 1: Accounting returns handoff with explicit audit reason
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            await handoffService.ReviewAsync(handoffId, accept: false, reason: "Missing cost center allocation details", user: "chief-accountant");
        }

        // Verify RETURNED status and transition record
        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.Include(h => h.Transitions).SingleAsync(h => h.Id == handoffId);
            Assert.Equal("RETURNED", handoff.DeliveryStatus);
            var transitions = handoff.Transitions.OrderBy(t => t.OccurredAtUtc).ToList();
            Assert.Single(transitions);
            Assert.Equal("DELIVERED", transitions[0].FromStatus);
            Assert.Equal("RETURNED", transitions[0].ToStatus);
            Assert.Equal("chief-accountant", transitions[0].Actor);
            Assert.Equal("Missing cost center allocation details", transitions[0].Reason);
        }

        // Guard: While RETURNED, cannot withdraw from HR, confirm JV, or re-review from Accounting
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var exWith = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handoffService.WithdrawPayrollHandoffAsync(runId, "Withdraw attempt", "hr-admin"));
            Assert.Contains("Payroll handoff can be withdrawn only before Accounting accepts it", exWith.Message);

            var exConfirm = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handoffService.ConfirmJournalPostedAsync(handoffId, 7, "confirm-evt-01", "accountant"));
            Assert.Contains("Journal voucher is not linked to this handoff", exConfirm.Message);

            var exReReview = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handoffService.ReviewAsync(handoffId, accept: true, reason: null, user: "chief-accountant"));
            Assert.Contains("Only a delivered handoff can be reviewed", exReReview.Message);
        }

        // Verify permanent RETURNED status and immutable audit record
        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.Include(h => h.Transitions).SingleAsync(h => h.Id == handoffId);
            Assert.Equal("RETURNED", handoff.DeliveryStatus);
            var transitions = handoff.Transitions.OrderBy(t => t.OccurredAtUtc).ToList();
            Assert.Single(transitions);
            Assert.Equal("DELIVERED", transitions[0].FromStatus);
            Assert.Equal("RETURNED", transitions[0].ToStatus);
            Assert.Equal("chief-accountant", transitions[0].Actor);
            Assert.Equal("Missing cost center allocation details", transitions[0].Reason);
        }
    }

    [Fact]
    public async Task PaymentFailureAndRePreparation_EndToEndLifecycle()
    {
        using var harness = new EndToEndLifecycleHarness();
        await harness.SeedInitialDataAsync();

        const long handoffId = 1001;
        const long runId = 10;

        await using (var db = harness.CreateDb())
        {
            var handoff = new HrAccountingHandoff
            {
                Id = handoffId,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "PAYROLL",
                SourceId = runId.ToString(),
                SourceVersion = 1,
                Operation = "SUBMIT",
                PayPeriod = "2026-11",
                HandoffReference = $"HR-PAY-3-{runId}-6",
                PayloadHash = "fail-reprep-hash-01",
                DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "hr-admin",
                Lines =
                [
                    new HrAccountingHandoffLine
                    {
                        LineType = "CONTROL:NET_PAY",
                        CurrencyCode = "USD",
                        Amount = 810m,
                        BranchId = 2,
                        SourceReference = "NET_PAY"
                    }
                ]
            };
            db.HrAccountingHandoffs.Add(handoff);

            db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
            {
                HandoffId = handoffId,
                VoucherId = 7,
                CreatedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        // Step 1: Prepare payment of 310 USD
        long paymentId;
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var payment = await handoffService.PreparePaymentAsync(handoffId, 310m, "USD", "BANK", "fail-reprep-key-1", "cashier");
            paymentId = payment.Id;
            Assert.Equal("PREPARED", payment.Status);
            harness.LinkPaymentVoucher(paymentId, 202);

            var summary = await handoffService.GetAccountingPaymentSummaryAsync(handoffId);
            Assert.NotNull(summary);
            Assert.Equal(310m, summary!.PreparedAmount);
            Assert.Equal(500m, summary.RemainingAmount);
        }

        // Step 2: Payment fails due to bank technical issue
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var failedPayment = await handoffService.FailPaymentAsync(paymentId, "Bank system rejection", "cashier");
            Assert.Equal("FAILED", failedPayment.Status);

            var summary = await handoffService.GetAccountingPaymentSummaryAsync(handoffId);
            Assert.NotNull(summary);
            Assert.Equal(0m, summary!.PreparedAmount);
            Assert.Equal(0m, summary.PaidAmount);
            Assert.Equal(810m, summary.RemainingAmount);
        }

        // Step 3: Cashier re-prepares payment for 500 USD
        long newPaymentId;
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var newPayment = await handoffService.PreparePaymentAsync(handoffId, 500m, "USD", "BANK", "fail-reprep-key-2", "cashier");
            newPaymentId = newPayment.Id;
            Assert.Equal("PREPARED", newPayment.Status);
            harness.LinkPaymentVoucher(newPaymentId, 201);
        }

        // Step 4: Confirm successful payment
        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);
            var confirmedPayment = await handoffService.ConfirmPaymentAsync(newPaymentId, "EXT-WIRE-SUCCESS-01", "EVID-WIRE-SUCCESS-01", "cashier");
            Assert.Equal("CONFIRMED", confirmedPayment.Status);

            var summary = await handoffService.GetAccountingPaymentSummaryAsync(handoffId);
            Assert.NotNull(summary);
            Assert.Equal(0m, summary!.PreparedAmount);
            Assert.Equal(500m, summary.PaidAmount);
            Assert.Equal(310m, summary.RemainingAmount);

            var payrollStatus = await handoffService.GetPayrollHandoffPaymentStatusAsync(runId);
            Assert.NotNull(payrollStatus);
            Assert.Equal("PARTIAL", payrollStatus!.PaymentStatus);
        }
    }

    [Fact]
    public async Task MixedCurrencyHandoff_EndToEndRejection_RejectsBeforePayment()
    {
        using var harness = new EndToEndLifecycleHarness();
        await harness.SeedInitialDataAsync();

        const long handoffId = 1007;
        const long runId = 10;

        await using (var db = harness.CreateDb())
        {
            var handoff = new HrAccountingHandoff
            {
                Id = handoffId,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "PAYROLL",
                SourceId = runId.ToString(),
                SourceVersion = 1,
                Operation = "SUBMIT",
                PayPeriod = "2026-11",
                HandoffReference = $"HR-PAY-3-{runId}-7",
                PayloadHash = "mixed-curr-hash-01",
                DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "hr-admin",
                Lines =
                [
                    new HrAccountingHandoffLine
                    {
                        LineType = "CONTROL:NET_PAY",
                        CurrencyCode = "USD",
                        Amount = 500m,
                        BranchId = 2,
                        SourceReference = "NET_PAY_USD"
                    },
                    new HrAccountingHandoffLine
                    {
                        LineType = "CONTROL:NET_PAY",
                        CurrencyCode = "EUR",
                        Amount = 300m,
                        BranchId = 2,
                        SourceReference = "NET_PAY_EUR"
                    }
                ]
            };
            db.HrAccountingHandoffs.Add(handoff);
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var handoffService = harness.CreateHandoffService(db);

            // Rejection 1: PreparePaymentAsync throws due to multi-currency lines
            var exPay = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handoffService.PreparePaymentAsync(handoffId, 500m, "USD", "BANK", "mixed-curr-key-1", "cashier"));
            Assert.Contains("Payment currency does not match the approved handoff currency.", exPay.Message);

            // Rejection 2: GetAccountingPaymentSummaryAsync throws due to multi-currency lines
            var exSum = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handoffService.GetAccountingPaymentSummaryAsync(handoffId));
            Assert.Contains("Payroll handoff has no single-currency net-pay amount.", exSum.Message);
        }
    }

    private sealed class EndToEndLifecycleHarness : IDisposable
    {
        private readonly DbContextOptions<OracleDbContext> options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;

        public Mock<IGlVoucherRepository> VoucherRepo { get; } = new();
        public Mock<IGlVoucherService> GlVouchers { get; } = new();
        public Mock<IReceiptPaymentVoucherService> ReceiptPayments { get; } = new();
        public Mock<IPayrollPeriodRepository> PayrollPeriodRepo { get; } = new();
        public Mock<IPayrollCalculationEngine> CalculationEngine { get; } = new();
        public Mock<IPayrollValidationService> ValidationService { get; } = new();
        public Mock<IHrUnitOfWork> UnitOfWork { get; } = new();

        private bool jvReversalCreated;
        private bool multiJvReversalCreated;
        private readonly Dictionary<long, long> paymentToVoucherMap = new();

        private readonly GlVoucherHeader originalJournal = new()
        {
            Id = 7,
            BranchId = 2,
            FiscalYearId = 1,
            VoucherType = 1,
            VoucherNo = 1001,
            SourceSystemCode = "HR_HANDOFF",
            SourceRefId = 1001,
            Status = 3, // Posted
            PostDate = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc),
            TotalAmount = 900m,
            TotalLocalDebit = 900m,
            TotalLocalCredit = 900m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 7, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "SALARY-EXP", Debit = 900m, LocalDebit = 900m, BaseDebit = 900m },
                new GlVoucherDetail { VoucherId = 7, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "ADVANCE-REC", Credit = 40m, LocalCredit = 40m, BaseCredit = 40m },
                new GlVoucherDetail { VoucherId = 7, LineSer = 3, BranchId = 2, CurrencyId = 9, AccountCode = "NET-PAYABLE", Credit = 810m, LocalCredit = 810m, BaseCredit = 810m },
                new GlVoucherDetail { VoucherId = 7, LineSer = 4, BranchId = 2, CurrencyId = 9, AccountCode = "LOAN-REC", Credit = 50m, LocalCredit = 50m, BaseCredit = 50m }
            ]
        };

        private readonly GlVoucherHeader reversalJournal = new()
        {
            Id = 8,
            BranchId = 2,
            FiscalYearId = 1,
            VoucherType = 1,
            VoucherNo = 1002,
            SourceSystemCode = "GL_REVERSAL",
            SourceRefId = 7,
            Status = 3, // Posted
            PostDate = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc),
            TotalAmount = 900m,
            TotalLocalDebit = 900m,
            TotalLocalCredit = 900m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 8, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "SALARY-EXP", Credit = 900m, LocalCredit = 900m, BaseCredit = 900m },
                new GlVoucherDetail { VoucherId = 8, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "ADVANCE-REC", Debit = 40m, LocalDebit = 40m, BaseDebit = 40m },
                new GlVoucherDetail { VoucherId = 8, LineSer = 3, BranchId = 2, CurrencyId = 9, AccountCode = "NET-PAYABLE", Debit = 810m, LocalDebit = 810m, BaseDebit = 810m },
                new GlVoucherDetail { VoucherId = 8, LineSer = 4, BranchId = 2, CurrencyId = 9, AccountCode = "LOAN-REC", Debit = 50m, LocalDebit = 50m, BaseDebit = 50m }
            ]
        };

        private readonly GlVoucherHeader eosJournal = new()
        {
            Id = 701,
            BranchId = 2,
            FiscalYearId = 1,
            VoucherType = 1,
            VoucherNo = 7001,
            SourceSystemCode = "HR_HANDOFF",
            SourceRefId = 2001,
            Status = 3, // Posted
            PostDate = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc),
            TotalAmount = 3500m,
            TotalLocalDebit = 3500m,
            TotalLocalCredit = 3500m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 701, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "SALARY-EXP", Debit = 3500m, LocalDebit = 3500m, BaseDebit = 3500m },
                new GlVoucherDetail { VoucherId = 701, LineSer = 3, BranchId = 2, CurrencyId = 9, AccountCode = "NET-PAYABLE", Credit = 3500m, LocalCredit = 3500m, BaseCredit = 3500m }
            ]
        };

        private readonly GlVoucherHeader multiJournal = new()
        {
            Id = 705,
            BranchId = 2,
            FiscalYearId = 1,
            VoucherType = 1,
            VoucherNo = 7005,
            SourceSystemCode = "HR_HANDOFF",
            SourceRefId = 1005,
            Status = 3, // Posted
            PostDate = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc),
            TotalAmount = 900m,
            TotalLocalDebit = 900m,
            TotalLocalCredit = 900m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 705, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "SALARY-EXP", Debit = 900m, LocalDebit = 900m, BaseDebit = 900m },
                new GlVoucherDetail { VoucherId = 705, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "ADVANCE-REC", Credit = 90m, LocalCredit = 90m, BaseCredit = 90m },
                new GlVoucherDetail { VoucherId = 705, LineSer = 3, BranchId = 2, CurrencyId = 9, AccountCode = "NET-PAYABLE", Credit = 750m, LocalCredit = 750m, BaseCredit = 750m },
                new GlVoucherDetail { VoucherId = 705, LineSer = 4, BranchId = 2, CurrencyId = 9, AccountCode = "LOAN-REC", Credit = 60m, LocalCredit = 60m, BaseCredit = 60m }
            ]
        };

        private readonly GlVoucherHeader multiReversalJournal = new()
        {
            Id = 805,
            BranchId = 2,
            FiscalYearId = 1,
            VoucherType = 1,
            VoucherNo = 8005,
            SourceSystemCode = "GL_REVERSAL",
            SourceRefId = 705,
            Status = 3, // Posted
            PostDate = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc),
            TotalAmount = 900m,
            TotalLocalDebit = 900m,
            TotalLocalCredit = 900m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 805, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "SALARY-EXP", Credit = 900m, LocalCredit = 900m, BaseCredit = 900m },
                new GlVoucherDetail { VoucherId = 805, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "ADVANCE-REC", Debit = 90m, LocalDebit = 90m, BaseDebit = 90m },
                new GlVoucherDetail { VoucherId = 805, LineSer = 3, BranchId = 2, CurrencyId = 9, AccountCode = "NET-PAYABLE", Debit = 750m, LocalDebit = 750m, BaseDebit = 750m },
                new GlVoucherDetail { VoucherId = 805, LineSer = 4, BranchId = 2, CurrencyId = 9, AccountCode = "LOAN-REC", Debit = 60m, LocalDebit = 60m, BaseDebit = 60m }
            ]
        };

        private readonly GlVoucherHeader pv1 = new()
        {
            Id = 201,
            BranchId = 2,
            FiscalYearId = 1,
            VoucherType = 103,
            VoucherNo = 2001,
            SourceSystemCode = "HR_PAYMENT",
            Status = 3,
            PostDate = new DateTime(2026, 11, 7, 12, 30, 0, DateTimeKind.Utc),
            TotalAmount = 500m,
            TotalLocalDebit = 500m,
            TotalLocalCredit = 500m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 201, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "NET-PAYABLE", Debit = 500m, LocalDebit = 500m, BaseDebit = 500m },
                new GlVoucherDetail { VoucherId = 201, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "BANK-ACC", Credit = 500m, LocalCredit = 500m, BaseCredit = 500m }
            ]
        };

        private readonly GlVoucherHeader pv1Rev = new()
        {
            Id = 211,
            BranchId = 2,
            FiscalYearId = 1,
            VoucherType = 103,
            VoucherNo = 2011,
            SourceSystemCode = "GL_REVERSAL",
            SourceRefId = 201,
            Status = 3,
            PostDate = new DateTime(2026, 11, 7, 13, 0, 0, DateTimeKind.Utc),
            TotalAmount = 500m,
            TotalLocalDebit = 500m,
            TotalLocalCredit = 500m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 211, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "NET-PAYABLE", Credit = 500m, LocalCredit = 500m, BaseCredit = 500m },
                new GlVoucherDetail { VoucherId = 211, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "BANK-ACC", Debit = 500m, LocalDebit = 500m, BaseDebit = 500m }
            ]
        };

        private readonly GlVoucherHeader pvDraft2 = new()
        {
            Id = 202,
            BranchId = 2,
            FiscalYearId = 1,
            VoucherType = 103,
            VoucherNo = 2002,
            SourceSystemCode = "HR_PAYMENT",
            Status = 1, // Draft
            TotalAmount = 310m,
            TotalLocalDebit = 310m,
            TotalLocalCredit = 310m
        };

        private readonly GlVoucherHeader pv3 = new()
        {
            Id = 203,
            BranchId = 2,
            FiscalYearId = 1,
            VoucherType = 103,
            VoucherNo = 2003,
            SourceSystemCode = "HR_PAYMENT",
            Status = 3,
            PostDate = new DateTime(2026, 11, 7, 13, 30, 0, DateTimeKind.Utc),
            TotalAmount = 310m,
            TotalLocalDebit = 310m,
            TotalLocalCredit = 310m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 203, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "NET-PAYABLE", Debit = 310m, LocalDebit = 310m, BaseDebit = 310m },
                new GlVoucherDetail { VoucherId = 203, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "BANK-ACC", Credit = 310m, LocalCredit = 310m, BaseCredit = 310m }
            ]
        };

        private readonly GlVoucherHeader pv3Rev = new()
        {
            Id = 213,
            BranchId = 2,
            FiscalYearId = 1,
            VoucherType = 103,
            VoucherNo = 2013,
            SourceSystemCode = "GL_REVERSAL",
            SourceRefId = 203,
            Status = 3,
            PostDate = new DateTime(2026, 11, 7, 14, 0, 0, DateTimeKind.Utc),
            TotalAmount = 310m,
            TotalLocalDebit = 310m,
            TotalLocalCredit = 310m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 213, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "NET-PAYABLE", Credit = 310m, LocalCredit = 310m, BaseCredit = 310m },
                new GlVoucherDetail { VoucherId = 213, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "BANK-ACC", Debit = 310m, LocalDebit = 310m, BaseDebit = 310m }
            ]
        };

        private readonly GlVoucherHeader pvEos = new()
        {
            Id = 301,
            BranchId = 2,
            FiscalYearId = 1,
            VoucherType = 103,
            VoucherNo = 3001,
            SourceSystemCode = "HR_PAYMENT",
            Status = 3,
            PostDate = new DateTime(2026, 11, 7, 15, 0, 0, DateTimeKind.Utc),
            TotalAmount = 3500m,
            TotalLocalDebit = 3500m,
            TotalLocalCredit = 3500m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 301, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "NET-PAYABLE", Debit = 3500m, LocalDebit = 3500m, BaseDebit = 3500m },
                new GlVoucherDetail { VoucherId = 301, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "BANK-ACC", Credit = 3500m, LocalCredit = 3500m, BaseCredit = 3500m }
            ]
        };

        private readonly GlVoucherHeader pvEosRev = new()
        {
            Id = 311,
            BranchId = 2,
            FiscalYearId = 1,
            VoucherType = 103,
            VoucherNo = 3011,
            SourceSystemCode = "GL_REVERSAL",
            SourceRefId = 301,
            Status = 3,
            PostDate = new DateTime(2026, 11, 7, 16, 0, 0, DateTimeKind.Utc),
            TotalAmount = 3500m,
            TotalLocalDebit = 3500m,
            TotalLocalCredit = 3500m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 311, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "NET-PAYABLE", Credit = 3500m, LocalCredit = 3500m, BaseCredit = 3500m },
                new GlVoucherDetail { VoucherId = 311, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "BANK-ACC", Debit = 3500m, LocalDebit = 3500m, BaseDebit = 3500m }
            ]
        };

        public void LinkPaymentVoucher(long paymentId, long voucherId)
        {
            paymentToVoucherMap[paymentId] = voucherId;
            if (voucherId == 201) pv1.SourceRefId = paymentId;
            else if (voucherId == 202) pvDraft2.SourceRefId = paymentId;
            else if (voucherId == 203) pv3.SourceRefId = paymentId;
            else if (voucherId == 301) pvEos.SourceRefId = paymentId;
        }

        public EndToEndLifecycleHarness()
        {
            VoucherRepo.Setup(v => v.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(originalJournal);
            VoucherRepo.Setup(v => v.GetByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(reversalJournal);
            VoucherRepo.Setup(v => v.GetByIdAsync(701, It.IsAny<CancellationToken>())).ReturnsAsync(eosJournal);
            VoucherRepo.Setup(v => v.GetByIdAsync(705, It.IsAny<CancellationToken>())).ReturnsAsync(multiJournal);
            VoucherRepo.Setup(v => v.GetByIdAsync(805, It.IsAny<CancellationToken>())).ReturnsAsync(multiReversalJournal);
            VoucherRepo.Setup(v => v.GetByIdAsync(201, It.IsAny<CancellationToken>())).ReturnsAsync(pv1);
            VoucherRepo.Setup(v => v.GetByIdAsync(211, It.IsAny<CancellationToken>())).ReturnsAsync(pv1Rev);
            VoucherRepo.Setup(v => v.GetByIdAsync(202, It.IsAny<CancellationToken>())).ReturnsAsync(pvDraft2);
            VoucherRepo.Setup(v => v.GetByIdAsync(203, It.IsAny<CancellationToken>())).ReturnsAsync(pv3);
            VoucherRepo.Setup(v => v.GetByIdAsync(213, It.IsAny<CancellationToken>())).ReturnsAsync(pv3Rev);
            VoucherRepo.Setup(v => v.GetByIdAsync(301, It.IsAny<CancellationToken>())).ReturnsAsync(pvEos);
            VoucherRepo.Setup(v => v.GetByIdAsync(311, It.IsAny<CancellationToken>())).ReturnsAsync(pvEosRev);

            VoucherRepo.Setup(v => v.GetBySourceAsync(2, "HR_HANDOFF", 1001, It.IsAny<CancellationToken>()))
                .ReturnsAsync([originalJournal]);
            VoucherRepo.Setup(v => v.GetBySourceAsync(2, "HR_HANDOFF", 1005, It.IsAny<CancellationToken>()))
                .ReturnsAsync([multiJournal]);
            VoucherRepo.Setup(v => v.GetBySourceAsync(2, "GL_REVERSAL", 7, It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<IReadOnlyList<GlVoucherHeader>>(jvReversalCreated ? [reversalJournal] : Array.Empty<GlVoucherHeader>()));
            VoucherRepo.Setup(v => v.GetBySourceAsync(2, "GL_REVERSAL", 705, It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<IReadOnlyList<GlVoucherHeader>>(multiJvReversalCreated ? [multiReversalJournal] : Array.Empty<GlVoucherHeader>()));

            VoucherRepo.Setup(v => v.GetBySourceAsync(2, "HR_PAYMENT", It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Returns<long, string, long, CancellationToken>((_, _, paymentId, _) =>
                {
                    if (paymentToVoucherMap.TryGetValue(paymentId, out var vId))
                    {
                        if (vId == 201) return Task.FromResult<IReadOnlyList<GlVoucherHeader>>([pv1]);
                        if (vId == 202) return Task.FromResult<IReadOnlyList<GlVoucherHeader>>([pvDraft2]);
                        if (vId == 203) return Task.FromResult<IReadOnlyList<GlVoucherHeader>>([pv3]);
                        if (vId == 301) return Task.FromResult<IReadOnlyList<GlVoucherHeader>>([pvEos]);
                    }
                    return Task.FromResult<IReadOnlyList<GlVoucherHeader>>(Array.Empty<GlVoucherHeader>());
                });

            GlVouchers.Setup(v => v.ReverseVoucherAsync(7, "accounting-lead", "Full correction needed", It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    originalJournal.IsReversed = true;
                    jvReversalCreated = true;
                })
                .ReturnsAsync(new GlVoucherHeaderDto());

            GlVouchers.Setup(v => v.ReverseVoucherAsync(705, "auditor", "Multi settlement reversal", It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    multiJournal.IsReversed = true;
                    multiJvReversalCreated = true;
                })
                .ReturnsAsync(new GlVoucherHeaderDto());

            GlVouchers.Setup(v => v.ReverseVoucherAsync(201, "accounting-cashier", "Payroll error recalculation", It.IsAny<CancellationToken>()))
                .Callback(() => pv1.IsReversed = true)
                .ReturnsAsync(new GlVoucherHeaderDto());
            GlVouchers.Setup(v => v.ReverseVoucherAsync(203, "accounting-cashier", "Payroll error recalculation", It.IsAny<CancellationToken>()))
                .Callback(() => pv3.IsReversed = true)
                .ReturnsAsync(new GlVoucherHeaderDto());
            GlVouchers.Setup(v => v.ReverseVoucherAsync(301, "accounting-cashier", "EOS correction", It.IsAny<CancellationToken>()))
                .Callback(() => pvEos.IsReversed = true)
                .ReturnsAsync(new GlVoucherHeaderDto());

            VoucherRepo.Setup(v => v.GetBySourceAsync(2, "GL_REVERSAL", 201, It.IsAny<CancellationToken>()))
                .ReturnsAsync([pv1Rev]);
            VoucherRepo.Setup(v => v.GetBySourceAsync(2, "GL_REVERSAL", 203, It.IsAny<CancellationToken>()))
                .ReturnsAsync([pv3Rev]);
            VoucherRepo.Setup(v => v.GetBySourceAsync(2, "GL_REVERSAL", 301, It.IsAny<CancellationToken>()))
                .ReturnsAsync([pvEosRev]);

            ReceiptPayments.Setup(p => p.CreatePaymentVoucherAsync(It.IsAny<CreatePaymentVoucherDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<CreatePaymentVoucherDto, string, CancellationToken>(async (dto, user, ct) =>
                {
                    var id = dto.TotalAmount == 500m ? 201L : (dto.TotalAmount == 310m ? (paymentToVoucherMap.ContainsValue(202) ? 203L : 202L) : (dto.TotalAmount == 3500m ? 301L : 205L));
                    await using var db = CreateDb();
                    if (!await db.GlVoucherHeaders.AnyAsync(h => h.Id == id, ct))
                    {
                        db.GlVoucherHeaders.Add(new GlVoucherHeader
                        {
                            Id = id,
                            BranchId = dto.BranchId,
                            FiscalYearId = dto.FiscalYearId,
                            VoucherYear = dto.VoucherDate.Year,
                            VoucherMonth = dto.VoucherDate.Month,
                            VoucherType = 103,
                            VoucherNo = (int)id,
                            VoucherDate = dto.VoucherDate,
                            TotalAmount = dto.TotalAmount,
                            TotalLocalDebit = dto.TotalAmount,
                            TotalLocalCredit = dto.TotalAmount,
                            Status = 1,
                            CreationUser = user,
                            CreationDate = DateTime.UtcNow
                        });
                        await db.SaveChangesAsync(ct);
                    }
                    return new PaymentVoucherDto { VoucherId = id };
                });

            ReceiptPayments.Setup(p => p.DeletePaymentVoucherAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<long, string, CancellationToken>(async (id, user, ct) =>
                {
                    await using var db = CreateDb();
                    var voucher = await db.GlVoucherHeaders.SingleAsync(h => h.Id == id, ct);
                    if (voucher.Status == 3) return false;
                    voucher.Status = 4;
                    voucher.UpdateUser = user;
                    voucher.UpdateDate = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    if (id == 202) pvDraft2.Status = 4;
                    return true;
                });

            // Unit of work mock for corrections
            UnitOfWork.Setup(u => u.ExecuteForPayrollPeriodAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<Func<Task<PayrollCalculationResultDto>>>(),
                It.IsAny<CancellationToken>()))
                .Returns<long, string, Func<Task<PayrollCalculationResultDto>>, CancellationToken>((_, _, action, _) => action());
        }

        public OracleDbContext CreateDb() => new(options);

        public (AccountingHandoffOutboxWorker Worker, ServiceProvider Provider) CreateOutboxWorker()
        {
            var services = new ServiceCollection();
            services.AddScoped(_ => new OracleDbContext(options));
            var provider = services.BuildServiceProvider();
            var worker = new AccountingHandoffOutboxWorker(
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<AccountingHandoffOutboxWorker>.Instance);
            return (worker, provider);
        }

        public HrAccountingHandoffService CreateHandoffService(OracleDbContext db) => new(
            db,
            VoucherRepo.Object,
            new HrPayrollPostingService(db),
            ReceiptPayments.Object,
            GlVouchers.Object);

        public IPayrollService CreatePayrollService(string handoffStatus)
        {
            var run = new PayrollRun
            {
                Id = 10,
                PayPeriod = "2026-11",
                BranchId = 2,
                Status = "APPROVED",
                ApprovalDate = DateTime.UtcNow,
                CorrectionVersion = 1,
                CorrectionRootRunId = null,
                CorrectionOfRunId = null,
                TotalGrossSalary = 900m,
                TotalNetSalary = 810m
            };
            run.Lines.Add(new PayrollRunLine
            {
                Id = 501,
                EmployeeCode = "E-501",
                BasicSalary = 900m,
                GrossSalary = 900m,
                NetPay = 810m
            });

            var period = new PayrollPeriod
            {
                Id = 100,
                CompanyId = 3,
                PeriodCode = "2026-11",
                Status = "OPEN",
                StartDate = new DateTime(2026, 11, 1),
                EndDate = new DateTime(2026, 11, 30)
            };

            PayrollPeriodRepo.Setup(r => r.GetPayrollRunSnapshotAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(run);
            PayrollPeriodRepo.Setup(r => r.GetPayrollRunByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(run);
            PayrollPeriodRepo.Setup(r => r.GetPeriodForRunAsync(It.IsAny<PayrollRun>(), It.IsAny<CancellationToken>())).ReturnsAsync(period);
            PayrollPeriodRepo.Setup(r => r.GetPeriodByCodeAsync(3, "2026-11", It.IsAny<CancellationToken>())).ReturnsAsync(period);
            PayrollPeriodRepo.Setup(r => r.GetLatestPayrollCorrectionAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(run);
            PayrollPeriodRepo.Setup(r => r.GetActiveEmployeesAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Employee>());

            ValidationService.Setup(v => v.ValidatePreCalculationAsync(3, "2026-11", 2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PayrollValidationResultDto(true, new(), new()));

            var calculatedRun = new PayrollRun
            {
                Id = 11,
                PayPeriod = "2026-11",
                BranchId = 2,
                Status = "CALCULATED",
                TotalGrossSalary = 900m,
                TotalNetSalary = 810m
            };
            calculatedRun.Lines.Add(new PayrollRunLine
            {
                Id = 502,
                EmployeeCode = "E-501",
                BasicSalary = 900m,
                GrossSalary = 900m,
                NetPay = 810m
            });

            CalculationEngine.Setup(c => c.CalculatePayrollForPeriodAsync(
                period,
                It.IsAny<IReadOnlyList<Employee>>(),
                2,
                "hr-admin",
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(calculatedRun);

            var handoffMock = new Mock<IHrAccountingHandoffService>();
            handoffMock.Setup(h => h.GetPayrollHandoffStatusAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PayrollHandoffStatusDto(
                    1001, "HR-PAY-3-10-1", 1, handoffStatus, "2026-11",
                    DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, "Reason"));

            return new PayrollService(
                PayrollPeriodRepo.Object,
                CalculationEngine.Object,
                ValidationService.Object,
                unitOfWork: UnitOfWork.Object,
                inputSnapshots: null,
                accountingHandoffs: handoffMock.Object);
        }

        public async Task SeedInitialDataAsync()
        {
            await using var db = CreateDb();

            // Accounting posting configuration for branch 2
            db.Add(new SysBranch { Id = 2, CompanyId = 3, BaseCurrencyId = 9, IsActive = true });
            db.Add(new SysCurrency { Id = 9, ShortNameEn = "USD", CurrencyNameEn = "US Dollar" });
            db.Add(new GlVoucherType { Id = 1, TypeCode = 1, SerialResetPolicy = "MONTHLY" });
            foreach (var code in new[] { "SALARY-EXP", "ADVANCE-REC", "LOAN-REC", "NET-PAYABLE", "BANK-ACC" })
                db.Add(new GlAccount { AccountCode = code, AccountType = "DETAIL", IsActive = true });
            db.Add(new SysFiscalYear { Id = 1, BranchId = 2, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), IsActive = true });
            db.Add(new PayrollPostingConfiguration
            {
                BranchId = 2, CurrencyId = 9, VoucherType = 1, SalaryExpense = "SALARY-EXP", EmployerSscExpense = "SALARY-EXP",
                SalaryPayable = "NET-PAYABLE", SscPayable = "SALARY-EXP", TaxPayable = "SALARY-EXP", DeductionsPayable = "SALARY-EXP"
            });

            db.PaymentMethods.Add(new PaymentMethod
            {
                Id = 5,
                BranchId = 2,
                Code = "BANK",
                MethodType = "BANK",
                GlAccountCode = "BANK-ACC",
                IsActive = true,
                ShowInVouchers = true
            });

            var employee = new Employee
            {
                EmployeeCode = "E-501",
                NameEn = "E2E Tester",
                NameLocal = "مختبر الشامل",
                NationalId = "NID-501",
                Nationality = "Local",
                BranchId = 2,
                IsActive = true,
                EmploymentStatus = "ACTIVE",
                CreationUser = "seed",
                CreationDate = DateTime.UtcNow
            };
            db.Employees.Add(employee);

            var loan = new EmployeeLoan
            {
                Id = 31,
                EmployeeCode = "E-501",
                LoanType = "PERSONAL",
                PrincipalAmount = 200m,
                CurrencyCode = "USD",
                MonthlyInstallmentAmount = 50m,
                TotalInstallments = 4,
                TotalPaidAmount = 0m,
                RemainingBalance = 200m,
                StartDate = new DateTime(2026, 11, 1),
                Status = "ACTIVE",
                CreationUser = "seed",
                CreationDate = DateTime.UtcNow
            };
            db.EmployeeLoans.Add(loan);

            var schedule = new LoanRepaymentSchedule
            {
                Id = 71,
                EmployeeLoanId = 31,
                InstallmentNo = 1,
                PayPeriod = "2026-11",
                ScheduledAmount = 50m,
                PaidAmount = 0m,
                CarriedForwardAmount = 0m,
                Status = "PENDING"
            };
            db.LoanRepaymentSchedules.Add(schedule);

            var advance = new EmployeeAdvance
            {
                Id = 41,
                EmployeeCode = "E-501",
                AdvanceAmount = 100m,
                CurrencyCode = "USD",
                TargetPayPeriod = "2026-11",
                DeductedAmount = 0m,
                RemainingBalance = 100m,
                Status = "ACTIVE",
                CreationUser = "seed",
                CreationDate = DateTime.UtcNow
            };
            db.EmployeeAdvances.Add(advance);

            await db.SaveChangesAsync();
        }

        public void Dispose() { }
    }
}
