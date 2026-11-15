using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrAccountingOutboxOracleVerification
{
    private const string FixtureUser = "HR_OUTBOX_ORACLE_VERIFY";

    public static async Task Verify(OracleConnection connection, string connectionString, List<string> report)
    {
        var safeConnectionString = new OracleConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseOracle(connection, provider => provider.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
            .Options;
        await using var db = new OracleDbContext(options);
        var branch = await db.SysBranches.AsNoTracking().Where(b => b.IsActive && b.CompanyId.HasValue)
            .Select(b => new { b.Id, CompanyId = b.CompanyId!.Value }).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("INT-10 Oracle outbox verification requires an active company branch.");

        await CleanupAbandonedFixturesAsync(db);
        var now = DateTime.UtcNow;
        if (await AccountingHandoffOutboxWorker.QueryDueMessages(db, now).AnyAsync())
            throw new InvalidOperationException("INT-10 verifier will not run while unrelated due outbox messages exist in DEV_TEMPLATE.");

        var token = Guid.NewGuid().ToString("N");
        var eventKey = $"{branch.CompanyId}:PAYROLL:OUTBOX_VERIFY:{token}:1:SUBMIT";
        var handoff = new HrAccountingHandoff
        {
            CompanyId = branch.CompanyId,
            BranchId = branch.Id,
            SourceType = "PAYROLL",
            SourceId = "OUTBOX_VERIFY:" + token,
            SourceVersion = 1,
            Operation = "SUBMIT",
            PayPeriod = "OUTBOX-VERIFY",
            SourceApprovedAtUtc = now,
            HandoffReference = "HR-OUTBOX-VERIFY-" + token,
            PayloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))),
            DeliveryStatus = "QUEUED",
            CreatedAtUtc = now,
            CreatedBy = FixtureUser,
            Transitions = [new HrAccountingHandoffTransition
            {
                FromStatus = "INITIAL", ToStatus = "QUEUED", Actor = FixtureUser, OccurredAtUtc = now
            }],
            Lines = [new HrAccountingHandoffLine
            {
                LineType = "CONTROL:GROSS", CurrencyCode = "OUTBOX", Amount = 1m,
                BranchId = branch.Id, SourceReference = "OUTBOX_VERIFY:" + token
            }]
        };
        var outbox = new HrAccountingOutboxMessage
        {
            EventKey = eventKey,
            EventType = "PAYROLL_SUBMIT",
            PayloadHash = handoff.PayloadHash,
            Status = "PENDING",
            CreatedAtUtc = now,
            Handoff = handoff
        };
        handoff.OutboxMessages.Add(outbox);

        long handoffId = 0;
        long outboxId = 0;
        long conflictOwnerId = 0;
        long conflictHandoffId = 0;
        long conflictOutboxId = 0;
        long retryHandoffId = 0;
        long retryOutboxId = 0;
        long fundingHandoffId = 0;
        long fundingOutboxId = 0;
        long fundingLoanId = 0;
        long eosSettlementId = 0;
        long eosHandoffId = 0;
        long eosOutboxId = 0;
        var conflictEventKey = string.Empty;
        var retryEventKey = string.Empty;
        var fundingEventKey = string.Empty;
        var eosEventKey = string.Empty;
        try
        {
            await using (var seed = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted))
            {
                db.HrAccountingHandoffs.Add(handoff);
                await db.SaveChangesAsync();
                handoffId = handoff.Id;
                outboxId = outbox.Id;
                await seed.CommitAsync();
            }

            var services = new ServiceCollection();
            var transientFailure = new OneShotRelaySaveFailureInterceptor();
            services.AddDbContext<OracleDbContext>(optionsBuilder => optionsBuilder
                .UseOracle(safeConnectionString, provider => provider.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
                .AddInterceptors(TemplateSchemaConnectionInterceptor.Instance, transientFailure));
            using var provider = services.BuildServiceProvider();
            var worker = new AccountingHandoffOutboxWorker(provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<AccountingHandoffOutboxWorker>.Instance);
            await Task.WhenAll(
                worker.RelayBatchAsync(CancellationToken.None),
                worker.RelayBatchAsync(CancellationToken.None));

            db.ChangeTracker.Clear();
            var delivered = await db.HrAccountingOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == outboxId);
            var received = await db.AccountingHandoffInboxMessages.AsNoTracking().SingleOrDefaultAsync(m => m.EventKey == eventKey);
            var deliveredHandoff = await db.HrAccountingHandoffs.Include(h => h.Transitions)
                .SingleAsync(h => h.Id == handoffId);
            if (delivered.Status != "DELIVERED" || delivered.AttemptCount != 1 || delivered.LastError != null ||
                received?.HandoffId != handoffId || received.PayloadHash != handoff.PayloadHash || received.Status != "RECEIVED" ||
                deliveredHandoff.DeliveryStatus != "DELIVERED" ||
                deliveredHandoff.Transitions.Count(t => t.FromStatus == "QUEUED" && t.ToStatus == "DELIVERED" && t.Actor == "OUTBOX") != 1)
                throw new InvalidOperationException("The real Oracle outbox worker did not atomically deliver its event, create a matching inbox receipt, and advance the handoff exactly once.");

            conflictEventKey = $"{branch.CompanyId}:PAYROLL:OUTBOX_CONFLICT_VERIFY:{token}:1:SUBMIT";
            var ownerHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token + ":INBOX_OWNER")));
            var conflictingHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token + ":CONFLICTING_EVENT")));
            var inboxOwner = new HrAccountingHandoff
            {
                CompanyId = branch.CompanyId, BranchId = branch.Id, SourceType = "PAYROLL",
                SourceId = "OUTBOX_CONFLICT_OWNER:" + token, SourceVersion = 1, Operation = "SUBMIT",
                PayPeriod = "OUTBOX-VERIFY", SourceApprovedAtUtc = now,
                HandoffReference = "HR-OUTBOX-OWNER-" + token, PayloadHash = ownerHash,
                DeliveryStatus = "DELIVERED", CreatedAtUtc = now, CreatedBy = FixtureUser,
                Transitions =
                [
                    new HrAccountingHandoffTransition { FromStatus = "INITIAL", ToStatus = "QUEUED", Actor = FixtureUser, OccurredAtUtc = now },
                    new HrAccountingHandoffTransition { FromStatus = "QUEUED", ToStatus = "DELIVERED", Actor = "OUTBOX", OccurredAtUtc = now }
                ]
            };
            var conflictingHandoff = new HrAccountingHandoff
            {
                CompanyId = branch.CompanyId, BranchId = branch.Id, SourceType = "PAYROLL",
                SourceId = "OUTBOX_CONFLICT_TARGET:" + token, SourceVersion = 1, Operation = "SUBMIT",
                PayPeriod = "OUTBOX-VERIFY", SourceApprovedAtUtc = now,
                HandoffReference = "HR-OUTBOX-TARGET-" + token, PayloadHash = conflictingHash,
                DeliveryStatus = "QUEUED", CreatedAtUtc = now, CreatedBy = FixtureUser,
                Transitions = [new HrAccountingHandoffTransition
                {
                    FromStatus = "INITIAL", ToStatus = "QUEUED", Actor = FixtureUser, OccurredAtUtc = now
                }]
            };
            var collisionInbox = new AccountingHandoffInboxMessage
            {
                EventKey = conflictEventKey, PayloadHash = ownerHash, ReceivedAtUtc = now,
                Status = "RECEIVED", Handoff = inboxOwner
            };
            var conflictOutbox = new HrAccountingOutboxMessage
            {
                EventKey = conflictEventKey, EventType = "PAYROLL_SUBMIT", PayloadHash = conflictingHash,
                Status = "PENDING", CreatedAtUtc = now, Handoff = conflictingHandoff
            };
            conflictingHandoff.OutboxMessages.Add(conflictOutbox);

            await using (var seedConflict = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted))
            {
                db.HrAccountingHandoffs.AddRange(inboxOwner, conflictingHandoff);
                db.AccountingHandoffInboxMessages.Add(collisionInbox);
                await db.SaveChangesAsync();
                conflictOwnerId = inboxOwner.Id;
                conflictHandoffId = conflictingHandoff.Id;
                conflictOutboxId = conflictOutbox.Id;
                await seedConflict.CommitAsync();
            }

            await Task.WhenAll(
                worker.RelayBatchAsync(CancellationToken.None),
                worker.RelayBatchAsync(CancellationToken.None));
            db.ChangeTracker.Clear();
            var conflictResult = await db.HrAccountingOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == conflictOutboxId);
            var preservedInbox = await db.AccountingHandoffInboxMessages.AsNoTracking().SingleAsync(m => m.EventKey == conflictEventKey);
            var preservedTarget = await db.HrAccountingHandoffs.Include(h => h.Transitions).SingleAsync(h => h.Id == conflictHandoffId);
            if (conflictResult.Status != "CONFLICT" || conflictResult.AttemptCount != 1 ||
                !conflictResult.LastError!.Contains("already exists", StringComparison.Ordinal) ||
                preservedInbox.HandoffId != conflictOwnerId || preservedInbox.PayloadHash != ownerHash ||
                preservedTarget.DeliveryStatus != "QUEUED" ||
                preservedTarget.Transitions.Any(t => t.FromStatus == "QUEUED" && t.ToStatus == "DELIVERED" && t.Actor == "OUTBOX"))
                throw new InvalidOperationException("The real Oracle outbox worker did not quarantine the duplicate EventKey/payload conflict while preserving both handoff states and the original inbox receipt.");

            var typedConflicts = new[]
            {
                await SeedTypedConflictAsync(db, branch.CompanyId, branch.Id, now, "LOAN", "LOAN_FUNDING_REQUEST", token),
                await SeedTypedConflictAsync(db, branch.CompanyId, branch.Id, now, "EOS", "EOS_SETTLEMENT_PAYOUT_REQUEST", token)
            };
            await Task.WhenAll(worker.RelayBatchAsync(CancellationToken.None), worker.RelayBatchAsync(CancellationToken.None));
            db.ChangeTracker.Clear();
            foreach (var typedConflict in typedConflicts)
            {
                var message = await db.HrAccountingOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == typedConflict.OutboxId);
                var inbox = await db.AccountingHandoffInboxMessages.AsNoTracking().SingleAsync(m => m.EventKey == typedConflict.EventKey);
                var target = await db.HrAccountingHandoffs.AsNoTracking().SingleAsync(h => h.Id == typedConflict.TargetHandoffId);
                if (message.EventType != typedConflict.EventType || message.Status != "CONFLICT" || message.AttemptCount != 1 ||
                    message.LastError?.Contains("already exists", StringComparison.Ordinal) != true ||
                    inbox.HandoffId != typedConflict.OwnerHandoffId || inbox.PayloadHash != typedConflict.OwnerHash ||
                    target.DeliveryStatus != "QUEUED")
                    throw new InvalidOperationException($"Oracle {typedConflict.EventType} conflict did not preserve its inbox owner and target handoff.");
            }

            retryEventKey = $"{branch.CompanyId}:PAYROLL:OUTBOX_RETRY_VERIFY:{token}:1:SUBMIT";
            var retryHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token + ":RETRY")));
            var retryHandoff = new HrAccountingHandoff
            {
                CompanyId = branch.CompanyId, BranchId = branch.Id, SourceType = "PAYROLL",
                SourceId = "OUTBOX_RETRY_VERIFY:" + token, SourceVersion = 1, Operation = "SUBMIT",
                PayPeriod = "OUTBOX-VERIFY", SourceApprovedAtUtc = now,
                HandoffReference = "HR-OUTBOX-RETRY-" + token, PayloadHash = retryHash,
                DeliveryStatus = "QUEUED", CreatedAtUtc = now, CreatedBy = FixtureUser,
                Transitions = [new HrAccountingHandoffTransition
                {
                    FromStatus = "INITIAL", ToStatus = "QUEUED", Actor = FixtureUser, OccurredAtUtc = now
                }],
                Lines = [new HrAccountingHandoffLine
                {
                    LineType = "CONTROL:GROSS", CurrencyCode = "OUTBOX", Amount = 1m,
                    BranchId = branch.Id, SourceReference = "OUTBOX_RETRY_VERIFY:" + token
                }]
            };
            var retryOutbox = new HrAccountingOutboxMessage
            {
                EventKey = retryEventKey, EventType = "PAYROLL_SUBMIT", PayloadHash = retryHash,
                Status = "PENDING", CreatedAtUtc = now, Handoff = retryHandoff
            };
            retryHandoff.OutboxMessages.Add(retryOutbox);
            await using (var seedRetry = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted))
            {
                db.HrAccountingHandoffs.Add(retryHandoff);
                await db.SaveChangesAsync();
                retryHandoffId = retryHandoff.Id;
                retryOutboxId = retryOutbox.Id;
                await seedRetry.CommitAsync();
            }

            transientFailure.ArmOnce();
            await worker.RelayBatchAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            var failedAttempt = await db.HrAccountingOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == retryOutboxId);
            if (failedAttempt.Status != "PENDING" || failedAttempt.AttemptCount != 1 ||
                failedAttempt.LastError?.Contains("Injected INT-10 Oracle retry verification failure", StringComparison.Ordinal) != true ||
                !failedAttempt.LastAttemptAtUtc.HasValue)
                throw new InvalidOperationException($"An injected Oracle relay transaction failure did not persist the expected retry state (status={failedAttempt.Status}, attempts={failedAttempt.AttemptCount}, lastAttempt={failedAttempt.LastAttemptAtUtc:O}, lastError={failedAttempt.LastError ?? "<null>"}).");

            await worker.RelayBatchAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            var deferredRetry = await db.HrAccountingOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == retryOutboxId);
            if (deferredRetry.Status != "PENDING" || deferredRetry.AttemptCount != 1 || deferredRetry.LastAttemptAtUtc != failedAttempt.LastAttemptAtUtc)
                throw new InvalidOperationException("Oracle outbox retry ignored the five-second backoff after a transient delivery failure.");

            for (var failedAttemptCount = 1; failedAttemptCount <= 8; failedAttemptCount++)
            {
                var retryDelaySeconds = (int)AccountingHandoffOutboxWorker.GetRetryDelay(failedAttemptCount).TotalSeconds;
                await db.HrAccountingOutboxMessages.Where(m => m.Id == retryOutboxId)
                    .ExecuteUpdateAsync(update => update.SetProperty(m => m.LastAttemptAtUtc,
                        DateTime.UtcNow.AddSeconds(-(retryDelaySeconds + 1))));
                transientFailure.ArmOnce();
                await worker.RelayBatchAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                var afterFailure = await db.HrAccountingOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == retryOutboxId);
                if (afterFailure.Status != "PENDING" || afterFailure.AttemptCount != failedAttemptCount + 1 ||
                    afterFailure.LastError?.Contains("Injected INT-10 Oracle retry verification failure", StringComparison.Ordinal) != true)
                    throw new InvalidOperationException($"Oracle outbox failure {failedAttemptCount + 1} did not preserve retry state after the {retryDelaySeconds}-second backoff interval.");

                await worker.RelayBatchAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                var deferred = await db.HrAccountingOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == retryOutboxId);
                if (deferred.Status != "PENDING" || deferred.AttemptCount != failedAttemptCount + 1 ||
                    deferred.LastAttemptAtUtc != afterFailure.LastAttemptAtUtc)
                    throw new InvalidOperationException($"Oracle outbox retry {failedAttemptCount + 1} was not deferred before its next backoff interval.");
            }

            var finalRetryDelaySeconds = (int)AccountingHandoffOutboxWorker.GetRetryDelay(9).TotalSeconds;
            await db.HrAccountingOutboxMessages.Where(m => m.Id == retryOutboxId)
                .ExecuteUpdateAsync(update => update.SetProperty(m => m.LastAttemptAtUtc,
                    DateTime.UtcNow.AddSeconds(-(finalRetryDelaySeconds + 1))));
            await worker.RelayBatchAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            var retried = await db.HrAccountingOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == retryOutboxId);
            var retryReceipt = await db.AccountingHandoffInboxMessages.AsNoTracking().SingleOrDefaultAsync(m => m.EventKey == retryEventKey);
            var retriedHandoff = await db.HrAccountingHandoffs.Include(h => h.Transitions).SingleAsync(h => h.Id == retryHandoffId);
            if (retried.Status != "DELIVERED" || retried.AttemptCount != 10 || retried.LastError != null ||
                retryReceipt?.HandoffId != retryHandoffId || retryReceipt.PayloadHash != retryHash ||
                retriedHandoff.DeliveryStatus != "DELIVERED" ||
                retriedHandoff.Transitions.Count(t => t.FromStatus == "QUEUED" && t.ToStatus == "DELIVERED" && t.Actor == "OUTBOX") != 1)
                throw new InvalidOperationException("Oracle outbox retry after the backoff interval did not deliver once and clear the recorded failure.");

            foreach (var (sourceType, eventType) in new[]
                     {
                         ("LOAN", "LOAN_FUNDING_REQUEST"),
                         ("EOS", "EOS_SETTLEMENT_PAYOUT_REQUEST")
                     })
            {
                var typedRetry = await SeedTypedRetryAsync(db, branch.CompanyId, branch.Id, now, sourceType, eventType, token);
                transientFailure.ArmOnce();
                await worker.RelayBatchAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                var failure = await db.HrAccountingOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == typedRetry.OutboxId);
                if (failure.Status != "PENDING" || failure.AttemptCount != 1 ||
                    failure.LastError?.Contains("Injected INT-10 Oracle retry verification failure", StringComparison.Ordinal) != true)
                    throw new InvalidOperationException($"Oracle {eventType} relay failure did not persist retry state.");

                await worker.RelayBatchAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                var deferred = await db.HrAccountingOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == typedRetry.OutboxId);
                if (deferred.Status != "PENDING" || deferred.AttemptCount != 1)
                    throw new InvalidOperationException($"Oracle {eventType} retry ignored the five-second backoff.");

                await db.HrAccountingOutboxMessages.Where(m => m.Id == typedRetry.OutboxId)
                    .ExecuteUpdateAsync(update => update.SetProperty(m => m.LastAttemptAtUtc, DateTime.UtcNow.AddSeconds(-6)));
                await worker.RelayBatchAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                var deliveredTypedEvent = await db.HrAccountingOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == typedRetry.OutboxId);
                var receipt = await db.AccountingHandoffInboxMessages.AsNoTracking().SingleOrDefaultAsync(m => m.EventKey == typedRetry.EventKey);
                var handoffAfterRetry = await db.HrAccountingHandoffs.Include(h => h.Transitions)
                    .SingleAsync(h => h.Id == typedRetry.HandoffId);
                if (deliveredTypedEvent.EventType != eventType || deliveredTypedEvent.Status != "DELIVERED" || deliveredTypedEvent.AttemptCount != 2 ||
                    deliveredTypedEvent.LastError != null || receipt?.HandoffId != typedRetry.HandoffId ||
                    receipt.PayloadHash != typedRetry.PayloadHash || handoffAfterRetry.DeliveryStatus != "DELIVERED" ||
                    handoffAfterRetry.Transitions.Count(t => t.FromStatus == "QUEUED" && t.ToStatus == "DELIVERED" && t.Actor == "OUTBOX") != 1)
                    throw new InvalidOperationException($"Oracle {eventType} did not deliver once after its failed attempt became due.");
            }

            db.ChangeTracker.Clear();
            var employeeCode = await db.Employees.AsNoTracking()
                .Where(e => e.BranchId == branch.Id && e.TerminationDate == null)
                .Select(e => e.EmployeeCode).FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("INT-10 funding-event verification requires a non-terminated employee in the selected branch.");
            var fundingCurrency = await db.SysCurrencies.AsNoTracking()
                .Where(c => c.ShortNameEn != null && c.ShortNameEn.Trim() != string.Empty)
                .GroupBy(c => c.ShortNameEn!.Trim().ToUpper())
                .Where(g => g.Count() == 1)
                .Select(g => g.Key).FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("INT-10 funding-event verification requires a uniquely configured ERP currency.");
            var fundingLoan = new EmployeeLoan
            {
                EmployeeCode = employeeCode, LoanType = "PERSONAL", PrincipalAmount = 1m,
                CurrencyCode = fundingCurrency, MonthlyInstallmentAmount = 1m, TotalInstallments = 1,
                RemainingBalance = 1m, StartDate = DateTime.UtcNow.Date.AddDays(1),
                EndDate = DateTime.UtcNow.Date.AddDays(31), Status = "APPROVED",
                ApprovedBy = FixtureUser, ApprovalDate = now, CreationUser = FixtureUser, CreationDate = now
            };
            await using (var seedFundingLoan = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted))
            {
                db.EmployeeLoans.Add(fundingLoan);
                await db.SaveChangesAsync();
                fundingLoanId = fundingLoan.Id;
                await seedFundingLoan.CommitAsync();
            }

            // The funding-handoff factory uses only OracleDbContext; the Accounting collaborators are intentionally
            // unused by CreateApprovedFundingHandoffAsync and remain null to keep this verifier isolated.
            var handoffService = new HrAccountingHandoffService(db, null!, null!, null!, null!);
            var fundingHandoff = await handoffService.CreateApprovedFundingHandoffAsync("LOAN", fundingLoanId, FixtureUser);
            fundingHandoffId = fundingHandoff.Id;
            var fundingOutbox = fundingHandoff.OutboxMessages.SingleOrDefault(m => m.EventType == "LOAN_FUNDING_REQUEST")
                ?? throw new InvalidOperationException("The real approved-loan handoff factory did not create its LOAN_FUNDING_REQUEST outbox event.");
            fundingOutboxId = fundingOutbox.Id;
            fundingEventKey = fundingOutbox.EventKey;
            if (fundingHandoff.DeliveryStatus != "QUEUED" || fundingOutbox.Status != "PENDING" ||
                fundingOutbox.PayloadHash != fundingHandoff.PayloadHash ||
                fundingOutbox.EventKey != $"{branch.CompanyId}:LOAN:{fundingLoanId}:1:FUND")
                throw new InvalidOperationException("The real approved-loan funding handoff produced an unexpected outbox event identity or payload fingerprint.");

            await worker.RelayBatchAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            var deliveredFundingOutbox = await db.HrAccountingOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == fundingOutboxId);
            var fundingReceipt = await db.AccountingHandoffInboxMessages.AsNoTracking().SingleOrDefaultAsync(m => m.EventKey == fundingEventKey);
            var deliveredFundingHandoff = await db.HrAccountingHandoffs.Include(h => h.Transitions).SingleAsync(h => h.Id == fundingHandoffId);
            if (deliveredFundingOutbox.Status != "DELIVERED" || deliveredFundingOutbox.AttemptCount != 1 ||
                fundingReceipt?.HandoffId != fundingHandoffId || fundingReceipt.PayloadHash != fundingHandoff.PayloadHash ||
                deliveredFundingHandoff.DeliveryStatus != "DELIVERED" ||
                deliveredFundingHandoff.Transitions.Count(t => t.FromStatus == "QUEUED" && t.ToStatus == "DELIVERED" && t.Actor == "OUTBOX") != 1)
                throw new InvalidOperationException("The real approved-loan funding event was not relayed to a matching Accounting inbox receipt exactly once.");

            db.ChangeTracker.Clear();
            var eosSettlement = new EndOfServiceSettlement
            {
                CompanyId = branch.CompanyId, BranchId = branch.Id, EmployeeCode = employeeCode,
                TerminationType = EndOfServiceTerminationTypes.ContractExpiry, LastWorkingDate = now.Date,
                SettlementDate = now.Date, HireDate = now.Date.AddYears(-5), ServiceYears = 5,
                ServiceMonths = 0, ServiceDays = 0, BaseSalarySnapshot = 1m, GrossSalarySnapshot = 1m,
                CurrencyCode = fundingCurrency, SeverancePay = 1m, TotalEntitlements = 1m,
                NetSettlementAmount = 1m, Status = EndOfServiceSettlementStatus.Approved,
                ApprovedBy = FixtureUser, ApprovedAtUtc = now, CreationUser = FixtureUser, CreationDate = now
            };
            await using (var seedEos = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted))
            {
                db.EndOfServiceSettlements.Add(eosSettlement);
                await db.SaveChangesAsync();
                eosSettlementId = eosSettlement.Id;
                await seedEos.CommitAsync();
            }

            var eosHandoff = await handoffService.CreateApprovedFundingHandoffAsync("EOS", eosSettlementId, FixtureUser);
            eosHandoffId = eosHandoff.Id;
            var eosOutbox = eosHandoff.OutboxMessages.SingleOrDefault(m => m.EventType == "EOS_SETTLEMENT_PAYOUT_REQUEST")
                ?? throw new InvalidOperationException("The real approved EOS handoff factory did not create its EOS_SETTLEMENT_PAYOUT_REQUEST outbox event.");
            eosOutboxId = eosOutbox.Id;
            eosEventKey = eosOutbox.EventKey;
            if (eosHandoff.DeliveryStatus != "QUEUED" || eosOutbox.Status != "PENDING" ||
                eosOutbox.PayloadHash != eosHandoff.PayloadHash ||
                eosOutbox.EventKey != $"{branch.CompanyId}:EOS:{eosSettlementId}:1:FUND")
                throw new InvalidOperationException("The real approved EOS payout handoff produced an unexpected outbox event identity or payload fingerprint.");

            await worker.RelayBatchAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            var deliveredEosOutbox = await db.HrAccountingOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == eosOutboxId);
            var eosReceipt = await db.AccountingHandoffInboxMessages.AsNoTracking().SingleOrDefaultAsync(m => m.EventKey == eosEventKey);
            var deliveredEosHandoff = await db.HrAccountingHandoffs.Include(h => h.Transitions).SingleAsync(h => h.Id == eosHandoffId);
            if (deliveredEosOutbox.Status != "DELIVERED" || deliveredEosOutbox.AttemptCount != 1 ||
                eosReceipt?.HandoffId != eosHandoffId || eosReceipt.PayloadHash != eosHandoff.PayloadHash ||
                deliveredEosHandoff.DeliveryStatus != "DELIVERED" ||
                deliveredEosHandoff.Transitions.Count(t => t.FromStatus == "QUEUED" && t.ToStatus == "DELIVERED" && t.Actor == "OUTBOX") != 1)
                throw new InvalidOperationException("The real approved EOS payout event was not relayed to a matching Accounting inbox receipt exactly once.");

            report.Add("Oracle INT-10/INT-14 outbox checks: concurrent duplicate PAYROLL_SUBMIT relay delivered once; PAYROLL_SUBMIT, LOAN_FUNDING_REQUEST, and EOS_SETTLEMENT_PAYOUT_REQUEST EventKey/hash conflicts were quarantined as CONFLICT while preserving the original inbox owner/hash and target QUEUED handoff. Oracle retry thresholds 5/10/20/40/80/160/320/640/900 seconds were exercised with injected failures and fixture timestamp advancement, not real-time waiting. Both LOAN_FUNDING_REQUEST and EOS_SETTLEMENT_PAYOUT_REQUEST also survived an injected relay failure, deferred immediate retry, and delivered once after the five-second threshold. The real CreateApprovedFundingHandoffAsync factory created successful loan funding and EOS payout events with expected EventKey/hash; the worker delivered each to one matching inbox receipt. Cleanup removed all marked handoffs, outbox/inbox rows, lines, transitions, loan, and EOS fixtures.");
        }
        finally
        {
            await CleanupAbandonedFixturesAsync(db);
            if ((handoffId > 0 && await db.HrAccountingHandoffs.IgnoreQueryFilters().AsNoTracking().AnyAsync(h => h.Id == handoffId)) ||
                (conflictOwnerId > 0 && await db.HrAccountingHandoffs.IgnoreQueryFilters().AsNoTracking().AnyAsync(h => h.Id == conflictOwnerId)) ||
                (conflictHandoffId > 0 && await db.HrAccountingHandoffs.IgnoreQueryFilters().AsNoTracking().AnyAsync(h => h.Id == conflictHandoffId)) ||
                (retryHandoffId > 0 && await db.HrAccountingHandoffs.IgnoreQueryFilters().AsNoTracking().AnyAsync(h => h.Id == retryHandoffId)) ||
                (fundingHandoffId > 0 && await db.HrAccountingHandoffs.IgnoreQueryFilters().AsNoTracking().AnyAsync(h => h.Id == fundingHandoffId)) ||
                (eosHandoffId > 0 && await db.HrAccountingHandoffs.IgnoreQueryFilters().AsNoTracking().AnyAsync(h => h.Id == eosHandoffId)) ||
                (outboxId > 0 && await db.HrAccountingOutboxMessages.AsNoTracking().AnyAsync(m => m.Id == outboxId)) ||
                (conflictOutboxId > 0 && await db.HrAccountingOutboxMessages.AsNoTracking().AnyAsync(m => m.Id == conflictOutboxId)) ||
                (retryOutboxId > 0 && await db.HrAccountingOutboxMessages.AsNoTracking().AnyAsync(m => m.Id == retryOutboxId)) ||
                (fundingOutboxId > 0 && await db.HrAccountingOutboxMessages.AsNoTracking().AnyAsync(m => m.Id == fundingOutboxId)) ||
                (eosOutboxId > 0 && await db.HrAccountingOutboxMessages.AsNoTracking().AnyAsync(m => m.Id == eosOutboxId)) ||
                (eventKey.Length > 0 && await db.AccountingHandoffInboxMessages.AsNoTracking().AnyAsync(m => m.EventKey == eventKey)) ||
                (conflictEventKey.Length > 0 && await db.AccountingHandoffInboxMessages.AsNoTracking().AnyAsync(m => m.EventKey == conflictEventKey)) ||
                (retryEventKey.Length > 0 && await db.AccountingHandoffInboxMessages.AsNoTracking().AnyAsync(m => m.EventKey == retryEventKey)) ||
                (fundingEventKey.Length > 0 && await db.AccountingHandoffInboxMessages.AsNoTracking().AnyAsync(m => m.EventKey == fundingEventKey)) ||
                (eosEventKey.Length > 0 && await db.AccountingHandoffInboxMessages.AsNoTracking().AnyAsync(m => m.EventKey == eosEventKey)) ||
                (eosSettlementId > 0 && await db.EndOfServiceSettlements.IgnoreQueryFilters().AsNoTracking().AnyAsync(s => s.Id == eosSettlementId)) ||
                (fundingLoanId > 0 && await db.EmployeeLoans.IgnoreQueryFilters().AsNoTracking().AnyAsync(l => l.Id == fundingLoanId)))
                throw new InvalidOperationException("INT-10 temporary outbox fixtures remain after cleanup.");
        }
    }

    private static async Task CleanupAbandonedFixturesAsync(OracleDbContext db)
    {
        var ids = await db.HrAccountingHandoffs.IgnoreQueryFilters().AsNoTracking()
            .Where(h => h.CreatedBy == FixtureUser).Select(h => h.Id).ToArrayAsync();
        if (ids.Length == 0) return;
        await using var cleanup = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        foreach (var id in ids)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ACC_HANDOFF_INBOX WHERE HANDOFF_ID = {id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_ACCOUNTING_OUTBOX WHERE HANDOFF_ID = {id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_ACCOUNTING_HANDOFF_TRANSITION WHERE HANDOFF_ID = {id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_ACCOUNTING_HANDOFF_LINE WHERE HANDOFF_ID = {id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM HR_ACCOUNTING_HANDOFF WHERE ID = {id} AND CREATED_BY = {FixtureUser}");
        }
        await db.EmployeeLoans.IgnoreQueryFilters().Where(l => l.CreationUser == FixtureUser).ExecuteDeleteAsync();
        await db.EndOfServiceSettlements.IgnoreQueryFilters().Where(s => s.CreationUser == FixtureUser).ExecuteDeleteAsync();
        await cleanup.CommitAsync();
        db.ChangeTracker.Clear();
        if (await db.HrAccountingHandoffs.IgnoreQueryFilters().AsNoTracking().AnyAsync(h => h.CreatedBy == FixtureUser))
            throw new InvalidOperationException("An abandoned marked INT-10 fixture remains after startup cleanup.");
    }

    private static async Task<TypedConflictFixture> SeedTypedConflictAsync(OracleDbContext db, long companyId,
        long branchId, DateTime now, string sourceType, string eventType, string token)
    {
        var key = $"{companyId}:{sourceType}:OUTBOX_TYPED_CONFLICT:{token}:1:FUND";
        var ownerHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(token + eventType + ":INBOX_OWNER")));
        var targetHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(token + eventType + ":CONFLICTING_PAYLOAD")));
        var owner = new HrAccountingHandoff
        {
            CompanyId = companyId, BranchId = branchId, SourceType = sourceType,
            SourceId = "OUTBOX_TYPED_OWNER:" + eventType + ":" + token, SourceVersion = 1,
            Operation = "FUND", HandoffReference = "HR-OUTBOX-TYPED-OWNER-" + eventType + "-" + token,
            PayloadHash = ownerHash, DeliveryStatus = "DELIVERED", CreatedAtUtc = now, CreatedBy = FixtureUser,
            Transitions =
            [
                new HrAccountingHandoffTransition { FromStatus = "INITIAL", ToStatus = "QUEUED", Actor = FixtureUser, OccurredAtUtc = now },
                new HrAccountingHandoffTransition { FromStatus = "QUEUED", ToStatus = "DELIVERED", Actor = "OUTBOX", OccurredAtUtc = now }
            ]
        };
        var target = new HrAccountingHandoff
        {
            CompanyId = companyId, BranchId = branchId, SourceType = sourceType,
            SourceId = "OUTBOX_TYPED_TARGET:" + eventType + ":" + token, SourceVersion = 1,
            Operation = "FUND", HandoffReference = "HR-OUTBOX-TYPED-TARGET-" + eventType + "-" + token,
            PayloadHash = targetHash, DeliveryStatus = "QUEUED", CreatedAtUtc = now, CreatedBy = FixtureUser,
            Transitions = [new HrAccountingHandoffTransition
            {
                FromStatus = "INITIAL", ToStatus = "QUEUED", Actor = FixtureUser, OccurredAtUtc = now
            }],
            Lines = [new HrAccountingHandoffLine
            {
                LineType = sourceType == "EOS" ? "EOS_SETTLEMENT" : "PRINCIPAL", CurrencyCode = "OUTBOX",
                Amount = 1m, BranchId = branchId, SourceReference = "OUTBOX_TYPED_TARGET:" + token
            }]
        };
        var outbox = new HrAccountingOutboxMessage
        {
            EventKey = key, EventType = eventType, PayloadHash = targetHash, Status = "PENDING",
            CreatedAtUtc = now, Handoff = target
        };
        target.OutboxMessages.Add(outbox);
        db.HrAccountingHandoffs.AddRange(owner, target);
        db.AccountingHandoffInboxMessages.Add(new AccountingHandoffInboxMessage
        {
            EventKey = key, PayloadHash = ownerHash, ReceivedAtUtc = now, Status = "RECEIVED", Handoff = owner
        });
        await using var seed = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await db.SaveChangesAsync();
        var fixture = new TypedConflictFixture(eventType, key, owner.Id, target.Id, outbox.Id, ownerHash);
        await seed.CommitAsync();
        return fixture;
    }

    private static async Task<TypedRetryFixture> SeedTypedRetryAsync(OracleDbContext db, long companyId,
        long branchId, DateTime now, string sourceType, string eventType, string token)
    {
        var sourceId = $"OUTBOX_TYPED_RETRY:{sourceType}:{token}";
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(sourceId + eventType)));
        var key = $"{companyId}:{sourceType}:{sourceId}:1:FUND";
        var handoff = new HrAccountingHandoff
        {
            CompanyId = companyId, BranchId = branchId, SourceType = sourceType,
            SourceId = sourceId, SourceVersion = 1, Operation = "FUND",
            HandoffReference = "HR-OUTBOX-TYPED-RETRY-" + sourceType + "-" + token,
            PayloadHash = hash, DeliveryStatus = "QUEUED", CreatedAtUtc = now, CreatedBy = FixtureUser,
            Transitions = [new HrAccountingHandoffTransition
            {
                FromStatus = "INITIAL", ToStatus = "QUEUED", Actor = FixtureUser, OccurredAtUtc = now
            }],
            Lines = [new HrAccountingHandoffLine
            {
                LineType = sourceType == "EOS" ? "EOS_SETTLEMENT" : "PRINCIPAL", CurrencyCode = "OUTBOX",
                Amount = 1m, BranchId = branchId, SourceReference = sourceId
            }]
        };
        var outbox = new HrAccountingOutboxMessage
        {
            EventKey = key, EventType = eventType, PayloadHash = hash, Status = "PENDING",
            CreatedAtUtc = now, Handoff = handoff
        };
        handoff.OutboxMessages.Add(outbox);
        db.HrAccountingHandoffs.Add(handoff);
        await using var seed = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await db.SaveChangesAsync();
        var fixture = new TypedRetryFixture(eventType, key, handoff.Id, outbox.Id, hash);
        await seed.CommitAsync();
        return fixture;
    }

    private sealed record TypedConflictFixture(string EventType, string EventKey, long OwnerHandoffId,
        long TargetHandoffId, long OutboxId, string OwnerHash);

    private sealed record TypedRetryFixture(string EventType, string EventKey, long HandoffId,
        long OutboxId, string PayloadHash);

    private sealed class TemplateSchemaConnectionInterceptor : DbConnectionInterceptor
    {
        public static readonly TemplateSchemaConnectionInterceptor Instance = new();

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE";
            command.ExecuteNonQuery();
        }

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private sealed class OneShotRelaySaveFailureInterceptor : SaveChangesInterceptor
    {
        private int _failNextSave;

        public void ArmOnce() => Interlocked.Exchange(ref _failNextSave, 1);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _failNextSave, 0) == 1)
                throw new InvalidOperationException("Injected INT-10 Oracle retry verification failure.");
            return ValueTask.FromResult(result);
        }
    }
}
