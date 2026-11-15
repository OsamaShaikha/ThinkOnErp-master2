using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ThinkOnErp.Infrastructure.Data;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations;

[DbContext(typeof(OracleDbContext))]
[Migration("20261114234500_AlignHrUnicodeStringColumns")]
public sealed class AlignHrUnicodeStringColumns : Migration
{
    private sealed record ColumnSpec(
        string Table,
        string Column,
        string Shadow,
        int Length,
        bool Nullable,
        bool Unique = false,
        string CheckConstraint = null,
        string CheckCondition = null,
        string UniqueConstraintName = null,
        string TemporaryIndexName = null);

    private static readonly ColumnSpec[] Columns =
    [
        new("HR_EMPLOYEE", "CITIZENSHIP_COUNTRY_CODE", "HR_U_TMP_01", 2, true),
        new("HR_LEAVE_SETTINGS", "ELIGIBLE_CITIZENSHIP_COUNTRY_CODE", "HR_U_TMP_02", 2, true),
        new("HR_LEAVE_SETTINGS", "LEAVE_CASHOUT_WAGE_BASIS_CODE", "HR_U_TMP_03", 30, true),
        new("HR_LEAVE_SETTINGS", "LEAVE_CASHOUT_BASIS_COMPONENT_CODE", "HR_U_TMP_04", 100, true),
        new("HR_LEAVE_SETTINGS", "LEAVE_CASHOUT_PAY_COMPONENT_CODE", "HR_U_TMP_05", 100, true),
        new("HR_LEAVE_YEAR_CLOSE_LINE", "YEAR_END_EXCESS_ACTION_SNAPSHOT", "HR_U_TMP_06", 20, true),
        new("HR_LEAVE_BALANCE_MIGRATION", "MIGRATION_KEY", "HR_U_TMP_07", 200, false, true,
            UniqueConstraintName: "UX_HR_BAL_MIGRATION_KEY", TemporaryIndexName: "UX_HR_BAL_MIG_KEY_TMP"),
        new("HR_LEAVE_BALANCE_MIGRATION", "PAYLOAD_HASH", "HR_U_TMP_08", 64, false),
        new("HR_LEAVE_BALANCE_MIGRATION", "STATUS", "HR_U_TMP_09", 20, false,
            CheckConstraint: "CK_HR_BAL_MIGRATION_STATUS", CheckCondition: "STATUS IN ('DRAFT','POSTED','REVERSED')"),
        new("HR_LEAVE_BALANCE_MIGRATION", "CREATION_USER", "HR_U_TMP_10", 200, false),
        new("HR_LEAVE_BALANCE_MIGRATION", "POSTED_BY", "HR_U_TMP_11", 200, true),
        new("HR_LEAVE_BALANCE_MIGRATION", "REVERSED_BY", "HR_U_TMP_12", 200, true),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "EMPLOYEE_CODE", "HR_U_TMP_13", 100, false),
        new("HR_LEAVE_BALANCE_MIGRATION_LINE", "LEAVE_TYPE_CODE", "HR_U_TMP_14", 100, false),
        new("HR_LEAVE_CASHOUT", "PAYLOAD_HASH", "HR_U_TMP_15", 64, false),
        new("HR_LEAVE_CASHOUT", "SETTLEMENT_KEY", "HR_U_TMP_16", 200, false, true,
            UniqueConstraintName: "UX_HR_LEAVE_CASHOUT_KEY", TemporaryIndexName: "UX_HR_LCO_KEY_TMP")
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // All data, column, constraint, and index checks run before the first DDL.
        foreach (var sql in GetSqlOperations())
            migrationBuilder.Sql(sql);
    }

    public static IReadOnlyList<string> GetSqlOperations()
    {
        return Columns.Select(BuildPreflight)
            .Concat(Columns.Select(BuildConversion))
            .ToArray();
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Unicode values may have been written after conversion; never narrow these columns on rollback.
    }

    private static string BuildPreflight(ColumnSpec spec)
    {
        var table = Q(spec.Table);
        var column = Q(spec.Column);
        var shadow = Q(spec.Shadow);
        var uniqueConstraint = spec.Unique ? spec.UniqueConstraintName : "";
        var tempIndex = spec.Unique ? spec.TemporaryIndexName : "";
        var finalIndex = spec.Unique ? spec.UniqueConstraintName : "";
        var allowedUniqueConstraint = spec.Unique
            ? $"(c.CONSTRAINT_NAME='{uniqueConstraint}' AND c.CONSTRAINT_TYPE='U')"
            : "1=0";
        var normalizedCheckCondition = spec.CheckCondition is null
            ? string.Empty
            : string.Concat(spec.CheckCondition.Where(character => !char.IsWhiteSpace(character) && character is not '(' and not ')' and not '"')).ToUpperInvariant().Replace("'", "''", StringComparison.Ordinal);
        var allowedCheckConstraint = spec.CheckConstraint is null || spec.CheckCondition is null
            ? "1=0"
            : $"(c.CONSTRAINT_NAME='{spec.CheckConstraint}' AND c.CONSTRAINT_TYPE='C' AND REGEXP_REPLACE(UPPER(c.SEARCH_CONDITION_VC), '[[:space:]\"()]', '')='{normalizedCheckCondition}')";
        var notNullCheck = $"(c.CONSTRAINT_TYPE='C' AND REGEXP_REPLACE(UPPER(c.SEARCH_CONDITION_VC), '[[:space:]\"()]', '')='{spec.Column.ToUpperInvariant()}ISNOTNULL')";

        return $"""
            DECLARE
                v_table_count NUMBER;
                v_column_count NUMBER;
                v_shadow_count NUMBER;
                v_type VARCHAR2(30);
                v_length NUMBER;
                v_nullable VARCHAR2(1);
                v_default_length NUMBER;
                v_virtual VARCHAR2(3);
                v_bad_rows NUMBER;
                v_bad_constraints NUMBER;
                v_bad_indexes NUMBER;
                v_key_state NUMBER;
            BEGIN
                SELECT COUNT(*) INTO v_table_count FROM ALL_TABLES
                WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}';
                IF v_table_count=0 THEN RETURN; END IF;

                SELECT COUNT(*) INTO v_column_count FROM ALL_TAB_COLS
                WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}' AND COLUMN_NAME='{spec.Column}';
                SELECT COUNT(*) INTO v_shadow_count FROM ALL_TAB_COLS
                WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}' AND COLUMN_NAME='{spec.Shadow}';
                IF v_column_count=0 AND v_shadow_count=0 THEN RETURN; END IF;

                IF '{(spec.Unique ? "Y" : "N")}'='Y' THEN
                    SELECT COUNT(*) INTO v_bad_indexes FROM ALL_INDEXES
                    WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA')
                      AND INDEX_NAME IN ('{tempIndex}','{finalIndex}') AND TABLE_NAME<>'{spec.Table}';
                    IF v_bad_indexes>0 THEN
                        RAISE_APPLICATION_ERROR(-20219,'{spec.Table}.{spec.Column} conversion index name is already used by another table.');
                    END IF;
                    SELECT COUNT(*) INTO v_bad_constraints FROM ALL_CONSTRAINTS
                    WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA')
                      AND CONSTRAINT_NAME='{uniqueConstraint}' AND TABLE_NAME<>'{spec.Table}';
                    IF v_bad_constraints>0 THEN
                        RAISE_APPLICATION_ERROR(-20220,'{spec.Table}.{spec.Column} conversion constraint name is already used by another table.');
                    END IF;
                END IF;

                IF v_column_count=1 THEN
                    BEGIN
                        EXECUTE IMMEDIATE
                            'SELECT DATA_TYPE,CHAR_LENGTH,NULLABLE,NVL(DEFAULT_LENGTH,0),VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER=SYS_CONTEXT(''USERENV'',''CURRENT_SCHEMA'') AND TABLE_NAME=:table_name AND COLUMN_NAME=:column_name'
                            INTO v_type,v_length,v_nullable,v_default_length,v_virtual
                            USING '{spec.Table}','{spec.Column}';
                    EXCEPTION WHEN OTHERS THEN
                        IF SQLCODE != -904 THEN RAISE; END IF;
                        -- Oracle releases without virtual columns also omit this dictionary field.
                        EXECUTE IMMEDIATE
                            'SELECT DATA_TYPE,CHAR_LENGTH,NULLABLE,NVL(DEFAULT_LENGTH,0) FROM ALL_TAB_COLS WHERE OWNER=SYS_CONTEXT(''USERENV'',''CURRENT_SCHEMA'') AND TABLE_NAME=:table_name AND COLUMN_NAME=:column_name'
                            INTO v_type,v_length,v_nullable,v_default_length
                            USING '{spec.Table}','{spec.Column}';
                        v_virtual := 'NO';
                    END;

                    IF v_virtual<>'NO' OR v_default_length<>0 OR v_nullable<>'{(spec.Nullable ? "Y" : "N")}' THEN
                        RAISE_APPLICATION_ERROR(-20201,'{spec.Table}.{spec.Column} has incompatible virtual/default/nullability metadata.');
                    END IF;

                    {(spec.CheckConstraint is null || spec.CheckCondition is null ? "" : $"""
                    SELECT COUNT(*) INTO v_bad_constraints FROM ALL_CONSTRAINTS c
                    WHERE c.OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND c.TABLE_NAME='{spec.Table}'
                      AND c.CONSTRAINT_NAME='{spec.CheckConstraint}' AND c.CONSTRAINT_TYPE='C'
                      AND c.STATUS='ENABLED' AND c.VALIDATED='VALIDATED'
                      AND REGEXP_REPLACE(UPPER(c.SEARCH_CONDITION_VC), '[[:space:]\"()]', '')='{normalizedCheckCondition}';
                    IF v_bad_constraints<>1 THEN
                        RAISE_APPLICATION_ERROR(-20222,'{spec.Table}.{spec.Column} expected check constraint is missing or incompatible.');
                    END IF;
                    """)}

                    IF v_type='NVARCHAR2' THEN
                        IF v_length<{spec.Length} OR v_shadow_count=1 THEN
                            RAISE_APPLICATION_ERROR(-20202,'{spec.Table}.{spec.Column} has incompatible NVARCHAR2 length or an unexpected shadow column.');
                        END IF;
                    ELSIF v_type='VARCHAR2' THEN
                        IF v_shadow_count=1 THEN
                            BEGIN
                                EXECUTE IMMEDIATE
                                    'SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER=SYS_CONTEXT(''USERENV'',''CURRENT_SCHEMA'') AND TABLE_NAME=:table_name AND COLUMN_NAME=:column_name AND DATA_TYPE=''NVARCHAR2'' AND CHAR_LENGTH>=:column_length AND NVL(DEFAULT_LENGTH,0)=0 AND VIRTUAL_COLUMN=''NO'''
                                    INTO v_bad_rows USING '{spec.Table}','{spec.Shadow}',{spec.Length};
                            EXCEPTION WHEN OTHERS THEN
                                IF SQLCODE != -904 THEN RAISE; END IF;
                                EXECUTE IMMEDIATE
                                    'SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER=SYS_CONTEXT(''USERENV'',''CURRENT_SCHEMA'') AND TABLE_NAME=:table_name AND COLUMN_NAME=:column_name AND DATA_TYPE=''NVARCHAR2'' AND CHAR_LENGTH>=:column_length AND NVL(DEFAULT_LENGTH,0)=0'
                                    INTO v_bad_rows USING '{spec.Table}','{spec.Shadow}',{spec.Length};
                            END;
                            IF v_bad_rows<>1 THEN
                                RAISE_APPLICATION_ERROR(-20203,'{spec.Table}.{spec.Shadow} is not a compatible conversion shadow.');
                            END IF;
                            SELECT COUNT(*) INTO v_bad_rows FROM ALL_COL_COMMENTS
                            WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}' AND COLUMN_NAME='{spec.Shadow}'
                              AND COMMENTS='HR-OPS16-NVARCHAR2';
                            IF v_bad_rows<>1 THEN
                                RAISE_APPLICATION_ERROR(-20221,'{spec.Table}.{spec.Shadow} has no conversion marker; manual review is required.');
                            END IF;
                            -- The original column remains authoritative until the rename. BuildConversion
                            -- rewrites the owned shadow from it, then validates the copied values.
                        END IF;

                        EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM {table} WHERE {column} IS NOT NULL AND (LENGTH({column})>{spec.Length} OR TO_CHAR(TO_NCHAR({column}))<>{column})' INTO v_bad_rows;
                        IF v_bad_rows>0 THEN
                            RAISE_APPLICATION_ERROR(-20205,'{spec.Table}.{spec.Column} has values too long or not round-trippable to NVARCHAR2.');
                        END IF;
                    ELSE
                        RAISE_APPLICATION_ERROR(-20206,'{spec.Table}.{spec.Column} has unsupported type '||v_type||'.');
                    END IF;

                    SELECT COUNT(*) INTO v_bad_constraints
                    FROM ALL_CONS_COLUMNS cc JOIN ALL_CONSTRAINTS c
                      ON c.OWNER=cc.OWNER AND c.CONSTRAINT_NAME=cc.CONSTRAINT_NAME
                    WHERE cc.OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND cc.TABLE_NAME='{spec.Table}' AND cc.COLUMN_NAME='{spec.Column}'
                      AND NOT ({allowedUniqueConstraint} OR {allowedCheckConstraint} OR {notNullCheck});
                    IF v_bad_constraints>0 THEN
                        RAISE_APPLICATION_ERROR(-20207,'{spec.Table}.{spec.Column} participates in unsupported constraints.');
                    END IF;

                    SELECT COUNT(*) INTO v_bad_indexes FROM ALL_IND_COLUMNS i
                    WHERE i.INDEX_OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND i.TABLE_NAME='{spec.Table}' AND i.COLUMN_NAME='{spec.Column}'
                      AND i.INDEX_NAME NOT IN ('{uniqueConstraint}','{tempIndex}','{finalIndex}');
                    IF v_bad_indexes>0 THEN
                        RAISE_APPLICATION_ERROR(-20208,'{spec.Table}.{spec.Column} participates in unsupported indexes.');
                    END IF;
                END IF;

                IF v_column_count=0 AND v_shadow_count=1 THEN
                    BEGIN
                        EXECUTE IMMEDIATE
                            'SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER=SYS_CONTEXT(''USERENV'',''CURRENT_SCHEMA'') AND TABLE_NAME=:table_name AND COLUMN_NAME=:column_name AND DATA_TYPE=''NVARCHAR2'' AND CHAR_LENGTH>=:column_length AND NULLABLE=:nullable AND VIRTUAL_COLUMN=''NO'''
                            INTO v_bad_rows USING '{spec.Table}','{spec.Shadow}',{spec.Length},'{(spec.Nullable ? "Y" : "N")}';
                    EXCEPTION WHEN OTHERS THEN
                        IF SQLCODE != -904 THEN RAISE; END IF;
                        EXECUTE IMMEDIATE
                            'SELECT COUNT(*) FROM ALL_TAB_COLS WHERE OWNER=SYS_CONTEXT(''USERENV'',''CURRENT_SCHEMA'') AND TABLE_NAME=:table_name AND COLUMN_NAME=:column_name AND DATA_TYPE=''NVARCHAR2'' AND CHAR_LENGTH>=:column_length AND NULLABLE=:nullable'
                            INTO v_bad_rows USING '{spec.Table}','{spec.Shadow}',{spec.Length},'{(spec.Nullable ? "Y" : "N")}';
                    END;
                    IF v_bad_rows<>1 THEN
                        RAISE_APPLICATION_ERROR(-20209,'{spec.Table}.{spec.Shadow} is not a compatible resumable conversion shadow.');
                    END IF;
                    SELECT COUNT(*) INTO v_bad_rows FROM ALL_COL_COMMENTS
                    WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}' AND COLUMN_NAME='{spec.Shadow}'
                      AND COMMENTS='HR-OPS16-NVARCHAR2';
                    IF v_bad_rows<>1 THEN
                        RAISE_APPLICATION_ERROR(-20210,'{spec.Table}.{spec.Shadow} has no conversion marker; manual review is required.');
                    END IF;
                END IF;

                IF '{(spec.Unique ? "Y" : "N")}'='Y' THEN
                    SELECT COUNT(*) INTO v_key_state FROM ALL_CONSTRAINTS
                    WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}'
                      AND CONSTRAINT_NAME='{uniqueConstraint}' AND CONSTRAINT_TYPE='U' AND STATUS='ENABLED' AND VALIDATED='VALIDATED';
                    SELECT COUNT(*) INTO v_bad_constraints
                    FROM ALL_CONS_COLUMNS cc JOIN ALL_CONSTRAINTS c
                      ON c.OWNER=cc.OWNER AND c.CONSTRAINT_NAME=cc.CONSTRAINT_NAME
                    WHERE c.OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND c.TABLE_NAME='{spec.Table}'
                      AND c.CONSTRAINT_NAME='{uniqueConstraint}'
                      AND (c.CONSTRAINT_TYPE<>'U' OR c.STATUS<>'ENABLED' OR c.VALIDATED<>'VALIDATED'
                           OR cc.COLUMN_NAME<>'{spec.Column}' OR cc.POSITION<>1);
                    IF v_bad_constraints>0 THEN
                        RAISE_APPLICATION_ERROR(-20216,'{uniqueConstraint} does not uniquely constrain {spec.Column} alone.');
                    END IF;
                    SELECT COUNT(*) INTO v_bad_constraints FROM ALL_CONSTRAINTS
                    WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND CONSTRAINT_TYPE='R'
                      AND R_OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA')
                      AND R_CONSTRAINT_NAME='{uniqueConstraint}';
                    IF v_bad_constraints>0 THEN
                        RAISE_APPLICATION_ERROR(-20218,'{uniqueConstraint} is referenced by a foreign key; review dependencies before conversion.');
                    END IF;
                    SELECT COUNT(*) INTO v_bad_indexes FROM ALL_IND_COLUMNS
                    WHERE INDEX_OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}'
                      AND INDEX_NAME IN ('{tempIndex}','{finalIndex}')
                      AND ((INDEX_NAME='{tempIndex}' AND (COLUMN_NAME<>'{spec.Shadow}' OR COLUMN_POSITION<>1))
                           OR (INDEX_NAME='{finalIndex}' AND (COLUMN_NAME<>'{spec.Column}' OR COLUMN_POSITION<>1)));
                    IF v_bad_indexes>0 THEN
                        RAISE_APPLICATION_ERROR(-20217,'{spec.Table}.{spec.Column} unique conversion index has an unexpected shape.');
                    END IF;
                    IF v_key_state=0 THEN
                        SELECT COUNT(*) INTO v_key_state FROM ALL_INDEXES
                        WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}'
                          AND INDEX_NAME IN ('{tempIndex}','{finalIndex}') AND UNIQUENESS='UNIQUE' AND STATUS='VALID';
                        IF v_key_state=0 THEN
                            RAISE_APPLICATION_ERROR(-20211,'{spec.Table}.{spec.Column} uniqueness is missing.');
                        END IF;
                    END IF;
                    IF v_type='VARCHAR2' THEN
                        EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM (SELECT TO_NCHAR({column}) FROM {table} WHERE {column} IS NOT NULL GROUP BY TO_NCHAR({column}) HAVING COUNT(*)>1)' INTO v_bad_rows;
                        IF v_bad_rows>0 THEN
                            RAISE_APPLICATION_ERROR(-20212,'Converted {spec.Table}.{spec.Column} values would violate uniqueness.');
                        END IF;
                    END IF;
                END IF;
            END;
            """;
    }

    private static string BuildConversion(ColumnSpec spec)
    {
        var table = Q(spec.Table);
        var column = Q(spec.Column);
        var shadow = Q(spec.Shadow);
        var keySql = spec.Unique
            ? $"""
                SELECT COUNT(*) INTO v_count FROM ALL_INDEXES WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}' AND INDEX_NAME='{spec.TemporaryIndexName}';
                IF v_count=0 THEN EXECUTE IMMEDIATE 'CREATE UNIQUE INDEX {spec.TemporaryIndexName} ON {table} ({shadow})'; END IF;
                SELECT COUNT(*) INTO v_count FROM ALL_CONSTRAINTS WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}' AND CONSTRAINT_NAME='{spec.UniqueConstraintName}';
                IF v_count>0 THEN EXECUTE IMMEDIATE 'ALTER TABLE {table} DROP CONSTRAINT {spec.UniqueConstraintName}'; END IF;
                """
            : "";
        var finalizeKeySql = spec.Unique
            ? $"""
                SELECT COUNT(*) INTO v_count FROM ALL_INDEXES WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}' AND INDEX_NAME='{spec.UniqueConstraintName}';
                IF v_count=0 THEN
                    SELECT COUNT(*) INTO v_count FROM ALL_INDEXES WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}' AND INDEX_NAME='{spec.TemporaryIndexName}';
                    IF v_count=1 THEN EXECUTE IMMEDIATE 'ALTER INDEX {spec.TemporaryIndexName} RENAME TO {spec.UniqueConstraintName}'; END IF;
                END IF;
                SELECT COUNT(*) INTO v_count FROM ALL_CONSTRAINTS WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}' AND CONSTRAINT_NAME='{spec.UniqueConstraintName}';
                IF v_count=0 THEN EXECUTE IMMEDIATE 'ALTER TABLE {table} ADD CONSTRAINT {spec.UniqueConstraintName} UNIQUE ({column}) USING INDEX {spec.UniqueConstraintName} ENABLE VALIDATE'; END IF;
                """
            : "NULL;";
        var checkDropSql = spec.CheckConstraint is null
            ? ""
            : $"""
                SELECT COUNT(*) INTO v_count FROM ALL_CONSTRAINTS
                WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}' AND CONSTRAINT_NAME='{spec.CheckConstraint}';
                IF v_count=1 THEN EXECUTE IMMEDIATE 'ALTER TABLE {table} DROP CONSTRAINT {spec.CheckConstraint}'; END IF;
                """;
        var checkConditionForDynamicSql = spec.CheckCondition?.Replace("'", "''", StringComparison.Ordinal);
        var checkAddSql = spec.CheckConstraint is null || checkConditionForDynamicSql is null
            ? ""
            : $"EXECUTE IMMEDIATE 'ALTER TABLE {table} ADD CONSTRAINT {spec.CheckConstraint} CHECK ({checkConditionForDynamicSql}) ENABLE VALIDATE';";

        return $"""
            DECLARE
                v_table_count NUMBER;
                v_column_count NUMBER;
                v_shadow_count NUMBER;
                v_count NUMBER;
                v_type VARCHAR2(30);
                v_bad NUMBER;
            BEGIN
                SELECT COUNT(*) INTO v_table_count FROM ALL_TABLES
                WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}';
                IF v_table_count=0 THEN RETURN; END IF;
                SELECT COUNT(*) INTO v_column_count FROM ALL_TAB_COLS
                WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}' AND COLUMN_NAME='{spec.Column}';
                SELECT COUNT(*) INTO v_shadow_count FROM ALL_TAB_COLS
                WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}' AND COLUMN_NAME='{spec.Shadow}';
                IF v_column_count=0 AND v_shadow_count=0 THEN RETURN; END IF;
                IF '{(spec.Unique ? "Y" : "N")}'='Y' THEN
                    SELECT COUNT(*) INTO v_bad FROM ALL_INDEXES
                    WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA')
                      AND INDEX_NAME IN ('{spec.TemporaryIndexName}','{spec.UniqueConstraintName}') AND TABLE_NAME<>'{spec.Table}';
                    IF v_bad>0 THEN RAISE_APPLICATION_ERROR(-20219,'HR_LEAVE_BALANCE_MIGRATION conversion index name is already used by another table.'); END IF;
                    SELECT COUNT(*) INTO v_bad FROM ALL_CONSTRAINTS
                    WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA')
                      AND CONSTRAINT_NAME='{spec.UniqueConstraintName}' AND TABLE_NAME<>'{spec.Table}';
                    IF v_bad>0 THEN RAISE_APPLICATION_ERROR(-20220,'HR_LEAVE_BALANCE_MIGRATION conversion constraint name is already used by another table.'); END IF;
                END IF;

                IF v_column_count=1 THEN
                    SELECT DATA_TYPE INTO v_type FROM ALL_TAB_COLS
                    WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME='{spec.Table}' AND COLUMN_NAME='{spec.Column}';
                    IF v_type='VARCHAR2' THEN
                        IF v_shadow_count=0 THEN
                            EXECUTE IMMEDIATE 'ALTER TABLE {table} ADD ({shadow} NVARCHAR2({spec.Length}) NULL)';
                            EXECUTE IMMEDIATE 'COMMENT ON COLUMN {table}.{shadow} IS ''HR-OPS16-NVARCHAR2''';
                        END IF;
                        EXECUTE IMMEDIATE 'UPDATE {table} SET {shadow}=TO_NCHAR({column})';
                        EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM {table} WHERE ({column} IS NULL AND {shadow} IS NOT NULL) OR ({column} IS NOT NULL AND ({shadow} IS NULL OR LENGTH({shadow})>{spec.Length} OR TO_CHAR({shadow})<>{column}))' INTO v_bad;
                        IF v_bad>0 THEN RAISE_APPLICATION_ERROR(-20213,'{spec.Table}.{spec.Column} conversion validation failed; source column retained.'); END IF;
                        {(spec.Nullable ? "" : $"EXECUTE IMMEDIATE 'ALTER TABLE {table} MODIFY ({shadow} NOT NULL)';")}
                        {keySql}
                        {checkDropSql}
                        EXECUTE IMMEDIATE 'ALTER TABLE {table} DROP COLUMN {column}';
                        EXECUTE IMMEDIATE 'ALTER TABLE {table} RENAME COLUMN {shadow} TO {column}';
                        {finalizeKeySql}
                        {checkAddSql}
                    ELSIF v_type='NVARCHAR2' THEN
                        {finalizeKeySql}
                    ELSE
                        RAISE_APPLICATION_ERROR(-20214,'{spec.Table}.{spec.Column} changed to an unsupported type during conversion.');
                    END IF;
                ELSE
                    IF v_shadow_count<>1 THEN RAISE_APPLICATION_ERROR(-20215,'{spec.Table}.{spec.Column} and its conversion shadow are both missing.'); END IF;
                    EXECUTE IMMEDIATE 'ALTER TABLE {table} RENAME COLUMN {shadow} TO {column}';
                    {finalizeKeySql}
                    {checkAddSql}
                END IF;
            END;
            """;
    }

    private static string Q(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
