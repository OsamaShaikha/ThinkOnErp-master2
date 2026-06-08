-- Migrate existing tenant schema THINKONERP_NEW1 for schema routing
-- Run connected as a user with DBA/GRANT privs (e.g., system, or as THINKON_ERP
-- if it has GRANT ANY OBJECT PRIVILEGE)

-- Grant access on ALL tables in the tenant schema to THINKON_ERP master user.
-- This is needed because CURRENT_SCHEMA routing means THINKON_ERP queries
-- objects in the tenant schema directly.

SET SERVEROUTPUT ON

DECLARE
    v_master_user VARCHAR2(100) := 'THINKON_ERP';
    v_tenant_schema VARCHAR2(100) := 'THINKONERP_NEW1';
BEGIN
    FOR r IN (SELECT table_name FROM all_tables WHERE owner = v_tenant_schema ORDER BY table_name)
    LOOP
        BEGIN
            EXECUTE IMMEDIATE 'GRANT SELECT, INSERT, UPDATE, DELETE ON "' || v_tenant_schema || '"."' || r.table_name || '" TO "' || v_master_user || '"';
            DBMS_OUTPUT.PUT_LINE('Granted: ' || r.table_name);
        EXCEPTION
            WHEN OTHERS THEN
                DBMS_OUTPUT.PUT_LINE('Skipped ' || r.table_name || ': ' || SQLERRM);
        END;
    END LOOP;
END;
/

-- Verify
SELECT table_name, privilege, grantee
FROM all_tab_privs
WHERE owner = 'THINKONERP_NEW1'
  AND grantee = 'THINKON_ERP'
ORDER BY table_name;

EXIT;
