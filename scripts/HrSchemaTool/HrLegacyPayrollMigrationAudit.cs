using Oracle.ManagedDataAccess.Client;

internal static class HrLegacyPayrollMigrationAudit
{
    public static async Task Run(OracleConnection connection, List<string> report)
    {
        using var transaction = connection.BeginTransaction();
        await using (var begin = connection.CreateCommand())
        {
            begin.Transaction = transaction;
            begin.CommandText = "SET TRANSACTION READ ONLY";
            await begin.ExecuteNonQueryAsync();
        }

        async Task<string[]> QueryOne(string sql)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.CommandTimeout = 30;
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) throw new InvalidOperationException("Migration audit query returned no result.");
            return Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.IsDBNull(i) ? "0" : Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture) ?? "0")
                .ToArray();
        }

        report.Add("Legacy payroll migration preflight: DEV_TEMPLATE only; read-only snapshot; no employee identifiers or individual payroll amounts are emitted.");

        // 1. Payroll runs status distribution
        var runs = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN STATUS='DRAFT' THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='CALCULATED' THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='APPROVED' THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='POSTED' THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='CANCELLED' THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='SUPERSEDED' THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.HR_PAYROLL_RUN");
        report.Add($"Payroll runs: total={runs[0]}, draft={runs[1]}, calculated={runs[2]}, approved={runs[3]}, posted={runs[4]}, cancelled={runs[5]}, superseded={runs[6]}.");

        // 1a. Finalized bank-transfer lines must rely on saved employee/payment snapshots,
        // never on the employee's current bank profile. Aggregate counts only; emit no PII.
        var bankSnapshots = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN L.CURRENCY_CODE IS NULL OR TRIM(L.CURRENCY_CODE) IS NULL THEN 1 ELSE 0 END),SUM(CASE WHEN L.BENEFICIARY_NAME IS NULL OR TRIM(L.BENEFICIARY_NAME) IS NULL THEN 1 ELSE 0 END),SUM(CASE WHEN L.BENEFICIARY_NATIONAL_ID IS NULL OR TRIM(L.BENEFICIARY_NATIONAL_ID) IS NULL THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.HR_PAYROLL_RUN_LINE L JOIN DEV_TEMPLATE.HR_PAYROLL_RUN R ON R.ID=L.PAYROLL_RUN_ID WHERE R.STATUS IN ('APPROVED','POSTED') AND UPPER(TRIM(L.PAYMENT_METHOD))='BANK_TRANSFER'");
        report.Add($"Finalized bank-transfer payroll snapshots: lines={bankSnapshots[0]}, missing-currency={bankSnapshots[1]}, missing-beneficiary-name={bankSnapshots[2]}, missing-beneficiary-national-id={bankSnapshots[3]}. Counts only; no current employee profile is consulted.");

        // Formula components have no persisted expression contract yet. Count active
        // salary-structure references before rollout so unsupported legacy use is visible.
        var formulaComponents = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN C.IS_ACTIVE=1 THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.HR_SALARY_STRUCTURE_LINE L JOIN DEV_TEMPLATE.HR_SALARY_STRUCTURE S ON S.ID=L.STRUCTURE_ID JOIN DEV_TEMPLATE.HR_SALARY_COMPONENT C ON C.COMPONENT_CODE=L.COMPONENT_CODE WHERE L.IS_ACTIVE=1 AND S.IS_ACTIVE=1 AND UPPER(TRIM(C.CALCULATION_TYPE))='FORMULA'");
        report.Add($"Active salary structure lines using formula components: total={formulaComponents[0]}, active-component-lines={formulaComponents[1]}. Counts only; formula execution remains unsupported until an approved expression contract exists.");

        // 2. Legacy runs without handoffs or with legacy journal voucher
        var legacyRuns = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN H.ID IS NULL AND R.STATUS='APPROVED' THEN 1 ELSE 0 END),SUM(CASE WHEN R.JOURNAL_VOUCHER_ID IS NOT NULL AND L.HANDOFF_ID IS NULL THEN 1 ELSE 0 END),SUM(CASE WHEN R.CORRECTION_OF_RUN_ID IS NOT NULL THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.HR_PAYROLL_RUN R LEFT JOIN DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF H ON H.SOURCE_TYPE='PAYROLL' AND H.SOURCE_ID=TO_CHAR(R.ID) AND H.SOURCE_VERSION=R.CORRECTION_VERSION AND H.OPERATION='SUBMIT' LEFT JOIN DEV_TEMPLATE.ACC_HANDOFF_JOURNAL_LINK L ON L.VOUCHER_ID=R.JOURNAL_VOUCHER_ID");
        report.Add($"Legacy payroll handoff linkage: approved-runs-without-handoff={legacyRuns[1]}, legacy-posted-runs-without-handoff-link={legacyRuns[2]}, corrected-lineage-runs={legacyRuns[3]}.");

        // 3. Employee loans audit
        var loans = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN L.STATUS='PENDING' THEN 1 ELSE 0 END),SUM(CASE WHEN L.STATUS='APPROVED' THEN 1 ELSE 0 END),SUM(CASE WHEN L.STATUS='ACTIVE' THEN 1 ELSE 0 END),SUM(CASE WHEN L.STATUS='COMPLETED' THEN 1 ELSE 0 END),SUM(CASE WHEN L.STATUS='CANCELLED' THEN 1 ELSE 0 END),SUM(CASE WHEN L.STATUS IN ('APPROVED','ACTIVE') AND R.ID IS NULL THEN 1 ELSE 0 END),SUM(CASE WHEN ABS(L.REMAINING_BALANCE+L.TOTAL_PAID_AMOUNT-L.PRINCIPAL_AMOUNT)>0.0001 THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.HR_EMPLOYEE_LOAN L LEFT JOIN DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT R ON R.SOURCE_TYPE='LOAN' AND R.SOURCE_ID=L.ID");
        report.Add($"Employee loans: total={loans[0]}, pending={loans[1]}, approved={loans[2]}, active={loans[3]}, completed={loans[4]}, cancelled={loans[5]}, legacy-active-without-funding-receipt={loans[6]}, balance-inconsistencies={loans[7]}.");

        // 4. Employee advances audit
        var advances = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN A.STATUS='PENDING' THEN 1 ELSE 0 END),SUM(CASE WHEN A.STATUS='APPROVED' THEN 1 ELSE 0 END),SUM(CASE WHEN A.STATUS='PAID' THEN 1 ELSE 0 END),SUM(CASE WHEN A.STATUS='DEDUCTED' THEN 1 ELSE 0 END),SUM(CASE WHEN A.STATUS='CANCELLED' THEN 1 ELSE 0 END),SUM(CASE WHEN A.STATUS IN ('APPROVED','PAID') AND R.ID IS NULL THEN 1 ELSE 0 END),SUM(CASE WHEN ABS(A.REMAINING_BALANCE+A.DEDUCTED_AMOUNT-A.ADVANCE_AMOUNT)>0.0001 THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.HR_EMPLOYEE_ADVANCE A LEFT JOIN DEV_TEMPLATE.ACC_LOAN_FUNDING_RECEIPT R ON R.SOURCE_TYPE='ADVANCE' AND R.SOURCE_ID=A.ID");
        report.Add($"Employee advances: total={advances[0]}, pending={advances[1]}, approved={advances[2]}, paid={advances[3]}, deducted={advances[4]}, cancelled={advances[5]}, legacy-active-without-funding-receipt={advances[6]}, balance-inconsistencies={advances[7]}.");

        // Active deductions now require an explicit recorded currency. Report aggregate counts only;
        // never infer a currency from an employee's current salary or profile.
        var missingActiveCurrencies = await QueryOne("SELECT (SELECT COUNT(*) FROM DEV_TEMPLATE.HR_EMPLOYEE_LOAN L WHERE L.STATUS='ACTIVE' AND (L.CURRENCY_CODE IS NULL OR TRIM(L.CURRENCY_CODE) IS NULL)),(SELECT COUNT(*) FROM DEV_TEMPLATE.HR_EMPLOYEE_ADVANCE A WHERE A.STATUS='ACTIVE' AND (A.CURRENCY_CODE IS NULL OR TRIM(A.CURRENCY_CODE) IS NULL)) FROM DUAL");
        report.Add($"Active loan/advance currency snapshots: loans-missing-explicit-currency={missingActiveCurrencies[0]}, advances-missing-explicit-currency={missingActiveCurrencies[1]}. Aggregate counts only; payroll currency is not inferred from current employee data.");

        // Explicit currency snapshots must still resolve to exactly one ERP currency row.
        var invalidActiveCurrencies = await QueryOne("SELECT (SELECT COUNT(*) FROM DEV_TEMPLATE.HR_EMPLOYEE_LOAN L WHERE L.STATUS='ACTIVE' AND TRIM(L.CURRENCY_CODE) IS NOT NULL AND (SELECT COUNT(*) FROM SYS_CURRENCY C WHERE UPPER(TRIM(C.SHORT_DESC_E))=UPPER(TRIM(L.CURRENCY_CODE)))=0),(SELECT COUNT(*) FROM DEV_TEMPLATE.HR_EMPLOYEE_LOAN L WHERE L.STATUS='ACTIVE' AND TRIM(L.CURRENCY_CODE) IS NOT NULL AND (SELECT COUNT(*) FROM SYS_CURRENCY C WHERE UPPER(TRIM(C.SHORT_DESC_E))=UPPER(TRIM(L.CURRENCY_CODE)))>1),(SELECT COUNT(*) FROM DEV_TEMPLATE.HR_EMPLOYEE_ADVANCE A WHERE A.STATUS='ACTIVE' AND TRIM(A.CURRENCY_CODE) IS NOT NULL AND (SELECT COUNT(*) FROM SYS_CURRENCY C WHERE UPPER(TRIM(C.SHORT_DESC_E))=UPPER(TRIM(A.CURRENCY_CODE)))=0),(SELECT COUNT(*) FROM DEV_TEMPLATE.HR_EMPLOYEE_ADVANCE A WHERE A.STATUS='ACTIVE' AND TRIM(A.CURRENCY_CODE) IS NOT NULL AND (SELECT COUNT(*) FROM SYS_CURRENCY C WHERE UPPER(TRIM(C.SHORT_DESC_E))=UPPER(TRIM(A.CURRENCY_CODE)))>1) FROM DUAL");
        report.Add($"Active loan/advance currency catalog integrity: loans-unconfigured={invalidActiveCurrencies[0]}, loans-ambiguous={invalidActiveCurrencies[1]}, advances-unconfigured={invalidActiveCurrencies[2]}, advances-ambiguous={invalidActiveCurrencies[3]}. Aggregate counts only; no currency is inferred or rewritten.");

        // Active loan schedules must reconcile to the loan balance before payroll can
        // consume them. Report aggregate counts only; do not emit employee or loan IDs.
        var activeLoanScheduleQuality = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN SCHEDULE_COUNT=0 THEN 1 ELSE 0 END),SUM(CASE WHEN INVALID_INSTALLMENT_COUNT>0 THEN 1 ELSE 0 END),SUM(CASE WHEN ABS(SCHEDULE_REMAINING_BALANCE-LOAN_REMAINING_BALANCE)>0.0001 THEN 1 ELSE 0 END) FROM (SELECT L.ID,L.REMAINING_BALANCE LOAN_REMAINING_BALANCE,COUNT(S.ID) SCHEDULE_COUNT,SUM(CASE WHEN S.ID IS NOT NULL AND (S.SCHEDULED_AMOUNT<0 OR S.PAID_AMOUNT<0 OR S.PAID_AMOUNT>S.SCHEDULED_AMOUNT) THEN 1 ELSE 0 END) INVALID_INSTALLMENT_COUNT,SUM(CASE WHEN S.ID IS NULL THEN 0 ELSE S.SCHEDULED_AMOUNT-S.PAID_AMOUNT END) SCHEDULE_REMAINING_BALANCE FROM DEV_TEMPLATE.HR_EMPLOYEE_LOAN L LEFT JOIN DEV_TEMPLATE.HR_LOAN_REPAYMENT_SCHEDULE S ON S.EMPLOYEE_LOAN_ID=L.ID WHERE L.STATUS='ACTIVE' GROUP BY L.ID,L.REMAINING_BALANCE)");
        report.Add($"Active loan schedule integrity: loans={activeLoanScheduleQuality[0]}, without-schedules={activeLoanScheduleQuality[1]}, with-invalid-installments={activeLoanScheduleQuality[2]}, schedule-vs-loan-balance-mismatch={activeLoanScheduleQuality[3]}. Aggregate counts only; affected records require explicit review before payroll use.");

        // The schema may not enforce this relationship on every legacy tenant.
        // Surface detached schedule rows without disclosing their IDs or values.
        var orphanSchedules = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN S.STATUS IN ('PENDING','PARTIAL','SKIPPED') THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.HR_LOAN_REPAYMENT_SCHEDULE S LEFT JOIN DEV_TEMPLATE.HR_EMPLOYEE_LOAN L ON L.ID=S.EMPLOYEE_LOAN_ID WHERE L.ID IS NULL");
        report.Add($"Loan repayment schedules without a parent loan: total={orphanSchedules[0]}, pending-or-partial={orphanSchedules[1]}. Aggregate counts only; review before any legacy migration or payroll cutover.");

        // 5. Payroll adjustments audit
        var adjustments = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN STATUS='PENDING' THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='APPROVED' THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='PROCESSED' THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='CANCELLED' THEN 1 ELSE 0 END),SUM(CASE WHEN PAYROLL_RUN_LINE_ID IS NOT NULL THEN 1 ELSE 0 END),SUM(CASE WHEN PAYROLL_RUN_LINE_ID IS NULL AND STATUS='PROCESSED' THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.HR_PAYROLL_ADJUSTMENT");
        report.Add($"Payroll adjustments: total={adjustments[0]}, pending={adjustments[1]}, approved={adjustments[2]}, processed={adjustments[3]}, cancelled={adjustments[4]}, linked-to-payroll-line={adjustments[5]}, processed-without-line-link={adjustments[6]}.");

        // 6. Accounting handoffs status distribution
        var handoffs = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN DELIVERY_STATUS='QUEUED' THEN 1 ELSE 0 END),SUM(CASE WHEN DELIVERY_STATUS='DELIVERED' THEN 1 ELSE 0 END),SUM(CASE WHEN DELIVERY_STATUS='ACCEPTED' THEN 1 ELSE 0 END),SUM(CASE WHEN DELIVERY_STATUS='POSTED' THEN 1 ELSE 0 END),SUM(CASE WHEN DELIVERY_STATUS='RETURNED' THEN 1 ELSE 0 END),SUM(CASE WHEN DELIVERY_STATUS='WITHDRAWN' THEN 1 ELSE 0 END),SUM(CASE WHEN DELIVERY_STATUS='REVERSED' THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF");
        report.Add($"Accounting handoffs: total={handoffs[0]}, queued={handoffs[1]}, delivered={handoffs[2]}, accepted={handoffs[3]}, posted={handoffs[4]}, returned={handoffs[5]}, withdrawn={handoffs[6]}, reversed={handoffs[7]}.");

        // 7. Posting receipts and settlement snapshots
        var receipts = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN SETTLEMENT_SNAPSHOT_JSON IS NOT NULL THEN 1 ELSE 0 END),SUM(CASE WHEN SETTLEMENT_SNAPSHOT_JSON IS NULL THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.ACC_HANDOFF_POSTING_RECEIPT");
        report.Add($"Posting receipts: total={receipts[0]}, with-settlement-snapshot={receipts[1]}, legacy-without-snapshot={receipts[2]}.");

        // Payroll handoffs that claim a final Accounting state must retain the exact
        // journal link and receipt pair needed for safe reversal. Emit counts only.
        var postingLinkIntegrity = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN H.DELIVERY_STATUS='POSTED' AND (P.HANDOFF_ID IS NULL OR J.HANDOFF_ID IS NULL) THEN 1 ELSE 0 END),SUM(CASE WHEN P.HANDOFF_ID IS NOT NULL AND (J.HANDOFF_ID IS NULL OR P.VOUCHER_ID<>J.VOUCHER_ID) THEN 1 ELSE 0 END),SUM(CASE WHEN H.DELIVERY_STATUS='REVERSED' AND P.HANDOFF_ID IS NULL THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF H LEFT JOIN DEV_TEMPLATE.ACC_HANDOFF_POSTING_RECEIPT P ON P.HANDOFF_ID=H.ID LEFT JOIN DEV_TEMPLATE.ACC_HANDOFF_JOURNAL_LINK J ON J.HANDOFF_ID=H.ID WHERE H.SOURCE_TYPE='PAYROLL' AND H.OPERATION='SUBMIT'");
        report.Add($"Payroll posting link integrity: handoffs={postingLinkIntegrity[0]}, posted-missing-receipt-or-journal-link={postingLinkIntegrity[1]}, receipt-journal-link-mismatches={postingLinkIntegrity[2]}, reversed-missing-original-receipt={postingLinkIntegrity[3]}. Aggregate counts only; discrepancies require accounting review before cutover.");

        // 8. Accounting handoff payments
        var payments = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN STATUS='PREPARED' THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='CONFIRMED' THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='FAILED' THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='REVERSED' THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.ACC_HANDOFF_PAYMENT");
        report.Add($"Accounting handoff payments: total={payments[0]}, prepared={payments[1]}, confirmed={payments[2]}, failed={payments[3]}, reversed={payments[4]}.");

        // Confirmed and reversed payment states must retain their exact posted
        // Payment Voucher relationship. Failed attempts may be unlinked, but any
        // linked voucher must be voided. Report counts only; never emit row IDs.
        var paymentVoucherIntegrity = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN STATUS='CONFIRMED' AND (PAYMENT_VOUCHER_COUNT<>1 OR VALID_CONFIRMED_VOUCHER_COUNT<>1) THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='FAILED' AND NON_VOIDED_VOUCHER_COUNT>0 THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='REVERSED' AND (PAYMENT_VOUCHER_COUNT<>1 OR VALID_REVERSAL_LINK_COUNT<>1 OR REVERSAL_VOUCHER_ID IS NULL) THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='PREPARED' AND POSTED_VOUCHER_COUNT>0 THEN 1 ELSE 0 END) FROM (SELECT P.ID,P.STATUS,P.REVERSAL_VOUCHER_ID,H.BRANCH_ID,(SELECT COUNT(*) FROM DEV_TEMPLATE.GL_VOUCHER_HEADER V WHERE V.SOURCE_SYSTEM_CODE='HR_PAYMENT' AND V.SOURCE_REF_ID=P.ID) PAYMENT_VOUCHER_COUNT,(SELECT COUNT(*) FROM DEV_TEMPLATE.GL_VOUCHER_HEADER V WHERE V.SOURCE_SYSTEM_CODE='HR_PAYMENT' AND V.SOURCE_REF_ID=P.ID AND V.BRANCH_ID=H.BRANCH_ID AND V.STATUS=3 AND NVL(V.IS_REVERSED,0)=0 AND V.POST_DATE IS NOT NULL) VALID_CONFIRMED_VOUCHER_COUNT,(SELECT COUNT(*) FROM DEV_TEMPLATE.GL_VOUCHER_HEADER V WHERE V.SOURCE_SYSTEM_CODE='HR_PAYMENT' AND V.SOURCE_REF_ID=P.ID AND (V.STATUS<>4 OR NVL(V.IS_REVERSED,0)<>0)) NON_VOIDED_VOUCHER_COUNT,(SELECT COUNT(*) FROM DEV_TEMPLATE.GL_VOUCHER_HEADER V WHERE V.SOURCE_SYSTEM_CODE='HR_PAYMENT' AND V.SOURCE_REF_ID=P.ID AND V.BRANCH_ID=H.BRANCH_ID AND V.STATUS=3 AND V.IS_REVERSED=1 AND V.POST_DATE IS NOT NULL AND EXISTS (SELECT 1 FROM DEV_TEMPLATE.GL_VOUCHER_HEADER R WHERE R.ID=P.REVERSAL_VOUCHER_ID AND R.SOURCE_SYSTEM_CODE='GL_REVERSAL' AND R.SOURCE_REF_ID=V.ID AND R.BRANCH_ID=H.BRANCH_ID AND R.STATUS=3 AND NVL(R.IS_REVERSED,0)=0 AND R.POST_DATE IS NOT NULL)) VALID_REVERSAL_LINK_COUNT,(SELECT COUNT(*) FROM DEV_TEMPLATE.GL_VOUCHER_HEADER V WHERE V.SOURCE_SYSTEM_CODE='HR_PAYMENT' AND V.SOURCE_REF_ID=P.ID AND V.STATUS=3) POSTED_VOUCHER_COUNT FROM DEV_TEMPLATE.ACC_HANDOFF_PAYMENT P JOIN DEV_TEMPLATE.HR_ACCOUNTING_HANDOFF H ON H.ID=P.HANDOFF_ID)");
        report.Add($"Payment voucher integrity: payments={paymentVoucherIntegrity[0]}, confirmed-missing-or-invalid-posted-voucher={paymentVoucherIntegrity[1]}, failed-with-nonvoided-voucher={paymentVoucherIntegrity[2]}, reversed-missing-original-or-reversal-link={paymentVoucherIntegrity[3]}, prepared-with-posted-voucher={paymentVoucherIntegrity[4]}. Aggregate counts only; exact voucher lines and any discrepancy require Accounting review before cutover.");

        report.Add("Preflight audit verification: No automatic guessing of GL vouchers, funding disbursement, or payment confirmation is allowed. Legacy approved runs without handoffs must be reviewed manually before cutover.");
        await transaction.RollbackAsync();
    }
}

