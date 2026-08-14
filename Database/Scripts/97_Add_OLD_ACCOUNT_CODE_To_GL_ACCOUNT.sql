-- ====================================================================
-- Script Name: 97_Add_OLD_ACCOUNT_CODE_To_GL_ACCOUNT.sql
-- Description: Adds OLD_ACCOUNT_CODE column and index to GL_ACCOUNT table in Oracle DB.
-- Author: ThinkOnErp Development Team
-- Date: 2026-08-12
-- Schema: DEV_TEMPLATE / THINKON_ERP / Tenant Schemas
-- ====================================================================

DECLARE
    v_column_exists NUMBER;
    v_index_exists  NUMBER;
BEGIN
    -- 1. Check if OLD_ACCOUNT_CODE column exists in GL_ACCOUNT
    SELECT COUNT(*) INTO v_column_exists
    FROM all_tab_cols
    WHERE table_name = 'GL_ACCOUNT' AND column_name = 'OLD_ACCOUNT_CODE' AND owner = USER;

    IF v_column_exists = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE GL_ACCOUNT ADD OLD_ACCOUNT_CODE NVARCHAR2(50)';
        DBMS_OUTPUT.PUT_LINE('Added OLD_ACCOUNT_CODE column to GL_ACCOUNT table.');
    END IF;

    -- 2. Check if IX_GL_ACCOUNT_OLD_CODE index exists
    SELECT COUNT(*) INTO v_index_exists
    FROM all_indexes
    WHERE index_name = 'IX_GL_ACCOUNT_OLD_CODE' AND owner = USER;

    IF v_index_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_GL_ACCOUNT_OLD_CODE ON GL_ACCOUNT (OLD_ACCOUNT_CODE)';
        DBMS_OUTPUT.PUT_LINE('Created IX_GL_ACCOUNT_OLD_CODE index.');
    END IF;
END;
/
