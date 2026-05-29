using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    public partial class AddCodeValueToSysCode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add column as nullable first (Oracle requires empty table for NOT NULL ADD)
            migrationBuilder.AddColumn<string>(
                name: "CODE_VALUE",
                table: "SYS_CODE",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: true);

            // Step 2: Populate CODE_VALUE for existing rows
            migrationBuilder.Sql(@"
                MERGE INTO ""SYS_CODE"" t
                USING (
                    SELECT 1 AS m, 1 AS n, 'Contracts' AS v FROM DUAL UNION ALL
                    SELECT 1, 2, 'Reports' FROM DUAL UNION ALL
                    SELECT 1, 3, 'Invoices' FROM DUAL UNION ALL
                    SELECT 1, 4, 'Receipts' FROM DUAL UNION ALL
                    SELECT 1, 5, 'Identification' FROM DUAL UNION ALL
                    SELECT 1, 6, 'Certificates' FROM DUAL UNION ALL
                    SELECT 1, 7, 'Financial' FROM DUAL UNION ALL
                    SELECT 1, 8, 'HR' FROM DUAL UNION ALL
                    SELECT 1, 9, 'Legal' FROM DUAL UNION ALL
                    SELECT 1, 10, 'Technical' FROM DUAL UNION ALL
                    SELECT 1, 11, 'Marketing' FROM DUAL UNION ALL
                    SELECT 1, 12, 'Other' FROM DUAL UNION ALL
                    SELECT 2, 1, 'Company' FROM DUAL UNION ALL
                    SELECT 2, 2, 'Branch' FROM DUAL UNION ALL
                    SELECT 2, 3, 'Super Admin' FROM DUAL UNION ALL
                    SELECT 3, 1, 'Brute Force Attack' FROM DUAL UNION ALL
                    SELECT 3, 2, 'SQL Injection' FROM DUAL UNION ALL
                    SELECT 3, 3, 'Cross-Site Scripting' FROM DUAL UNION ALL
                    SELECT 3, 4, 'Denial of Service' FROM DUAL UNION ALL
                    SELECT 3, 5, 'Suspicious Login' FROM DUAL UNION ALL
                    SELECT 3, 6, 'Unauthorized Access' FROM DUAL UNION ALL
                    SELECT 3, 7, 'Data Exfiltration' FROM DUAL UNION ALL
                    SELECT 3, 8, 'Malware Detected' FROM DUAL UNION ALL
                    SELECT 4, 1, 'Low' FROM DUAL UNION ALL
                    SELECT 4, 2, 'Medium' FROM DUAL UNION ALL
                    SELECT 4, 3, 'High' FROM DUAL UNION ALL
                    SELECT 4, 4, 'Critical' FROM DUAL UNION ALL
                    SELECT 5, 1, 'Info' FROM DUAL UNION ALL
                    SELECT 5, 2, 'Warning' FROM DUAL UNION ALL
                    SELECT 5, 3, 'Error' FROM DUAL UNION ALL
                    SELECT 5, 4, 'Critical' FROM DUAL UNION ALL
                    SELECT 6, 1, 'Authentication' FROM DUAL UNION ALL
                    SELECT 6, 2, 'Authorization' FROM DUAL UNION ALL
                    SELECT 6, 3, 'DataChange' FROM DUAL UNION ALL
                    SELECT 6, 4, 'Configuration' FROM DUAL UNION ALL
                    SELECT 6, 5, 'Security' FROM DUAL UNION ALL
                    SELECT 6, 6, 'System' FROM DUAL UNION ALL
                    SELECT 6, 7, 'Integration' FROM DUAL UNION ALL
                    SELECT 6, 8, 'Permission' FROM DUAL UNION ALL
                    SELECT 6, 9, 'Exception' FROM DUAL UNION ALL
                    SELECT 6, 10, 'Request' FROM DUAL UNION ALL
                    SELECT 7, 1, 'USER' FROM DUAL UNION ALL
                    SELECT 7, 2, 'SUPER_ADMIN' FROM DUAL UNION ALL
                    SELECT 7, 3, 'SYSTEM' FROM DUAL UNION ALL
                    SELECT 7, 4, 'ANONYMOUS' FROM DUAL UNION ALL
                    SELECT 7, 5, 'COMPANY_ADMIN' FROM DUAL UNION ALL
                    SELECT 8, 1, 'Request' FROM DUAL UNION ALL
                    SELECT 8, 2, 'Exception' FROM DUAL UNION ALL
                    SELECT 8, 3, 'Security' FROM DUAL UNION ALL
                    SELECT 9, 1, 'None' FROM DUAL UNION ALL
                    SELECT 9, 2, 'Metadata Only' FROM DUAL UNION ALL
                    SELECT 9, 3, 'Full' FROM DUAL UNION ALL
                    SELECT 10, 1, 'Healthy' FROM DUAL UNION ALL
                    SELECT 10, 2, 'Degraded' FROM DUAL UNION ALL
                    SELECT 10, 3, 'Unhealthy' FROM DUAL UNION ALL
                    SELECT 10, 4, 'Unresponsive' FROM DUAL UNION ALL
                    SELECT 11, 1, 'Normal' FROM DUAL UNION ALL
                    SELECT 11, 2, 'Warning' FROM DUAL UNION ALL
                    SELECT 11, 3, 'Critical' FROM DUAL UNION ALL
                    SELECT 11, 4, 'Severe' FROM DUAL UNION ALL
                    SELECT 11, 5, 'Unknown' FROM DUAL UNION ALL
                    SELECT 12, 1, 'API Key' FROM DUAL UNION ALL
                    SELECT 12, 2, 'Signing Key' FROM DUAL UNION ALL
                    SELECT 12, 3, 'Encryption Key' FROM DUAL UNION ALL
                    SELECT 12, 4, 'Internal Key' FROM DUAL UNION ALL
                    SELECT 12, 5, 'External Key' FROM DUAL UNION ALL
                    SELECT 13, 1, 'Security Alert' FROM DUAL UNION ALL
                    SELECT 13, 2, 'Performance Alert' FROM DUAL UNION ALL
                    SELECT 13, 3, 'System Alert' FROM DUAL UNION ALL
                    SELECT 13, 4, 'Business Alert' FROM DUAL UNION ALL
                    SELECT 14, 1, 'Arabic' FROM DUAL UNION ALL
                    SELECT 14, 2, 'English' FROM DUAL
                ) s
                ON (t.""CODE_MGR"" = s.m AND t.""CODE_MNR"" = s.n)
                WHEN MATCHED THEN UPDATE SET t.""CODE_VALUE"" = s.v
            ");

            // Insert missing Actor Types: COMPANY_ADMIN (mgr=7, mnr=5)
            migrationBuilder.Sql(@"
                MERGE INTO ""SYS_CODE"" t
                USING (
                    SELECT 7 AS m, 5 AS n, 1 AS l, CAST('مدير الشركة' AS NVARCHAR2(1000)) AS d, CAST('COMPANY_ADMIN' AS NVARCHAR2(200)) AS v FROM DUAL UNION ALL
                    SELECT 7, 5, 2, CAST('Company Admin' AS NVARCHAR2(1000)), CAST('COMPANY_ADMIN' AS NVARCHAR2(200)) FROM DUAL
                ) s
                ON (t.""CODE_MGR"" = s.m AND t.""CODE_MNR"" = s.n AND t.""CODE_LANG"" = s.l)
                WHEN NOT MATCHED THEN
                    INSERT (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    VALUES (s.m, s.n, s.l, s.d, s.v, 1, CAST('seed' AS NVARCHAR2(100)), SYSTIMESTAMP)
            ");

            // Insert missing Event Categories: Permission (mgr=6, mnr=8), Exception (mgr=6, mnr=9), Request (mgr=6, mnr=10)
            migrationBuilder.Sql(@"
                MERGE INTO ""SYS_CODE"" t
                USING (
                    SELECT 6 AS m, 8 AS n, 1 AS l, CAST('صلاحيات' AS NVARCHAR2(1000)) AS d, CAST('Permission' AS NVARCHAR2(200)) AS v FROM DUAL UNION ALL
                    SELECT 6, 8, 2, CAST('Permission' AS NVARCHAR2(1000)), CAST('Permission' AS NVARCHAR2(200)) FROM DUAL UNION ALL
                    SELECT 6, 9, 1, CAST('استثناء' AS NVARCHAR2(1000)), CAST('Exception' AS NVARCHAR2(200)) FROM DUAL UNION ALL
                    SELECT 6, 9, 2, CAST('Exception' AS NVARCHAR2(1000)), CAST('Exception' AS NVARCHAR2(200)) FROM DUAL UNION ALL
                    SELECT 6, 10, 1, CAST('طلب' AS NVARCHAR2(1000)), CAST('Request' AS NVARCHAR2(200)) FROM DUAL UNION ALL
                    SELECT 6, 10, 2, CAST('Request' AS NVARCHAR2(1000)), CAST('Request' AS NVARCHAR2(200)) FROM DUAL
                ) s
                ON (t.""CODE_MGR"" = s.m AND t.""CODE_MNR"" = s.n AND t.""CODE_LANG"" = s.l)
                WHEN NOT MATCHED THEN
                    INSERT (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    VALUES (s.m, s.n, s.l, s.d, s.v, 1, CAST('seed' AS NVARCHAR2(100)), SYSTIMESTAMP)
            ");

            // Step 3: Now make the column NOT NULL (rows are populated)
            migrationBuilder.Sql(@"ALTER TABLE ""SYS_CODE"" MODIFY (""CODE_VALUE"" NVARCHAR2(200) NOT NULL)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Delete rows added by this migration
            migrationBuilder.Sql(@"DELETE FROM ""SYS_CODE"" WHERE ""CODE_MGR"" = 7 AND ""CODE_MNR"" = 5");
            migrationBuilder.Sql(@"DELETE FROM ""SYS_CODE"" WHERE ""CODE_MGR"" = 6 AND ""CODE_MNR"" IN (8, 9, 10)");
            // Cannot revert CODE_VALUE values cleanly, drop column instead
            migrationBuilder.DropColumn(
                name: "CODE_VALUE",
                table: "SYS_CODE");
        }
    }
}