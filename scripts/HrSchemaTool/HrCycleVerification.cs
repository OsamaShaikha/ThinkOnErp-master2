using Oracle.ManagedDataAccess.Client;

internal static class HrCycleVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            await using var probe = connection.CreateCommand();
            probe.CommandText = "SELECT COUNT(*) FROM DEV_TEMPLATE.HR_LEAVE_BALANCE WHERE ID IN (-9000000000000,-9000000000001,-9000000000002)";
            if (Convert.ToInt32(await probe.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException("Cycle-test IDs already exist; no test rows inserted.");
            probe.CommandText = "SELECT MIN(ID) FROM DEV_TEMPLATE.HR_LEAVE_BALANCE";
            var source = await probe.ExecuteScalarAsync();
            if (source is null or DBNull)
                throw new InvalidOperationException("Cycle verification requires an existing balance as FK source.");

            async Task Insert(long id, DateTime start)
            {
                await using var command = connection.CreateCommand();
                command.BindByName = true;
                command.CommandText = """
                    INSERT INTO DEV_TEMPLATE.HR_LEAVE_BALANCE
                      (ID,EMPLOYEE_CODE,LEAVE_TYPE_CODE,YEAR_NO,ACCRUED_DAYS,USED_DAYS,CARRIED_FORWARD_DAYS,CREATION_USER,CREATION_DATE,CYCLE_START_DATE,CYCLE_END_DATE)
                    SELECT :id,EMPLOYEE_CODE,LEAVE_TYPE_CODE,9000,0,0,0,'HR_MIGRATION_VERIFY',SYSTIMESTAMP,:startDate,:endDate
                    FROM DEV_TEMPLATE.HR_LEAVE_BALANCE WHERE ID=:sourceId
                    """;
                command.Parameters.Add("id", OracleDbType.Int64).Value = id;
                command.Parameters.Add("startDate", OracleDbType.TimeStamp).Value = start;
                command.Parameters.Add("endDate", OracleDbType.TimeStamp).Value = start.AddMonths(6).AddDays(-1);
                command.Parameters.Add("sourceId", OracleDbType.Int64).Value = Convert.ToInt64(source);
                if (await command.ExecuteNonQueryAsync() != 1)
                    throw new InvalidOperationException("Cycle-test source changed.");
            }

            await Insert(-9000000000000, new DateTime(9000, 1, 1));
            await Insert(-9000000000001, new DateTime(9000, 7, 1));
            var rejected = false;
            try { await Insert(-9000000000002, new DateTime(9000, 1, 1)); }
            catch (OracleException ex) when (ex.Number == 1) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Duplicate cycle was accepted.");
            report.Add("Oracle cycle-key test: distinct cycles in one year accepted; duplicate cycle rejected.");
        }
        finally
        {
            await transaction.RollbackAsync();
        }
        await using var verify = connection.CreateCommand();
        verify.CommandText = "SELECT COUNT(*) FROM DEV_TEMPLATE.HR_LEAVE_BALANCE WHERE ID IN (-9000000000000,-9000000000001,-9000000000002)";
        if (Convert.ToInt32(await verify.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException("Cycle-test rollback verification failed.");
        report.Add("Oracle cycle-key test rollback verified: no temporary rows retained.");
    }
}
