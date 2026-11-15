using Oracle.ManagedDataAccess.Client;

internal static class HrLeaveBalanceMigrationAudit
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

        report.Add("Leave-balance migration preflight: DEV_TEMPLATE only; read-only snapshot; no employee identifiers or individual balances are emitted.");

        var settings = await QueryOne("SELECT COUNT(*),NVL(MIN(LEAVE_YEAR_START_MONTH),0),NVL(MIN(LEAVE_YEAR_START_DAY),0),NVL(MAX(LEAVE_YEAR_START_MONTH),0),NVL(MAX(LEAVE_YEAR_START_DAY),0) FROM DEV_TEMPLATE.HR_LEAVE_SETTINGS");
        report.Add($"Company leave-year settings: rows={settings[0]}, min-start={settings[1]}-{settings[2]}, max-start={settings[3]}-{settings[4]}.");

        var balances = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN CYCLE_START_DATE IS NULL AND CYCLE_END_DATE IS NULL THEN 1 ELSE 0 END),SUM(CASE WHEN (CYCLE_START_DATE IS NULL AND CYCLE_END_DATE IS NOT NULL) OR (CYCLE_START_DATE IS NOT NULL AND CYCLE_END_DATE IS NULL) THEN 1 ELSE 0 END),SUM(CASE WHEN CYCLE_START_DATE IS NOT NULL AND CYCLE_END_DATE IS NOT NULL AND CYCLE_END_DATE<CYCLE_START_DATE THEN 1 ELSE 0 END),SUM(CASE WHEN CYCLE_START_DATE IS NOT NULL AND EXTRACT(YEAR FROM CYCLE_START_DATE)<>YEAR_NO THEN 1 ELSE 0 END),NVL(SUM(ACCRUED_DAYS),0),NVL(SUM(USED_DAYS),0),NVL(SUM(CARRIED_FORWARD_DAYS),0),NVL(SUM(ACCRUED_DAYS+CARRIED_FORWARD_DAYS-USED_DAYS),0) FROM DEV_TEMPLATE.HR_LEAVE_BALANCE");
        report.Add($"Balances: rows={balances[0]}, legacy-without-cycle-dates={balances[1]}, partial-cycle-dates={balances[2]}, invalid-date-ranges={balances[3]}, year-label-mismatches={balances[4]}, aggregate-accrued/used/carried/available={balances[5]}/{balances[6]}/{balances[7]}/{balances[8]}.");

        var duplicateCycles = await QueryOne("SELECT COUNT(*) FROM (SELECT EMPLOYEE_CODE,LEAVE_TYPE_CODE,CYCLE_START_DATE FROM DEV_TEMPLATE.HR_LEAVE_BALANCE GROUP BY EMPLOYEE_CODE,LEAVE_TYPE_CODE,CYCLE_START_DATE HAVING COUNT(*)>1)");
        report.Add($"Duplicate employee/type/cycle groups: {duplicateCycles[0]}.");

        var movementCoverage = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN M.BALANCE_ID IS NULL THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.HR_LEAVE_BALANCE B LEFT JOIN (SELECT DISTINCT BALANCE_ID FROM DEV_TEMPLATE.HR_LEAVE_MOVEMENT) M ON M.BALANCE_ID=B.ID");
        var movementCount = await QueryOne("SELECT COUNT(*) FROM DEV_TEMPLATE.HR_LEAVE_MOVEMENT");
        report.Add($"Ledger: movements={movementCount[0]}, balance-rows-without-any-movement={movementCoverage[1]} of {movementCoverage[0]}.");

        var requests = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN STATUS='PENDING' THEN 1 ELSE 0 END),SUM(CASE WHEN STATUS='APPROVED' THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.HR_LEAVE_REQUEST");
        var closes = await QueryOne("SELECT COUNT(*),SUM(CASE WHEN STATUS='POSTED' THEN 1 ELSE 0 END) FROM DEV_TEMPLATE.HR_LEAVE_YEAR_CLOSE");
        report.Add($"Dependent workflow rows: leave-requests total/pending/approved={requests[0]}/{requests[1]}/{requests[2]}, year-closes total/posted={closes[0]}/{closes[1]}.");
        report.Add("Preflight does not choose a cutover date, reclassify historical usage, or modify balances. Those values must be explicit in a reviewed migration batch.");
        await transaction.RollbackAsync();
    }
}
