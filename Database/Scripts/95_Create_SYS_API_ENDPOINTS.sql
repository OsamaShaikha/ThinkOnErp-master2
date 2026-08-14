-- ====================================================================
-- Script Name: 95_Create_SYS_API_ENDPOINTS.sql
-- Description: Creates SYS_API_ENDPOINTS table & sequence in Oracle DB to persist all individual API endpoints for Swagger & Gateway Routing.
-- Author: ThinkOnErp Development Team
-- Date: 2026-08-11
-- Schema: Master / DEV_TEMPLATE
-- ====================================================================

DECLARE
    v_table_exists NUMBER;
    v_seq_exists NUMBER;
BEGIN
    -- 1. Create SYS_API_ENDPOINTS Table
    SELECT COUNT(*) INTO v_table_exists 
    FROM all_tables 
    WHERE table_name = 'SYS_API_ENDPOINTS' AND owner = USER;

    IF v_table_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE SYS_API_ENDPOINTS (
                ID NUMBER(19) NOT NULL,
                CATEGORY_CODE VARCHAR2(50) NOT NULL,
                CONTROLLER_NAME VARCHAR2(100) NOT NULL,
                ACTION_NAME VARCHAR2(100) NOT NULL,
                HTTP_METHOD VARCHAR2(10) NOT NULL,
                ROUTE_PATH VARCHAR2(300) NOT NULL,
                DISPLAY_NAME VARCHAR2(200),
                DESCRIPTION VARCHAR2(500),
                IS_ACTIVE NUMBER(1) DEFAULT 1,
                CONSTRAINT PK_SYS_API_ENDPOINTS PRIMARY KEY (ID),
                CONSTRAINT FK_SYS_API_ENDPOINTS_CAT FOREIGN KEY (CATEGORY_CODE) REFERENCES SYS_API_CATEGORIES(CATEGORY_CODE),
                CONSTRAINT UX_SYS_API_ENDPOINTS_ROUTE UNIQUE (HTTP_METHOD, ROUTE_PATH)
            )
        ';
        DBMS_OUTPUT.PUT_LINE('Table SYS_API_ENDPOINTS created successfully.');
    END IF;

    -- 2. Create Sequence SEQ_SYS_API_ENDPOINTS
    SELECT COUNT(*) INTO v_seq_exists 
    FROM all_sequences 
    WHERE sequence_name = 'SEQ_SYS_API_ENDPOINTS' AND sequence_owner = USER;

    IF v_seq_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_SYS_API_ENDPOINTS START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE';
        DBMS_OUTPUT.PUT_LINE('Sequence SEQ_SYS_API_ENDPOINTS created successfully.');
    END IF;
END;
/
