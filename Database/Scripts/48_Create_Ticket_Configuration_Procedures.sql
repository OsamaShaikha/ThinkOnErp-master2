-- =============================================
-- Script: 48_Create_Ticket_Configuration_Procedures.sql
-- Description: Creates stored procedures for SYS_TICKET_CONFIG CRUD operations
-- Author: ThinkOnERP Development Team
-- Date: 2024
-- =============================================

-- =============================================
-- Procedure: SP_SYS_TICKET_CONFIG_SELECT_ALL
-- Description: Retrieves all active ticket configuration settings
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_CONFIG_SELECT_ALL (
    p_cursor OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_cursor FOR
        SELECT 
            ROW_ID,
            CONFIG_KEY,
            CONFIG_VALUE,
            CONFIG_TYPE,
            DESCRIPTION_AR,
            DESCRIPTION_EN,
            IS_ACTIVE,
            CREATION_USER,
            CREATION_DATE,
            UPDATE_USER,
            UPDATE_DATE
        FROM SYS_TICKET_CONFIG
        WHERE IS_ACTIVE = 'Y'
        ORDER BY CONFIG_TYPE, CONFIG_KEY;
END;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_CONFIG_SELECT_BY_KEY
-- Description: Retrieves a specific configuration by key
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_CONFIG_SELECT_BY_KEY (
    p_config_key IN NVARCHAR2,
    p_cursor OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_cursor FOR
        SELECT 
            ROW_ID,
            CONFIG_KEY,
            CONFIG_VALUE,
            CONFIG_TYPE,
            DESCRIPTION_AR,
            DESCRIPTION_EN,
            IS_ACTIVE,
            CREATION_USER,
            CREATION_DATE,
            UPDATE_USER,
            UPDATE_DATE
        FROM SYS_TICKET_CONFIG
        WHERE CONFIG_KEY = p_config_key
        AND IS_ACTIVE = 'Y';
END;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_CONFIG_SELECT_BY_TYPE
-- Description: Retrieves all configurations of a specific type
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_CONFIG_SELECT_BY_TYPE (
    p_config_type IN NVARCHAR2,
    p_cursor OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_cursor FOR
        SELECT 
            ROW_ID,
            CONFIG_KEY,
            CONFIG_VALUE,
            CONFIG_TYPE,
            DESCRIPTION_AR,
            DESCRIPTION_EN,
            IS_ACTIVE,
            CREATION_USER,
            CREATION_DATE,
            UPDATE_USER,
            UPDATE_DATE
        FROM SYS_TICKET_CONFIG
        WHERE CONFIG_TYPE = p_config_type
        AND IS_ACTIVE = 'Y'
        ORDER BY CONFIG_KEY;
END;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_CONFIG_INSERT
-- Description: Inserts a new ticket configuration setting
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_CONFIG_INSERT (
    p_config_key IN NVARCHAR2,
    p_config_value IN NCLOB,
    p_config_type IN NVARCHAR2,
    p_description_ar IN NVARCHAR2,
    p_description_en IN NVARCHAR2,
    p_creation_user IN NVARCHAR2,
    p_row_id OUT NUMBER
)
AS
BEGIN
    SELECT SEQ_SYS_TICKET_CONFIG.NEXTVAL INTO p_row_id FROM DUAL;
    
    INSERT INTO SYS_TICKET_CONFIG (
        ROW_ID,
        CONFIG_KEY,
        CONFIG_VALUE,
        CONFIG_TYPE,
        DESCRIPTION_AR,
        DESCRIPTION_EN,
        IS_ACTIVE,
        CREATION_USER,
        CREATION_DATE
    ) VALUES (
        p_row_id,
        p_config_key,
        p_config_value,
        p_config_type,
        p_description_ar,
        p_description_en,
        'Y',
        p_creation_user,
        SYSDATE
    );
    
    COMMIT;
END;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_CONFIG_UPDATE
-- Description: Updates an existing ticket configuration setting
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_CONFIG_UPDATE (
    p_row_id IN NUMBER,
    p_config_value IN NCLOB,
    p_description_ar IN NVARCHAR2,
    p_description_en IN NVARCHAR2,
    p_update_user IN NVARCHAR2
)
AS
BEGIN
    UPDATE SYS_TICKET_CONFIG
    SET 
        CONFIG_VALUE = p_config_value,
        DESCRIPTION_AR = p_description_ar,
        DESCRIPTION_EN = p_description_en,
        UPDATE_USER = p_update_user,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = p_row_id;
    
    COMMIT;
END;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_CONFIG_UPDATE_BY_KEY
-- Description: Updates a configuration setting by key
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_CONFIG_UPDATE_BY_KEY (
    p_config_key IN NVARCHAR2,
    p_config_value IN NCLOB,
    p_update_user IN NVARCHAR2
)
AS
BEGIN
    UPDATE SYS_TICKET_CONFIG
    SET 
        CONFIG_VALUE = p_config_value,
        UPDATE_USER = p_update_user,
        UPDATE_DATE = SYSDATE
    WHERE CONFIG_KEY = p_config_key
    AND IS_ACTIVE = 'Y';
    
    COMMIT;
END;
/

-- =============================================
-- Procedure: SP_SYS_TICKET_CONFIG_DELETE
-- Description: Soft deletes a ticket configuration (sets IS_ACTIVE to 'N')
-- =============================================
CREATE OR REPLACE PROCEDURE SP_SYS_TICKET_CONFIG_DELETE (
    p_row_id IN NUMBER,
    p_update_user IN NVARCHAR2
)
AS
BEGIN
    UPDATE SYS_TICKET_CONFIG
    SET 
        IS_ACTIVE = 'N',
        UPDATE_USER = p_update_user,
        UPDATE_DATE = SYSDATE
    WHERE ROW_ID = p_row_id;
    
    COMMIT;
END;
/

-- Display success message
SELECT 'SYS_TICKET_CONFIG stored procedures created successfully' AS STATUS FROM DUAL;
