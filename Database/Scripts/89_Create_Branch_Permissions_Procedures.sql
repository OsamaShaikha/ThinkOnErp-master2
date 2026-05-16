-- =====================================================
-- Branch Permissions Stored Procedures
-- Provides CRUD operations for branch system and screen permissions
-- =====================================================

-- =====================================================
-- Procedure: SP_BRANCH_SYSTEMS_SELECT_BY_BRANCH
-- Description: Gets all systems assigned to a branch
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_BRANCH_SYSTEMS_SELECT_BY_BRANCH (
	P_BRANCH_ID IN NUMBER,
	P_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
	OPEN P_CURSOR FOR
	SELECT 
		bs.ROW_ID,
		bs.BRANCH_ID,
		bs.SYSTEM_ID,
		sys.SYSTEM_CODE,
		sys.SYSTEM_NAME,
		sys.SYSTEM_NAME_E,
		sys.ICON,
		sys.DISPLAY_ORDER,
		bs.IS_ALLOWED,
		bs.GRANTED_BY,
		bs.GRANTED_DATE,
		bs.REVOKED_DATE,
		bs.NOTES,
		bs.CREATION_USER,
		bs.CREATION_DATE,
		bs.UPDATE_USER,
		bs.UPDATE_DATE
	FROM SYS_BRANCH_SYSTEM bs
	INNER JOIN SYS_SYSTEM sys ON bs.SYSTEM_ID = sys.ROW_ID
	WHERE bs.BRANCH_ID = P_BRANCH_ID
	ORDER BY sys.DISPLAY_ORDER;
END SP_BRANCH_SYSTEMS_SELECT_BY_BRANCH;
/

-- =====================================================
-- Procedure: SP_BRANCH_SCREENS_SELECT_BY_BRANCH
-- Description: Gets all screens assigned to a branch
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_BRANCH_SCREENS_SELECT_BY_BRANCH (
	P_BRANCH_ID IN NUMBER,
	P_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
	OPEN P_CURSOR FOR
	SELECT 
		bs.ROW_ID,
		bs.BRANCH_ID,
		bs.SCREEN_ID,
		scr.SCREEN_CODE,
		scr.SCREEN_NAME,
		scr.SCREEN_NAME_E,
		scr.ROUTE,
		scr.SYSTEM_ID,
		sys.SYSTEM_NAME,
		sys.SYSTEM_CODE,
		scr.DISPLAY_ORDER,
		bs.IS_ALLOWED,
		bs.GRANTED_BY,
		bs.GRANTED_DATE,
		bs.REVOKED_DATE,
		bs.NOTES,
		bs.CREATION_USER,
		bs.CREATION_DATE,
		bs.UPDATE_USER,
		bs.UPDATE_DATE
	FROM SYS_BRANCH_SCREEN bs
	INNER JOIN SYS_SCREEN scr ON bs.SCREEN_ID = scr.ROW_ID
	INNER JOIN SYS_SYSTEM sys ON scr.SYSTEM_ID = sys.ROW_ID
	WHERE bs.BRANCH_ID = P_BRANCH_ID
	ORDER BY sys.DISPLAY_ORDER, scr.DISPLAY_ORDER;
END SP_BRANCH_SCREENS_SELECT_BY_BRANCH;
/

-- =====================================================
-- Procedure: SP_GET_ALL_SYSTEMS
-- Description: Gets all available systems in the platform
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_GET_ALL_SYSTEMS (
	P_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
	OPEN P_CURSOR FOR
	SELECT 
		ROW_ID,
		SYSTEM_CODE,
		SYSTEM_NAME,
		SYSTEM_NAME_E,
		ICON,
		DISPLAY_ORDER,
		IS_ACTIVE,
		CREATION_USER,
		CREATION_DATE,
		UPDATE_USER,
		UPDATE_DATE
	FROM SYS_SYSTEM
	WHERE IS_ACTIVE = '1'
	ORDER BY DISPLAY_ORDER, SYSTEM_NAME;
END SP_GET_ALL_SYSTEMS;
/

-- =====================================================
-- Procedure: SP_GET_ALL_SCREENS
-- Description: Gets all available screens in the platform
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_GET_ALL_SCREENS (
	P_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
	OPEN P_CURSOR FOR
	SELECT 
		s.ROW_ID,
		s.SYSTEM_ID,
		sys.SYSTEM_CODE,
		sys.SYSTEM_NAME,
		s.SCREEN_CODE,
		s.SCREEN_NAME,
		s.SCREEN_NAME_E,
		s.ROUTE,
		s.DISPLAY_ORDER,
		s.IS_ACTIVE,
		s.CREATION_USER,
		s.CREATION_DATE,
		s.UPDATE_USER,
		s.UPDATE_DATE
	FROM SYS_SCREEN s
	INNER JOIN SYS_SYSTEM sys ON s.SYSTEM_ID = sys.ROW_ID
	WHERE s.IS_ACTIVE = '1'
	ORDER BY sys.DISPLAY_ORDER, s.DISPLAY_ORDER, s.SCREEN_NAME;
END SP_GET_ALL_SCREENS;
/

-- =====================================================
-- Procedure: SP_GET_SCREENS_BY_SYSTEM
-- Description: Gets all screens for a specific system
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_GET_SCREENS_BY_SYSTEM (
	P_SYSTEM_ID IN NUMBER,
	P_CURSOR OUT SYS_REFCURSOR
)
AS
BEGIN
	OPEN P_CURSOR FOR
	SELECT 
		ROW_ID,
		SYSTEM_ID,
		SCREEN_CODE,
		SCREEN_NAME,
		SCREEN_NAME_E,
		ROUTE,
		DISPLAY_ORDER,
		IS_ACTIVE,
		CREATION_USER,
		CREATION_DATE,
		UPDATE_USER,
		UPDATE_DATE
	FROM SYS_SCREEN
	WHERE SYSTEM_ID = P_SYSTEM_ID
	AND IS_ACTIVE = '1'
	ORDER BY DISPLAY_ORDER, SCREEN_NAME;
END SP_GET_SCREENS_BY_SYSTEM;
/

-- =====================================================
-- Procedure: SP_BRANCH_SYSTEM_GRANT
-- Description: Grants a system to a branch
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_BRANCH_SYSTEM_GRANT (
	P_BRANCH_ID IN NUMBER,
	P_SYSTEM_ID IN NUMBER,
	P_GRANTED_BY IN NUMBER,
	P_NOTES IN NVARCHAR2,
	P_CREATION_USER IN NVARCHAR2,
	P_NEW_ID OUT NUMBER
)
AS
	V_EXISTING_ID NUMBER;
BEGIN
	-- Check if permission already exists
	SELECT ROW_ID INTO V_EXISTING_ID 
	FROM SYS_BRANCH_SYSTEM 
	WHERE BRANCH_ID = P_BRANCH_ID 
	AND SYSTEM_ID = P_SYSTEM_ID;

	-- If exists, update it instead
	UPDATE SYS_BRANCH_SYSTEM
	SET IS_ALLOWED = '1',
		GRANTED_BY = P_GRANTED_BY,
		GRANTED_DATE = SYSDATE,
		REVOKED_DATE = NULL,
		NOTES = P_NOTES,
		UPDATE_USER = P_CREATION_USER,
		UPDATE_DATE = SYSDATE
	WHERE ROW_ID = V_EXISTING_ID;

	P_NEW_ID := V_EXISTING_ID;

EXCEPTION
	WHEN NO_DATA_FOUND THEN
		-- Insert new permission
		SELECT SEQ_SYS_BRANCH_SYSTEM.NEXTVAL INTO P_NEW_ID FROM DUAL;

		INSERT INTO SYS_BRANCH_SYSTEM (
			ROW_ID, BRANCH_ID, SYSTEM_ID, IS_ALLOWED, GRANTED_BY, 
			GRANTED_DATE, NOTES, CREATION_USER, CREATION_DATE
		) VALUES (
			P_NEW_ID, P_BRANCH_ID, P_SYSTEM_ID, '1', P_GRANTED_BY,
			SYSDATE, P_NOTES, P_CREATION_USER, SYSDATE
		);
END SP_BRANCH_SYSTEM_GRANT;
/

-- =====================================================
-- Procedure: SP_BRANCH_SYSTEM_REVOKE
-- Description: Revokes a system from a branch
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_BRANCH_SYSTEM_REVOKE (
	P_BRANCH_ID IN NUMBER,
	P_SYSTEM_ID IN NUMBER,
	P_UPDATE_USER IN NVARCHAR2,
	P_UPDATED_ID OUT NUMBER
)
AS
BEGIN
	UPDATE SYS_BRANCH_SYSTEM
	SET IS_ALLOWED = '0',
		REVOKED_DATE = SYSDATE,
		UPDATE_USER = P_UPDATE_USER,
		UPDATE_DATE = SYSDATE
	WHERE BRANCH_ID = P_BRANCH_ID
	AND SYSTEM_ID = P_SYSTEM_ID;

	SELECT ROW_ID INTO P_UPDATED_ID
	FROM SYS_BRANCH_SYSTEM
	WHERE BRANCH_ID = P_BRANCH_ID
	AND SYSTEM_ID = P_SYSTEM_ID;
END SP_BRANCH_SYSTEM_REVOKE;
/

-- =====================================================
-- Procedure: SP_BRANCH_SCREEN_GRANT
-- Description: Grants a screen to a branch
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_BRANCH_SCREEN_GRANT (
	P_BRANCH_ID IN NUMBER,
	P_SCREEN_ID IN NUMBER,
	P_GRANTED_BY IN NUMBER,
	P_NOTES IN NVARCHAR2,
	P_CREATION_USER IN NVARCHAR2,
	P_NEW_ID OUT NUMBER
)
AS
	V_EXISTING_ID NUMBER;
BEGIN
	-- Check if permission already exists
	SELECT ROW_ID INTO V_EXISTING_ID 
	FROM SYS_BRANCH_SCREEN 
	WHERE BRANCH_ID = P_BRANCH_ID 
	AND SCREEN_ID = P_SCREEN_ID;

	-- If exists, update it instead
	UPDATE SYS_BRANCH_SCREEN
	SET IS_ALLOWED = '1',
		GRANTED_BY = P_GRANTED_BY,
		GRANTED_DATE = SYSDATE,
		REVOKED_DATE = NULL,
		NOTES = P_NOTES,
		UPDATE_USER = P_CREATION_USER,
		UPDATE_DATE = SYSDATE
	WHERE ROW_ID = V_EXISTING_ID;

	P_NEW_ID := V_EXISTING_ID;

EXCEPTION
	WHEN NO_DATA_FOUND THEN
		-- Insert new permission
		SELECT SEQ_SYS_BRANCH_SCREEN.NEXTVAL INTO P_NEW_ID FROM DUAL;

		INSERT INTO SYS_BRANCH_SCREEN (
			ROW_ID, BRANCH_ID, SCREEN_ID, IS_ALLOWED, GRANTED_BY, 
			GRANTED_DATE, NOTES, CREATION_USER, CREATION_DATE
		) VALUES (
			P_NEW_ID, P_BRANCH_ID, P_SCREEN_ID, '1', P_GRANTED_BY,
			SYSDATE, P_NOTES, P_CREATION_USER, SYSDATE
		);
END SP_BRANCH_SCREEN_GRANT;
/

-- =====================================================
-- Procedure: SP_BRANCH_SCREEN_REVOKE
-- Description: Revokes a screen from a branch
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_BRANCH_SCREEN_REVOKE (
	P_BRANCH_ID IN NUMBER,
	P_SCREEN_ID IN NUMBER,
	P_UPDATE_USER IN NVARCHAR2,
	P_UPDATED_ID OUT NUMBER
)
AS
BEGIN
	UPDATE SYS_BRANCH_SCREEN
	SET IS_ALLOWED = '0',
		REVOKED_DATE = SYSDATE,
		UPDATE_USER = P_UPDATE_USER,
		UPDATE_DATE = SYSDATE
	WHERE BRANCH_ID = P_BRANCH_ID
	AND SCREEN_ID = P_SCREEN_ID;

	SELECT ROW_ID INTO P_UPDATED_ID
	FROM SYS_BRANCH_SCREEN
	WHERE BRANCH_ID = P_BRANCH_ID
	AND SCREEN_ID = P_SCREEN_ID;
END SP_BRANCH_SCREEN_REVOKE;
/

-- =====================================================
-- Procedure: SP_CHECK_BRANCH_SYSTEM_ALLOWED
-- Description: Checks if a branch has access to a system
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_CHECK_BRANCH_SYSTEM_ALLOWED (
	P_BRANCH_ID IN NUMBER,
	P_SYSTEM_ID IN NUMBER,
	P_IS_ALLOWED OUT CHAR
)
AS
	V_IS_ALLOWED CHAR(1);
BEGIN
	SELECT IS_ALLOWED INTO V_IS_ALLOWED
	FROM SYS_BRANCH_SYSTEM
	WHERE BRANCH_ID = P_BRANCH_ID
	AND SYSTEM_ID = P_SYSTEM_ID;

	P_IS_ALLOWED := V_IS_ALLOWED;

EXCEPTION
	WHEN NO_DATA_FOUND THEN
		P_IS_ALLOWED := '0';
END SP_CHECK_BRANCH_SYSTEM_ALLOWED;
/

-- =====================================================
-- Procedure: SP_CHECK_BRANCH_SCREEN_ALLOWED
-- Description: Checks if a branch has access to a screen
-- =====================================================
CREATE OR REPLACE PROCEDURE SP_CHECK_BRANCH_SCREEN_ALLOWED (
	P_BRANCH_ID IN NUMBER,
	P_SCREEN_ID IN NUMBER,
	P_IS_ALLOWED OUT CHAR
)
AS
	V_IS_ALLOWED CHAR(1);
BEGIN
	SELECT IS_ALLOWED INTO V_IS_ALLOWED
	FROM SYS_BRANCH_SCREEN
	WHERE BRANCH_ID = P_BRANCH_ID
	AND SCREEN_ID = P_SCREEN_ID;

	P_IS_ALLOWED := V_IS_ALLOWED;

EXCEPTION
	WHEN NO_DATA_FOUND THEN
		P_IS_ALLOWED := '0';
END SP_CHECK_BRANCH_SCREEN_ALLOWED;
/

COMMIT;

-- =====================================================
-- Script Execution Complete
-- =====================================================
-- Usage Notes:
-- 1. All procedures follow naming convention: SP_[TABLE]_[OPERATION]
-- 2. All procedures use SYS_REFCURSOR for Oracle compatibility
-- 3. All CRUD operations include audit trail (CREATION_USER, UPDATE_USER)
-- 4. Sequences are auto-incremented for primary keys
-- 5. Date columns use SYSDATE for server-side timestamps
-- =====================================================
