-- ====================================================================
-- Script Name: 98_Consolidated_Latest_Database_Updates.sql
-- Description: Complete consolidated Oracle DDL & DML update script for ThinkOnErp.
-- Includes:
--   1. SYS_API_CATEGORIES (Table & Seed Data)
--   2. SYS_API_ENDPOINTS (Sequence, Table & Seed Endpoints including COA Structure)
--   3. GL_ACCOUNT_STRUCTURE_CONFIG (Table & 5-Level Seed Config)
--   4. GL_ACCOUNT.OLD_ACCOUNT_CODE (Column & Index)
-- Author: ThinkOnErp Development Team
-- Date: 2026-08-13
-- Target Schemas: Master (THINKON_ERP), Developer Template (DEV_TEMPLATE), Tenant Schemas
-- ====================================================================

SET DEFINE OFF;
SET SERVEROUTPUT ON;

-- --------------------------------------------------------------------
-- 1. SYS_API_CATEGORIES Table & Seed Data
-- --------------------------------------------------------------------
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM all_tables WHERE table_name = 'SYS_API_CATEGORIES' AND owner = USER;
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE SYS_API_CATEGORIES (
                CATEGORY_CODE    VARCHAR2(50)   NOT NULL PRIMARY KEY,
                DISPLAY_TITLE    VARCHAR2(150)  NOT NULL,
                DESCRIPTION      VARCHAR2(500),
                DISPLAY_ORDER    NUMBER(5)      DEFAULT 0 NOT NULL,
                CONTROLLER_NAMES VARCHAR2(1000),
                IS_ACTIVE        NUMBER(1)      DEFAULT 1 NOT NULL
            )
        ';
        DBMS_OUTPUT.PUT_LINE('Created table SYS_API_CATEGORIES.');
    END IF;
END;
/

MERGE INTO SYS_API_CATEGORIES target
USING (
    SELECT 'superadmin' AS CATEGORY_CODE, '1. SuperAdmin API (إدارة النظام)' AS DISPLAY_TITLE, 'APIs for SuperAdmin tenant provisioning and system maintenance' AS DESCRIPTION, 1 AS DISPLAY_ORDER, 1 AS IS_ACTIVE FROM DUAL UNION ALL
    SELECT 'company', '2. Company & Tenant API (إدارة الشركات والفروع)', 'APIs for managing companies, branches, tenant schemas, and branch permissions', 2, 1 FROM DUAL UNION ALL
    SELECT 'accounting', '3. Accounting & COA API (النظام المحاسبي)', 'APIs for General Ledger, Chart of Accounts, currencies, fiscal years, and COA structure', 3, 1 FROM DUAL UNION ALL
    SELECT 'auth', '4. Auth & Security API (المصادقة والصلاحيات)', 'APIs for authentication, users, roles, and granular permissions', 4, 1 FROM DUAL UNION ALL
    SELECT 'audit', '5. Audit & Monitoring API (سجلات التدقيق الأمني)', 'APIs for audit logs, trail tracking, system monitoring, health status, and security alerts', 5, 1 FROM DUAL UNION ALL
    SELECT 'support', '6. Support & Ticketing API (الدعم الفني والخدمات)', 'APIs for support tickets, issue management, and ticket classifications', 6, 1 FROM DUAL UNION ALL
    SELECT 'system', '7. System & Lookup Codes API (إعدادات كود النظام)', 'APIs for system codes, settings, document management, and public configurations', 7, 1 FROM DUAL
) src
ON (target.CATEGORY_CODE = src.CATEGORY_CODE)
WHEN MATCHED THEN
    UPDATE SET target.DISPLAY_TITLE = src.DISPLAY_TITLE, target.DESCRIPTION = src.DESCRIPTION, target.DISPLAY_ORDER = src.DISPLAY_ORDER, target.IS_ACTIVE = src.IS_ACTIVE
WHEN NOT MATCHED THEN
    INSERT (CATEGORY_CODE, DISPLAY_TITLE, DESCRIPTION, DISPLAY_ORDER, IS_ACTIVE)
    VALUES (src.CATEGORY_CODE, src.DISPLAY_TITLE, src.DESCRIPTION, src.DISPLAY_ORDER, src.IS_ACTIVE);

COMMIT;
/

-- --------------------------------------------------------------------
-- 2. SYS_API_ENDPOINTS Sequence & Table & Seed Data
-- --------------------------------------------------------------------
DECLARE
    v_seq_count NUMBER;
    v_tbl_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_seq_count FROM all_sequences WHERE sequence_name = 'SEQ_SYS_API_ENDPOINTS' AND sequence_owner = USER;
    IF v_seq_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_SYS_API_ENDPOINTS START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE';
        DBMS_OUTPUT.PUT_LINE('Created sequence SEQ_SYS_API_ENDPOINTS.');
    END IF;

    SELECT COUNT(*) INTO v_tbl_count FROM all_tables WHERE table_name = 'SYS_API_ENDPOINTS' AND owner = USER;
    IF v_tbl_count = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE SYS_API_ENDPOINTS (
                ID              NUMBER(19)    NOT NULL PRIMARY KEY,
                CATEGORY_CODE   VARCHAR2(50)  NOT NULL,
                CONTROLLER_NAME VARCHAR2(100) NOT NULL,
                ACTION_NAME     VARCHAR2(100) NOT NULL,
                HTTP_METHOD     VARCHAR2(10)  NOT NULL,
                ROUTE_PATH      VARCHAR2(300) NOT NULL,
                DISPLAY_NAME    VARCHAR2(200),
                DESCRIPTION     VARCHAR2(500),
                IS_ACTIVE       NUMBER(1)     DEFAULT 1 NOT NULL,
                CONSTRAINT FK_SYS_API_ENDPOINTS_CAT FOREIGN KEY (CATEGORY_CODE) REFERENCES SYS_API_CATEGORIES(CATEGORY_CODE),
                CONSTRAINT UX_SYS_API_ENDPOINTS_ROUTE UNIQUE (HTTP_METHOD, ROUTE_PATH)
            )
        ';
        DBMS_OUTPUT.PUT_LINE('Created table SYS_API_ENDPOINTS.');
    END IF;
END;
/

MERGE INTO SYS_API_ENDPOINTS target
USING (
    SELECT 'accounting' AS CATEGORY_CODE, 'GlAccountStructureController' AS CONTROLLER_NAME, 'GetStructureConfigs' AS ACTION_NAME, 'GET' AS HTTP_METHOD, 'api/accounting/gl-account-structure' AS ROUTE_PATH, 'Get COA Level Digit Configurations' AS DISPLAY_NAME FROM DUAL UNION ALL
    SELECT 'accounting', 'GlAccountStructureController', 'UpdateStructureConfigs', 'PUT', 'api/accounting/gl-account-structure', 'Update COA Level Digit Configurations' FROM DUAL UNION ALL
    SELECT 'accounting', 'GlAccountStructureController', 'PreviewDefaultTree', 'GET', 'api/accounting/gl-account-structure/preview-default-tree', 'Preview Default COA Tree' FROM DUAL UNION ALL
    SELECT 'accounting', 'GlAccountStructureController', 'SeedDefaultTree', 'POST', 'api/accounting/gl-account-structure/seed-default-tree', 'Seed Default COA Tree' FROM DUAL
) src
ON (target.HTTP_METHOD = src.HTTP_METHOD AND target.ROUTE_PATH = src.ROUTE_PATH)
WHEN MATCHED THEN
    UPDATE SET target.CATEGORY_CODE = src.CATEGORY_CODE, target.CONTROLLER_NAME = src.CONTROLLER_NAME, target.ACTION_NAME = src.ACTION_NAME, target.DISPLAY_NAME = src.DISPLAY_NAME
WHEN NOT MATCHED THEN
    INSERT (ID, CATEGORY_CODE, CONTROLLER_NAME, ACTION_NAME, HTTP_METHOD, ROUTE_PATH, DISPLAY_NAME, IS_ACTIVE)
    VALUES (SEQ_SYS_API_ENDPOINTS.NEXTVAL, src.CATEGORY_CODE, src.CONTROLLER_NAME, src.ACTION_NAME, src.HTTP_METHOD, src.ROUTE_PATH, src.DISPLAY_NAME, 1);

COMMIT;
/

-- --------------------------------------------------------------------
-- 3. GL_ACCOUNT_STRUCTURE_CONFIG Table & Seed Data
-- --------------------------------------------------------------------
DECLARE
    v_tbl_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_tbl_count FROM all_tables WHERE table_name = 'GL_ACCOUNT_STRUCTURE_CONFIG' AND owner = USER;
    IF v_tbl_count = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE GL_ACCOUNT_STRUCTURE_CONFIG (
                LEVEL_NUMBER  NUMBER(2)      NOT NULL PRIMARY KEY,
                DIGIT_LENGTH  NUMBER(2)      NOT NULL,
                LEVEL_NAME_AR NVARCHAR2(100) NOT NULL,
                LEVEL_NAME_EN NVARCHAR2(100) NOT NULL,
                DESCRIPTION   NVARCHAR2(500),
                IS_ACTIVE     NUMBER(1)      DEFAULT 1 NOT NULL
            )
        ';
        DBMS_OUTPUT.PUT_LINE('Created table GL_ACCOUNT_STRUCTURE_CONFIG.');
    END IF;
END;
/

MERGE INTO GL_ACCOUNT_STRUCTURE_CONFIG target
USING (
    SELECT 1 AS LEVEL_NUMBER, 1 AS DIGIT_LENGTH, 'المستوى الأول - الحسابات الرئيسية العالية' AS LEVEL_NAME_AR, 'Level 1 - Main Primary Accounts' AS LEVEL_NAME_EN, 'خانة واحدة للحسابات الرئيسية' AS DESCRIPTION, 1 AS IS_ACTIVE FROM DUAL UNION ALL
    SELECT 2, 1, 'المستوى الثاني - الفئات الرئيسية', 'Level 2 - Main Categories', 'خانة واحدة إضافية للفئات الرئيسية', 1 FROM DUAL UNION ALL
    SELECT 3, 1, 'المستوى الثالث - المجموعات الفرعية', 'Level 3 - Sub Groups', 'خانة واحدة إضافية للمجموعات الفرعية', 1 FROM DUAL UNION ALL
    SELECT 4, 1, 'المستوى الرابع - الحسابات التجميعية', 'Level 4 - Summary Accounts', 'خانة واحدة إضافية للحسابات التجميعية', 1 FROM DUAL UNION ALL
    SELECT 5, 2, 'المستوى الخامس - الحسابات الفرعية التفصيلية', 'Level 5 - Detail Sub Accounts', 'خانة أو خانتان تفصيلية للحسابات الفرعية للتسجيل والترحيل المباشر', 1 FROM DUAL
) src
ON (target.LEVEL_NUMBER = src.LEVEL_NUMBER)
WHEN MATCHED THEN
    UPDATE SET target.DIGIT_LENGTH = src.DIGIT_LENGTH, target.LEVEL_NAME_AR = src.LEVEL_NAME_AR, target.LEVEL_NAME_EN = src.LEVEL_NAME_EN, target.DESCRIPTION = src.DESCRIPTION, target.IS_ACTIVE = src.IS_ACTIVE
WHEN NOT MATCHED THEN
    INSERT (LEVEL_NUMBER, DIGIT_LENGTH, LEVEL_NAME_AR, LEVEL_NAME_EN, DESCRIPTION, IS_ACTIVE)
    VALUES (src.LEVEL_NUMBER, src.DIGIT_LENGTH, src.LEVEL_NAME_AR, src.LEVEL_NAME_EN, src.DESCRIPTION, src.IS_ACTIVE);

COMMIT;
/

-- --------------------------------------------------------------------
-- 4. GL_ACCOUNT.OLD_ACCOUNT_CODE Column & Index
-- --------------------------------------------------------------------
DECLARE
    v_col NUMBER;
    v_idx NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_col FROM all_tab_cols WHERE table_name = 'GL_ACCOUNT' AND column_name = 'OLD_ACCOUNT_CODE' AND owner = USER;
    IF v_col = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE GL_ACCOUNT ADD OLD_ACCOUNT_CODE NVARCHAR2(50)';
        DBMS_OUTPUT.PUT_LINE('Added OLD_ACCOUNT_CODE column to GL_ACCOUNT table.');
    END IF;

    SELECT COUNT(*) INTO v_idx FROM all_indexes WHERE index_name = 'IX_GL_ACCOUNT_OLD_CODE' AND owner = USER;
    IF v_idx = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_GL_ACCOUNT_OLD_CODE ON GL_ACCOUNT (OLD_ACCOUNT_CODE)';
        DBMS_OUTPUT.PUT_LINE('Created IX_GL_ACCOUNT_OLD_CODE index.');
    END IF;
END;
/

DBMS_OUTPUT.PUT_LINE('CONSOLIDATED DATABASE UPDATE SCRIPT EXECUTED SUCCESSFULLY!');
