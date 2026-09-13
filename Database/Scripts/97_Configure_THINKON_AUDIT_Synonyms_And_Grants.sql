-- =============================================================================
-- ThinkOn ERP - Configure THINKON_AUDIT Grants and Synonyms Across Schemas
-- Script 97: Synchronize Centralized Audit Schema Access
-- =============================================================================

SET SERVEROUTPUT ON SIZE UNLIMITED;

PROMPT =============================================================================
PROMPT 1. Granting THINKON_AUDIT Object Privileges to Schemas
PROMPT =============================================================================

DECLARE
    TYPE t_schemas IS TABLE OF VARCHAR2(100);
    v_schemas t_schemas := t_schemas(
        'THINKON_ERP', 
        'DEV_TEMPLATE', 
        'THINKONERP_1122', 
        'THINKONERP_A17', 
        'THINKONERP_D1', 
        'THINKONERP_D2', 
        'THINKONERP_KM'
    );
BEGIN
    FOR s IN (
        SELECT username FROM all_users 
        WHERE username IN ('THINKON_ERP', 'DEV_TEMPLATE') 
           OR username LIKE 'THINKONERP_%'
    ) LOOP
        FOR t IN (
            SELECT table_name FROM all_tables WHERE owner = 'THINKON_AUDIT'
        ) LOOP
            BEGIN
                EXECUTE IMMEDIATE 'GRANT SELECT, INSERT, UPDATE, DELETE ON "THINKON_AUDIT"."' || t.table_name || '" TO "' || s.username || '"';
            EXCEPTION WHEN OTHERS THEN
                NULL;
            END;
        END LOOP;
    END LOOP;
    DBMS_OUTPUT.PUT_LINE('Finished granting privileges on THINKON_AUDIT tables to all schemas.');
END;
/

PROMPT =============================================================================
PROMPT 2. Harmonizing Audit Synonyms in DEV_TEMPLATE and Tenant Schemas
PROMPT =============================================================================

DECLARE
    v_row_count NUMBER;
    TYPE t_audit_tables IS TABLE OF VARCHAR2(50);
    v_audit_tables t_audit_tables := t_audit_tables(
        'SYS_AUDIT_LOG',
        'SYS_AUDIT_LOG_ARCHIVE',
        'SYS_RETENTION_POLICIES',
        'SYS_AUDIT_INTEGRITY'
    );
BEGIN
    FOR s IN (
        SELECT username AS schema_name FROM all_users 
        WHERE username = 'DEV_TEMPLATE' 
           OR username IN ('THINKONERP_1122', 'THINKONERP_A17', 'THINKONERP_D1', 'THINKONERP_D2', 'THINKONERP_KM')
    ) LOOP
        FOR i IN 1..v_audit_tables.COUNT LOOP
            DECLARE
                v_table VARCHAR2(50) := v_audit_tables(i);
                v_is_table NUMBER := 0;
                v_is_synonym NUMBER := 0;
            BEGIN
                -- Check if physical table exists
                SELECT COUNT(*) INTO v_is_table 
                FROM all_tables 
                WHERE owner = s.schema_name AND table_name = v_table;

                IF v_is_table > 0 THEN
                    -- Check row count
                    EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM "' || s.schema_name || '"."' || v_table || '"' INTO v_row_count;
                    IF v_row_count = 0 THEN
                        -- Safe to drop empty local table in favor of centralized audit synonym
                        EXECUTE IMMEDIATE 'DROP TABLE "' || s.schema_name || '"."' || v_table || '" CASCADE CONSTRAINTS';
                        DBMS_OUTPUT.PUT_LINE('Dropped empty local table: ' || s.schema_name || '.' || v_table);
                        v_is_table := 0;
                    ELSE
                        DBMS_OUTPUT.PUT_LINE('WARNING: ' || s.schema_name || '.' || v_table || ' has ' || v_row_count || ' rows, skipping drop.');
                    END IF;
                END IF;

                IF v_is_table = 0 THEN
                    -- Check if synonym already exists
                    SELECT COUNT(*) INTO v_is_synonym 
                    FROM all_synonyms 
                    WHERE owner = s.schema_name AND synonym_name = v_table;

                    IF v_is_synonym = 0 THEN
                        EXECUTE IMMEDIATE 'CREATE SYNONYM "' || s.schema_name || '"."' || v_table || '" FOR "THINKON_AUDIT"."' || v_table || '"';
                        DBMS_OUTPUT.PUT_LINE('Created synonym: ' || s.schema_name || '.' || v_table || ' -> THINKON_AUDIT.' || v_table);
                    ELSE
                        DBMS_OUTPUT.PUT_LINE('Synonym already exists: ' || s.schema_name || '.' || v_table);
                    END IF;
                END IF;
            EXCEPTION WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('Error on ' || s.schema_name || '.' || v_table || ': ' || SQLERRM);
            END;
        END LOOP;
    END LOOP;
END;
/

PROMPT =============================================================================
PROMPT 3. Verification of Audit Synonyms and Connectivity
PROMPT =============================================================================

SELECT s.owner AS schema_name, s.synonym_name, s.table_owner, s.table_name
FROM all_synonyms s
WHERE s.owner IN ('DEV_TEMPLATE', 'THINKONERP_1122', 'THINKONERP_A17', 'THINKONERP_D1', 'THINKONERP_D2', 'THINKONERP_KM')
  AND s.synonym_name IN ('SYS_AUDIT_LOG', 'SYS_AUDIT_LOG_ARCHIVE', 'SYS_RETENTION_POLICIES', 'SYS_AUDIT_INTEGRITY')
ORDER BY s.owner, s.synonym_name;

COMMIT;
EXIT;
