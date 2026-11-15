using System.Text.RegularExpressions;

namespace ThinkOnErp.Infrastructure.Services;

internal static class OracleTenantSchemaDiffSqlBuilder
{
    private static readonly Regex SchemaIdentifier = new("^[A-Z][A-Z0-9_$#]{0,29}$", RegexOptions.CultureInvariant);

    public static string Build(string sourceSchema, string targetSchema)
    {
        ValidateSchemaIdentifier(sourceSchema, nameof(sourceSchema));
        ValidateSchemaIdentifier(targetSchema, nameof(targetSchema));
        if (string.Equals(sourceSchema, targetSchema, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Source and target Oracle schemas must be different.", nameof(targetSchema));

        return $@"
            DECLARE
                v_sql VARCHAR2(4000);
                v_length NUMBER;
                v_length_semantics VARCHAR2(8);
            BEGIN
                FOR r IN (
                    SELECT src.table_name, src.column_name, src.data_type, src.data_length,
                           src.data_precision, src.data_scale, src.char_length, src.char_used,
                           src.nullable, src.data_default
                    FROM all_tab_columns src
                    JOIN all_tables src_table ON src_table.owner=src.owner AND src_table.table_name=src.table_name
                    JOIN all_tables target_table ON target_table.owner='{targetSchema}' AND target_table.table_name=src.table_name
                    LEFT JOIN all_tab_columns tgt ON tgt.owner='{targetSchema}' AND tgt.table_name=src.table_name AND tgt.column_name=src.column_name
                    WHERE src.owner='{sourceSchema}' AND tgt.column_name IS NULL
                    ORDER BY src.table_name, src.column_id
                ) LOOP
                    v_sql := 'ALTER TABLE ""{targetSchema}"".""' || r.table_name || '"" ADD ""' || r.column_name || '"" ' || r.data_type;
                    IF r.data_type IN ('VARCHAR2', 'NVARCHAR2', 'RAW', 'CHAR', 'NCHAR') THEN
                        IF r.data_type='RAW' THEN
                            v_length := r.data_length;
                            v_length_semantics := '';
                        ELSIF r.data_type IN ('NVARCHAR2', 'NCHAR') OR r.char_used='C' THEN
                            v_length := r.char_length;
                            v_length_semantics := CASE WHEN r.data_type IN ('NVARCHAR2', 'NCHAR') THEN '' ELSE ' CHAR' END;
                        ELSE
                            v_length := r.data_length;
                            v_length_semantics := ' BYTE';
                        END IF;
                        v_sql := v_sql || '(' || v_length || v_length_semantics || ')';
                    ELSIF r.data_type='NUMBER' AND r.data_precision IS NOT NULL THEN
                        IF r.data_scale IS NOT NULL AND r.data_scale >= 0 THEN
                            v_sql := v_sql || '(' || r.data_precision || ',' || r.data_scale || ')';
                        ELSE
                            v_sql := v_sql || '(' || r.data_precision || ')';
                        END IF;
                    END IF;

                    IF r.data_default IS NOT NULL THEN
                        v_sql := v_sql || ' DEFAULT ' || TRIM(r.data_default);
                    END IF;
                    IF r.nullable='N' AND r.data_default IS NOT NULL THEN
                        v_sql := v_sql || ' NOT NULL';
                    ELSE
                        v_sql := v_sql || ' NULL';
                    END IF;
                    EXECUTE IMMEDIATE v_sql;
                END LOOP;

                FOR r IN (
                    SELECT src.table_name, src.column_name, src.data_type,
                           src.data_length AS src_length, src.char_length AS src_char_length,
                           src.char_used AS src_char_used, tgt.data_length AS tgt_length,
                           tgt.char_length AS tgt_char_length, tgt.char_used AS tgt_char_used,
                           src.data_precision AS src_precision, src.data_scale AS src_scale,
                           tgt.data_precision AS tgt_precision, tgt.data_scale AS tgt_scale
                    FROM all_tab_columns src
                    JOIN all_tab_columns tgt ON tgt.owner='{targetSchema}' AND tgt.table_name=src.table_name AND tgt.column_name=src.column_name
                    WHERE src.owner='{sourceSchema}' AND src.data_type=tgt.data_type
                      AND (
                        (src.data_type IN ('NVARCHAR2','NCHAR') OR src.char_used='C')
                            AND src.char_length>tgt.char_length
                        OR src.data_type='RAW' AND src.data_length>tgt.data_length
                        OR src.data_type IN ('VARCHAR2','CHAR') AND src.char_used='B' AND src.data_length>tgt.data_length
                        OR src.data_type='NUMBER' AND src.data_precision IS NOT NULL AND tgt.data_precision IS NOT NULL
                            AND NVL(src.data_scale,0)>=NVL(tgt.data_scale,0)
                            AND src.data_precision-NVL(src.data_scale,0)>=tgt.data_precision-NVL(tgt.data_scale,0)
                      )
                ) LOOP
                    IF r.data_type IN ('NVARCHAR2','NCHAR') OR r.src_char_used='C' THEN
                        v_length := r.src_char_length;
                        v_length_semantics := CASE WHEN r.data_type IN ('NVARCHAR2','NCHAR') THEN '' ELSE ' CHAR' END;
                        v_sql := 'ALTER TABLE ""{targetSchema}"".""' || r.table_name || '"" MODIFY (""' || r.column_name || '"" ' || r.data_type || '(' || v_length || v_length_semantics || '))';
                    ELSIF r.data_type='RAW' THEN
                        v_sql := 'ALTER TABLE ""{targetSchema}"".""' || r.table_name || '"" MODIFY (""' || r.column_name || '"" RAW(' || r.src_length || '))';
                    ELSIF r.data_type IN ('VARCHAR2','CHAR') THEN
                        v_sql := 'ALTER TABLE ""{targetSchema}"".""' || r.table_name || '"" MODIFY (""' || r.column_name || '"" ' || r.data_type || '(' || r.src_length || ' BYTE))';
                    ELSE
                        IF r.src_scale IS NOT NULL AND r.src_scale >= 0 THEN
                            v_sql := 'ALTER TABLE ""{targetSchema}"".""' || r.table_name || '"" MODIFY (""' || r.column_name || '"" NUMBER(' || r.src_precision || ',' || r.src_scale || '))';
                        ELSE
                            v_sql := 'ALTER TABLE ""{targetSchema}"".""' || r.table_name || '"" MODIFY (""' || r.column_name || '"" NUMBER(' || r.src_precision || '))';
                        END IF;
                    END IF;
                    EXECUTE IMMEDIATE v_sql;
                END LOOP;
            END;";
    }

    public static void ValidateSchemaIdentifier(string schema, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(schema) || !SchemaIdentifier.IsMatch(schema))
            throw new ArgumentException("Oracle schema name is not a valid unquoted identifier.", parameterName);
    }
}
