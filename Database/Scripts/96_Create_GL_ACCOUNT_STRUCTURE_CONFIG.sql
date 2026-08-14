-- ====================================================================
-- Script Name: 96_Create_GL_ACCOUNT_STRUCTURE_CONFIG.sql
-- Description: Creates GL_ACCOUNT_STRUCTURE_CONFIG table & seeds default level digit configurations in Oracle DB.
-- Author: ThinkOnErp Development Team
-- Date: 2026-08-12
-- Schema: Master / DEV_TEMPLATE / Tenant Schemas
-- ====================================================================

DECLARE
    v_table_exists NUMBER;
BEGIN
    -- 1. Create GL_ACCOUNT_STRUCTURE_CONFIG Table in Current Schema
    SELECT COUNT(*) INTO v_table_exists 
    FROM all_tables 
    WHERE table_name = 'GL_ACCOUNT_STRUCTURE_CONFIG' AND owner = USER;

    IF v_table_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE GL_ACCOUNT_STRUCTURE_CONFIG (
                LEVEL_NUMBER NUMBER(2) NOT NULL,
                DIGIT_LENGTH NUMBER(2) NOT NULL,
                LEVEL_NAME_AR NVARCHAR2(100) NOT NULL,
                LEVEL_NAME_EN NVARCHAR2(100) NOT NULL,
                DESCRIPTION NVARCHAR2(500),
                IS_ACTIVE NUMBER(1) DEFAULT 1 NOT NULL,
                CONSTRAINT PK_GL_ACCOUNT_STRUCT_CFG PRIMARY KEY (LEVEL_NUMBER),
                CONSTRAINT CK_GL_STRUCT_LEVEL CHECK (LEVEL_NUMBER BETWEEN 1 AND 10),
                CONSTRAINT CK_GL_STRUCT_DIGITS CHECK (DIGIT_LENGTH BETWEEN 1 AND 10),
                CONSTRAINT CK_GL_STRUCT_ACTIVE CHECK (IS_ACTIVE IN (0, 1))
            )
        ';
        DBMS_OUTPUT.PUT_LINE('Table GL_ACCOUNT_STRUCTURE_CONFIG created successfully.');
    END IF;
END;
/

-- Seed / Merge Default 5-Level Digit Structure
MERGE INTO GL_ACCOUNT_STRUCTURE_CONFIG target
USING (
    SELECT 1 AS LEVEL_NUMBER, 1 AS DIGIT_LENGTH, 'المستوى الأول - الحسابات الرئيسية العالية' AS LEVEL_NAME_AR, 'Level 1 - Main Primary Accounts' AS LEVEL_NAME_EN, 'خانة واحدة للحسابات الرئيسية (1: الأصول، 2: الخصوم، 3: حقوق الملكية...)' AS DESCRIPTION, 1 AS IS_ACTIVE FROM DUAL UNION ALL
    SELECT 2, 1, 'المستوى الثاني - الفئات الرئيسية', 'Level 2 - Main Categories', 'خانة واحدة إضافية للفئات الرئيسية (11: الأصول المتداولة، 12: الأصول غير المتداولة...)', 1 FROM DUAL UNION ALL
    SELECT 3, 1, 'المستوى الثالث - المجموعات الفرعية', 'Level 3 - Sub Groups', 'خانة واحدة إضافية للمجموعات الفرعية (111: النقدية وما في حكمها...)', 1 FROM DUAL UNION ALL
    SELECT 4, 1, 'المستوى الرابع - الحسابات التجميعية', 'Level 4 - Summary Accounts', 'خانة واحدة إضافية للحسابات التجميعية (1111: البنوك...)', 1 FROM DUAL UNION ALL
    SELECT 5, 2, 'المستوى الخامس - الحسابات الفرعية التفصيلية', 'Level 5 - Detail Sub Accounts', 'خانة أو خانتان تفصيلية للحسابات الفرعية للتسجيل والترحيل المباشر (111101: بنك الاتحاد - حساب جاري...)', 1 FROM DUAL
) src
ON (target.LEVEL_NUMBER = src.LEVEL_NUMBER)
WHEN MATCHED THEN
    UPDATE SET 
        target.DIGIT_LENGTH = src.DIGIT_LENGTH,
        target.LEVEL_NAME_AR = src.LEVEL_NAME_AR,
        target.LEVEL_NAME_EN = src.LEVEL_NAME_EN,
        target.DESCRIPTION = src.DESCRIPTION,
        target.IS_ACTIVE = src.IS_ACTIVE
WHEN NOT MATCHED THEN
    INSERT (LEVEL_NUMBER, DIGIT_LENGTH, LEVEL_NAME_AR, LEVEL_NAME_EN, DESCRIPTION, IS_ACTIVE)
    VALUES (src.LEVEL_NUMBER, src.DIGIT_LENGTH, src.LEVEL_NAME_AR, src.LEVEL_NAME_EN, src.DESCRIPTION, src.IS_ACTIVE);

COMMIT;
/
